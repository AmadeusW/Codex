using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Codex.Analysis;
using Codex.ObjectModel;
using Codex.Storage.DataModel;
using Codex.Storage.ElasticProviders;
using Codex.Utilities;
using Classification = Codex.Storage.DataModel.Classification;

namespace Codex.Storage
{
    public class ElasticsearchStorage : IStorage, IAnalysisTarget
    {
        private const int DefaultNumberOfItems = 1000;

        private ElasticProvider m_provider;
        public ElasticProvider Provider
        {
            get
            {
                UpdateProjects();
                return m_provider;
            }
        }

        public ElasticsearchStorage(string endpoint, bool requiresProjectGraph = false)
        {
            Contract.Requires(!string.IsNullOrEmpty(endpoint));
            m_provider = ElasticProvider.Create(endpoint);
            m_requiresProjectGraph = requiresProjectGraph;
            UpdateProjects();
        }

        public Dictionary<string, ProjectModel> Projects = new Dictionary<string, ProjectModel>();
        public MultiDictionary<string, string> ReferencingProjects = new MultiDictionary<string, string>(StringComparer.OrdinalIgnoreCase, StringComparer.OrdinalIgnoreCase);
        public DateTime LastUpdateTime;
        public Exception Exception;
        private int m_updating = 0;

        public Dictionary<string, int> ProjectRanks = new Dictionary<string, int>();
        private object syncLock = new object();
        private readonly bool m_requiresProjectGraph;

        public async Task UpdateProjectsAsync(bool force = false)
        {
            if (!m_requiresProjectGraph && !force)
            {
                return;
            }

            var elapsed = DateTime.UtcNow - LastUpdateTime;
            if (elapsed.TotalMinutes > 5 || force)
            {
                try
                {
                    if (Interlocked.CompareExchange(ref m_updating, 1, 0) == 0)
                    {
                        var projectsResult = await Provider.GetProjectsAsync();
                        var projects = projectsResult.Result;
                        if (projects == null)
                        {
                            return;
                        }

                        lock (syncLock)
                        {
                            Projects = projects.GroupBy(pm => pm.Id ?? string.Empty, StringComparer.OrdinalIgnoreCase)
                                .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

                            var referencingProjects = new MultiDictionary<string, string>();
                            foreach (var project in Projects)
                            {
                                foreach (var reference in project.Value.ReferencedProjects.Select(r => r.ProjectId))
                                {
                                    referencingProjects.Add(reference, project.Key);
                                }
                            }

                            ReferencingProjects = referencingProjects;

                            ProjectRanks = projects
                                .SelectMany(pm => pm.ReferencedProjects.Select(rp => rp.ProjectId))
                                .GroupBy(s => s, StringComparer.OrdinalIgnoreCase)
                                .ToDictionary(g => g.Key, g => g.Count(), StringComparer.OrdinalIgnoreCase);
                            LastUpdateTime = DateTime.UtcNow;
                        }
                    }
                }
                catch (Exception ex)
                {
                    Exception = ex;
                }
                finally
                {
                    Interlocked.CompareExchange(ref m_updating, 0, 1);
                }
            }
        }

        private void UpdateProjects()
        {
            UpdateProjectsAsync().IgnoreAsync();
        }

        async Task<bool> IStorage.AddRepositoryAsync(string repositoryName, string sourceIndexName, bool throwOnExists)
        {
            sourceIndexName = sourceIndexName ?? (repositoryName.ToLowerInvariant());
            await Provider.AddRepositoryAsync(new RepositoryModel(repositoryName, sourceIndexName), throwOnExists);
            return true;
        }

        Task IStorage.RemoveRepository(string repositoryName)
        {
            return Provider.DeleteRepositoryAndSourceIndexAsync(repositoryName);
        }

        async Task IStorage.AddProjectsAsync(IEnumerable<AnalyzedProject> projects)
        {
            foreach (var project in projects)
            {
                await AddProjectAsync(project, project.RepositoryName.ToLowerInvariant());

                var modelSources = ModelConverter.FromObjectModel(project.AdditionalSourceFiles);
                await Provider.AddSourcesAsync(project.RepositoryName, modelSources);
            }
        }

        public async Task<List<SymbolSearchResultEntry>> GetRelatedDefinitions(
            ICollection<string> repos,
            string definitionId,
            string projectId)
        {
            ElasticResponse<List<DefinitionSearchSpanModel>> results;
            results = await Provider.GetRelatedDefinitions(repos, definitionId, projectId);
            List<SymbolSearchResultEntry> convertedEntries = ModelConverter.ToSymbolSearchResults(results.Result);

            return convertedEntries;
        }

