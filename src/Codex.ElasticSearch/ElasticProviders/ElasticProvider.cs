using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Contracts;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;
using Codex.ObjectModel;
using Codex.Storage.DataModel;
using Nest;
using static Nest.Infer;
using static Codex.Storage.ElasticProviders.ElasticUtility;
using Classification = Codex.Storage.DataModel.Classification;
using System.Text;
using Codex.Storage.Utilities;
using Elasticsearch.Net;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using Codex.Utilities;
using Codex.Search;

namespace Codex.Storage.ElasticProviders
{
    public sealed class ElasticProvider
    {
        private static string[] _emptyArray = new string[] { };

        private readonly ElasticProviderConfig _providerConfig;

        private const string PathToSpansDefinition = "definitions";
        private const string PathToSpansReference = "references";

        public const string SearchSourceTypeName = "sources";
        public const string SearchPropertiesTypeName = "props";
        public const string SearchDefinitionTypeName = "defs";
        public const string SearchReferenceTypeName = "refs";

        private const int SpansMaxLimit = int.MaxValue;
        private const int DefaultMaxNumberOfItems = ElasticProviderConfig.DefaultMaxNumbersOfRecordsToReturn;

        private const int DefaultBatchSize = 100;

        private string Endpoint => _providerConfig.Endpoint;
        private string CoreIndexName => _providerConfig.CoreIndexName;
        private string ProjectTypeName => _providerConfig.ProjectTypeName;
        private string SourcesTypeName => _providerConfig.SourceTypeName;
        private string RepositoryTypeName => _providerConfig.RepositoryTypeName;
        private string CombinedSourcesIndexAlias => _providerConfig.CombinedSourcesIndexAlias;

        // Local cache with indexName -> Task mapping
        // TODO: add eviction policy to this cache
        private readonly ConcurrentDictionary<string, Task> _createdIndices = new ConcurrentDictionary<string, Task>();
        private readonly ConcurrentDictionary<string, Task<string>> _repoSourceIndexNames = new ConcurrentDictionary<string, Task<string>>();
        private Task<ElasticResponse> _createMainIndexTask;

        internal ElasticClient Client { get; private set; }

        #region Constructors
        internal ElasticProvider(ElasticProviderConfig providerConfig)
        {
            Contract.Requires(providerConfig != null);
            _providerConfig = providerConfig;
            Client = CreateClientCore();
        }

        private ElasticClient CreateClient()
        {
            return Client;
        }

        private ElasticClient CreateClientCore()
        {

            var settings = new CodexConnectionSettings(new Uri(Endpoint))
                .DisableDirectStreaming(true)
                .PrettyJson()
                .EnableHttpCompression();

            var client = new ElasticClient(settings);
            return client;
        }

        private class CodexConnectionSettings : ConnectionSettings
        {
            private Serializer SharedSerializer;
            public CodexConnectionSettings(Uri uri = null) : base(uri)
            {
            }

            protected override IElasticsearchSerializer DefaultSerializer(ConnectionSettings settings)
            {
                if (SharedSerializer == null)
                {
                    SharedSerializer = new Serializer(settings);
                }

                return SharedSerializer;
            }

            private class Serializer : JsonNetSerializer
            {

                public Serializer(IConnectionSettingsValues settings)
                    : base(settings, ModifyJsonSerializerSettings)
                {
                    
                }

                private static void ModifyJsonSerializerSettings(JsonSerializerSettings arg1, IConnectionSettingsValues arg2)
                {
                    arg1.ContractResolver = new CachingResolver(arg1.ContractResolver);
                }
            }

            private class CachingResolver : IContractResolver
            {
                private IContractResolver m_inner;
                private ConcurrentDictionary<Type, JsonContract> ContractsByType
                    = new ConcurrentDictionary<System.Type, JsonContract>();

                public CachingResolver(IContractResolver inner)
                {
                    m_inner = inner;
                }

                public JsonContract ResolveContract(Type objectType)
                {
                    JsonContract contract;
                    if (!ContractsByType.TryGetValue(objectType, out contract))
                    {
                        contract = ContractsByType.GetOrAdd(objectType, m_inner.ResolveContract(objectType));
                    }

                    return contract;
                }
            }
        }

        internal ElasticProvider(string endpoint, string coreIndexName, string projectTypeName, string sourcesTypeName)
            : this(ElasticProviderConfig.Create(endpoint).WithCoreIndexName(coreIndexName).WithProjectTypeName(projectTypeName).WithSourceTypeName(sourcesTypeName))
        { }

        public static ElasticProvider Create(string endpoint)
        {
            return ElasticProviderConfig.Create(endpoint).CreateProvider();
        }
        #endregion Constructors

        #region Repository-level operations
        public Task<ElasticResponse> AddRepositoryAsync(RepositoryModel repoModel, bool throwOnExists = false)
        {
            Contract.Requires(repoModel != null);
            Contract.Requires(repoModel.Name != null);

            return UseElasticClient(async client =>
            {
                await CreateCoreIndexIfNeeded(CoreIndexName);

                var repo = await GetRepositoryAsync(repoModel.SourcesIndexName);

                await CreateSourcesIndexIfNeeded(repoModel.SourcesIndexName);

                if (repo.Result != null)
                {
                    if (throwOnExists)
                    {
                        throw new RepositoryAlreadyExistsException(repoModel.Name);
                    }
                }

                return await client.IndexAsync(repoModel,
                    p => p.Index(CoreIndexName).Type(RepositoryTypeName).Id(repoModel.SourcesIndexName));
            });
        }

