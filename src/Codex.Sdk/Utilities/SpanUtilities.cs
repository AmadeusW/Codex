using System.Collections.Generic;
using System.Linq;
using System.Collections;
using Codex.ObjectModel;

namespace Codex.Utilities
{
    public static class SpanUtilities
    {
        public static ListSegment<T> GetSpans<T>(this IReadOnlyList<T> spans, int start, int length)
            where T : ObjectModel.Span
        {
            IIndexableSpans<T> spansList = spans as IIndexableSpans<T>;
            if (spansList != null)
            {
                return spansList.GetSpans(start: start, length: length);
            }

            return RangeHelper.GetRange(spans, new Range(start, length),
                minimumComparer: (range, span) => RangeHelper.MinCompare(range, new Range(span.Start, span.Length), inclusive: true),
                maximumComparer: (range, span) => RangeHelper.MaxCompare(range, new Range(span.Start, span.Length), inclusive: true));
        }

        public static Range ToRange(this Span span)
        {
            return new Range(start: span.Start, length: span.Length);
        }
    }
}
