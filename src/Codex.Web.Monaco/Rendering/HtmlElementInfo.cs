using System.Collections.Generic;
using Codex.ObjectModel;

namespace WebUI.Rendering
{
    public class HtmlElementInfo
    {
        public string Name { get; set; }
        public Dictionary<string, string> Attributes { get; private set; }

        public bool RequiresWrappingSpan { get; set; }
        public Symbol DeclaredSymbol { get; set; }
        public string DeclaredSymbolId { get; set; }

        public HtmlElementInfo()
        {
            Attributes = new Dictionary<string, string>();
        }
    }
}