        public Task<ElasticResponse> FinalizeRepositoryAsync(string repositoryName, string targetIndexName)
        {
            Contract.Requires(repositoryName != null);
            Contract.Requires(targetIndexName != null);

            return UseElasticClient(async client =>
            {
                targetIndexName = targetIndexName.ToLowerInvariant();
                repositoryName = repositoryName.ToLowerInvariant();

                // Flush and refresh to ensure index is ready and searchable
                await client.FlushAsync(Indices(targetIndexName));
                await client.RefreshAsync(Indices(targetIndexName));

                // Point repository alias at new target index
                var indicesForAlias = await client.GetIndicesPointingToAliasAsync(repositoryName);

                return await client.AliasAsync(
                    ba => ba
                    .ForEach(indicesForAlias, (b, index) => b.Remove(ar => ar.Index(index).Alias(repositoryName)))
                    .ForEach(indicesForAlias, (b, index) => b.Remove(ar => ar.Index(index).Alias(CombinedSourcesIndexAlias)))
                    .Add(a => a.Index(targetIndexName).Alias(repositoryName))
                    .Add(a => a.Index(targetIndexName).Alias(CombinedSourcesIndexAlias))
                    );
            });
        }
        public async Task DeleteRepositoryAndSourceIndexAsync(string indexName)
        {
            Contract.Requires(!string.IsNullOrEmpty(indexName));

            await DeleteRepositoryAsync(indexName);

            // If index exists we need to delete both alias and index itself!

            await RemoveAliasForSourceIndexAsync(indexName);

            await DeleteIndexAsync(indexName);
        }

        public async Task<ElasticResponse<List<RepositoryModel>>> SearchRepositoriesAsync()
        {
            var sw = Stopwatch.StartNew();
            var client = CreateClient();

            var result = await client
                .SearchAsync<RepositoryModel>(sd =>
                    sd.Type(RepositoryTypeName)
                      .Index(CoreIndexName))
                .ThrowOnFailure();

            var entries = result.Hits.Select(x => x.Source).ToList();

            return ElasticResponse.Create(result, entries, (int)result.Total, (int)sw.ElapsedMilliseconds,
                result.Took);
        }

        public async Task<ElasticResponse<RepositoryModel>> GetRepositoryAsync(string repoName)
        {
            Contract.Requires(repoName != null);

            var sw = Stopwatch.StartNew();

            var client = CreateClient();

            var result = await client.GetAsync<RepositoryModel>(repoName, gd => gd.Index(CoreIndexName).Type(RepositoryTypeName)); //gd => gd CoreIndexName, RepositoryTypeName).ThrowOnFailure();

            return ElasticResponse.Create(result, result.Source, total: 1, operationDuration: (int)sw.ElapsedMilliseconds, backendDurationMilliseconds: null);
        }

        private Task<ElasticResponse> DeleteRepositoryAsync(string repositoryName)
        {
            Contract.Requires(repositoryName != null);

            return UseElasticClient(async client =>
            {
                var existsQuery = await client.IndexExistsAsync(CoreIndexName).ThrowOnFailure();
                if (existsQuery.Exists)
                {
                    return existsQuery;
                }

                return await client.DeleteAsync<RepositoryModel>(repositoryName, dd => dd.Index(CoreIndexName).Type(RepositoryTypeName));
            });
        }

        private Task<ElasticResponse> AddAliasForSourceIndexAsync(string indexName)
        {
            return UseElasticClient(async client =>
            {
                await client.AliasAsync(a => a
                    .Add(add => add
                        .Index(indexName)
                        .Alias(CombinedSourcesIndexAlias)));

                return await client.FlushAsync(AllIndices);
            });
        }

        private Task<ElasticResponse> RemoveAliasForSourceIndexAsync(string indexName)
        {
            return UseElasticClient(async client =>
            {
                var existsQuery = (await client.IndexExistsAsync(indexName)).ThrowOnFailure();
                if (!existsQuery.Exists)
                {
                    return existsQuery;
                }

                await client.AliasAsync(a => a
                    .Remove(remove => remove
                        .Index(indexName)
                        .Alias(CombinedSourcesIndexAlias)));

                return await client.FlushAsync(AllIndices);
            });
        }

        #endregion Repository-level operations

        #region Project-level operations
        public Task<ElasticResponse> AddProjectAsync(ProjectModel projectModel, string targetIndexName = null)
        {
            Contract.Requires(projectModel != null);

            return UseElasticClient(async client =>
            {
                targetIndexName = targetIndexName ?? await CreateSourcesIndexForRepoIfNeeded(projectModel.RepositoryName);

                return await client
                    .IndexAsync(projectModel, p => p.Index(targetIndexName).Type(ProjectTypeName)
                    .Id(projectModel.Id));
            });
        }

        public async Task<ElasticResponse<ProjectModel>> GetProjectAsync(string projectId)
        {
            Contract.Requires(projectId != null);

            var sw = Stopwatch.StartNew();

            var client = CreateClient();

            var result = await client
                .SearchAsync<ProjectModel>(sd =>
                    sd.Type(ProjectTypeName)
                    .Query(f =>
                        f.Bool(bq => bq.Filter(
                            f1 => f1.CaseTerm(project => project.Id, projectId))))
                      .Source(sf => sf.Excludes(fd => fd.Field(pm => pm.ReferencedProjects.First().Definitions)))
                      .Index(CombinedSourcesIndexAlias)
                      .Take(1))
                .ThrowOnFailure();

            var success = result.ApiCall.Success && result.Hits.Any();

            return ElasticResponse.Create(result, success ? result.Hits.First().Source : null, total: success ? 1 : 0, operationDuration: (int)sw.ElapsedMilliseconds, backendDurationMilliseconds: null);
        }

