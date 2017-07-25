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

        public static ListSegment<ReferenceSpan> FindOverlappingReferenceSpans(this BoundSourceFile boundSourceFile, Range span)
        {
            if (boundSourceFile.References == null)
            {
                return new ListSegment<ReferenceSpan>(CollectionUtilities.Empty<ReferenceSpan>.List);
            }

            return boundSourceFile.References.GetRange(span, CompareSpanMin, CompareSpanMax);
        }

        public static ListSegment<T> FindOverlappingSpans<T>(this IReadOnlyList<T> spans, Range span)
            where T : Span
        {
            if (spans == null)
            {
                return new ListSegment<T>(CollectionUtilities.Empty<T>.List);
            }

            return spans.GetRange(span, CompareSpanMin, CompareSpanMax);
        }

        private static int CompareSpanMax(Range intersectSpan, Span span)
        {
            if (intersectSpan.End < span.Start)
            {
                return -1;
            }

            return 0;
        }

        private static int CompareSpanMin(Range intersectSpan, Span span)
        {
            if (intersectSpan.Start > (span.Start + span.Length))
            {
                return 1;
            }

            return 0;
        }

    }
}
