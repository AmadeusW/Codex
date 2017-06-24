using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Contracts;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Web;
using Codex.ObjectModel;

namespace WebUI.Rendering
{
    public class SourceFileRenderer
    {
        BoundSourceFile _sourceFile;
        private string projectId;

        public SourceFileRenderer(BoundSourceFile sourceFile, string projectId)
        {
            Contract.Requires(sourceFile != null);

            _sourceFile = sourceFile;
            this.projectId = projectId;
        }

        /// <summary>
        /// Gets the contents of the source file with <span> tags added around
        /// all the spans specified for this BoundSourceFile that have a
        /// class of the Classification for the Symbol
        /// </summary>
        /// <returns></returns>
        public async Task<EditorModel> RenderAsync()
        {
            var filePath = _sourceFile.SourceFile?.Info?.Path;
            var model = new EditorModel()
            {
                ProjectId = projectId,
                FilePath = filePath,
                WebLink = _sourceFile.SourceFile?.Info?.WebAddress,
                RepoRelativePath = _sourceFile.SourceFile?.Info?.RepoRelativePath
            };

            string sourceText = await _sourceFile.SourceFile.GetContentsAsync();
            int lineCount = GetLineCount(sourceText);
            var url = $"/?rightProject={HttpUtility.UrlEncode(projectId)}&file={HttpUtility.UrlEncode(filePath)}";
            model.LineNumberText = GenerateLineNumberText(lineCount, url);
            var ret = new StringBuilder();
            int textIndex = 0;

            Span prevSpan = null;

            using (StringWriter sw = new StringWriter(ret))
            {
                int referenceIndex = -1;
                ReferenceSpan referenceSpan = null;

                foreach (ClassificationSpan span in _sourceFile.ClassificationSpans.OrderBy(s => s.Start))
                {
                    if (span.Start > sourceText.Length)
                    { //Not sure how this happened but a span is off the end of our text
                        Debug.WriteLine(
                            $"Span had Start of {span.Start}, which is greater than text length for file '{_sourceFile.SourceFile.Info.Path}'", "BoundSourceFileMarkup");
                        break;
                    }
                    if (prevSpan != null && span.Start == prevSpan.Start)
                    {
                        //  Overlapping spans?
                        continue;
                    }

                    if (span.Start > textIndex)
                    { //Span is ahead of our current index, just write the normal text between the two to the buffer
                        ret.Append(HttpUtility.HtmlEncode(sourceText.Substring(textIndex, span.Start - textIndex)));
                        textIndex = span.Start;
                    }

                    string spanText = sourceText.Substring(span.Start, span.Length);
                    GenerateSpan(sw, span, spanText, ref referenceIndex, ref referenceSpan, _sourceFile.References);

                    textIndex += span.Length;
                    prevSpan = span;
                }

                // Append any leftover text
                ret.Append(HttpUtility.HtmlEncode(sourceText.Substring(textIndex)));

                model.Text = ret.ToString();
                return model;
            }
        }

        private static string GenerateLineNumberText(int lineNumbers, string documentUrl)
        {
            if (lineNumbers == 0)
            {
                return string.Empty;
            }

            string href = documentUrl + "&line=";
            var sb = new StringBuilder();

            for (int i = 1; i <= lineNumbers; i++)
            {
                var lineNumber = i.ToString();
                sb.AppendFormat(
                    "<a id=\"l{0}\" href=\"{1}\" onclick=\"GoToLine({0});return false;\">{0}</a><br/>",
                    lineNumber,
                    href + lineNumber);
            }

            return sb.ToString();
        }

        public static int GetLineCount(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return 0;
            }

            int lineCount = 0;
            bool previousWasCarriageReturn = false;

            for (int i = 0; i < text.Length; i++)
            {
                if (text[i] == '\r')
                {
                    if (previousWasCarriageReturn)
                    {
                        lineCount++;
                    }
                    else
                    {
                        previousWasCarriageReturn = true;
                    }
                }
                else if (text[i] == '\n')
                {
                    previousWasCarriageReturn = false;
                    lineCount++;
                }
                else
                {
                    previousWasCarriageReturn = false;
                }
            }

