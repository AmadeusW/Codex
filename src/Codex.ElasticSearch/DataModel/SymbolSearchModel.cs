using System.Collections.Generic;

namespace Codex.Storage.DataModel
{
    public class SymbolSearchResultModel
    {
        public List<DefinitionSearchSpanModel> Entries { get; set; }

        public int Total { get; set; }
    }
}