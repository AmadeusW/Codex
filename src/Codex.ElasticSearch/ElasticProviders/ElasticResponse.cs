using System.Collections;
using System.Diagnostics;
using System.Diagnostics.Contracts;
using System.Text;
using Nest;

namespace Codex.Storage.ElasticProviders
{
    public class ElasticResponse
    {
        protected ElasticResponse(IResponse response, int operationDuration, long? backendDuration = null)
        {
            OperationDuration = operationDuration;
            BackendDuration = backendDuration;

            // For checking cluster existence request could be null!
            if (response?.ApiCall?.RequestBodyInBytes == null)
            {
                Request = "<none>";
            }
            else
            {
                Request = ElasticUtility.GetRequestString(response.ApiCall.RequestBodyInBytes);
            }

            Url = response?.ApiCall?.Uri?.ToString() ?? "<none>";
        }

        public static ElasticResponse CreateFakeResponse(int operationDuration)
        {
            return new ElasticResponse(response: null, operationDuration: operationDuration, backendDuration: null);
        }

        public static ElasticResponse CreateResponse(IResponse response, int operationDuration)
        {
            Contract.Requires(response != null);

            return new ElasticResponse(response, operationDuration);
        }

        public static ElasticResponse<T> Create<T>(IResponse response, T result, int total, int operationDuration,
            long? backendDurationMilliseconds) where T : class
        {
            Contract.Requires(response != null);
            Contract.Requires(total >= 0);

            return new ElasticResponse<T>(response, result, total, operationDuration, backendDurationMilliseconds);
        }

        public static ElasticResponse<U> FromSearchResult<T, U>(ISearchResponse<T> response, U entries, Stopwatch sw) where T : class where U : class
        {
            return Create(response, entries, (int)response.Total, (int)sw.ElapsedMilliseconds, response.Took);
        }

        public string Url { get; }

        /// <summary>
        /// Original request string
        /// </summary>
        public string Request { get; }

        //internal IResponse Response { get; }

        /// <summary>
        /// Duration of the current operation in ms
        /// </summary>
        public int OperationDuration { get; }

        /// <summary>
        /// Duration of the operation at the Elasticsearch in ms
        /// </summary>
        public long? BackendDuration { get; }

        public string ToString(bool includeRequest)
        {
            return $"{ToString()}\r\nUrl:{Url}\r\nRequest:\r\n{Request}";
        }

        public override string ToString()
        {
            var sb = new StringBuilder();
            sb.AppendLine($"Took: {OperationDuration}ms");
            if (BackendDuration != null)
            {
                sb.AppendLine($"Backend took: {BackendDuration}ms");
            }

            return sb.ToString();
        }
    }

    /// <summary>
    /// Represents a smart response from the Elasticsearch
    /// </summary>
    public sealed class ElasticResponse<T> : ElasticResponse
    {
        public ElasticResponse(IResponse response, T result, int total, int operationDuration, long? backendDuration)
            : base(response, operationDuration, backendDuration)
        {
            Result = result;
            Total = total;
        }

        /// <summary>
        /// Result itself (could be null!)
        /// </summary>
        public T Result { get; }

        /// <summary>
        /// Total hits (returned by ES)
        /// </summary>
        public int Total { get; }

        public override string ToString()
        {
            var sb = new StringBuilder();
            var collection = Result as ICollection;
            sb.AppendLine($"Received {collection?.Count ?? 0} of {Total}");

            sb.AppendLine($"Took: {OperationDuration}ms");
            if (BackendDuration != null)
            {
                sb.AppendLine($"Backend took: {BackendDuration}ms");
            }

            return sb.ToString();
        }
    }
}