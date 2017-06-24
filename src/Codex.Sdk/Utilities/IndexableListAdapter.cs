using System.Collections.Generic;
using System.Linq;
using System.Collections;

namespace Codex.Utilities
{
    public static class IndexableListAdapter
    {
        public static IReadOnlyList<T> GetReadOnlyList<T>(this IIndexable<T> model)
        {
            if (model == null)
            {
                return IndexableSpans.Empty<T>();
            }

            return new IndexableListAdapter<T>(model);
        }

        public static IReadOnlySpanList<T> GetSpanList<T>(this IIndexableSpans<T> model)
        {
            if (model == null)
            {
                return IndexableSpans.Empty<T>();
            }

            return new IndexableSpanListAdapter<T>(model);
        }
    }

    public class IndexableListAdapter<T> : IReadOnlyList<T>
    {
        private readonly IIndexable<T> model;

        public IndexableListAdapter(IIndexable<T> model)
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

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }
}