            lineCount++;

            return lineCount;
        }

        private void WriteReferenceText(TextWriter tw, ClassificationSpan span, string spanText, ref int referenceIndex, ref ReferenceSpan currentReference, IReadOnlyList<ReferenceSpan> referenceSpans)
        {
            int startPosition = span.Start;
            int currentPosition = span.Start;
            int end = span.End;

            while (currentReference != null && currentReference.Start >= currentPosition && currentReference.Start < end && currentReference.End <= end)
            {
                if (currentReference != null)
                {
                    GetBestReference(ref referenceIndex, ref currentReference, referenceSpans);
                }

                if (currentReference.Start > currentPosition)
                {
                    HttpUtility.HtmlEncode(spanText.Substring(currentPosition - startPosition, currentReference.Start - currentPosition), tw);
                    currentPosition = currentReference.Start;
                }

                if (currentReference.Length > 0)
                {
                    var htmlElementInfo = GenerateHyperlinkForReference(currentReference.Reference);
                    WriteHtmlElement(tw, htmlElementInfo, spanText.Substring(currentReference.Start - startPosition, currentReference.Length));

                    currentPosition = currentReference.End;
                }

                referenceIndex++;
                if (referenceIndex < referenceSpans.Count)
                {
                    currentReference = referenceSpans[referenceIndex];
                }
                else
                {
                    break;
                }
            }

            if (currentPosition < end)
            {
                HttpUtility.HtmlEncode(spanText.Substring(currentPosition - startPosition, end - currentPosition), tw);
            }
        }

        private void GetBestReference(ref int referenceIndex, ref ReferenceSpan currentReference, IReadOnlyList<ReferenceSpan> referenceSpans)
        {
            for (int i = referenceIndex; i < referenceSpans.Count; i++)
            {
                var reference = referenceSpans[i];
                if (reference.Start != currentReference.Start)
                {
                    break;
                }

                if (currentReference.Reference.IsImplicitlyDeclared || reference.Reference.ReferenceKind == nameof(ReferenceKind.Definition))
                {
                    currentReference = reference;
                    referenceIndex = i;
                }
            }
        }

        private void GenerateSpan(TextWriter tw, ClassificationSpan span, string spanText, ref int referenceIndex, ref ReferenceSpan currentReference, IReadOnlyList<ReferenceSpan> referenceSpans)
        {
            while ((currentReference == null || currentReference.Start < span.Start) && referenceIndex < referenceSpans.Count)
            {
                referenceIndex++;
                if (referenceIndex < referenceSpans.Count)
                {
                    currentReference = referenceSpans[referenceIndex];
                }
                else
                {
                    currentReference = null;
                }
            }

            if (currentReference != null)
            {
                GetBestReference(ref referenceIndex, ref currentReference, referenceSpans);
            }

            string cssClass = MapClassificationToCssClass(span.Classification);
            string referenceClass = string.Empty;
            if (span.LocalGroupId > 0)
            {
                referenceClass = $"r{span.LocalGroupId} r";
                cssClass = string.IsNullOrEmpty(cssClass) ? referenceClass : $"{referenceClass} {cssClass}";
            }

            HtmlElementInfo htmlElementInfo = null;
            if (currentReference?.SpanEquals(span) == true)
            {
                htmlElementInfo = GenerateHyperlinkForReference(currentReference.Reference);
            }

            if (htmlElementInfo == null && !span.Contains(currentReference))
            {
                if (cssClass == null)
                {
                    tw.Write(HttpUtility.HtmlEncode(spanText));
                    return;
                }
            }

            string elementName = "span";
            bool classAttributeSpecified = false;
            if (htmlElementInfo != null)
            {
                elementName = htmlElementInfo.Name;

                if (htmlElementInfo.RequiresWrappingSpan)
                {
                    tw.Write("<span");
                    AddAttribute(tw, "class", cssClass);
                    tw.Write(">");
                    classAttributeSpecified = true;
                }
            }

            tw.Write("<" + elementName);

            if (htmlElementInfo != null)
            {
                foreach (var attribute in htmlElementInfo.Attributes)
                {
                    if (AddAttribute(tw, attribute.Key, attribute.Value))
                    {
                        if (attribute.Key == "class")
                        {
                            classAttributeSpecified = true;
                        }
                    }
                }
            }

            if (!classAttributeSpecified)
            {
                AddAttribute(tw, "class", cssClass);
            }

            if (span.LocalGroupId > 0 && (htmlElementInfo?.Attributes?.ContainsKey("onclick") != true))
            {
                AddAttribute(tw, "onclick", "t(this);");
            }

            tw.Write(">");
            if (htmlElementInfo != null || !span.Contains(currentReference))
            {
                tw.Write(HttpUtility.HtmlEncode(spanText));
            }
            else
            {
                WriteReferenceText(tw, span, spanText, ref referenceIndex, ref currentReference, referenceSpans);
            }

            tw.Write("</" + elementName + ">");

            if (htmlElementInfo?.RequiresWrappingSpan == true)
            {
                tw.Write("</span>");
            }
        }

