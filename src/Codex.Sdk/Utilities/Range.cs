using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Codex.Utilities
{
    public struct Range : IComparable<Range>
    {
        public int Start;
        public int End
        {
            get
            {
                return Start + Length - 1;
            }
        }

        public int Length;

        public Range(int start, int length)
        {
            Start = start;
            Length = length;
        }

        public int CompareTo(Range other)
        {
            if (Start > other.End)
            {
                return RangeHelper.FirstGreaterThanSecond;
            }
            else if (End < other.Start)
            {
                return RangeHelper.FirstLessThanSecond;
            }

            return RangeHelper.FirstEqualSecond;
        }
    }
}
