using System;
using System.Collections.Generic;

namespace Codex.Utilities
{
    /// <summary>
    /// Provides utility methods for performing binary search operations on lists
    /// </summary>
    public static class RangeHelper
    {
        public const int FirstGreaterThanSecond = 1;
        public const int FirstEqualSecond = 0;
        public const int FirstLessThanSecond = -1;

        /// <summary>
        /// Performs a comparison of a range minimum.
        /// </summary>
        /// <typeparam name="T">the operand type</typeparam>
        /// <param name="minimum">the range minimum</param>
        /// <param name="item">the item to compare</param>
        /// <param name="inclusive">specifies whether the minimum is inclusive</param>
        /// <returns>
        /// Less than zero if the minimum should be positioned before the item in a sequence
        /// where all values between the minimum and maximum are considered in the range.
        /// Otherwise, greater than zero.
        /// </returns>
        public static int MinCompare<T, TComparable>(this TComparable minimum, T item, bool inclusive)
            where TComparable : IComparable<T>
        {
            if (minimum == null)
            {
                return FirstLessThanSecond;
            }

            var result = minimum.CompareTo(item);
            if (result == FirstEqualSecond)
            {
                if (inclusive) return FirstLessThanSecond;
                else return FirstGreaterThanSecond;
            }
            return result;
        }

        /// <summary>
        /// Performs a comparison of a range maximum.
        /// </summary>
        /// <typeparam name="T">the operand type</typeparam>
        /// <param name="minimum">the range maximum</param>
        /// <param name="item">the item to compare</param>
        /// <param name="inclusive">specifies whether the maximum is inclusive</param>
        /// <returns>
        /// Greater than zero if the minimum should be positioned before the item in a sequence
        /// where all values between the minimum and maximum are considered in the range.
        /// Otherwise, less than zero.
        /// </returns>
        public static int MaxCompare<T, TComparable>(this TComparable maximum, T item, bool inclusive)
            where TComparable : IComparable<T>
        {
            // Null is considered greater than all values
            if (maximum == null)
            {
                return FirstGreaterThanSecond;
            }

            var result = maximum.CompareTo(item);
            if (result == FirstEqualSecond)
            {
                if (inclusive) return FirstGreaterThanSecond;
                else return FirstLessThanSecond;
            }
            return result;
        }

        /// <summary>
        /// Returns the minimum of two comparable items
        /// </summary>
        /// <typeparam name="T">the operand type</typeparam>
        /// <param name="item1">the first operand</param>
        /// <param name="item2">the second operand</param>
        /// <returns>the minimum item</returns>
        public static T Min<T>(T item1, T item2) where T : IComparable<T>
        {
            int result = MinCompare(item1, item2, false);
            if (result == -1)
            {
                return item1;
            }
            else
            {
                return item2;
            }
        }

        /// <summary>
        /// Returns the maximum of two comparable items
        /// </summary>
        /// <typeparam name="T">the operand type</typeparam>
        /// <param name="item1">the first operand</param>
        /// <param name="item2">the second operand</param>
        /// <returns>the maximum item</returns>
        public static T Max<T>(T item1, T item2) where T : IComparable<T>
        {
            int result = MaxCompare(item1, item2, false);
            if (result == 1)
            {
                return item1;
            }
            else
            {
                return item2;
            }
        }

        /// <summary>
        /// Gets the range in the list matched by the specified range
        /// </summary>
        /// <typeparam name="TItem">the list item type</typeparam>
        /// <typeparam name="TRange">the range type</typeparam>
        /// <param name="list">the list</param>
        /// <param name="range">the range to find</param>
        /// <param name="minimumComparer">
        /// comparison function which returns a negative value if and only if the minimum of the range excludes the item
        /// </param>
        /// <param name="maximumComparer">
        /// comparison function which returns a positive value if and only if the maximum of the range excludes the item
        /// </param>
        /// <returns>
        /// A list segment containing representing matched range
        /// </returns>
        public static ListSegment<TItem> GetRange<TItem, TRange>(
            this IReadOnlyList<TItem> list,
            TRange range,
            Func<TRange, TItem, int> minimumComparer,
            Func<TRange, TItem, int> maximumComparer)
        {
            var resultRange = GetRange<TItem, TRange, IReadOnlyList<TItem>>(list, range, minimumComparer, maximumComparer);
            return new ListSegment<TItem>(list, resultRange.Start, resultRange.Length);
        }