        void WriteHtmlElement(TextWriter tw, HtmlElementInfo htmlElementInfo, string innerText)
        {
            tw.Write("<" + htmlElementInfo.Name);
            foreach (var att in htmlElementInfo.Attributes)
            {
                AddAttribute(tw, att.Key, att.Value);
            }

            tw.Write(">");
            HttpUtility.HtmlEncode(innerText, tw);
            tw.Write("</" + htmlElementInfo.Name + ">");
        }

        bool AddAttribute(TextWriter tw, string name, string value)
        {
            if (value != null)
            {
                tw.Write(" " + name + "=\"" + value + "\"");
                return true;
            }

            return false;
        }

        HtmlElementInfo GenerateHyperlinkForReference(ReferenceSymbol symbol)
        {
            string idHash = symbol.Id.Value;

            bool isMsBuild = _sourceFile.SourceFile.Info.Language == "msbuild";
            bool isDistributedDefinition =
                symbol.Kind == nameof(SymbolKinds.MSBuildItem) ||
                symbol.Kind == nameof(SymbolKinds.MSBuildItemMetadata) ||
                symbol.Kind == nameof(SymbolKinds.MSBuildProperty) ||
                symbol.Kind == nameof(SymbolKinds.MSBuildTarget) ||
                symbol.Kind == nameof(SymbolKinds.MSBuildTask) ||
                symbol.Kind == nameof(SymbolKinds.MSBuildTaskParameter);
            var isDefinition = symbol.ReferenceKind == nameof(ReferenceKind.Definition) || isDistributedDefinition;

            bool isProjectScopedReference = symbol.ReferenceKind == nameof(ReferenceKind.ProjectLevelReference);
            if (!isDefinition)
            {
                idHash = null;
            }

            string jsmethod = isDefinition || isProjectScopedReference ? "R" : "D";
            string additionalParams = isProjectScopedReference ? $@", '{projectId}'" : string.Empty;
            string onclick = $@"{jsmethod}('{symbol.ProjectId}', '{symbol.Id}'{additionalParams});return false;";

            string url = "";
            if (isDefinition)
            {
                url = $"/?leftProject={symbol.ProjectId}&leftSymbol={symbol.Id}&file={HttpUtility.UrlEncode(this._sourceFile.SourceFile.Info.Path)}";
            }
            else if (isProjectScopedReference)
            {
                url = $"/?leftProject={symbol.ProjectId}&leftSymbol={symbol.Id}&projectScope={projectId}";
            }
            else
            {
                url = $"/?rightProject={symbol.ProjectId}&rightSymbol={symbol.Id}";
            }

            var result = new HtmlElementInfo()
            {
                Name = "a",
                Attributes =
                {
                    { "id", idHash },
                    { "onclick", onclick },
                    { "href", url },
                    { "class", isDistributedDefinition ? "msbuildlink" : null }
                },
                DeclaredSymbolId = symbol.Id.Value,
                RequiresWrappingSpan = isMsBuild,
            };

            return result;
        }

