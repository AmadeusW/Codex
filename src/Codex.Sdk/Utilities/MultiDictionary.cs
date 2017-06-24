using System;
using System.Collections.Generic;
using System.Linq;

namespace Codex.Utilities
{
    public class MultiDictionary<K, V> : Dictionary<K, HashSet<V>>
    {
        private IEqualityComparer<V> valueComparer;

        public MultiDictionary()
        {
        }

        public MultiDictionary(IEnumerable<IGrouping<K, V>> groups)
        {
            foreach (var group in groups)
            {
                foreach (var item in group)
                {
                    Add(group.Key, item);
                }
            }
        }

        public MultiDictionary(IEqualityComparer<K> keyComparer, IEqualityComparer<V> valueComparer)
            : base(keyComparer)
        {
            this.valueComparer = valueComparer;
        }

        public void Add(K key, V value)
        {
            if (EqualityComparer<K>.Default.Equals(default(K), key))
            {
                throw new ArgumentNullException("key");
            }

            HashSet<V> bucket = null;
            if (!TryGetValue(key, out bucket))
            {
                bucket = new HashSet<V>(valueComparer);
                this.Add(key, bucket);
            }

            bucket.Add(value);
        }
    }
}
