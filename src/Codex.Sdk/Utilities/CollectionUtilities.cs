using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Codex.ObjectModel;

namespace Codex.Utilities
{
    public static class CollectionUtilities
    {
        public static IEnumerable<T> Interleave<T>(IEnumerable<T> spans1, IEnumerable<T> spans2)
            where T : Span
        {
            bool end1 = false;
            bool end2 = false;

            var enumerator1 = spans1.GetEnumerator();
            var enumerator2 = spans2.GetEnumerator();

            T current1 = default(T);
            T current2 = default(T);

            end1 = MoveNext(enumerator1, ref current1);
            end2 = MoveNext(enumerator2, ref current2);

            while (!end1 || !end2)
            {
                while (!end1)
                {
                    if (end2 || current1.Start <= current2.Start)
                    {
                        yield return current1;
                    }
                    else
                    {
                        break;
                    }

                    end1 = MoveNext(enumerator1, ref current1);
                }

                while (!end2)
                {
                    if (end1 || current2.Start <= current1.Start)
                    {
                        yield return current2;
                    }
                    else
                    {
                        break;
                    }

                    end2 = MoveNext(enumerator2, ref current2);
                }
            }
        }

        public static IEnumerable<T> ExclusiveInterleave<T>(IEnumerable<T> spans1, IEnumerable<T> spans2, IComparer<T> comparer)
            where T : Span
        {
            bool end1 = false;
            bool end2 = false;

            var enumerator1 = spans1.GetEnumerator();
            var enumerator2 = spans2.GetEnumerator();

            T current1 = default(T);
            T current2 = default(T);

            end1 = MoveNext(enumerator1, ref current1);
            end2 = MoveNext(enumerator2, ref current2);

            while (!end1 || !end2)
            {
                while (!end1)
                {
                    if (end2 || comparer.Compare(current1, current2) <= 0)
                    {
                        yield return current1;

                        // Skip over matching spans from second list
                        while (!end2 && comparer.Compare(current1, current2) == 0)
                        {
                            end2 = MoveNext(enumerator2, ref current2);
                        }
                    }
                    else
                    {
                        break;
                    }

                    end1 = MoveNext(enumerator1, ref current1);
                }

                while (!end2)
                {
                    if (end1 || comparer.Compare(current1, current2) > 0)
                    {
                        yield return current2;
                    }
                    else
                    {
                        break;
                    }

                    end2 = MoveNext(enumerator2, ref current2);
                }
            }
        }

        private static bool MoveNext<T>(IEnumerator<T> enumerator1, ref T current) where T : Span
        {
            if (enumerator1.MoveNext())
            {
                current = enumerator1.Current;
                return true;
            }
            else
            {
                return false;
            }
        }

        public static TValue GetOrDefault<TKey, TValue>(this IDictionary<TKey, TValue> dictionary, TKey key, TValue defaultValue = default(TValue))
        {
            TValue value;
            if (!dictionary.TryGetValue(key, out value))
            {
                value = defaultValue;
            }

            return value;
        }

        public static TValue GetOrAdd<TKey, TValue>(this IDictionary<TKey, TValue> dictionary, TKey key, TValue defaultValue = default(TValue))
        {
            TValue value;
            if (!dictionary.TryGetValue(key, out value))
            {
                dictionary[key] = defaultValue;
                value = defaultValue;
            }

            return value;
        }
    }
}
