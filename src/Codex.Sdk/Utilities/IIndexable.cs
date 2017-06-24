using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Codex.Utilities
{
    public interface IIndexable<T>
    {
        T this[int index] { get; }

        int Count { get; }
    }
}
