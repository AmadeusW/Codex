using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Codex.ObjectModel;
using Codex.Utilities;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Classification;
using Microsoft.CodeAnalysis.Text;

namespace Codex.Analysis
{
    class DocumentAnalyzer
    {
        private Document _document;
        private Compilation _compilation;
        private CompilationServices CompilationServices;

        private ConcurrentDictionary<INamedTypeSymbol, ILookup<ISymbol, ISymbol>> interfaceMemberImplementationMap = new ConcurrentDictionary<INamedTypeSymbol, ILookup<ISymbol, ISymbol>>();
        private Dictionary<ISymbol, int> localSymbolIdMap = new Dictionary<ISymbol, int>();

        public SemanticModel SemanticModel;
        private AnalyzedProject _analyzedProject;
        private BoundSourceFileBuilder boundSourceFile;
        private List<ReferenceSpan> references;
        private AnalyzedProjectContext context;
        private SourceText DocumentText;
        private readonly SemanticServices semanticServices;

        public DocumentAnalyzer(
            SemanticServices semanticServices,
            Document document,
            CompilationServices compilationServices,
            string logicalPath,
            AnalyzedProjectContext context,
            BoundSourceFileBuilder boundSourceFile)
        {
            _document = document;
            CompilationServices = compilationServices;
            _compilation = compilationServices.Compilation;
            _analyzedProject = context.Project;
            this.semanticServices = semanticServices;
            this.context = context;

            references = new List<ReferenceSpan>();

            this.boundSourceFile = boundSourceFile;
        }

        private string Language => _document.Project.Language;

        public string GetText(ClassifiedSpan span)
        {
            return DocumentText.ToString(span.TextSpan);
        }

