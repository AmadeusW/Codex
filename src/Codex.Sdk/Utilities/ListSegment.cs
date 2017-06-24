using System;
using System.Collections.Generic;

namespace Codex.Utilities
{
    /// <summary>
    /// A segment of a list
    /// </summary>
    /// <typeparam name="T">the list item type</typeparam>
    public struct ListSegment<T> : IReadOnlyList<T>
    {
        /// <summary>
        /// The underlying list
        /// </summary>
        public IReadOnlyList<T> List { get; private set; }

        /// <summary>
        /// The start index
        /// </summary>
        public int Start { get; private set; }

        /// <summary>
        /// The end index
        /// </summary>
        public int End { get; private set; }

        /// <summary>
        /// Constructs a new list segment containing the full list
        /// </summary>
        /// <param name="list">the underlying list</param>
        public ListSegment(IReadOnlyList<T> list)
            : this(list, 0, list.Count)
        {
        }

        /// <summary>
        /// Constructs a new list segment specified part of the list
        /// </summary>
        /// <param name="list">the underlying list</param>
        public ListSegment(IReadOnlyList<T> list, int start, int count)
            : this()
        {
            List = list;
            Start = start;
            End = (start + count - 1);
        }

        /// <summary>
        /// Returns true if this list segment contains the given index
        /// </summary>
        /// <param name="index">the index in the underlying list</param>
        /// <returns>true if the segment contains the index, false otherwise.</returns>
        public bool ContainsIndex(int index)
        {
            if (index < Start || index > End)
            {
                return false;
            }

            return true;
        }

        #region IReadOnlyList<T> Members

        /// <summary>
        /// Returns the item at the index in the segment relative to the segment start.
        /// </summary>
        /// <param name="index">the segment index</param>
        /// <returns>the item at the index</returns>
        public T this[int index]
        {
            get
            {
                if (index < 0 || index >= Count)
                {
                    throw new IndexOutOfRangeException();
                }
                return List[Start + index];
            }
        }

        #endregion

        #region IReadOnlyCollection<T> Members

        /// <summary>
        /// The count of items in the segment
        /// </summary>
        public int Count
        {
            get { return End - Start + 1; }
        }

        #endregion

        #region IEnumerable<T> Members

        public IEnumerator<T> GetEnumerator()
        {
            for (int i = Start; i <= End; i++)
            {
                yield return List[i];
            }
        }

        #endregion

        #region IEnumerable Members

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        #endregion
    }
}