        private static HashSet<string> ignoreClassifications = new HashSet<string>(new[]
            {
                "operator",
                "number",
                "punctuation",
                "preprocessor text",
                "xml literal - text",
                "xml - text"
            });

        private static Dictionary<string, string> replaceClassifications = new Dictionary<string, string>
            {
                { "xml - delimiter", Constants.ClassificationXmlDelimiter },
                { "xml - name", Constants.ClassificationXmlName },
                { "xml - attribute name", Constants.ClassificationXmlAttributeName },
                { "xml - attribute quotes", Constants.ClassificationXmlAttributeQuotes },
                { "xml - attribute value", Constants.ClassificationXmlAttributeValue },
                { "xml - entity reference", Constants.ClassificationXmlEntityReference },
                { "xml - cdata section", Constants.ClassificationXmlCDataSection },
                { "xml - processing instruction", Constants.ClassificationXmlProcessingInstruction },
                { "xml - comment", Constants.ClassificationComment },

                { "keyword", Constants.ClassificationKeyword },
                { "identifier", Constants.ClassificationIdentifier },
                { "class name", Constants.ClassificationTypeName },
                { "struct name", Constants.ClassificationTypeName },
                { "interface name", Constants.ClassificationTypeName },
                { "enum name", Constants.ClassificationTypeName },
                { "delegate name", Constants.ClassificationTypeName },
                { "module name", Constants.ClassificationTypeName },
                { "type parameter name", Constants.ClassificationTypeName },
                { "preprocessor keyword", Constants.ClassificationKeyword },
                { "xml doc comment - delimiter", Constants.ClassificationComment },
                { "xml doc comment - name", Constants.ClassificationComment },
                { "xml doc comment - text", Constants.ClassificationComment },
                { "xml doc comment - comment", Constants.ClassificationComment },
                { "xml doc comment - entity reference", Constants.ClassificationComment },
                { "xml doc comment - attribute name", Constants.ClassificationComment },
                { "xml doc comment - attribute quotes", Constants.ClassificationComment },
                { "xml doc comment - attribute value", Constants.ClassificationComment },
                { "xml doc comment - cdata section", Constants.ClassificationComment },
                { "xml literal - delimiter", Constants.ClassificationXmlLiteralDelimiter },
                { "xml literal - name", Constants.ClassificationXmlLiteralName },
                { "xml literal - attribute name", Constants.ClassificationXmlLiteralAttributeName },
                { "xml literal - attribute quotes", Constants.ClassificationXmlLiteralAttributeQuotes },
                { "xml literal - attribute value", Constants.ClassificationXmlLiteralAttributeValue },
                { "xml literal - entity reference", Constants.ClassificationXmlLiteralEntityReference },
                { "xml literal - cdata section", Constants.ClassificationXmlLiteralCDataSection },
                { "xml literal - processing instruction", Constants.ClassificationXmlLiteralProcessingInstruction },
                { "xml literal - embedded expression", Constants.ClassificationXmlLiteralEmbeddedExpression },
                { "xml literal - comment", Constants.ClassificationComment },
                { "comment", Constants.ClassificationComment },
                { "string", Constants.ClassificationLiteral },
                { "string - verbatim", Constants.ClassificationLiteral },
                { "excluded code", Constants.ClassificationExcludedCode },
            };

        public static string MapClassificationToCssClass(string classificationType)
        {
            if (classificationType == null || ignoreClassifications.Contains(classificationType))
            {
                return null;
            }

            if (classificationType == Constants.ClassificationKeyword)
            {
                return classificationType;
            }

            string replacement = null;
            if (replaceClassifications.TryGetValue(classificationType, out replacement))
            {
                classificationType = replacement;
            }

            if (classificationType == null ||
                classificationType == "" ||
                classificationType == Constants.ClassificationIdentifier ||
                classificationType == Constants.ClassificationPunctuation)
            {
                // identifiers are conveniently black by default so let's save some space
                return null;
            }

            return classificationType;
        }

