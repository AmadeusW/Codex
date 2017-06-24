using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics.Contracts;

namespace Codex.ObjectModel
{
    public class AnalyzedProject
    {
        public AnalyzedProject(string repositoryName, string projectId)
        {
            Contract.Requires(!string.IsNullOrEmpty(repositoryName));
            Contract.Requires(!string.IsNullOrEmpty(projectId));

            RepositoryName = repositoryName;
            Id = projectId;
        }

        public string RepositoryName { get; set; }

        public string Id { get; set; }

        public string WebAddress { get; set; }

        public string DirectoryPath { get; set; }

        public string ProjectKind { get; set; }

        public List<BoundSourceFile> AdditionalSourceFiles { get; set; } = new List<BoundSourceFile>();

        /// <summary>
        /// Descriptions of referenced projects and used definitions from the projects
        /// </summary>
        public List<ReferencedProject> ReferencedProjects { get; set; } = new List<ReferencedProject>();

        public ConcurrentDictionary<Symbol, DefinitionSymbol> ReferenceDefinitionMap = new ConcurrentDictionary<Symbol, DefinitionSymbol>();
    }
}