        public async Task<BoundSourceFile> CreateBoundSourceFile()
        {
            var syntaxRoot = await _document.GetSyntaxRootAsync();
            var syntaxTree = syntaxRoot.SyntaxTree;
            SemanticModel = _compilation.GetSemanticModel(syntaxTree);
            DocumentText = await _document.GetTextAsync();

            boundSourceFile.SourceFile.Info.Lines = DocumentText.Lines.Count;
            boundSourceFile.SourceFile.Info.Size = DocumentText.Length;

            var classificationSpans = (IReadOnlyList<ClassifiedSpan>)await Classifier.GetClassifiedSpansAsync(_document, syntaxRoot.FullSpan);
            var text = await _document.GetTextAsync();

            classificationSpans = MergeSpans(classificationSpans).ToList();
            var fileClassificationSpans = new List<ClassificationSpan>();

            foreach (var span in classificationSpans)
            {
                if (SkipSpan(span))
                {
                    continue;
                }

                ClassificationSpan classificationSpan = new ClassificationSpan();
                fileClassificationSpans.Add(classificationSpan);

                classificationSpan.Start = span.TextSpan.Start;
                classificationSpan.Length = span.TextSpan.Length;
                classificationSpan.Classification = span.ClassificationType;

                if (!IsSemanticSpan(span))
                {
                    continue;
                }

                var token = syntaxRoot.FindToken(span.TextSpan.Start, findInsideTrivia: true);

                if (semanticServices.IsNewKeyword(token))
                {
                    continue;
                }

                ISymbol symbol = null;
                ISymbol declaredSymbol = null;
                bool isThis = false;

                if (span.ClassificationType != ClassificationTypeNames.Keyword)
                {
                    declaredSymbol = SemanticModel.GetDeclaredSymbol(token.Parent);
                }

                var usingExpression = semanticServices.TryGetUsingExpressionFromToken(token);
                if (usingExpression != null)
                {
                    var disposeSymbol = CompilationServices.IDisposable_Dispose.Value;
                    if (disposeSymbol != null)
                    {
                        var typeInfo = SemanticModel.GetTypeInfo(usingExpression);
                        var disposeImplSymbol = typeInfo.Type?.FindImplementationForInterfaceMember(disposeSymbol);
                        if (disposeImplSymbol != null)
                        {
                            SymbolSpan usingSymbolSpan = CreateSymbolSpan(syntaxTree, text, span, classificationSpan);
                            references.Add(usingSymbolSpan.CreateReference(GetReferenceSymbol(disposeImplSymbol, ReferenceKind.UsingDispose)));
                        }
                    }
                }

                if (semanticServices.IsOverrideKeyword(token))
                {
                    var bindableNode = token.Parent;
                    bindableNode = semanticServices.GetEventField(bindableNode);

                    var parentSymbol = SemanticModel.GetDeclaredSymbol(bindableNode);

                    SymbolSpan parentSymbolSpan = CreateSymbolSpan(syntaxTree, text, span, classificationSpan);

                    // Don't allow this to show up in search. It's only added for go to definition navigation
                    // on override keyword.
                    AddReferencesToOverriddenMembers(parentSymbolSpan, parentSymbol, excludeFromSearch: true);
                }

                if (symbol == null)
                {
                    symbol = declaredSymbol;

                    if (declaredSymbol == null)
                    {
                        var node = GetBindableParent(token);
                        if (node != null)
                        {
                            symbol = GetSymbol(node, out isThis);
                        }
                    }
                }

                if (symbol == null || symbol.ContainingAssembly == null)
                {
                    continue;
                }

                if (symbol.Kind == SymbolKind.Local ||
                    symbol.Kind == SymbolKind.Parameter ||
                    symbol.Kind == SymbolKind.TypeParameter ||
                    symbol.Kind == SymbolKind.RangeVariable)
                {
                    //  Just generate group ids rather than full ref/def
                    var localSymbolId = localSymbolIdMap.GetOrAdd(symbol, localSymbolIdMap.Count + 1);
                    classificationSpan.LocalGroupId = localSymbolId;
                    continue;
                }

                if ((symbol.Kind == SymbolKind.Event ||
                     symbol.Kind == SymbolKind.Field ||
                     symbol.Kind == SymbolKind.Method ||
                     symbol.Kind == SymbolKind.NamedType ||
                     symbol.Kind == SymbolKind.Property) &&
                     symbol.Locations.Length >= 1)
                {
                    var documentationId = GetDocumentationCommentId(symbol);

                    if (string.IsNullOrEmpty(documentationId))
                    {
                        continue;
                    }

                    SymbolSpan symbolSpan = CreateSymbolSpan(syntaxTree, text, span, classificationSpan);

                    if (declaredSymbol != null)
                    {
                        // This is a definition
                        var definitionSymbol = GetDefinitionSymbol(symbol, documentationId);
                        var definitionSpan = symbolSpan.CreateDefinition(definitionSymbol);
                        boundSourceFile.AddDefinition(definitionSpan);

                        // A reference symbol for the definition is added so the definition is found in find all references
                        var definitionReferenceSymbol = GetReferenceSymbol(symbol, referenceKind: ReferenceKind.Definition);
                        references.Add(symbolSpan.CreateReference(definitionReferenceSymbol));

                        ProcessDefinitionAndAddAdditionalReferenceSymbols(symbol, definitionSpan, token);
                    }
                    else
                    {
                        // This is a reference
                        var referenceSymbol = GetReferenceSymbol(symbol, documentationId, token);
                        var referenceSpan = symbolSpan.CreateReference(referenceSymbol);

                        // This parameter should not show up in find all references search
                        // but should navigate to type for go to definition
                        referenceSymbol.ExcludeFromSearch = isThis;// token.IsKind(SyntaxKind.ThisKeyword) || token.IsKind(SyntaxKind.BaseKeyword);
                        references.Add(referenceSpan);

                        // Reference to external project
                        if (referenceSymbol.ProjectId != _analyzedProject.Id)
                        {
                            if (!_analyzedProject.ReferenceDefinitionMap.ContainsKey(referenceSymbol))
                            {
                                _analyzedProject.ReferenceDefinitionMap.TryAdd(referenceSymbol, GetDefinitionSymbol(symbol, documentationId));
                            }
                        }

                        AddAdditionalReferenceSymbols(symbol, referenceSpan, token);
                    }
                }
            }

            boundSourceFile.AddClassifications(fileClassificationSpans);
            boundSourceFile.AddReferences(references);
            return boundSourceFile.Build();
        }

        private static SymbolSpan CreateSymbolSpan(SyntaxTree syntaxTree, SourceText text, ClassifiedSpan span, ClassificationSpan classificationSpan)
        {
            var lineSpan = syntaxTree.GetLineSpan(span.TextSpan);
            var symbolSpan = new SymbolSpan()
            {
                Start = classificationSpan.Start,
                Length = classificationSpan.Length,
            };

            return symbolSpan;
        }

