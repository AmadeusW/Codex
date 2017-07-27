using Codex.Storage.ElasticProviders;
using Nest;

namespace Codex.Storage.DataModel
{
    using System.ComponentModel;
    using Newtonsoft.Json;
    using static CustomAnalyzers;

    public class DefinitionSymbolModel : SymbolModel
    {
        [DataString]
        public string Comment { get; set; }

        [DataString]
        public string TypeName { get; set; }

        [DataString]
        public string ShortDisplayName { get; set; }

        /// <summary>
        /// The display name of the symbol
        /// </summary>
        /// <remarks>
        /// Definition only
        /// </remarks>
        [DataString]
        public string DisplayName { get; set; }

        /// <summary>
        /// The short name of the symbol (i.e. Task).
        /// This is used to find the symbol when search term does not contain '.'
        /// </summary>
        /// <remarks>
        /// Definition only
        /// </remarks>
        [SearchString(Analyzer = PrefixFilterPartialNameNGramAnalyzerName)]
        public string ShortName { get; set; }

        /// <summary>
        /// The qualified name of the symbol (i.e. System.Threading.Tasks.Task).
        /// This is used to find the symbol when the search term contains '.'
        /// </summary>
        /// <remarks>
        /// Definition only
        /// </remarks>
        [SearchString(Analyzer = PrefixFilterFullNameNGramAnalyzerName, Name = "QualifiedName")]
        public string ContainerQualifiedName { get; set; }

        [DataString]
        [JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore)]
        public string Glyph { get; set; }

        [DataInteger]
        [JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore)]
        public int SymbolDepth { get; set; }

        /// <summary>
        /// The unique identifier for the symbol
        /// NOTE: This property is only used to carry information for <see cref="DefinitionSearchSpanModel"/>
        /// NOTE: This is not applicable to most symbols. Only set for symbols
        /// which are added in a shared context and need this for deduplication).
        /// </summary>
        [DataString]
        [JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore)]
        public string Uid { get; set; }

        public int GetEstimatedSize() => (ProjectId?.Length ?? 0 + Id?.Length ?? 0 + DisplayName?.Length ?? 0 + ShortName?.Length ?? 0 + ContainerQualifiedName?.Length ?? 0) * 2 + Kind?.Length ?? 0;
    }

    public class ReferenceSymbolModel : SymbolModel
    {
        /// <summary>
        /// The type of reference. Read | Write | ReadWrite
        /// </summary>
        [Sortword]
        [JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore)]
        public string ReferenceKind { get; set; }

        public int GetEstimatedSize() => (ProjectId?.Length ?? 0 + Id?.Length ?? 0) * 2 + Kind?.Length ?? 0 + ReferenceKind?.Length ?? 0;
    }

    public class SymbolModel
    {
        /// <summary>
        /// The identifier of the project containing the symbol (i.e. mscorlib)
        /// </summary>
        /// <remarks>
        /// Reference and definition
        /// </remarks>
        [SearchString(Analyzer = LowerCaseKeywordAnalyzerName)]
        [JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore)]
        public string ProjectId { get; set; }

        /// <summary>
        /// The identifier of the the symbol (i.e. T:System.Threading.Tasks.Task)
        /// </summary>
        /// <remarks>
        /// Reference and definition
        /// </remarks>
        [Keyword]
        [JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore)]
        public string Id { get; set; }

        /// <summary>
        /// Symbol type (or kind). Similar to <see cref="Microsoft.CodeAnalysis.SymbolKind"/>
        /// </summary>
        [JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore)]
        [SearchString(Analyzer = LowerCaseKeywordAnalyzerName)]
        public string Kind { get; set; }

        /// <summary>
        /// Indicates if the symbol should be excluded from the definition/find all references search (by default).
        /// Symbol will only be included if kind is explicitly specified
        /// </summary>
        [Boolean(NullValue = false)]
        [JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore)]
        public bool ExcludeFromDefaultSearch { get; set; }

        /// <summary>
        /// Indicates if the symbol should NEVER be included in the definition/find all references search.
        /// </summary>
        [Boolean(NullValue = false)]
        [JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore)]
        public bool ExcludeFromSearch { get; set; }

        /// <summary>
        /// Indicates the corresponding definition is implicitly declared and therefore this should not be
        /// used for find all references search
        /// </summary>
        [DataBoolean]
        [JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore)]
        public bool IsImplicitlyDeclared { get; set; }
    }
}