        public async Task<ElasticResponse<List<ProjectModel>>> GetProjectsAsync(string projectKind = null)
        {
            var sw = Stopwatch.StartNew();
            var client = CreateClient();

            var result = await client
                .SearchAsync<ProjectModel>(sd =>
                    sd.Type(ProjectTypeName)
                    .Query(f =>
                        f.Bool(bq => bq.Filter(
                            f1 => f1.CaseTerm(project => project.ProjectKind, projectKind))))
                      .Source(sf => sf.Excludes(fd => fd.Field(pm => pm.ReferencedProjects.First().Definitions)))
                      .Index(CombinedSourcesIndexAlias)
                      .Take(10000))
                .ThrowOnFailure();

            var entries = result.Hits.Select(x => x.Source).ToList();

            return ElasticResponse.Create(result, entries, (int)result.Total, (int)sw.ElapsedMilliseconds,
                result.Took);
        }

        public async Task<ElasticResponse> RemoveProjectAsync(string projectId, string targetIndex)
        {
            Contract.Requires(projectId != null);

            try
            {
                var result = await UseElasticClient(async client =>
                {
                    var existsResult = await client.DocumentExistsAsync<ProjectModel>(projectId);
                    if (!existsResult.Exists)
                    {
                        return existsResult;
                    }

                    return await client.DeleteAsync<ProjectModel>(projectId, gd => gd.Index(targetIndex).Type(ProjectTypeName));
                });

                return result;
            }
            catch
            {
                return null;
            }
        }

        #endregion Project-level operations

        #region Index operations

        public Task<ElasticResponse> DeleteCodexCoreIndexAsync()
        {
            return DeleteIndexAsync(CoreIndexName);
        }

        public async Task<ElasticResponse> DeleteSourceIndexAsync(string repoName)
        {
            var sourceIndexName = await GetSourcesIndexNameByRepositoryName(repoName);
            if (sourceIndexName == null)
            {
                throw new ElasticProviderException($"Failed to get index name for project '{repoName}'");
            }

            return await DeleteIndexAsync(sourceIndexName);
        }

        public Task<ElasticResponse> DeleteIndexAsync(string indexName)
        {
            return UseElasticClient(async client =>
            {
                var existsQuery = (await client.IndexExistsAsync(indexName)).ThrowOnFailure();
                if (!existsQuery.Exists)
                {
                    return existsQuery;
                }

                await client.DeleteIndexAsync(indexName, d => d).ThrowOnFailure();
                return await client.FlushAsync(AllIndices);
            });
        }

        public async Task CreateSourcesIndexIfNeeded(string indexName)
        {
            Contract.Requires(indexName != null);

            // Due to Elasticsearch policies, index name should be in lowercase!
            indexName = indexName.ToLowerInvariant();
            await _createdIndices.GetOrAdd(indexName, async n =>
            {
                if (await IndexExistsAsync(indexName))
                {
                    return;
                }

                string[] requestHolder = new string[1];

                var client = CreateClient();
                var response = await client
                    .CreateIndexAsync(indexName,
                        c => CustomAnalyzers.AddNGramAnalyzerFunc(c)
                            .Mappings(m => m.Map<SourceFileModel>(SourcesTypeName, tm => tm.AutoMapEx())
                                            .Map<PropertyModel>(SearchPropertiesTypeName, tm => tm.AutoMapEx())
                                            .Map<DefinitionSearchSpanModel>(SearchDefinitionTypeName, tm => tm.AutoMapEx())
                                            .Map<ReferenceSearchResultModel>(SearchReferenceTypeName, tm => tm.AutoMapEx())
                                            .Map<ProjectModel>(ProjectTypeName, tm => tm.AutoMapEx())
                                            .Map<RepositoryModel>(RepositoryTypeName, tm => tm.AutoMapEx())
                                            )
                            .CaptureRequest(client, requestHolder)
                            )
                    .ThrowOnFailure();

                await client.UpdateIndexSettingsAsync(indexName, up => up.IndexSettings(id =>
                    id.RefreshInterval(TimeSpan.FromSeconds(10))));

                await client.FlushAsync(AllIndices);
            });
        }

        public async Task<string> CreateSourcesIndexForRepoIfNeeded(string repoName)
        {
            var mainIndexTask = CreateCoreIndexIfNeeded(CoreIndexName);

            var sourceIndexName = await GetSourcesIndexNameByRepositoryName(repoName);

            if (sourceIndexName == null)
            {
                // Suppress unobserved exception
                mainIndexTask.ContinueWith(_ => { }).IgnoreAsync();

                throw new ElasticProviderException($"Failed to get index name for repository '{repoName}'");
            }

            var sourceIndexTask = CreateSourcesIndexIfNeeded(sourceIndexName);

            await Task.WhenAll(mainIndexTask, sourceIndexTask);

            return sourceIndexName;
        }

        public Task<ElasticResponse> CreateCoreIndexIfNeeded(bool force = false)
        {
            return CreateCoreIndexIfNeeded(CoreIndexName, force);
        }