        private void AddAdditionalReferenceSymbols(ISymbol symbol, ReferenceSpan symbolSpan, SyntaxToken token)
        {
            //var bindableParent = GetBindableParent(token);

            if (symbol.Kind == SymbolKind.Method)
            {
                // Case: Constructor
                IMethodSymbol methodSymbol = symbol as IMethodSymbol;
                if (methodSymbol != null)
                {
                    if (methodSymbol.MethodKind == MethodKind.Constructor)
                    {
                        // Add special reference kind for instantiation
                        references.Add(symbolSpan.CreateReference(GetReferenceSymbol(methodSymbol.ContainingType, ReferenceKind.Instantiation)));
                    }
                }

            }
            else if (symbol.Kind == SymbolKind.Property)
            {

            }
            else if (symbol.Kind == SymbolKind.Field)
            {

            }
            else if (symbol.Kind == SymbolKind.NamedType)
            {
                // Case: Derived class

                // Case: Partial class
            }
        }

        private void ProcessDefinitionAndAddAdditionalReferenceSymbols(ISymbol symbol, DefinitionSpan symbolSpan, SyntaxToken token)
        {
            // Handle potentially virtual or interface member implementations
            if (symbol.Kind == SymbolKind.Method || symbol.Kind == SymbolKind.Property || symbol.Kind == SymbolKind.Event)
            {
                AddReferencesToOverriddenMembers(symbolSpan, symbol, relatedDefinition: symbolSpan.Definition.Id);

                AddReferencesToImplementedMembers(symbolSpan, symbol, symbolSpan.Definition.Id);
            }

            if (symbol.Kind == SymbolKind.Method)
            {

                IMethodSymbol methodSymbol = symbol as IMethodSymbol;
                if (methodSymbol != null)
                {
                    // Case: Constructor and Static Constructor
                    if (methodSymbol.MethodKind == MethodKind.Constructor || methodSymbol.MethodKind == MethodKind.StaticConstructor)
                    {
                        // Exclude constructors from default search
                        // Add a constructor reference with the containing type
                        symbolSpan.Definition.ExcludeFromDefaultSearch = true;
                        references.Add(symbolSpan.CreateReference(GetReferenceSymbol(methodSymbol.ContainingType, ReferenceKind.Constructor)));
                    }
                }
            }
        }

        private static string GetSymbolKind(ISymbol symbol)
        {
            if (symbol.Kind == SymbolKind.Method)
            {
                IMethodSymbol methodSymbol = symbol as IMethodSymbol;
                if (methodSymbol != null)
                {
                    // Case: Constructor and Static Constructor
                    if (methodSymbol.MethodKind == MethodKind.Constructor || methodSymbol.MethodKind == MethodKind.StaticConstructor)
                    {
                        // Exclude constructors from default search
                        // Add a constructor reference with the containing type
                        return nameof(MethodKind.Constructor);
                    }
                }

            }
            else if (symbol.Kind == SymbolKind.NamedType)
            {
                INamedTypeSymbol typeSymbol = symbol as INamedTypeSymbol;
                if (typeSymbol != null)
                {
                    switch (typeSymbol.TypeKind)
                    {
                        case TypeKind.Class:
                        case TypeKind.Delegate:
                        case TypeKind.Enum:
                        case TypeKind.Interface:
                        case TypeKind.Struct:
                            return typeSymbol.TypeKind.GetString();
                        default:
                            break;
                    }
                }
            }

            return symbol.Kind.GetString();
        }

        private void AddReferencesToImplementedMembers(
            SymbolSpan symbolSpan,
            ISymbol declaredSymbol,
            SymbolId relatedDefinition = default(SymbolId))
        {
            var declaringType = declaredSymbol.ContainingType;
            var implementationLookup = interfaceMemberImplementationMap.GetOrAdd(declaringType, type =>
            {
                return type.AllInterfaces
                    .SelectMany(implementedInterface =>
                        implementedInterface.GetMembers()
                            .Select(member => CreateKeyValuePair(type.FindImplementationForInterfaceMember(member), member))
                            .Where(kvp => kvp.Key != null))
                    .ToLookup(kvp => kvp.Key, kvp => kvp.Value);
            });

            foreach (var implementedMember in implementationLookup[declaredSymbol])
            {
                references.Add(symbolSpan.CreateReference(GetReferenceSymbol(implementedMember, ReferenceKind.InterfaceMemberImplementation), relatedDefinition));
            }
        }

