namespace Codex.Storage.ElasticProviders
{
    public sealed class ElasticProviderConfig
    {
        public const string DefaultCoreIndexName = "codex_core";

        public const string DefaultProjectTypeName = "projects";

        public const string DefaultSourceIndexName = "codex_store";

        public const string DefaultSourceTypeName = "sources";

        public const string DefaultEndpoint = "http://localhost:9200";

        public const string DefaultRepositoryTypeName = "repos";

        public const string DefaultCombinedSourcesIndexName = "codex_source";

        public const int DefaultMaxNumbersOfRecordsToReturn = 200;

        // "Core" index stores projects and repos
        public string CoreIndexName { get; private set; } = DefaultCoreIndexName;
        public string ProjectTypeName { get; private set; } = DefaultProjectTypeName;
        public string RepositoryTypeName { get; private set; } = DefaultRepositoryTypeName;

        public string SourceTypeName { get; private set; } = DefaultSourceTypeName;

        public string CombinedSourcesIndexAlias { get; private set; } = DefaultCombinedSourcesIndexName;

        public string Endpoint { get; private set; } = DefaultEndpoint;

        public int MaxNumbersOfRecordsToReturn { get; private set; } = DefaultMaxNumbersOfRecordsToReturn;

        public ElasticProviderConfig WithCoreIndexName(string projectIndexName)
        {
            CoreIndexName = projectIndexName;
            return this;
        }

        public ElasticProviderConfig WithProjectTypeName(string projectTypeName)
        {
            ProjectTypeName = projectTypeName;
            return this;
        }

        public ElasticProviderConfig WithSourceTypeName(string sourceTypeName)
        {
            SourceTypeName = sourceTypeName;
            return this;
        }

        public ElasticProviderConfig WithEndpoint(string endpoint)
        {
            Endpoint = endpoint;
            return this;
        }

        public ElasticProviderConfig WithRepositoryTypeName(string repositoryTypeName)
        {
            RepositoryTypeName = repositoryTypeName;
            return this;
        }

        public ElasticProvider CreateProvider()
        {
            return new ElasticProvider(Endpoint, CoreIndexName, ProjectTypeName, SourceTypeName);
        }

        public static ElasticProviderConfig Create(string endpoint)
        {
            return new ElasticProviderConfig().WithEndpoint(endpoint);
        }
    }
}