        public async Task<ElasticResponse> CreateCoreIndexIfNeeded(string indexName, bool force = false)
        {
            // Using cached (potentially finished) task for creating main index.
            var task = Volatile.Read(ref _createMainIndexTask);

            if (task != null)
            {
                return await task;
            }

            TaskCompletionSource<ElasticResponse> taskCompletion = new TaskCompletionSource<ElasticResponse>();
            if (Interlocked.CompareExchange(ref _createMainIndexTask, taskCompletion.Task, null) != null)
            {
                return await Volatile.Read(ref _createMainIndexTask);
            }

            var result = await UseElasticClient(async client =>
            {
                var existsQuery = await client.IndexExistsAsync(indexName).ThrowOnFailure();
                if (existsQuery.Exists)
                {
                    if (force)
                    {
                        return await client.AliasAsync(ba => ba.Add(aa => aa.Alias(CombinedSourcesIndexAlias).Index(indexName)));
                    }
                    return existsQuery;
                }

                var response = await client
                    .CreateIndexAsync(indexName,
                        c => c.Mappings(m => m.Map<RepositoryModel>(RepositoryTypeName, tm => tm.AutoMapEx()))
                        .Aliases(ad => ad.Alias(CombinedSourcesIndexAlias)))
                    .ThrowOnFailure();

                return await client.FlushAsync(AllIndices);
            });

            taskCompletion.SetResult(result);

            return result;
        }

        public async Task<bool> IndexExistsAsync(string indexName)
        {
            var client = CreateClient();

            var existsQuery = await client.IndexExistsAsync(indexName).ThrowOnFailure();

            return existsQuery.Exists;
        }

        private Task<string> GetSourcesIndexNameByRepositoryName(string repoName)
        {
            Contract.Requires(!string.IsNullOrEmpty(repoName));
            Task<string> sourceIndexNameTask;

            if (!_repoSourceIndexNames.TryGetValue(repoName, out sourceIndexNameTask))
            {
                sourceIndexNameTask = _repoSourceIndexNames.GetOrAdd(repoName, async (name) =>
                {
                    var repo = await GetRepositoryAsync(repoName);

                    return repo.Result?.SourcesIndexName ?? repoName;
                });
            }

            return sourceIndexNameTask;
        }

        #endregion Index operations

        #region Source-level operations

        public async Task<ElasticResponse> AddSourcesAsync(string repositoryName, IEnumerable<SourceFileModel> sources, int batchSize = DefaultBatchSize)
        {
            Contract.Requires(sources != null);

            var sourcesIndexName = await CreateSourcesIndexForRepoIfNeeded(repositoryName);

            return await AddSourcesToIndexAsync(sourcesIndexName, sources, batchSize);
        }

        public async Task<ElasticResponse> AddSourcesToIndexAsync(string sourcesIndexName, IEnumerable<SourceFileModel> sources, int batchSize = DefaultBatchSize)
        {
            // This method does not fits into the UseClient pattern!
            var sw = Stopwatch.StartNew();
            Contract.Assert(sourcesIndexName != null);

            sources = SplitSources(sources);

            foreach (var chunk in CreateSourceBatches(sources, batchSize))
            {
                await DoAddSourcesAsync(sourcesIndexName, chunk);
            }

            return ElasticResponse.CreateFakeResponse((int)sw.ElapsedMilliseconds);
        }

        private IEnumerable<SourceFileModel> SplitSources(IEnumerable<SourceFileModel> sources)
        {
            foreach (var source in sources)
            {
                yield return source;
            }
        }

        private async Task<IndexName[]> GetSourceIndicesAsync(ICollection<string> repos)
        {
            if (repos == null || repos.Count == 0)
            {
                return new IndexName[] { CombinedSourcesIndexAlias };
            }

            var tasks = repos.Select(async x =>
            {
                return new { Repo = x, Index = await GetSourcesIndexNameByRepositoryName(x) };
            }).ToList();

            await Task.WhenAll(tasks);

            var unresolvedRepos = tasks.Select(x => x.Result).Where(x => string.IsNullOrEmpty(x.Index)).ToList();

            if (unresolvedRepos.Count != 0)
            {
                throw new CantResolveIndicesException(unresolvedRepos.Select(x => x.Repo).ToList());
            }

            return tasks.Select(x => x.Result).Select(x => (IndexName)x.Index).ToArray();
        }

        public async Task<ElasticResponse<List<DefinitionSearchSpanModel>>> GetSourcesForProjectAsync(
            ICollection<string> repos, string projectId)
        {
            Contract.Requires(projectId != null);

            var sw = Stopwatch.StartNew();

            var client = CreateClient();

            var indices = await GetSourceIndicesAsync(repos);

            var result = await client.SearchAsync<DefinitionSearchSpanModel>(
                s => s.Query(f =>
                f.Bool(bq => bq.Filter(
                        f1 => f1.CaseTerm(source => source.Span.Definition.ProjectId, projectId),
                        f1 => f1.CaseTerm(source => source.Span.Definition.Kind, nameof(SymbolKinds.File)))))
                    .Index(indices)
                    .Type(SearchDefinitionTypeName)
                    .Take(3000))
                .ThrowOnFailure();

            var entries = result.Hits.Select(x => x.Source).ToList();

            return ElasticResponse.FromSearchResult(result, entries, sw);
        }

        //public async Task<ElasticResponse<SourceFileModel>> GetSourceFileAsync(
        //    ICollection<string> repos,
        //    string projectId,
        //    string filePath,
        //    bool includeContent = true,
        //    bool includeClassifications = true,
        //    bool includeReferences = true,
        //    bool includeDefinitions = false)
        //{

