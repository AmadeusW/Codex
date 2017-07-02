using System;
using System.Collections.Generic;
using Nest;

namespace Codex.Storage.ElasticProviders
{
    public abstract class ElasticProviderExceptionBase : Exception
    {
        protected ElasticProviderExceptionBase(string message, Exception innerException)
            : base(message, innerException)
        {
        }
        protected ElasticProviderExceptionBase(string message)
            : base(message)
        {
        }
    }

    public sealed class ElasticProviderCommunicationException : ElasticProviderExceptionBase
    {
        public ElasticProviderCommunicationException(IResponse response, string esQuery)
            : base($"Failed to use Elasticsearch\r\n'''\r\n{esQuery}\r\n'''\r\n", response.OriginalException)
        {
            EsQuery = esQuery;
        }

        public string EsQuery { get; }
    }

    public sealed class ElasticProviderException : ElasticProviderExceptionBase
    {
        public ElasticProviderException(string message) : base(message)
        { }
    }

    public sealed class RepositoryAlreadyExistsException : ElasticProviderExceptionBase
    {
        public RepositoryAlreadyExistsException(string repoName)
            : base($"Repository '{repoName}' already exists")
        {
        }
    }

    public sealed class CantResolveIndicesException : ElasticProviderExceptionBase
    {
        public CantResolveIndicesException(ICollection<string> unresolvedRepos)
            : base($"Can't resolve source indices for repos [{string.Join(", ", unresolvedRepos)}]")
        {
        }
    }
}