        /// <summary>
        /// Gets the range in the list matched by the specified range
        /// </summary>
        /// <typeparam name="TItem">the list item type</typeparam>
        /// <typeparam name="TRange">the range type</typeparam>
        /// <param name="list">the list</param>
        /// <param name="range">the range to find</param>
        /// <param name="minimumComparer">
        /// comparison function which returns a negative value if and only if the minimum of the range excludes the item
        /// </param>
        /// <param name="maximumComparer">
        /// comparison function which returns a positive value if and only if the maximum of the range excludes the item
        /// </param>
        /// <returns>
        /// The matched range
        /// </returns>
        public static Range GetRange<TItem, TRange, TList>(
            this TList list,
            TRange range,
            Func<TRange, TItem, int> minimumComparer,
            Func<TRange, TItem, int> maximumComparer)
            where TList : IReadOnlyList<TItem>
        {
            int lower = 0;
            int lowerMax = list.Count - 1;
            int lowerMiddle = 0;

            int upperMax = list.Count - 1;
            int upper = 0;
            int upperMiddle = upperMax;

            int comparisonResult = 0;
            bool continueLoop = true;

            while (continueLoop)
            {
                continueLoop = false;

                if (lower <= lowerMax)
                {
                    comparisonResult = minimumComparer(range, list[lowerMiddle]);
                    int newLowerMiddle = lowerMiddle;

                    while (lower <= lowerMax && newLowerMiddle == lowerMiddle)
                    {
                        if (comparisonResult <= 0)
                        {
                            lowerMax = lowerMiddle - 1;
                        }
                        else if (comparisonResult > 0)
                        {
                            lower = lowerMiddle + 1;
                        }

                        newLowerMiddle = lower + (lowerMax - lower) / 2;
                    }

                    lowerMiddle = newLowerMiddle;
                    continueLoop = true;
                }

                if (upper <= upperMax)
                {
                    comparisonResult = maximumComparer(range, list[upperMiddle]);
                    int newUpperMiddle = upperMiddle;

                    while (upper <= upperMax && newUpperMiddle == upperMiddle)
                    {
                        if (comparisonResult < 0)
                        {
                            upperMax = upperMiddle - 1;
                        }
                        else if (comparisonResult >= 0)
                        {
                            upper = upperMiddle + 1;
                        }

                        newUpperMiddle = upper + (upperMax - upper) / 2;
                    }

                    upperMiddle = newUpperMiddle;
                    continueLoop = true;
                }
            }

            upper--;
            if (upper < lower)
            {
                lower = 0;
                upper = -1;
            }

            return new Range(lower, (upper - lower) + 1);
        }

        public static ListSegment<TItem> GetPrefixRange<TItem>(
            this IReadOnlyList<TItem> list,
            string prefix,
            Func<TItem, string> stringGetter)
        {
            return GetRange(
                list,
                prefix,
                (prefix1, item) => CompareTermLower(prefix1, stringGetter(item)),
                (prefix1, item) => CompareTermUpper(prefix1, stringGetter(item)));
        }

        private static int CompareTermUpper(string searchTerm, string value)
        {
            var comparisonResult = StringComparer.OrdinalIgnoreCase.Compare(searchTerm, value);
            if (comparisonResult < 0)
            {
                if (value.StartsWith(searchTerm, StringComparison.OrdinalIgnoreCase))
                {
                    return 0;
                }
            }

            return comparisonResult;
        }

        private static int CompareTermLower(string searchTerm, string value)
        {
            var comparisonResult = StringComparer.OrdinalIgnoreCase.Compare(searchTerm, value);
            if (comparisonResult < 0)
            {
                if (value.StartsWith(searchTerm, StringComparison.OrdinalIgnoreCase))
                {
                    return 0;
                }
            }

            return comparisonResult;
        }
    }
}
