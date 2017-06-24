using System;
using System.Collections.Generic;
using Codex.ObjectModel;

namespace Codex.Storage
{
    public class SearchResultSorter
    {
        public static void Sort(List<SymbolSearchResultEntry> entries, string searchTerm)
        {
            foreach (var entry in entries)
            {
                entry.MatchLevel = MatchLevel(entry.Symbol.ShortName, searchTerm);
                entry.KindRank = Rank(entry);
            }

            entries.Sort(SymbolSorter);
        }

        public class Comparer : IComparer<SymbolSearchResultEntry>
        {
            public int Compare(SymbolSearchResultEntry x, SymbolSearchResultEntry y)
            {
                return SymbolSorter(x, y);
            }
        }

        public static readonly Comparer SymbolComparer = new Comparer();

        /// <summary>
        /// This defines the ordering of results based on the kind of symbol and other heuristics
        /// </summary>
        public static int SymbolSorter(SymbolSearchResultEntry left, SymbolSearchResultEntry right)
        {
            if (left == right)
            {
                return 0;
            }

            if (left == null || right == null)
            {
                return 1;
            }

            var comparison = left.MatchLevel.CompareTo(right.MatchLevel);
            if (comparison != 0)
            {
                return comparison;
            }

            comparison = left.KindRank.CompareTo(right.KindRank);
            if (comparison != 0)
            {
                return comparison;
            }

            comparison = right.Rank.CompareTo(left.Rank);
            if (comparison != 0)
            {
                return comparison;
            }

            if (left.Symbol.ShortName != null && right.Symbol.ShortName != null)
            {
                comparison = left.Symbol.ShortName.CompareTo(right.Symbol.ShortName);
                if (comparison != 0)
                {
                    return comparison;
                }
            }

            comparison = left.Symbol.ProjectId.CompareTo(right.Symbol.ProjectId);
            if (comparison != 0)
            {
                return comparison;
            }

            comparison = StringComparer.Ordinal.Compare(left.DisplayName, right.DisplayName);
            return comparison;
        }

        /// <summary>
        /// This defines the ordering of the results, assigning weight to different types of matches
        /// </summary>
        public static ushort MatchLevel(string candidate, string query)
        {
            int indexOf = candidate.IndexOf(query);
            int indexOfIgnoreCase = candidate.IndexOf(query, StringComparison.OrdinalIgnoreCase);

            if (indexOf == 0)
            {
                if (candidate.Length == query.Length)
                {
                    // candidate == query
                    return 1;
                }
                else
                {
                    // candidate.StartsWith(query)
                    return 3;
                }
            }
            else if (indexOf > 0)
            {
                if (indexOfIgnoreCase == 0)
                {
                    if (candidate.Length == query.Length)
                    {
                        return 2;
                    }
                    else
                    {
                        return 4;
                    }
                }
                else
                {
                    return 5;
                }
            }
            else // indexOf < 0
            {
                if (indexOfIgnoreCase == 0)
                {
                    if (candidate.Length == query.Length)
                    {
                        // query.Equals(candidate, StringComparison.OrdinalIgnoreCase)
                        return 2;
                    }
                    else
                    {
                        // candidate.StartsWith(query, StringComparison.OrdinalIgnoreCase)
                        return 4;
                    }
                }
                else
                {
                    return 7;
                }
            }
        }

        public static ushort Rank(SymbolSearchResultEntry entry)
        {
            var kind = entry.Symbol.Kind.ToLowerInvariant();
            switch (kind)
            {
                case "class":
                case "struct":
                case "interface":
                case "enum":
                case "delegate":
                    return 1;
                case "field":
                    return 3;
                case "file":
                    return 4;
                default:
                    return 2;
            }
        }
    }
}
