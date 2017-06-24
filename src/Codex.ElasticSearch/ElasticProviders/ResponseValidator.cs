using System.Threading.Tasks;
using Nest;

namespace Codex.Storage.ElasticProviders
{
    internal static class ResponseValidator
    {
        public static async Task<T> ThrowOnFailure<T>(this Task<T> result) where T : IResponse
        {
            return (await result).ThrowOnFailure();
        }

        public static T ThrowOnFailure<T>(this T result) where T : IResponse
        {
            if (result == null)
            {
                return default(T);
            }

            if (!result.ApiCall.Success)
            {
                throw new ElasticProviderCommunicationException(result, result.DebugInformation);
            }

            return result;
        }
    }
}