        //}

        public async Task<ElasticResponse<SourceFileModel>> GetSourceFileAsync(
            ICollection<string> repos,
            string projectId,
            string filePath,
            bool includeContent = true,
            bool includeClassifications = true,
            bool includeReferences = true,
            bool includeDefinitions = false,
            Func<BoolQueryDescriptor<SourceFileModel>, BoolQueryDescriptor<SourceFileModel>> filter = null)
        {
            Contract.Requires(projectId != null);
            Contract.Requires(filePath != null);

            var sw = Stopwatch.StartNew();

            var client = CreateClient();

            if (filter == null)
            {
                filter = bf => bf.Filter(fc => fc.CaseTerm(source => source.ProjectId, projectId),
                                            fc => fc.Term(source => source.Path, filePath));
            }

            var indices = await GetSourceIndicesAsync(repos);

            var result = await client.MultiSearchAsync(ms => ms
                .Search<SourceFileModel>(SearchSourceTypeName,
                    s => s.Query(f => f.Bool(
                        bf => bf.Filter(fc => fc.CaseTerm(source => source.ProjectId, projectId),
                                        fc => fc.Term(source => source.Path, filePath))))
                        .Source(source => source
                            .Excludes(sd => sd
                                .ConfigureIf(!includeContent, sd1 => sd1.Field(sfm => sfm.Content))
                                .ConfigureIf(!includeClassifications, sd1 => sd1.Field(sfm => sfm.Classifications))
                                .ConfigureIf(!includeDefinitions, sd1 => sd1.Field(sfm => sfm.Definitions))
                                .ConfigureIf(!includeReferences, sd1 => sd1.Field(sfm => sfm.References))))
                        .Index(indices)
                        .Take(10)
                    )
                .ConfigureIf(includeDefinitions, ms1 => ms1
                    .Search<DefinitionSearchSpanModel>(SearchDefinitionTypeName,
                        s => s.Query(f => f.Bool(
                            bf => bf.Filter(fc => fc.Term(source => source.FilePath, filePath),
                                            fc => fc.CaseTerm(source => source.Span.Definition.ProjectId, projectId))))
                        .Index(indices)
                        .Take(1000)))
                )
                .ThrowOnFailure();

            var sourceFileResponse = result.GetResponse<SourceFileModel>(SearchSourceTypeName);
            var sourceFileResults = sourceFileResponse.Hits.Select(x => x.Source);

            SourceFileModel mergedSourceFileResult = null;
            foreach (var sourceFileResult in sourceFileResults)
            {
                if (mergedSourceFileResult == null)
                {
                    mergedSourceFileResult = sourceFileResult;
                }
                else if (mergedSourceFileResult.MergeId == sourceFileResult.MergeId)
                {
                    mergedSourceFileResult.Classifications = mergedSourceFileResult.Classifications ?? sourceFileResult.Classifications;
                    mergedSourceFileResult.References = mergedSourceFileResult.References ?? sourceFileResult.References;

                    if ((sourceFileResult.Definitions?.Count ?? 0) != 0)
                    {
                        mergedSourceFileResult.Definitions.AddRange(sourceFileResult.Definitions);
                    }
                }
                else
                {
                    break;
                }
            }

            if (includeDefinitions)
            {
                var definitionResults = result.GetResponse<DefinitionSearchSpanModel>(SearchDefinitionTypeName)
                    .Hits
                    .Select(x => x.Source);
                foreach (var definitionResult in definitionResults)
                {
                    if (definitionResult.FileMergeId == mergedSourceFileResult.MergeId)
                    {
                        mergedSourceFileResult.Definitions.Add(definitionResult.Span);
                    }
                }

                mergedSourceFileResult?.Definitions?.Sort((ds1, ds2) => ds1.Start.CompareTo(ds2.Start));
            }

            return ElasticResponse.FromSearchResult(sourceFileResponse, mergedSourceFileResult, sw);
        }

        public async Task<ElasticResponse<List<TextReferenceEntry>>> TextSearchAsync(
            ICollection<string> repos,
            string searchPhrase,
            int maxNumberOfItems)
        {
            Contract.Requires(searchPhrase != null);

            var sw = Stopwatch.StartNew();

            var client = CreateClient();

            var indices = await GetSourceIndicesAsync(repos);

            searchPhrase = searchPhrase.Trim();
            bool isPrefix = searchPhrase.EndsWith("*");
            searchPhrase = searchPhrase.TrimEnd('*');

            var result = await client.SearchAsync<SourceFileModel>(
                s => s
                    .Query(f => 
                        f.Bool(bq => 
                        bq.Filter(qcd => !qcd.Term(sf => sf.ExcludeFromSearch, true))
                          .Must(qcd => qcd.ConfigureIfElse(isPrefix,
                            f0 => f0.MatchPhrasePrefix(mpp => mpp.Field(sf => sf.Content).Query(searchPhrase).MaxExpansions(100)),
                            f0 => f0.MatchPhrase(mpp => mpp.Field(sf => sf.Content).Query(searchPhrase))))))
                    .Highlight(h => h.Fields(hf => hf.Field(sf => sf.Content).BoundaryCharacters("\n\r")))
                    .Source(source => source
                        .Excludes(sd => sd.Fields(
                            sf => sf.Content,
                            sf => sf.Classifications,
                            sf => sf.Definitions,
                            sf => sf.References)))
                    .Index(indices)
                    .Take(maxNumberOfItems))
                .ThrowOnFailure();

            var sourceFileResults =
                (from hit in result.Hits
                 from highlightHit in hit.Highlights.Values
                 from highlight in highlightHit.Highlights
                 from span in FullTextUtilities.ParseHighlightSpans(highlight)
                 select new TextReferenceEntry()
                 {
                     ReferringFilePath = hit.Source.Path,
                     ReferringProjectId = hit.Source.ProjectId,
                     ReferringSpan = span
                 }).ToList();

            return ElasticResponse.FromSearchResult(result, sourceFileResults, sw);
        }

