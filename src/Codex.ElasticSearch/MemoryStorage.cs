using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Codex.ObjectModel;
using Codex.Utilities;

namespace Codex.Storage
{
    public class MemoryStorage : IStorage
    {
        private readonly SemaphoreSlim m_lock = TaskUtilities.CreateMutex();
        private readonly List<SymbolSearchResultEntry> m_symbolsByShortName = new List<SymbolSearchResultEntry>();

        public async Task AddProjectsAsync(IEnumerable<AnalyzedProject> projects)
        {
            using (await m_lock.AcquireAsync())
            {
                foreach (var project in projects)
                {
                    foreach (var file in project.AdditionalSourceFiles)
                    {
                        foreach (var span in file.Definitions)
                        {
                            if (span.Definition != null)
                            {
                                if (!string.IsNullOrEmpty(span.Definition.ShortName))
                                {
                                    m_symbolsByShortName.Add(new SymbolSearchResultEntry
                                    {
                                        File = file.SourceFile.Info.Path,
                                        Span = span,
                                    });
                                }
                            }
                        }
                    }
                }

                m_symbolsByShortName.Sort(SymbolShortNameComparer.Instance);
            }
        }

        async Task<SymbolSearchResult> IStorage.SearchAsync(ICollection<string> repositories, string searchTerm, string classification, int maxNumberOfItems)
        {
            var result = await SearchAsync(searchTerm);

            return new SymbolSearchResult()
            {
                Entries = result,
                Total = result.Count,
                QueryText = searchTerm
            };
        }

        public async Task<List<SymbolSearchResultEntry>> SearchAsync(string searchTerm)
        {
            List<SymbolSearchResultEntry> results = new List<SymbolSearchResultEntry>();
            if (!string.IsNullOrEmpty(searchTerm))
            {
                using (await m_lock.AcquireAsync())
                {
                    var range = RangeHelper.GetRange(m_symbolsByShortName,
                        searchTerm,
                        CompareTermLower,
                        CompareTermUpper);
                    results.AddRange(range);
                }
            }

            return results;
        }

        private int CompareTermUpper(string searchTerm, SymbolSearchResultEntry symbolResultEntry)
        {
            var comparisonResult = StringComparer.OrdinalIgnoreCase.Compare(searchTerm, symbolResultEntry.Symbol.ShortName);
            if (comparisonResult < 0)
            {
                if (symbolResultEntry.Symbol.ShortName.StartsWith(searchTerm, StringComparison.OrdinalIgnoreCase))
                {
                    return 0;
                }
            }

            return comparisonResult;
        }

        private int CompareTermLower(string searchTerm, SymbolSearchResultEntry symbolResultEntry)
        {
            var comparisonResult = StringComparer.OrdinalIgnoreCase.Compare(searchTerm, symbolResultEntry.Symbol.ShortName);
            if (comparisonResult < 0)
            {
                if (symbolResultEntry.Symbol.ShortName.StartsWith(searchTerm))
                {
                    return 0;
                }
            }

            return comparisonResult;
        }

        public Task<SymbolReferenceResult> GetReferencesToSymbolAsync(Symbol symbol, int maxNumberOfItems = 100)
        {
            throw new NotImplementedException();
        }

        Task<bool> IStorage.AddRepositoryAsync(string repositoryName, string indexName, bool throwOnExists)
        {
            return Task.FromResult(true);
        }

        Task IStorage.RemoveRepository(string repositoryName)
        {
            return Task.Delay(1);
        }

        Task IStorage.AddProjectsAsync(IEnumerable<AnalyzedProject> projects)
        {
            throw new NotImplementedException();
        }

        Task<IList<GetDefinitionResult>> IStorage.GetDefinitionsAsync(ICollection<string> repositories, string projectId, string symbolId)
        {
            throw new NotImplementedException();
        }

        Task<BoundSourceFile> IStorage.GetBoundSourceFileAsync(ICollection<string> repositories, string projectId, string filePath, bool includeDefinitions)
        {
            throw new NotImplementedException();
        }

        Task<SymbolReferenceResult> IStorage.GetReferencesToSymbolAsync(ICollection<string> repositories, Symbol symbol, int maxNumberOfItems)
        {
            throw new NotImplementedException();
        }

        Task<ProjectContents> IStorage.GetProjectContentsAsync(ICollection<string> repositories, string projectId)
        {
            throw new NotImplementedException();
        }

        public Task<IEnumerable<string>> GetReferencingProjects(string projectId)
        {
            throw new NotImplementedException();
        }

        private class SymbolShortNameComparer : IComparer<SymbolSearchResultEntry>
        {
            public static readonly SymbolShortNameComparer Instance = new SymbolShortNameComparer();

            public int Compare(SymbolSearchResultEntry x, SymbolSearchResultEntry y)
            {
                return StringComparer.OrdinalIgnoreCase.Compare(x.Symbol.ShortName, y.Symbol.ShortName);
            }
        }
    }
}
