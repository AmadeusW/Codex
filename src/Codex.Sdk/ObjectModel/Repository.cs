using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Codex.ObjectModel
{
    public class Repository
    {
        public Repository(string name, string sourcesIndexName = null)
        {
            Contract.Requires(name != null);

            Id = name;
            Name = name;
            SourcesIndexName = sourcesIndexName ?? name.ToLower();
        }

        public string Id { get; set; }

        public string WebAddress { get; set; }

        public string SourceControlLocation { get; set; }

        public string BranchName { get; set; }

        public DateTime DateUploaded { get; set; }

        public DateTime DateCommitted { get; set; }

        public string CommitId { get; set; }

        /// <summary>
        /// Consider using a string formatted to be sortable
        /// i.e. 1.0.0.0 becomes '00001.00000.00000.00000'
        /// There might be a good data type or NEST/ES might support Version
        /// </summary>
        public string Version { get; set; }

        public string Name { get; set; }

        public string SourcesIndexName { get; set; }
    }
}
