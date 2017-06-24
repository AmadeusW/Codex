using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Linq;
using System.Threading.Tasks;

namespace Codex.ObjectModel
{
    public class RepositoryAlreadyExistsException : Exception
    {
        public RepositoryAlreadyExistsException(string repositoryName)
            : base($"Repository '{repositoryName}' already exists.")
        {
        }
    }

    public class StorageScope
    {
        public ICollection<string> Repositories { get; set; }

        public string ProjectId { get; set; }
    }

    [ContractClass(typeof(IStorageContract))]
    public interface IStorage
    {
        Task<bool> AddRepositoryAsync(string repositoryName, string sourceIndexName = null, bool throwOnExists = false);
        Task RemoveRepository(string repositoryName);

        Task AddProjectsAsync(IEnumerable<AnalyzedProject> projects);

        //  api/search/{searchTerm}
        Task<SymbolSearchResult> SearchAsync(ICollection<string> repositories, string searchTerm, string classification, int maxNumberOfItems = 100);

        //  api/definitions/{projectId}/{symbolName}
        // TODO: wrap the result into smart object as well
        Task<IList<GetDefinitionResult>> GetDefinitionsAsync(ICollection<string> repositories, string projectId, string symbolId);

        //  Source text: api/sourceText/{projectId}/{filePath}
        //  Bound spans: api/semanticInfo/{projectId}/{filePath}
        Task<BoundSourceFile> GetBoundSourceFileAsync(ICollection<string> repositories, string projectId, string filePath, bool includeDefinitions = false);

        //Task<SymbolReferenceResult> GetReferencesToSymbolAsync(Symbol symbol, int maxNumberOfItems = 100);
        Task<SymbolReferenceResult> GetReferencesToSymbolAsync(ICollection<string> repositories, Symbol symbol, int maxNumberOfItems = 100);

        //  api/project/{projectId}
        /// <summary>
        /// Task.Result will be null if project is not found.
        /// </summary>
        Task<ProjectContents> GetProjectContentsAsync(ICollection<string> repositories, string projectId);

        Task<IEnumerable<string>> GetReferencingProjects(string projectId);
    }

    public static class StorageExtensions
    {
        private static readonly string[] _emptyArray = { };

        public static async Task<bool> TryAddRepository(
            this IStorage storage, string repositoryName, string sourcesIndexName = null)
        {
            Contract.Requires(storage != null);
            Contract.Requires(repositoryName != null);

            try
            {
                await storage.AddRepositoryAsync(repositoryName, sourcesIndexName, throwOnExists: false);
                return true;
            }
            catch (RepositoryAlreadyExistsException)
            {
                return false;
            }
        }

        public static Task<SymbolSearchResult> SearchAsync(this IStorage storage, string searchTerm, string classificationFilter = null,
            int maxNumberOfItems = 100)
        {
            Contract.Requires(storage != null);
            return storage.SearchAsync(_emptyArray, searchTerm, classification: classificationFilter, maxNumberOfItems: maxNumberOfItems);
        }

        public static Task<SymbolSearchResult> SearchAsync(this IStorage storage, string[] repos, string searchTerm, string classificationFilter = null,
            int maxNumberOfItems = 100)
        {
            Contract.Requires(storage != null);
            return storage.SearchAsync(repos, searchTerm, classification: classificationFilter, maxNumberOfItems: maxNumberOfItems);
        }

        public static Task<BoundSourceFile> GetBoundSourceFileAsync(this IStorage storage, string projectId, string filePath)
        {
            Contract.Requires(storage != null);
            return storage.GetBoundSourceFileAsync(_emptyArray, projectId, filePath);
        }

        public static Task<SymbolReferenceResult> GetReferencesToSymbolAsync(this IStorage storage, Symbol symbol,
            int maxNumberOfItems = 100)
        {
            Contract.Requires(storage != null);
            return storage.GetReferencesToSymbolAsync(_emptyArray, symbol, maxNumberOfItems);
        }

        public static Task<ProjectContents> GetProjectContentsAsync(this IStorage storage, string projectId)
        {
            Contract.Requires(storage != null);
            return storage.GetProjectContentsAsync(_emptyArray, projectId);
        }

        public static Task<IList<GetDefinitionResult>> GetDefinitionsAsync(this IStorage storage, string projectId, string symbolId)
        {
            Contract.Requires(storage != null);

            return storage.GetDefinitionsAsync(_emptyArray, projectId, symbolId);
        }

        public static async Task<string> GetFirstDefinitionFilePath(this IStorage storage, string projectId, string symbolId)
        {
            var definitions = await storage.GetDefinitionsAsync(projectId, symbolId);
            var definition = definitions.FirstOrDefault();
            return definition?.File?.Path;
        }
    }

    [ContractClassFor(typeof(IStorage))]
    abstract class IStorageContract : IStorage
    {
        Task<bool> IStorage.AddRepositoryAsync(string repositoryName, string sourceIndexName, bool throwOnExists)
        {
            Contract.Requires(repositoryName != null);
            throw new NotImplementedException();
        }

        Task IStorage.RemoveRepository(string repositoryName)
        {
            Contract.Requires(repositoryName != null);
            throw new NotImplementedException();
        }

        Task IStorage.AddProjectsAsync(IEnumerable<AnalyzedProject> projects)
        {
            Contract.Requires(projects != null);
            throw new NotImplementedException();
        }

        Task<SymbolSearchResult> IStorage.SearchAsync(ICollection<string> repositories, string searchTerm, string classification, int maxNumberOfItems)
        {
            Contract.Requires(searchTerm != null);
            Contract.Requires(repositories != null);
            throw new NotImplementedException();
        }

        public Task<IList<GetDefinitionResult>> GetDefinitionsAsync(ICollection<string> repositories, string projectId, string symbolId)
        {
            Contract.Requires(repositories != null);
            Contract.Requires(projectId != null);
            Contract.Requires(symbolId != null);
            throw new NotImplementedException();
        }

        public Task<BoundSourceFile> GetBoundSourceFileAsync(ICollection<string> repositories, string projectId, string filePath, bool includeDefinitions = false)
        {
            Contract.Requires(repositories != null);
            Contract.Requires(projectId != null);
            Contract.Requires(filePath != null);
            throw new NotImplementedException();
        }

        public Task<SymbolReferenceResult> GetReferencesToSymbolAsync(Symbol symbol, int maxNumberOfItems = 100)
        {
            Contract.Requires(symbol != null);
            throw new NotImplementedException();
        }

        Task<SymbolReferenceResult> IStorage.GetReferencesToSymbolAsync(ICollection<string> repositories, Symbol symbol, int maxNumberOfItems)
        {
            Contract.Requires(symbol != null);
            Contract.Requires(repositories != null);
            throw new NotImplementedException();
        }

        public Task<ProjectContents> GetProjectContentsAsync(ICollection<string> repositories, string projectId)
        {
            Contract.Requires(repositories != null);
            Contract.Requires(projectId != null);
            throw new NotImplementedException();
        }

        public Task<IEnumerable<string>> GetReferencingProjects(string projectId)
        {
            Contract.Requires(projectId != null);
            throw new NotImplementedException();
        }
    }
}