using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Codex.ObjectModel
{
    public class LanguageDescriptor
    {
        public readonly string Name;

        public readonly string DisplayName;

        public Dictionary<string, ushort> SymbolKindSortKeys { get; set; } = new Dictionary<string, ushort>();

        public Dictionary<string, ushort> ReferenceKindSortKeys { get; set; } = new Dictionary<string, ushort>();

        public LanguageDescriptor(string name, string displayName)
        {
            Name = name;
            DisplayName = displayName;
        }
    }
}