        private KeyValuePair<TKey, TValue> CreateKeyValuePair<TKey, TValue>(TKey key, TValue value)
        {
            return new KeyValuePair<TKey, TValue>(key, value);
        }

        private void AddReferencesToOverriddenMembers(
            SymbolSpan symbolSpan,
            ISymbol declaredSymbol,
            bool excludeFromSearch = false,
            SymbolId relatedDefinition = default(SymbolId))
        {
            if (!declaredSymbol.IsOverride)
            {
                return;
            }

            IMethodSymbol method = declaredSymbol as IMethodSymbol;
            if (method != null)
            {
                var overriddenMethod = method.OverriddenMethod;
                if (overriddenMethod != null)
                {
                    references.Add(symbolSpan.CreateReference(GetReferenceSymbol(overriddenMethod, ReferenceKind.Override), relatedDefinition));
                }
            }

            IPropertySymbol property = declaredSymbol as IPropertySymbol;
            if (property != null)
            {
                var overriddenProperty = property.OverriddenProperty;
                if (overriddenProperty != null)
                {
                    references.Add(symbolSpan.CreateReference(GetReferenceSymbol(overriddenProperty, ReferenceKind.Override), relatedDefinition));
                }
            }

            IEventSymbol eventSymbol = declaredSymbol as IEventSymbol;
            if (eventSymbol != null)
            {
                var overriddenEvent = eventSymbol.OverriddenEvent;
                if (overriddenEvent != null)
                {
                    references.Add(symbolSpan.CreateReference(GetReferenceSymbol(overriddenEvent, ReferenceKind.Override), relatedDefinition));
                }
            }

            if (excludeFromSearch)
            {
                references[references.Count - 1].Reference.ExcludeFromSearch = true;
            }

            // TODO: Should we add transitive overrides
        }

        private ISymbol GetExplicitlyImplementedMember(ISymbol symbol)
        {
            IMethodSymbol methodSymbol = symbol as IMethodSymbol;
            if (methodSymbol != null)
            {
                return methodSymbol.ExplicitInterfaceImplementations.FirstOrDefault();
            }

            IPropertySymbol propertySymbol = symbol as IPropertySymbol;
            if (propertySymbol != null)
            {
                return propertySymbol.ExplicitInterfaceImplementations.FirstOrDefault();
            }

            IEventSymbol eventSymbol = symbol as IEventSymbol;
            if (eventSymbol != null)
            {
                return eventSymbol.ExplicitInterfaceImplementations.FirstOrDefault();
            }

            return null;
        }

        private static DefinitionSymbol GetDefinitionSymbol(ISymbol symbol, string id = null)
        {
            // Use unspecialized generic
            symbol = symbol.OriginalDefinition;
            id = id ?? GetDocumentationCommentId(symbol);

            bool isMember = symbol.Kind == SymbolKind.Field ||
                symbol.Kind == SymbolKind.Method ||
                symbol.Kind == SymbolKind.Event ||
                symbol.Kind == SymbolKind.Property;

            string containerQualifierName = string.Empty;
            if (symbol.ContainingSymbol != null)
            {
                containerQualifierName = symbol.ContainingSymbol.ToDisplayString(DisplayFormats.QualifiedNameDisplayFormat);
            }

            var boundSymbol = new DefinitionSymbol()
            {
                ProjectId = symbol.ContainingAssembly.Name,
                Id = CreateSymbolId(id),
                DisplayName = symbol.GetDisplayString(),
                ShortName = symbol.ToDisplayString(DisplayFormats.ShortNameDisplayFormat),
                ContainerQualifiedName = containerQualifierName,
                Kind = GetSymbolKind(symbol),
                Glyph = symbol.GetGlyph(),
                SymbolDepth = symbol.GetSymbolDepth(),
                Comment = symbol.GetDocumentationCommentXml(),
                DeclarationName = symbol.ToDeclarationName(),
                TypeName = GetTypeName(symbol)
            };

            return boundSymbol;
        }

        /// <summary>
        /// Transforms a documentation id into a symbol id
        /// </summary>
        private static SymbolId CreateSymbolId(string id)
        {
            // First process the id from standard form T:System.String
            // to form System.String:T which ensure most common prefixes
            if (id != null && id.Length > 3 && id[1] == ':')
            {
                id = id.Substring(2) + ":" + id[0];
            }

            return SymbolId.CreateFromId(id);
        }

