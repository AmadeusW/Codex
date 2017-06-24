using System;
using Nest;
using System.Collections.Generic;
using Newtonsoft.Json;
using Codex.ObjectModel;

namespace Codex.Storage.DataModel
{
    public enum Classification
    {
        Class,
        Struct,
        Interface,

        Method,
        Field,
    }

    public static class ClassificationEx
    {
        public static Classification? TryParse(string str)
        {
            Classification result;
            if (Enum.TryParse(str, true, out result))
            {
                return result;
            }

            return null;
        }

        public static string Text(this Classification classification)
        {
            switch (classification)
            {
                case Classification.Class:
                    return "class name";
                case Classification.Struct:
                    return "struct name";
                case Classification.Interface:
                    return "interface name";
                case Classification.Method:
                case Classification.Field:
                    return "identifier";
                default:
                    throw new ArgumentOutOfRangeException(nameof(classification), classification, null);
            }
        }

        public static string Kind(this Classification classification)
        {
            switch (classification)
            {
                case Classification.Class:
                case Classification.Struct:
                case Classification.Interface:
                    return "NamedType";
                case Classification.Method:
                    return "Method";
                case Classification.Field:
                    return "Field";
                default:
                    throw new ArgumentOutOfRangeException(nameof(classification), classification, null);
            }
        }
    }

    public class ReferencedProjectModel
    {
        /// <summary>
        /// The identifier of the referenced project
        /// </summary>
        [String(Index = FieldIndexOption.NotAnalyzed)]
        public string ProjectId { get; set; }

        /// <summary>
        /// Used definitions for the project. Sorted.
        /// </summary>
        [Object]
        public List<DefinitionSymbolModel> Definitions { get; set; } = new List<DefinitionSymbolModel>();

        public override string ToString()
        {
            return ProjectId;
        }
    }

    public class SpanModel
    {
        /// <summary>
        /// The absolute character position where the span starts in the documents
        /// </summary>
        public int Start { get; set; }

        /// <summary>
        /// The length of the span
        /// </summary>
        public int Length { get; set; }
    }

    public class ClassificationSpanModel : SpanModel
    {
        /// <summary>
        /// The classification identifier used to colorize the span
        /// </summary>
        [String(Index = FieldIndexOption.NotAnalyzed)]
        public string Classification { get; set; }

        /// <summary>
        /// The default classification color for the span. This is used for
        /// contexts where a mapping from classification id to color is not
        /// available.
        /// </summary>
        public int DefaultClassificationColor { get; set; }

        public int GetEstimatedSize() => (Classification?.Length * 2 ?? 0) + 10;
    }

    public class SymbolSpanModelBase : SpanModel
    {
        /// <summary>
        /// The character position where the span starts in the line text
        /// </summary>
        [DataInteger]
        [JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore)]
        public int LineNumber { get; set; }

        /// <summary>
        /// The character position where the span starts in the line text
        /// </summary>
        [DataInteger]
        [JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore)]
        public int LineSpanStart { get; set; }
    }

    public class SymbolSpanModel : SymbolSpanModelBase
    {
        /// <summary>
        /// The line text
        /// TODO: Remove this. Currently, only used to carry the data until
        /// search span is created. Search span should be created directly.
        /// </summary>
        [String(Ignore = true)]
        public string LineSpanText { get; set; }

        public SymbolLineSpanModel ToLineSpan()
        {
            return new SymbolLineSpanModel()
            {
                Start = Start,
                Length = Length,
                LineSpanStart = LineSpanStart,
                LineSpanText = LineSpanText,
                LineNumber = LineNumber
            };
        }
    }

    public class SymbolLineSpanModel : SymbolSpanModelBase
    {
        /// <summary>
        /// The line text
        /// </summary>
        [String(Index = FieldIndexOption.No)]
        public string LineSpanText { get; set; }
    }

    public class ReferenceSpanModel : SymbolSpanModel
    {
        /// <summary>
        /// The reference symbol
        /// </summary>
        public ReferenceSymbolModel Reference { get; set; }

        /// <summary>
        /// Gets the symbol id of the definition which provides this reference 
        /// (i.e. method definition for interface implementation)
        /// </summary>
        [DataString]
        [JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore)]
        public string RelatedDefinition { get; set; }

        public int GetEstimatedSize() => Reference?.GetEstimatedSize() ?? 0 + 10;
    }

    public class DefinitionSpanModel : SymbolSpanModel
    {
        /// <summary>
        /// The definition symbol
        /// </summary>
        public DefinitionSymbolModel Definition { get; set; }

        public int GetEstimatedSize() => Definition?.GetEstimatedSize() ?? 0 + 10;
    }

    [ElasticsearchType(Name = ElasticProviders.ElasticProvider.SearchDefinitionTypeName, IdProperty = nameof(Uid))]
    public class DefinitionSearchSpanModel : VersionedSearchModelBase
    {
        /// <summary>
        /// The unique identifier for the symbol
        /// NOTE: This is not applicable to most symbols. Only set for symbols
        /// which are added in a shared context and need this for deduplication)
        /// </summary>
        [JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore)]
        public string Uid { get; set; }

        [JsonIgnore]
        public string ReferenceKind { get; set; }

        [Sortword]
        public string[] Tags { get; set; }

        public DefinitionSpanModel Span { get; set; }
    }

    ///// <summary>
    ///// TODO: Deprecate
    ///// </summary>
    //public class ReferenceSearchSpanModel : SearchSpanModel
    //{
    //    /// <summary>
    //    /// The line text.
    //    /// </summary>
    //    [String(Index = FieldIndexOption.No)]
    //    public string LineSpanText { get; set; }

    //    public ReferenceSpanModel Span { get; set; }
    //}

    public class ReferenceSearchResultModel : VersionedSearchModelBase
    {
        [Sortword]
        public string ProjectId { get; set; }

        /// <summary>
        /// The reference symbol
        /// </summary>
        public ReferenceSymbolModel Reference { get; set; }

        /// <summary>
        /// The related symbols for the reference
        /// </summary>
        [Keyword]
        [JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore)]
        public List<string> RelatedDefinitions { get; set; }

        [DataObject]
        public SymbolLineSpanListModel SymbolLineSpanList { get; set; }

        [DataObject]
        public List<SymbolLineSpanModel> SymbolLineSpans { get; set; }
    }
}