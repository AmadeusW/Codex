using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Codex.ObjectModel;

namespace Codex.Search
{
    public static class SearchUtilities
    {
        public class ReferenceSearchExtensionData : ExtensionData
        {
            public string ProjectScope;
        }

        public static Symbol SetProjectScope(this Symbol symbol, string projectScope)
        {
            var refData = symbol.GetReferenceSearchExtensionData();
            if (refData == null)
            {
                refData = new ReferenceSearchExtensionData();
                symbol.SetReferenceSearchExtensionData(refData);
            }

            refData.ProjectScope = projectScope;
            return symbol;
        }

        public static Symbol SetReferenceSearchExtensionData(this Symbol symbol, ReferenceSearchExtensionData data)
        {
            symbol.ExtData = data;
            return symbol;
        }

        public static ReferenceSearchExtensionData GetReferenceSearchExtensionData(this Symbol symbol)
        {
            return symbol.ExtData as ReferenceSearchExtensionData;
        }
    }
}
