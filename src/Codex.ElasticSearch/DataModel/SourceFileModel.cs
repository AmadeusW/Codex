using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using Codex.Storage.Utilities;
using Nest;
using Newtonsoft.Json;

namespace Codex.Storage.DataModel
{
    [ElasticsearchType(Name = ElasticProviders.ElasticProvider.SearchSourceTypeName, IdProperty = nameof(Uid))]
    public class SourceFileModel
    {
        /// <summary>
        /// The unique identifier for the file
        /// NOTE: This is not applicable to most files. Only set for files
        /// which are added in a shared context and need this for deduplication)
        /// </summary>
        [JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore)]
        public string Uid { get; set; }

        [Keyword]
        public string MergeId { get; set; } = Guid.NewGuid().ToString();

        // NOTE: We DO return search references/definitions for files that have ExcludeFromSearch true because
        // the file reference needs to be found for discovering files in the project. This only applies to content
        public bool ExcludeFromSearch { get; set; }

        [Sortword]
        public string RepoId { get; set; }

        [DataString]
        public string WebAddress { get; set; }

        [Sortword]
        public string ProjectId { get; set; }

        [HierachicalPath]
        public string Path { get; set; }

        [HierachicalPath]
        public string RepoRelativePath { get; set; }

        [Sortword]
        public string Language { get; set; }

        [FullText(DataInclusionOptions.Content)]
        public string Content { get; set; }

        [DataObject(DataInclusionOptions.Definitions)]
        public List<DefinitionSpanModel> Definitions { get; set; } = new List<DefinitionSpanModel>();

        [DataObject(DataInclusionOptions.References)]
        public ReferenceListModel References { get; set; }

        [Object(Ignore = true)]
        public List<ReferenceSpanModel> SearchReferencesSource { get; set; }

        [DataObject(DataInclusionOptions.Classifications)]
        public ClassificationListModel Classifications { get; set; }

        [Object(Ignore = true)]
        public IReadOnlyDictionary<string, string> Properties { get; set; } = new Dictionary<string, string>();

        public SourceFileModel()
        {
        }

        [OnSerializing]
        public void OnSerializing(StreamingContext context)
        {
            Content = FullTextUtilities.EncodeFullTextString(Content);
        }

        [OnDeserialized]
        public void OnDeserialized(StreamingContext context)
        {
            Content = FullTextUtilities.DecodeFullTextString(Content);
        }

        public IEnumerable<PropertyModel> GetSearchProperties()
        {
            if (Properties == null)
            {
                yield break;
            }

            foreach (var property in Properties)
            {
                yield return new PropertyModel()
                {
                    ObjectId = MergeId,
                    Key = property.Key,
                    Value = property.Value
                };
            }
        }

        public IEnumerable<DefinitionSearchSpanModel> GetSearchDefinitions()
        {
            var fileTags = GetFileTags().Distinct().ToArray();
            if (!DataInclusion.HasOption(DataInclusionOptions.SearchDefinitions))
            {
                yield break;
            }

            foreach (var definition in Definitions)
            {
                if (definition.Definition.ExcludeFromSearch)
                {
                    continue;
                }

                yield return new DefinitionSearchSpanModel()
                {
                    FileMergeId = MergeId,
                    Span = definition,
                    //LineSpanText = definition.LineSpanText,
                    FilePath = Path,
                    Language = Language,
                    Tags = fileTags,
                };
            }
        }

        private IEnumerable<string> GetFileTags()
        {
            yield return ProjectId.ToLowerInvariant();

            if (!string.IsNullOrEmpty(Path))
            {
                var path = Path.ToLowerInvariant();

                string fileName = path.Substring(path.LastIndexOfAny(new[] { '\\', '/' }) + 1);
                yield return fileName;

                var extensionSeparatorIndex = fileName.LastIndexOf('.');
                if (extensionSeparatorIndex > -1)
                {
                    yield return fileName.Substring(extensionSeparatorIndex + 1);

                    yield return fileName.Substring(0, extensionSeparatorIndex);
                }
            }
        }

        public IEnumerable<ReferenceSearchResultModel> GetSearchReferences()
        {
            if (!DataInclusion.HasOption(DataInclusionOptions.SearchReferences))
            {
                yield break;
            }

            var referenceLookup = SearchReferencesSource
                .Where(r => !(r.Reference.ExcludeFromSearch))
                .ToLookup(r => r.Reference, ReferenceListModel.ReferenceSymbolModelComparer);

            foreach (var referenceGroup in referenceLookup.OrderBy(r => r.Key.Id).ThenBy(r => r.Key.ProjectId))
            {
                var searchResultModel = new ReferenceSearchResultModel()
                {
                    FileMergeId = MergeId,
                    FilePath = Path,
                    Language = Language,
                    ProjectId = ProjectId,
                    Reference = referenceGroup.Key,
                };

                if (referenceGroup.Any(r => r.RelatedDefinition != null))
                {
                    searchResultModel.RelatedDefinitions = new List<string>(
                        referenceGroup.Select(r => r.RelatedDefinition).Where(rd => rd != null).Distinct());
                }

                if (referenceGroup.Count() < 10)
                {
                    searchResultModel.SymbolLineSpans = new List<SymbolLineSpanModel>(
                        referenceGroup.Select(reference => reference.ToLineSpan()));
                }
                else
                {
                    searchResultModel.SymbolLineSpanList = new SymbolLineSpanListModel(referenceGroup.ToObjectModel());
                }

                yield return searchResultModel;
            }
        }

        public int GetEstimatedSize() => Content?.Length * 2 ?? 0
            + Classifications?.GetEstimatedSize() ?? 0
             + SearchReferencesSource?.Select(x => x.GetEstimatedSize()).Sum() ?? 0
             + Definitions?.Select(x => x.GetEstimatedSize()).Sum() ?? 0;
    }
}