        private ReferenceSymbol GetReferenceSymbol(ISymbol symbol, string id, SyntaxToken token)
        {
            ReferenceKind referenceKind = DetermineReferenceKind(symbol, token);

            return GetReferenceSymbol(symbol, referenceKind, id);
        }

        private ReferenceKind DetermineReferenceKind(ISymbol referencedSymbol, SyntaxToken token)
        {
            var kind = ReferenceKind.Reference;
            // Case: nameof() - Do we really care about distinguishing this case.

            if (referencedSymbol.Kind == SymbolKind.NamedType)
            {
                var node = GetBindableParent(token);
                var typeArgumentList = (SyntaxNode)node.FirstAncestorOrSelf<Microsoft.CodeAnalysis.CSharp.Syntax.TypeArgumentListSyntax>();
                if (typeArgumentList != null)
                {
                    return kind;
                }

                typeArgumentList = (SyntaxNode)node.FirstAncestorOrSelf<Microsoft.CodeAnalysis.VisualBasic.Syntax.TypeArgumentListSyntax>();
                if (typeArgumentList != null)
                {
                    return kind;
                }

                var baseList =
                    (SyntaxNode)node.FirstAncestorOrSelf<Microsoft.CodeAnalysis.CSharp.Syntax.BaseListSyntax>() ??
                    (SyntaxNode)node.FirstAncestorOrSelf<Microsoft.CodeAnalysis.VisualBasic.Syntax.InheritsStatementSyntax>() ??
                    node.FirstAncestorOrSelf<Microsoft.CodeAnalysis.VisualBasic.Syntax.ImplementsStatementSyntax>();
                if (baseList != null)
                {
                    var typeDeclaration = baseList.Parent;
                    if (typeDeclaration != null)
                    {
                        var derivedType = SemanticModel.GetDeclaredSymbol(typeDeclaration) as INamedTypeSymbol;
                        if (derivedType != null)
                        {
                            INamedTypeSymbol baseSymbol = referencedSymbol as INamedTypeSymbol;
                            if (baseSymbol != null)
                            {
                                if (baseSymbol.TypeKind == TypeKind.Class && baseSymbol.Equals(derivedType.BaseType))
                                {
                                    kind = ReferenceKind.DerivedType;
                                }
                                else if (baseSymbol.TypeKind == TypeKind.Interface)
                                {
                                    if (derivedType.TypeKind == TypeKind.Interface && derivedType.Interfaces.Contains(baseSymbol))
                                    {
                                        kind = ReferenceKind.InterfaceInheritance;
                                    }
                                    else if (derivedType.Interfaces.Contains(baseSymbol))
                                    {
                                        kind = ReferenceKind.InterfaceImplementation;
                                    }
                                }
                            }
                        }
                    }
                }
            }
            else if (referencedSymbol.Kind == SymbolKind.Field ||
                referencedSymbol.Kind == SymbolKind.Property)
            {
                var node = GetBindableParent(token);
                if (IsWrittenTo(node))
                {
                    kind = ReferenceKind.Write;
                }
            }

            return kind;
        }

        private ReferenceSymbol GetReferenceSymbol(ISymbol symbol, ReferenceKind referenceKind, string id = null)
        {
            // Use unspecialized generic
            // TODO: Consider adding reference symbol for specialized generic as well so one can find all references
            // to List<string>.Add rather than just List<T>.Add
            symbol = symbol.OriginalDefinition;

            var boundSymbol = new ReferenceSymbol()
            {
                ProjectId = symbol.ContainingAssembly.Name,
                Id = CreateSymbolId(id ?? GetDocumentationCommentId(symbol)),
                Kind = GetSymbolKind(symbol),
                ReferenceKind = referenceKind.GetString(),
                IsImplicitlyDeclared = symbol.IsImplicitlyDeclared
            };

            if (!string.IsNullOrEmpty(symbol.Name))
            {
                DefinitionSymbol definition;
                if (!context.ReferenceDefinitionMap.TryGetValue(boundSymbol.Id.Value, out definition))
                {
                    var createdDefinition = GetDefinitionSymbol(symbol);
                    definition = context.ReferenceDefinitionMap.GetOrAdd(boundSymbol.Id.Value, createdDefinition);
                    if (createdDefinition == definition)
                    {
                        if (symbol.Kind != SymbolKind.Namespace &&
                            symbol.ContainingNamespace != null &&
                            !symbol.ContainingNamespace.IsGlobalNamespace)
                        {
                            var namespaceSymbol = GetReferenceSymbol(symbol.ContainingNamespace, ReferenceKind.Reference);
                            var extData = context.GetReferenceNamespaceData(namespaceSymbol.Id.Value);
                            definition.ExtData = extData;
                        }
                    }
                }

                if (referenceKind != ReferenceKind.Definition)
                {
                    definition.IncrementReferenceCount();
                }
            }

            return boundSymbol;
        }