        public class Constants
        {
            //public static readonly string IDResolvingFileName = "A";
            //public static readonly string PartialResolvingFileName = "P";
            //public static readonly string ReferencesFileName = "R";
            //public static readonly string DeclaredSymbolsFileName = "D";
            //public static readonly string MasterIndexFileName = "DeclaredSymbols.txt";
            //public static readonly string ReferencedAssemblyList = "References";
            //public static readonly string UsedReferencedAssemblyList = "UsedReferences";
            //public static readonly string ReferencingAssemblyList = "ReferencingAssemblies";
            //public static readonly string ProjectInfoFileName = "i";
            //public static readonly string MasterProjectMap = "Projects";
            //public static readonly string MasterAssemblyMap = "Assemblies";
            //public static readonly string Namespaces = "namespaces.html";

            public static readonly string ClassificationIdentifier = "i";
            public static readonly string ClassificationKeyword = "k";
            public static readonly string ClassificationTypeName = "t";
            public static readonly string ClassificationComment = "c";
            public static readonly string ClassificationLiteral = "s";

            public static readonly string ClassificationXmlDelimiter = "xd";
            public static readonly string ClassificationXmlName = "xn";
            public static readonly string ClassificationXmlAttributeName = "xan";
            public static readonly string ClassificationXmlAttributeValue = "xav";
            public static readonly string ClassificationXmlAttributeQuotes = null;
            public static readonly string ClassificationXmlEntityReference = "xer";
            public static readonly string ClassificationXmlCDataSection = "xcs";
            public static readonly string ClassificationXmlProcessingInstruction = "xpi";

            public static readonly string ClassificationXmlLiteralDelimiter = "xld";
            public static readonly string ClassificationXmlLiteralName = "xln";
            public static readonly string ClassificationXmlLiteralAttributeName = "xlan";
            public static readonly string ClassificationXmlLiteralAttributeValue = "xlav";
            public static readonly string ClassificationXmlLiteralAttributeQuotes = "xlaq";
            public static readonly string ClassificationXmlLiteralEntityReference = "xler";
            public static readonly string ClassificationXmlLiteralCDataSection = "xlcs";
            public static readonly string ClassificationXmlLiteralEmbeddedExpression = "xlee";
            public static readonly string ClassificationXmlLiteralProcessingInstruction = "xlpi";

            public static readonly string ClassificationExcludedCode = "e";
            //public static readonly string RoslynClassificationKeyword = "keyword";
            //public static readonly string DeclarationMap = "DeclarationMap";
            public static readonly string ClassificationPunctuation = "punctuation";
            //public static readonly string ProjectExplorer = "ProjectExplorer";
            //public static readonly string SolutionExplorer = "SolutionExplorer";
            //public static readonly string HuffmanFileName = "Huffman.txt";
            //public static readonly string TopReferencedAssemblies = "TopReferencedAssemblies";
            //public static readonly string BaseMembersFileName = "BaseMembers";
            //public static readonly string ImplementedInterfaceMembersFileName = "ImplementedInterfaceMembers";
            //public static readonly string GuidAssembly = "GuidAssembly";
            //public static readonly string MSBuildPropertiesAssembly = "MSBuildProperties";
            //public static readonly string MSBuildItemsAssembly = "MSBuildItems";
            //public static readonly string MSBuildTargetsAssembly = "MSBuildTargets";
            //public static readonly string MSBuildTasksAssembly = "MSBuildTasks";
            //public static readonly string MSBuildFiles = "MSBuildFiles";
            //public static readonly string TypeScriptFiles = "TypeScriptFiles";
            //public static readonly string AssemblyPaths = @"AssemblyPaths.txt";
        }
    }
}