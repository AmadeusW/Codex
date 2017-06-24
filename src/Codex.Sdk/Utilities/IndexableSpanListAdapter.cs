using System.Collections.Generic;
using System.Linq;
using System.Collections;
using System;

namespace Codex.Utilities
{
    public class IndexableSpanListAdapter<T> : IReadOnlySpanList<T>
    {
        private readonly IIndexableSpans<T> model;

        public IndexableSpanListAdapter(IIndexableSpans<T> model)
        {
            this.model = model;
        }

        public T this[int index]
        {
            get
            {
                return model[index];
            }
        }

        public int Count
        {
            get
            {
                return model.Count;
            }
        }

        public IEnumerator<T> GetEnumerator()
        {
            for (int i = 0; i < model.Count; i++)
            {
                yield return model[i];
            }
        }

        public ListSegment<T> GetSpans(int startPosition, int endPosition)
        {
            return model.GetSpans(startPosition, endPosition);
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }
}
