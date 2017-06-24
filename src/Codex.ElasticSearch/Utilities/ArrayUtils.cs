using System.Collections.Generic;
using System.Diagnostics.Contracts;

namespace Codex.Storage.Utilities
{
    public static class ArrayUtils
    {
        public static ICollection<T> ToCollection<T>(this T value) where T : class
        {
            Contract.Ensures(Contract.Result<ICollection<T>>() != null);

            if (value == default(T))
            {
                // Use singleton array after switching to .NET 4.6
                return new T[] { };
            }

            return new T[] { value };
        }
    }
}