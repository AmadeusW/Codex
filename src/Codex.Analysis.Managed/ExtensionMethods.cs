using Codex.Utilities;
using Microsoft.CodeAnalysis;

namespace Codex.Analysis
{
    public static class ExtensionMethods
    {
        public static string GetDisplayString(this ISymbol symbol)
        {
            var specialType = symbol.As<ITypeSymbol>()?.SpecialType;
            bool isSpecialType = specialType.GetValueOrDefault(SpecialType.None) != SpecialType.None;

            return isSpecialType ? symbol.ToDisplayString(DisplayFormats.QualifiedNameDisplayFormat) : symbol.ToDisplayString(DisplayFormats.GetDisplayFormat(symbol.Language));
        }

        public static int GetSymbolDepth(this ISymbol symbol)
        {
            ISymbol current = symbol.ContainingSymbol;
            int depth = 0;
            while (current != null)
            {
                var namespaceSymbol = current as INamespaceSymbol;
                if (namespaceSymbol != null)
                {
                    // if we've reached the global namespace, we're already at the top; bail
                    if (namespaceSymbol.IsGlobalNamespace)
                    {
                        break;
                    }
                }
                else
                {
                    // we don't want namespaces to add to our "depth" because they won't be displayed in the tree
                    depth++;
                }

                current = current.ContainingSymbol;
            }

            return depth;
        }
    }
}
