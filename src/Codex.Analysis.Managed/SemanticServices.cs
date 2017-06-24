using System;
using System.Threading;
using Codex.Utilities;
using Microsoft.CodeAnalysis;

using CS = Microsoft.CodeAnalysis.CSharp;
using VB = Microsoft.CodeAnalysis.VisualBasic;

namespace Codex.Analysis
{
    public class SemanticServices
    {
        private readonly Func<SyntaxToken, SyntaxNode> getBindableParentDelegate;
        private readonly Func<SemanticModel, SyntaxNode, CancellationToken, bool> isWrittenToDelegate;
        private readonly string language;

        public SemanticServices(Workspace workspace, string language)
        {
            this.language = language;

            var syntaxFactsService = WorkspaceHacks.GetSyntaxFactsService(workspace, language);
            var semanticFactsService = WorkspaceHacks.GetSemanticFactsService(workspace, language);

            var semanticFactsServiceType = semanticFactsService.GetType();
            var isWrittenTo = semanticFactsServiceType.GetMethod("IsWrittenTo");
            isWrittenToDelegate = (Func<SemanticModel, SyntaxNode, CancellationToken, bool>)
                Delegate.CreateDelegate(typeof(Func<SemanticModel, SyntaxNode, CancellationToken, bool>), semanticFactsService, isWrittenTo);

            var syntaxFactsServiceType = syntaxFactsService.GetType();
            var getBindableParent = syntaxFactsServiceType.GetMethod("GetBindableParent");
            getBindableParentDelegate = (Func<SyntaxToken, SyntaxNode>)
                Delegate.CreateDelegate(typeof(Func<SyntaxToken, SyntaxNode>), syntaxFactsService, getBindableParent);
        }

        public SyntaxNode GetBindableParent(SyntaxToken syntaxToken)
        {
            return getBindableParentDelegate(syntaxToken);
        }

        public bool IsWrittenTo(SemanticModel semanticModel, SyntaxNode syntaxNode, CancellationToken cancellationToken)
        {
            return isWrittenToDelegate(semanticModel, syntaxNode, cancellationToken);
        }

        public bool IsNewKeyword(SyntaxToken token)
        {
            return (language == LanguageNames.CSharp && token.IsKind(Microsoft.CodeAnalysis.CSharp.SyntaxKind.NewKeyword))
                || (language == LanguageNames.VisualBasic && token.IsKind(Microsoft.CodeAnalysis.VisualBasic.SyntaxKind.NewKeyword));
        }

        public bool IsOverrideKeyword(SyntaxToken token)
        {
            return (language == LanguageNames.CSharp && token.IsKind(CS.SyntaxKind.OverrideKeyword))
                || (language == LanguageNames.VisualBasic && token.IsKind(VB.SyntaxKind.OverridesKeyword));
        }

        public SyntaxNode TryGetUsingExpressionFromToken(SyntaxToken token)
        {
            if (language == LanguageNames.CSharp)
            {
                if (token.IsKind(CS.SyntaxKind.UsingKeyword))
                {
                    var node = token.Parent;
                    if (node.IsKind(CS.SyntaxKind.UsingStatement))
                    {
                        var usingStatement = (CS.Syntax.UsingStatementSyntax)node;

                        return (SyntaxNode)usingStatement.Expression ?? usingStatement.Declaration?.Type;
                    }
                }
            }
            else if(language == LanguageNames.VisualBasic)
            {
                if (token.IsKind(VB.SyntaxKind.UsingKeyword))
                {
                    var node = token.Parent;
                    if (node.IsKind(VB.SyntaxKind.UsingStatement))
                    {
                        var usingStatement = (VB.Syntax.UsingStatementSyntax)node;
                        return (SyntaxNode)usingStatement.Expression ?? usingStatement.Variables.FirstOrDefault();
                    }
                }
            }

            return null;
        }

        public SyntaxNode GetEventField(SyntaxNode bindableNode)
        {
            if (language == LanguageNames.CSharp)
            {
                if (bindableNode.IsKind(Microsoft.CodeAnalysis.CSharp.SyntaxKind.EventFieldDeclaration))
                {
                    var eventFieldSyntax = bindableNode as Microsoft.CodeAnalysis.CSharp.Syntax.EventFieldDeclarationSyntax;
                    if (eventFieldSyntax != null)
                    {
                        bindableNode = eventFieldSyntax.Declaration.Variables[0];
                    }
                }
            }

            return bindableNode;
        }
    }
}
