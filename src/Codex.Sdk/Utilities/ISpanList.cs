using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace Codex.Utilities
{
    public interface IIndexableSpans<T> : IIndexable<T>
    {
        ListSegment<T> GetSpans(int start, int length);
    }

    public interface IReadOnlySpanList<T> : IIndexableSpans<T>, IReadOnlyList<T>
    {
    }

    public static class IndexableSpans
    {
        public static IReadOnlySpanList<T> Empty<T>()
        {
            return EmptyIndexableSpans<T>.Instance;
        }

        private class EmptyIndexableSpans<T> : IIndexableSpans<T>, IReadOnlySpanList<T>
        {
            public static readonly EmptyIndexableSpans<T> Instance = new EmptyIndexableSpans<T>();

            public T this[int index]
            {
                get
                {
                    throw new NotImplementedException();
                }
            }

            public int Count
            {
                get
                {
                    return 0;
                }
            }

            public IEnumerator<T> GetEnumerator()
            {
                return Enumerable.Empty<T>().GetEnumerator();
            }

            public ListSegment<T> GetSpans(int startPosition, int endPosition)
            {
                return new ListSegment<T>();
            }

            IEnumerator IEnumerable.GetEnumerator()
            {
                return Enumerable.Empty<T>().GetEnumerator();
            }
        }
    }
}