        private bool IsWrittenTo(SyntaxNode node)
        {
            bool result = semanticServices.IsWrittenTo(SemanticModel, node, CancellationToken.None);
            return result;
        }

        private ISymbol GetSymbol(SyntaxNode node, out bool isThis)
        {
            var symbolInfo = SemanticModel.GetSymbolInfo(node);
            isThis = false;
            ISymbol symbol = symbolInfo.Symbol;
            if (symbol == null)
            {
                return null;
            }

            if (IsThisParameter(symbol))
            {
                isThis = true;
                var typeInfo = SemanticModel.GetTypeInfo(node);
                if (typeInfo.Type != null)
                {
                    return typeInfo.Type;
                }
            }
            else if (IsFunctionValue(symbol))
            {
                var method = symbol.ContainingSymbol as IMethodSymbol;
                if (method != null)
                {
                    if (method.AssociatedSymbol != null)
                    {
                        return method.AssociatedSymbol;
                    }
                    else
                    {
                        return method;
                    }
                }
            }
            else if (symbol.Kind == SymbolKind.Method)
            {
                var method = symbol as IMethodSymbol;
                if (method != null)
                {
                    if (method.ReducedFrom != null)
                    {
                        return method.ReducedFrom;
                    }
                }
            }

            symbol = ResolveAccessorParameter(symbol);

            return symbol;
        }

        private static string GetTypeName(ISymbol symbol)
        {
            if (symbol.Kind == SymbolKind.Method)
            {
                // Case: Constructor
                IMethodSymbol methodSymbol = symbol as IMethodSymbol;
                return methodSymbol.ReturnType?.ToDisplayString(DisplayFormats.TypeNameDisplayFormat);

            }
            else if (symbol.Kind == SymbolKind.Property)
            {
                IPropertySymbol propertySymbol = symbol as IPropertySymbol;
                return propertySymbol.Type?.ToDisplayString(DisplayFormats.TypeNameDisplayFormat);
            }
            else if (symbol.Kind == SymbolKind.Field)
            {
                IFieldSymbol fieldSymbol = symbol as IFieldSymbol;
                return fieldSymbol.Type?.ToDisplayString(DisplayFormats.TypeNameDisplayFormat);
            }

            return null;
        }

        private static string GetDocumentationCommentId(ISymbol symbol)
        {
            string result = null;
            if (!symbol.IsDefinition)
            {
                symbol = symbol.OriginalDefinition;
            }

            result = symbol.GetDocumentationCommentId();
            if (result == null)
            {
                return symbol.ToDisplayString();
            }

            result = result.Replace("#ctor", "ctor");

            return result;
        }

        private ISymbol ResolveAccessorParameter(ISymbol symbol)
        {
            if (symbol == null || !symbol.IsImplicitlyDeclared)
            {
                return symbol;
            }

            var parameterSymbol = symbol as IParameterSymbol;
            if (parameterSymbol == null)
            {
                return symbol;
            }

            var accessorMethod = parameterSymbol.ContainingSymbol as IMethodSymbol;
            if (accessorMethod == null)
            {
                return symbol;
            }

            var property = accessorMethod.AssociatedSymbol as IPropertySymbol;
            if (property == null)
            {
                return symbol;
            }

            int ordinal = parameterSymbol.Ordinal;
            if (property.Parameters.Length <= ordinal)
            {
                return symbol;
            }

            return property.Parameters[ordinal];
        }

        private static bool IsFunctionValue(ISymbol symbol)
        {
            return symbol is ILocalSymbol && ((ILocalSymbol)symbol).IsFunctionValue;
        }

