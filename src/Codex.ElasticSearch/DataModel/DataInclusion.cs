using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Codex.Storage.DataModel
{
    public enum DataInclusionOptions
    {
        None = 0,
        Definitions = 1,
        References = 1 << 1,
        Classifications = 1 << 2,
        SearchDefinitions = 1 << 3,
        SearchReferences = 1 << 4,
        Content = 1 << 5,
        All = Definitions | References | Classifications | SearchDefinitions | SearchReferences | Content,

        // Default does not include definitions since they can be queried lazily rather than eagerly retrieved.
        Default = References | Classifications | SearchDefinitions | SearchReferences | Content
    }

    internal static class DataInclusion
    {
        /// <summary>
        /// Removing definitions from inclusion to
        /// </summary>
        public static readonly DataInclusionOptions Options = GetDataInclusion();

        public static DataInclusionOptions GetDataInclusion()
        {
            var dataInclusionValue = Environment.GetEnvironmentVariable("DataInclusion");

            if (!string.IsNullOrEmpty(dataInclusionValue))
            {
                DataInclusionOptions options = DataInclusionOptions.None;
                foreach (var option in dataInclusionValue.Split(new[] { '|' }, StringSplitOptions.RemoveEmptyEntries).Select(s => s.Trim()))
                {
                    options |= (DataInclusionOptions)Enum.Parse(typeof(DataInclusionOptions), option);
                }

                Console.WriteLine("DataInclusion={0}", options);
                return options;
            }

            return DataInclusionOptions.Default;
        }

        public static bool HasOption(DataInclusionOptions option)
        {
            return (Options & option) == option;
        }
    }
}
