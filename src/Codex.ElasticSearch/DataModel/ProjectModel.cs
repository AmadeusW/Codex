using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Linq;
using Codex.Storage.ElasticProviders;
using Nest;
using Newtonsoft.Json;

namespace Codex.Storage.DataModel
{
    using static CustomAnalyzers;

    public class ProjectModel
    {
        public ProjectModel()
        { }

        public ProjectModel(string id, string repositoryName)
        {
            Contract.Requires(!string.IsNullOrEmpty(id));
            Contract.Requires(!string.IsNullOrEmpty(repositoryName));

            Id = id;
            RepositoryName = repositoryName;
        }

        [Keyword]
        public string Id { get; set; }

        [Keyword]
        public string RepositoryName { get; set; }

        [SearchString(Analyzer = LowerCaseKeywordAnalyzerName)]
        [JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore)]
        public string ProjectKind { get; set; }

        [Sortword]
        public string RepoId { get; set; }

        [Date]
        public DateTime DateUploaded { get; set; }

        [Nested]
        public List<ReferencedProjectModel> ReferencedProjects { get; set; } = new List<ReferencedProjectModel>();

        [DataString]
        public string WebAddress { get; set; }

        public ProjectModel AddReferencedProjects(params ReferencedProjectModel[] referencedProjects)
        {
            ReferencedProjects.AddRange(referencedProjects);
            return this;
        }

        public ProjectModel AddReferencedProjects(params string[] referencedProjects)
        {
            ReferencedProjects.AddRange(referencedProjects.Select(r => new ReferencedProjectModel() { ProjectId = r }));
            return this;
        }
    }
}