        private static bool IsThisParameter(ISymbol symbol)
        {
            return symbol != null && symbol.Kind == SymbolKind.Parameter && ((IParameterSymbol)symbol).IsThis;
        }

        private IEnumerable<ClassifiedSpan> MergeSpans(IEnumerable<ClassifiedSpan> classificationSpans)
        {
            ClassifiedSpan mergedSpan = default(ClassifiedSpan);
            bool skippedNonWhitespace = false;
            foreach (var span in classificationSpans)
            {
                if (!TryMergeSpan(ref mergedSpan, span, ref skippedNonWhitespace))
                {
                    // Reset skippedNonWhitespace value
                    skippedNonWhitespace = false;
                    yield return mergedSpan;
                    mergedSpan = span;
                }
            }

            if (!string.IsNullOrEmpty(mergedSpan.ClassificationType))
            {
                yield return mergedSpan;
            }
        }

        bool TryMergeSpan(ref ClassifiedSpan current, ClassifiedSpan next, ref bool skippedNonWhitespace)
        {
            if (next.ClassificationType == ClassificationTypeNames.WhiteSpace)
            {
                return true;
            }

            if (SkipSpan(next))
            {
                skippedNonWhitespace = true;
                return true;
            }

            try
            {
                if (string.IsNullOrEmpty(current.ClassificationType))
                {
                    current = next;
                    return true;
                }

                if (!AllowMerge(next))
                {
                    return false;
                }

                var normalizedClassification = NormalizeClassification(current);
                if (normalizedClassification != NormalizeClassification(next))
                {
                    return false;
                }

                if (current.TextSpan.End > next.TextSpan.Start && !skippedNonWhitespace)
                {
                    return false;
                }

                current = new ClassifiedSpan(normalizedClassification, new TextSpan(current.TextSpan.Start, next.TextSpan.End - current.TextSpan.Start));
                return true;
            }
            finally
            {
                skippedNonWhitespace = false;
            }
        }

        static bool AllowMerge(ClassifiedSpan span)
        {
            var classificationType = NormalizeClassification(span);

            switch (classificationType)
            {
                case ClassificationTypeNames.Comment:
                case ClassificationTypeNames.StringLiteral:
                case ClassificationTypeNames.XmlDocCommentComment:
                case ClassificationTypeNames.ExcludedCode:
                    return true;
                default:
                    return false;
            }
        }

        static bool SkipSpan(ClassifiedSpan span)
        {
            switch (span.ClassificationType)
            {
                case ClassificationTypeNames.WhiteSpace:
                case ClassificationTypeNames.Punctuation:
                case ClassificationTypeNames.Operator:
                    return true;
                default:
                    return false;
            }
        }

        static T ExchangeDefault<T>(ref T value)
        {
            var captured = value;
            value = default(T);
            return captured;
        }

        static string NormalizeClassification(ClassifiedSpan span)
        {
            if (span.ClassificationType == null)
            {
                return null;
            }

            switch (span.ClassificationType)
            {
                case ClassificationTypeNames.XmlDocCommentName:
                case ClassificationTypeNames.XmlDocCommentAttributeName:
                case ClassificationTypeNames.XmlDocCommentAttributeQuotes:
                case ClassificationTypeNames.XmlDocCommentCDataSection:
                case ClassificationTypeNames.XmlDocCommentComment:
                case ClassificationTypeNames.XmlDocCommentDelimiter:
                case ClassificationTypeNames.XmlDocCommentText:
                case ClassificationTypeNames.XmlDocCommentProcessingInstruction:
                    return ClassificationTypeNames.XmlDocCommentComment;
                default:
                    return span.ClassificationType;
            }
        }

        static bool IsSemanticSpan(ClassifiedSpan span)
        {
            switch (span.ClassificationType)
            {
                case ClassificationTypeNames.Keyword:
                case ClassificationTypeNames.Identifier:
                case ClassificationTypeNames.ClassName:
                case ClassificationTypeNames.InterfaceName:
                case ClassificationTypeNames.StructName:
                case ClassificationTypeNames.EnumName:
                case ClassificationTypeNames.DelegateName:
                case ClassificationTypeNames.TypeParameterName:
                    return true;
                default:
                    return false;
            }
        }

        private SyntaxNode GetBindableParent(SyntaxToken token)
        {
            return semanticServices.GetBindableParent(token);
        }
    }
}