        private async Task<ElasticResponse> DoAddSourcesAsync(
            string indexName, IEnumerable<SourceFileModel> sources)
        {
            Contract.Requires(!string.IsNullOrEmpty(indexName));

            // This method does not fits into UseClient pattern!
            var sw = Stopwatch.StartNew();

            var client = CreateClient();

            int itemsInBatch = 0;
            var bulkDescriptor = new BulkDescriptor();

            foreach (var source in sources)
            {
                itemsInBatch++;
                bulkDescriptor.Index<SourceFileModel>(
                        bd => bd.Index(indexName).Type(SourcesTypeName).Document(source));

                foreach (var searchProperty in source.GetSearchProperties())
                {
                    bulkDescriptor.Index<PropertyModel>(
                        bd => bd.Index(indexName).Type(SearchPropertiesTypeName).Document(searchProperty));
                }

                foreach (var searchReference in source.GetSearchReferences())
                {
                    bulkDescriptor.Index<ReferenceSearchResultModel>(
                        bd => bd.Index(indexName).Type(SearchReferenceTypeName).Document(searchReference));
                }

                foreach (var searchDefinition in source.GetSearchDefinitions())
                {
                    bulkDescriptor.Index<DefinitionSearchSpanModel>(
                        bd => bd.Index(indexName).Type(SearchDefinitionTypeName).Document(searchDefinition));
                }
            }

            // It is possible that we just skipped everything!
            if (itemsInBatch == 0)
                return ElasticResponse.CreateFakeResponse((int)sw.ElapsedMilliseconds);

            var result = await client.BulkAsync(bulkDescriptor).ThrowOnFailure();

            //var result = await client.FlushAsync(AllIndices).ThrowOnFailure();

            return ElasticResponse.CreateResponse(result, (int)sw.ElapsedMilliseconds);
        }

        #endregion Source-level operations

        #region Definitions & References

        public async Task<ElasticResponse<List<DefinitionSearchSpanModel>>> GetDefinitionsAsync(
            ICollection<string> repos, string projectId, string symbolId, int maxNumberOfItems)
        {
            Contract.Requires(projectId != null);
            Contract.Requires(symbolId != null);

            var sw = Stopwatch.StartNew();

            var client = CreateClient();

            var indices = await GetSourceIndicesAsync(repos);

            var spansName = nameof(DefinitionSearchSpanModel);

            var result = await client.MultiSearchAsync(ms => ms
                .Search<DefinitionSearchSpanModel>(spansName,
                    s => s.Query(qcd => qcd.Bool(bq =>
                                    bq.Filter(fd => fd.CaseTerm(searchSpan => searchSpan.Span.Definition.ProjectId, projectId),
                                              fd => fd.Term(searchSpan => searchSpan.Span.Definition.Id, symbolId))))
                         .Type(SearchDefinitionTypeName)
                         // TODO: Need to sort here and above
                         .Index(indices)
                         .Take(maxNumberOfItems))
                ).ThrowOnFailure();

            var spanResult = result.GetResponse<DefinitionSearchSpanModel>(spansName);

            var entries = spanResult.Hits.Select(r =>
            {
                var searchResult = r.Source;
                return searchResult;
            }).Where(s => s != null).ToList();

            return ElasticResponse.FromSearchResult(spanResult, entries, sw);
        }

        public Task<ElasticResponse<List<SymbolReferenceModel>>> GetReferencesToSymbolAsync(
            ICollection<string> repos, Symbol symbol, int maxNumberOfItems)
        {
            Contract.Requires(symbol != null);

            return GetReferencesToSymbolByIdAsync(
                repos,
                symbol.ProjectId,
                symbol.Id.Value,
                symbol.Kind,
                maxNumberOfItems,
                relatedDefinitionProjectId: symbol.GetReferenceSearchExtensionData()?.ProjectScope);
        }

        public static readonly IEqualityComparer<SymbolModel> SymbolComparer = new EqualityComparerBuilder<SymbolModel>()
            .CompareByAfter(s => s.ProjectId)
            .CompareByAfter(s => s.Id);

        public async Task<ElasticResponse<List<DefinitionSearchSpanModel>>> GetRelatedDefinitions(
            ICollection<string> repos,
            string definitionId,
            string projectId)
        {
            var sw = Stopwatch.StartNew();

            var result = await GetReferencesToSymbolByIdAsync(repos,
                projectId: null,
                symbolId: null,
                referenceKind: null,
                relatedDefinitionId: definitionId,
                relatedDefinitionProjectId: projectId);

            Dictionary<SymbolModel, string> refKindBySymbol = new Dictionary<SymbolModel, string>(SymbolComparer);
            var terms = result.Result.Select(s => s.ReferringSpan.Reference).Distinct().ToArray();

            foreach (var reference in terms)
            {
                refKindBySymbol[reference] = reference.ReferenceKind;
            }

            var client = CreateClient();
            var indices = await GetSourceIndicesAsync(repos);

            var definitionsResult = await client.SearchAsync<DefinitionSearchSpanModel>(s =>
                        s.Query(GetIdFilter(terms))
                         .Type(SearchDefinitionTypeName)
                         .Index(indices)
                         .Take(DefaultMaxNumberOfItems)
                ).ThrowOnFailure();

            var entries = definitionsResult.Hits.Select(s => s.Source).Where(s => s != null).ToList();

            foreach (var entry in entries)
            {
                entry.ReferenceKind = refKindBySymbol.GetOrDefault(entry.Span.Definition, nameof(ReferenceKind.Definition));
            }

            return ElasticResponse.FromSearchResult(definitionsResult, entries, sw);
        }