        public async Task<SymbolSearchResult> SearchAsync(ICollection<string> repositories, string searchTerm, string classification, int maxNumberOfItems)
        {
            Classification? parsedClassification = ClassificationEx.TryParse(classification);

            ElasticResponse<List<DefinitionSearchSpanModel>> results;
            results = await Provider.SearchByTermInDefinition(repositories, searchTerm, parsedClassification, maxNumberOfItems);

            //if (searchTerm.Contains("."))
            //{
            //}
            //else
            //{
            //    results = await _provider.SearchByShortNameAsync(repositories, searchTerm, parsedClassification, maxNumberOfItems);
            //}

            List<SymbolSearchResultEntry> convertedEntries = ModelConverter.ToSymbolSearchResults(results.Result);

            var projectRanks = ProjectRanks;
            convertedEntries.ForEach(symbol => symbol.Rank = projectRanks.GetOrDefault(symbol.Symbol.ProjectId));

            SearchResultSorter.Sort(convertedEntries, searchTerm);

            return new SymbolSearchResult
            {
                Entries = convertedEntries,
                Total = results.Total,
                QueryText = searchTerm
            };
        }

        async Task<IList<GetDefinitionResult>> IStorage.GetDefinitionsAsync(ICollection<string> repositories, string projectId, string symbolId)
        {
            var result = await Provider.GetDefinitionsAsync(repositories, projectId, symbolId, DefaultNumberOfItems);

            return ModelConverter.ToDefinitionResult(result.Result);
        }

        public async Task<List<TextReferenceEntry>> TextSearchAsync(
            ICollection<string> repos,
            string searchPhrase,
            int maxNumberOfItems = DefaultNumberOfItems)
        {
            var results = await Provider.TextSearchAsync(repos, searchPhrase, maxNumberOfItems);
            if (results == null)
            {
                return null;
            }

            return results.Result;
        }

        public async Task<BoundSourceFile> GetBoundSourceFileAsync(ICollection<string> repositories, string projectId, string filePath, bool includeDefinitions = false)
        {
            var sourceFile = await Provider.GetSourceFileAsync(repositories, projectId, filePath, includeDefinitions: includeDefinitions);

            if (sourceFile?.Result == null)
            {
                return null;
            }

            return ModelConverter.ToBoundSourceFile(sourceFile.Result);
        }

        public async Task<SymbolReferenceResult> GetReferencesToSymbolAsync(ICollection<string> repositories, Symbol symbol, int maxNumberOfItems = 100)
        {
            var symbols = await Provider.GetReferencesToSymbolAsync(repositories, symbol, maxNumberOfItems);
            List<SymbolReferenceEntry> entries = ModelConverter.ToSymbolReferences(symbols.Result);
            return new SymbolReferenceResult
            {
                Entries = entries,
                Total = symbols.Total,
                ProjectId = symbol.ProjectId,
                SymbolId = symbol.Id.Value,
                SymbolName = symbol.ToString()
            };
        }

        async Task<ProjectContents> IStorage.GetProjectContentsAsync(ICollection<string> repositories, string projectId)
        {
            var project = await Provider.GetProjectAsync(projectId);

            if (project == null)
            {
                return null;
            }

            var sources = await Provider.GetSourcesForProjectAsync(repositories, projectId);
            return ModelConverter.ToProjectContents(project?.Result ?? new ProjectModel(projectId, "Unknown Repository"), sources.Result);
        }

        public async Task UploadAsync(IRepoFile repoFile, BoundSourceFile boundSourceFile)
        {
            await Provider.AddSourcesToIndexAsync(repoFile.Repo.TargetIndex, new[] { ModelConverter.FromObjectModel(boundSourceFile) });
        }

        public Task<IEnumerable<string>> GetReferencingProjects(string projectId)
        {
            return Task.FromResult<IEnumerable<string>>(ReferencingProjects.GetOrDefault(projectId, new HashSet<string>()).ToArray());
        }

        public async Task AddProjectAsync(IRepoProject repoProject, AnalyzedProject analyzedProject)
        {
            string targetIndex = repoProject.Repo.TargetIndex;
            await AddProjectAsync(analyzedProject, targetIndex);
        }

        private async Task AddProjectAsync(AnalyzedProject analyzedProject, string targetIndex)
        {
            Console.WriteLine("Updating project {0}", analyzedProject.Id);
            await Provider.RemoveProjectAsync(analyzedProject.Id, targetIndex);

            var modelProject = ModelConverter.FromObjectModel(analyzedProject);
            modelProject.DateUploaded = DateTime.UtcNow;
            await Provider.AddProjectAsync(modelProject, targetIndexName: targetIndex);
        }

        public async Task AddRepositiory(IRepo repo)
        {
            Console.WriteLine($"Add repository {repo.RepositoryName} to index {repo.TargetIndex}");
            await Provider.AddRepositoryAsync(new RepositoryModel(repo.RepositoryName, repo.TargetIndex)
            {
                DateUploaded = DateTime.UtcNow,
            }, throwOnExists: false);
        }

        public async Task FinalizeRepository(IRepo repo)
        {
            Console.WriteLine($"Finalizing repository {repo.RepositoryName} to index {repo.TargetIndex}");
            await Provider.FinalizeRepositoryAsync(repo.RepositoryName, repo.TargetIndex);
        }
    }
}