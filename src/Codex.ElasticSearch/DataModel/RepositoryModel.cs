using System.Diagnostics.Contracts;
using Nest;
using System;

namespace Codex.Storage.DataModel
{
    public class RepositoryModel
    {
        public RepositoryModel()
        { }

        public RepositoryModel(string name, string sourcesIndexName)
        {
            Contract.Requires(name != null);
            Contract.Requires(sourcesIndexName != null);

            Id = name;
            Name = name;
            SourcesIndexName = sourcesIndexName;
        }

        [Keyword]
        public string Id { get; set; }

        [DataString]
        public string WebAddress { get; set; }

        [Keyword]
        public string SourceControlLocation { get; set; }

        [Keyword]
        public string BranchName { get; set; }

        [Date]
        public DateTime DateUploaded { get; set; }

        [Date]
        public DateTime DateCommitted { get; set; }

        [Keyword]
        public string CommitId { get; set; }

        /// <summary>
        /// Consider using a string formatted to be sortable
        /// i.e. 1.0.0.0 becomes '00001.00000.00000.00000'
        /// There might be a good data type or NEST/ES might support Version
        /// </summary>
        [Sortword]
        public string Version { get; set; }

        [Keyword]
        public string Name { get; set; }

        [Keyword]
        public string SourcesIndexName { get; set; }
    }
}