        private static Func<QueryContainerDescriptor<DefinitionSearchSpanModel>, QueryContainer> GetIdFilter(SymbolModel[] terms)
        {
            return qcd => qcd.Bool(bq => bq.Filter(fq => GetIdFilters(fq, terms)));
        }

        private static QueryContainer GetIdFilters(
            QueryContainerDescriptor<DefinitionSearchSpanModel> fq,
            SymbolModel[] terms)
        {
            QueryContainer qc = fq;
            foreach (var term in terms)
            {
                qc |= (fq.Term(dss => dss.Span.Definition.Id, term.Id) && fq.CaseTerm(dss => dss.Span.Definition.ProjectId, term.ProjectId));
            }

            return qc;
        }

        public async Task<ElasticResponse<List<SymbolReferenceModel>>> GetReferencesToSymbolByIdAsync(
            ICollection<string> repos,
            string projectId,
            string symbolId,
            string referenceKind,
            int? maxNumberOfItems = null,
            string relatedDefinitionId = null,
            string relatedDefinitionProjectId = null)
        {
            var sw = Stopwatch.StartNew();

            var client = CreateClient();

            maxNumberOfItems = maxNumberOfItems ?? DefaultMaxNumberOfItems;

            var indices = await GetSourceIndicesAsync(repos);

            var spansName = nameof(ReferenceSearchResultModel);

            var result = await client.MultiSearchAsync(ms => ms
                .Search<ReferenceSearchResultModel>(spansName,
                    s => s.Query(qcd => qcd.Bool(bq =>
                            bq.Filter(
                                fq => fq.Term(searchSpan => searchSpan.ProjectId, relatedDefinitionProjectId),
                                fq => fq.Term(searchSpan => searchSpan.RelatedDefinitions.First(), relatedDefinitionId),
                                fq => fq.CaseTerm(searchSpan => searchSpan.Reference.ProjectId, projectId),
                                fq => fq.Term(searchSpan => searchSpan.Reference.Id, symbolId),
                                fq => fq.CaseTerm(searchSpan => searchSpan.Reference.ReferenceKind, referenceKind),
                                fq => !fq.Term(searchSpan => searchSpan.Reference.ExcludeFromDefaultSearch, true)
                            )))
                         .Type(SearchReferenceTypeName)
                         // TODO: Need to sort here and above
                         .Sort(sfd => sfd.Field(sf => sf
                            .Field(sr => sr.Reference.ReferenceKind)
                            .Order(SortOrder.Ascending)))
                         .Index(indices)
                         .Take(maxNumberOfItems.Value))
                ).ThrowOnFailure();

            var spanResult = result.GetResponse<ReferenceSearchResultModel>(spansName);

            var entries = spanResult.Hits.SelectMany(r =>
            {
                var searchResult = r.Source;

                return ModelConverter.ToSymbolReferences(searchResult);
            }).Where(s => s != null).ToList();

            return ElasticResponse.FromSearchResult(spanResult, entries, sw);
        }

        public async Task<ElasticResponse<List<DefinitionSearchSpanModel>>> SearchByTermInDefinition(
            ICollection<string> repos,
            string searchTerm,
            Classification? classification,
            int maxNumberOfItems = DefaultMaxNumberOfItems)
        {
            Contract.Requires(!string.IsNullOrEmpty(searchTerm));
            var sw = Stopwatch.StartNew();

            var terms = searchTerm.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

            var client = CreateClient();

            var indices = await GetSourceIndicesAsync(repos);

            var spansName = nameof(DefinitionSearchSpanModel);

            var result = await client.MultiSearchAsync(ms => ms
                .Search<DefinitionSearchSpanModel>(spansName,
                    s => s.Query(qcd => qcd
                        .FunctionScore(fsqd => fsqd
                            .BoostMode(FunctionBoostMode.Max)
                            .Query(GetTermsFilter(terms))
                            .Functions(sfd => sfd.Weight(wfd => wfd.Weight(2).Filter(GetTermsFilter(terms, boostOnly: true)))

                            )))
                         .Type(SearchDefinitionTypeName)
                         //.Filter(fd => fd.)
                         //.Sort(sfd => sfd.Field(sf => sf.Span.Definition.ProjectId, SortOrder.Ascending))
                         .Index(indices)
                         .Take(maxNumberOfItems))
                ).ThrowOnFailure();

            var classificationKind = classification?.Kind();

            var spanResult = result.GetResponse<DefinitionSearchSpanModel>(spansName);

            var entries = spanResult.Hits.Select(s => s.Source).Where(s => s != null).ToList();

            return ElasticResponse.FromSearchResult(spanResult, entries, sw);
        }

        private static Func<QueryContainerDescriptor<DefinitionSearchSpanModel>, QueryContainer> GetTermsFilter(string[] terms, bool boostOnly = false)
        {
            return qcd => qcd.Bool(bq => bq.Filter(GetTermsFilters(terms, boostOnly)));
        }

        private static IEnumerable<Func<QueryContainerDescriptor<DefinitionSearchSpanModel>, QueryContainer>>
            GetTermsFilters(string[] terms, bool boostOnly = false)
        {
            foreach (var term in terms)
            {
                yield return fq => ApplyTermFilter(term, fq, boostOnly);
            }

            if (!boostOnly)
            {
                yield return fq => fq.Bool(bqd => bqd.MustNot(fq1 => fq1.Term(dss => dss.Span.Definition.ExcludeFromDefaultSearch, true)));
            }
        }

        private static QueryContainer FileFilter(string term, QueryContainerDescriptor<DefinitionSearchSpanModel> fq)
        {
            if (term.Contains('.'))
            {
                return fq.Term(dss => dss.Tags, term.ToLowerInvariant());
            }

            return fq;
        }

        private static QueryContainer NameFilter(string term, QueryContainerDescriptor<DefinitionSearchSpanModel> fq, bool boostOnly)
        {
            var terms = term.CreateNameTerm();

            if (boostOnly)
            {
                return fq.Term(dss => dss.Span.Definition.ShortName, terms.ExactNameTerm.ToLowerInvariant());
            }
            else
            {
                return fq.Term(dss => dss.Span.Definition.ShortName, terms.NameTerm.ToLowerInvariant())
                        || fq.Term(dss => dss.Span.Definition.ShortName, terms.SecondaryNameTerm.ToLowerInvariant());
            }
        }

        private static QueryContainer QualifiedNameTermFilters(string term, QueryContainerDescriptor<DefinitionSearchSpanModel> fq)
        {
            var terms = ParseContainerAndName(term);

            // TEMPORARY HACK: This is needed due to the max length placed on container terms
            // The analyzer should be changed to use path_hierarchy with reverse option
            if ((terms.ContainerTerm.Length > (CustomAnalyzers.MaxGram - 2)) && terms.ContainerTerm.Contains("."))
            {
                terms.ContainerTerm = terms.ContainerTerm.SubstringAfterFirstOccurrence('.');
            }

            return fq.Bool(bq => bq.Filter(
                fq1 => fq1.Term(dss => dss.Span.Definition.ShortName, terms.NameTerm.ToLowerInvariant())
                    || fq1.Term(dss => dss.Span.Definition.ShortName, terms.SecondaryNameTerm.ToLowerInvariant()),
                fq1 => fq1.Term(dss => dss.Span.Definition.ContainerQualifiedName, terms.ContainerTerm.ToLowerInvariant())));
        }

        private static QueryContainer ProjectTermFilters(string term, QueryContainerDescriptor<DefinitionSearchSpanModel> fq)
        {
            var result = fq.Term(dss => dss.Span.Definition.ProjectId, term)
                || fq.Term(dss => dss.Span.Definition.ProjectId, term.Capitalize());
            if (term != term.ToLowerInvariant())
            {
                result |= fq.Term(dss => dss.Span.Definition.ProjectId, term.ToLowerInvariant());
            }

            return result;
        }

        private static QueryContainer KindTermFilters(string term, QueryContainerDescriptor<DefinitionSearchSpanModel> fq)
        {
            return fq.Term(dss => dss.Span.Definition.Kind, term.ToLowerInvariant())
                || fq.Term(dss => dss.Span.Definition.Kind, term.Capitalize());
        }

        private static QueryContainer IndexTermFilters(string term, QueryContainerDescriptor<DefinitionSearchSpanModel> fq)
        {
            return fq.Term("_index", term.ToLowerInvariant());
        }

        private static QueryContainer ApplyTermFilter(string term, QueryContainerDescriptor<DefinitionSearchSpanModel> fq, bool boostOnly)
        {
            var d = NameFilter(term, fq, boostOnly);

            if (!boostOnly)
            {
                d |= FileFilter(term, fq);
                d |= QualifiedNameTermFilters(term, fq);
                d |= ProjectTermFilters(term, fq);
                d |= IndexTermFilters(term, fq);
                d |= KindTermFilters(term, fq);
            }

            return d;
        }

        #endregion Definitions & References

        /// <summary>
        /// Splits incoming sequence by <paramref name="windowSize"/> or by <paramref name="maxBatchSize"/> whatever meats first.
        /// </summary>
        private IEnumerable<List<SourceFileModel>> CreateSourceBatches(IEnumerable<SourceFileModel> sources, int windowSize, int maxBatchSize = 500 * 1000)
        {
            int size = 0;
            var result = new List<SourceFileModel>(windowSize);

            foreach (var file in sources)
            {
                if (result.Count == windowSize || (size + file.GetEstimatedSize() >= maxBatchSize && result.Count > 0))
                {
                    yield return result;
                    result = new List<SourceFileModel>(windowSize);
                    size = 0;
                }

                result.Add(file);
                size += file.GetEstimatedSize();
            }

            if (result.Count != 0)
            {
                yield return result;
            }
        }

        private async Task<ElasticResponse> UseElasticClient(Func<ElasticClient, Task<IResponse>> selector)
        {
            var sw = Stopwatch.StartNew();

            var client = CreateClient();

            var result = (await selector(client)).ThrowOnFailure();

            return ElasticResponse.CreateResponse(result, (int)sw.ElapsedMilliseconds);
        }
    }
}