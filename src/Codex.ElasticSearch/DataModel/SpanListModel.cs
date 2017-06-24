using Codex.ObjectModel;
using Codex.Storage.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static Codex.Storage.Utilities.NumberUtils;
using System.Collections;
using Codex.Utilities;
using System.Runtime.Serialization;
using System.IO;
using System.IO.Compression;
using Newtonsoft.Json;
using System.Diagnostics.Contracts;

namespace Codex.Storage.DataModel
{
    public abstract class SpanListModel<TSpan, TSegment, TShared, TSharedKey> :
                IIndexable<TSpan>,
                IIndexableSpans<TSpan>
        where TSpan : Span
        where TSegment : SpanListSegmentModel
    {
        public const int SegmentSpanCountBitWidth = 12;
        public const int SegmentSpanCount = 1 << SegmentSpanCountBitWidth;
        public const int SegmentOffsetBitMask = SegmentSpanCount - 1;

        // Stored data
        public List<TShared> SharedValues { get; set; }
        public List<TSegment> Segments { get; set; }
        public int Count { get; set; }

        [JsonIgnore]
        public bool Optimize { get; set; } = true;

        public TSpan this[int index]
        {
            get
            {
                TSegment segment;
                int segmentOffset;
                TShared shared;
                Get(index, out segment, out segmentOffset, out shared);

                return CreateSpan(segment.Starts[segmentOffset], segment.Lengths[segmentOffset], shared, segment, segmentOffset);
            }
        }

        private void Get(int index, out TSegment segment, out int segmentOffset, out TShared shared)
        {
            GetSegmentAndOffset(index, out segment, out segmentOffset);
            var sharedIndex = segment.SharedIndices[segmentOffset];
            shared = SharedValues[sharedIndex];
        }

        private void GetSegmentAndOffset(int index, out TSegment segment, out int segmentOffset)
        {
            var segmentIndex = index >> SegmentSpanCountBitWidth;
            segment = Segments[segmentIndex];

            if (segment.Optimized)
            {
                segment.Expand(new OptimizationContext());
            }

            segmentOffset = (index & SegmentOffsetBitMask);
        }

        public abstract TSpan CreateSpan(int start, int length, TShared shared, TSegment segment, int segmentOffset);

        public abstract TSegment CreateSegment(ListSegment<TSpan> segmentSpans);

        public abstract TSharedKey GetSharedKey(TSpan span);

        public abstract TShared GetShared(TSpan span);

        public virtual int GetStart(TSpan span, TShared shared)
        {
            return span.Start;
        }

        [OnSerializing]
        public void OnSerializing(StreamingContext context)
        {
            if (Optimize)
            {
                var optimizationContext = new OptimizationContext();
                foreach (var segment in Segments)
                {
                    segment.Optimize(optimizationContext);
                }
            }
        }

        public SpanListModel()
        {
            SharedValues = new List<TShared>();
            Segments = new List<TSegment>();
        }

        public SpanListModel(
            IReadOnlyList<TSpan> spans,
            IEqualityComparer<TSharedKey> sharedKeyComparer = null,
            IComparer<TSharedKey> sharedKeySorter = null,
            IComparer<TShared> sharedValueSorter = null)
            : this()
        {
            Optimize = true;
            Count = spans.Count;
            List<TSharedKey> sharedKeys = new List<TSharedKey>();
            Dictionary<TSharedKey, int> sharedMap = new Dictionary<TSharedKey, int>(sharedKeyComparer ?? EqualityComparer<TSharedKey>.Default);
            foreach (var span in spans)
            {
                var sharedKey = GetSharedKey(span);
                if (!sharedMap.ContainsKey(sharedKey))
                {
                    sharedMap.Add(sharedKey, SharedValues.Count);
                    SharedValues.Add(GetShared(span));
                    sharedKeys.Add(sharedKey);
                }
            }

            if (sharedValueSorter != null || sharedKeySorter != null)
            {
                TSharedKey[] sharedKeyArray = sharedKeys.ToArray();
                TShared[] sharedValueArray = SharedValues.ToArray();

                if (sharedValueSorter != null)
                {
                    Array.Sort(sharedValueArray, sharedKeyArray, sharedValueSorter);

                }
                else
                {
                    Array.Sort(sharedKeyArray, sharedValueArray, sharedKeySorter);
                }

                // Ensure shared keys is not used after this point
                sharedKeys = null;
                SharedValues.Clear();
                SharedValues.AddRange(sharedValueArray);
                for (int index = 0; index < sharedKeyArray.Length; index++)
                {
                    sharedMap[sharedKeyArray[index]] = index;
                }
            }

            var segmentsLength = NumberUtils.Ceiling(spans.Count, SegmentSpanCount);
            int offset = 0;
            for (int segmentIndex = 0; segmentIndex < segmentsLength; segmentIndex++, offset += SegmentSpanCount)
            {
                var length = Math.Min(SegmentSpanCount, spans.Count - offset);
                if (length == 0)
                {
                    break;
                }

                var segmentSpans = new ListSegment<TSpan>(spans, offset, length);
                var segment = CreateSegment(segmentSpans);
                Segments.Add(segment);

                var firstSpanStart = segmentSpans[0].Start;
                var lastSpan = segmentSpans[length - 1];

                segment.Starts = IntegerListModel.Create(segmentSpans, s => GetStart(s, SharedValues[sharedMap[GetSharedKey(s)]]));
                segment.Lengths = IntegerListModel.Create(segmentSpans, s => s.Length);
                segment.FullLength = (lastSpan.Start + lastSpan.Length) - firstSpanStart;
                segment.SharedIndices = IntegerListModel.Create(segmentSpans, s => sharedMap[GetSharedKey(s)]);
            }
        }

        public virtual int GetEstimatedSize()
        {
            return Segments.Select(segment =>
                segment.Starts.DataLength +
                segment.Lengths.DataLength +
                segment.SharedIndices.DataLength).Sum();
        }

        public ListSegment<TSpan> GetSpans(int startPosition, int length)
        {
            var segmentList = new SegmentRangeList(Segments);
            var segmentListRange = segmentList.GetReadOnlyList().GetRange(new Range(startPosition, length),
                (searchRange, segmentRange) => RangeHelper.MinCompare(searchRange, segmentRange, inclusive: true),
                (searchRange, segmentRange) => RangeHelper.MaxCompare(searchRange, segmentRange, inclusive: true));

            var segmentStart = segmentListRange.Start;
            var segmentCount = segmentListRange.Count;

            var absoluteStart = segmentStart << SegmentSpanCountBitWidth;
            var absoluteLength = segmentCount << SegmentSpanCountBitWidth;
            var endSegmentExclusive = segmentStart + segmentCount;
            if (endSegmentExclusive >= Segments.Count)
            {
                absoluteLength -= (SegmentOffsetBitMask - (SegmentOffsetBitMask & (Count - 1)));
            }

            var spanRangeList = IndexableListAdapter.GetReadOnlyList(new SpanRangeList(this));

            var rangeListSegment = new ListSegment<Range>(spanRangeList, absoluteStart, absoluteLength);

            var spanRange = rangeListSegment.GetRange(new Range(startPosition, length),
                (r, start) => RangeHelper.MinCompare(r, start, inclusive: true),
                (r, start) => RangeHelper.MaxCompare(r, start, inclusive: true));

            return new ListSegment<TSpan>(this.GetReadOnlyList(), rangeListSegment.Start + spanRange.Start, spanRange.Count);
        }

        private Range GetRange(int index)
        {
            TSegment segment;
            int segmentOffset;
            GetSegmentAndOffset(index, out segment, out segmentOffset);
            return new Range(segment.Starts[segmentOffset], segment.Lengths[segmentOffset]);
        }

        protected static string AssignDuplicate(string value, ref string lastValue)
        {
            if (value == null)
            {
                return lastValue;
            }
            else
            {
                lastValue = value;
                return value;
            }
        }

        protected static string RemoveDuplicate(string value, ref string lastValue)
        {
            if (value == lastValue)
            {
                return null;
            }
            else
            {
                lastValue = value;
                return value;
            }
        }

        private struct SpanRangeList : IIndexable<Range>
        {
            private SpanListModel<TSpan, TSegment, TShared, TSharedKey> spanList;

            public SpanRangeList(SpanListModel<TSpan, TSegment, TShared, TSharedKey> spanList)
            {
                this.spanList = spanList;
            }

            public Range this[int index]
            {
                get
                {
                    return spanList.GetRange(index);
                }
            }

            public int Count
            {
                get
                {
                    return spanList.Count;
                }
            }
        }
    }

    internal struct SegmentRangeList : IIndexable<Range>
    {
        private IReadOnlyList<SpanListSegmentModel> segments;

        public SegmentRangeList(IReadOnlyList<SpanListSegmentModel> segments)
        {
            this.segments = segments;
        }

        public Range this[int index]
        {
            get
            {
                var segment = segments[index];
                return new Range(segment.Starts.MinValue, segment.FullLength);
            }
        }

        public int Count
        {
            get
            {
                return segments.Count;
            }
        }
    }

    public class SpanListSegmentModel
    {
        public bool Optimized { get; set; }

        public int FullLength { get; set; }

        [JsonIgnore]
        public bool ExpandedLengths { get; set; }

        [JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore)]
        public bool StartsExpanded { get; set; }

        public IntegerListModel Starts { get; set; }

        public IntegerListModel Lengths { get; set; }

        public IntegerListModel SharedIndices { get; set; }

        internal virtual void OptimizeLists(OptimizationContext context)
        {
        }

        internal virtual void ExpandLists(OptimizationContext context)
        {
        }

        internal void Optimize(OptimizationContext context)
        {
            if (Optimized)
            {
                return;
            }

            if (FullLength == 0)
            {
                var end = Starts[Starts.Count - 1] + Lengths[Lengths.Count - 1];
                FullLength = end - Starts.MinValue + 1;
            }

            OptimizeStarts(context);
            Starts.Optimize(context);
            Lengths.Optimize(context);
            SharedIndices.Optimize(context);

            OptimizeLists(context);

            Optimized = true;
        }

        internal void Expand(OptimizationContext context)
        {
            if (!Optimized)
            {
                return;
            }

            Starts.ExpandData(context);
            Lengths.ExpandData(context);
            SharedIndices.ExpandData(context);

            if (!StartsExpanded)
            {
                ExpandStarts();
            }

            ExpandLists(context);

            if (FullLength == 0)
            {
                var end = Starts[Starts.Count - 1] + Lengths[Lengths.Count - 1];
                FullLength = end - Starts.MinValue + 1;
            }

            Optimized = false;
        }

        private void ExpandStarts()
        {
            Contract.Assert(Lengths.CompressedData == null, "Lengths must be decompressed prior to optimizing starts");
            var count = Starts.Count;
            int priorStart = Starts.MinValue;
            int priorLength = 0;
            int max = 0;

            for (int i = 0; i < count; i++)
            {
                int startOffset = Starts.GetIndexDirect(i);
                int newValue;
                if (startOffset == 0)
                {
                    // 0 is reserved to indicate this span starts at the same position as the prior start
                    newValue = priorStart;
                }
                else
                {
                    // This is a delta from the end of the previous span + 1 (0 is reserved)
                    var priorEnd = priorStart + priorLength;
                    newValue = priorEnd + startOffset - 1;
                }

                Starts[i] = newValue;
                priorStart = newValue;
                priorLength = Lengths[i];
                max = Math.Max(newValue, max);
            }

            var newStartsMinByteWidth = NumberUtils.GetByteWidth(max - Starts.MinValue);
            Contract.Assert(newStartsMinByteWidth == Starts.ValueByteWidth);
        }

        private void OptimizeStarts(OptimizationContext context)
        {
            Contract.Assert(Lengths.CompressedData == null, "Lengths must be decompressed prior to optimizing starts");

            //var starts = Starts.GetReadOnlyList().ToList();

            context.Stream.Position = 0;
            var startsDataLength = Starts.Data.Length;
            context.Stream.Write(Starts.Data, 0, startsDataLength);
            var count = Starts.Count;
            int priorStart = Starts.MinValue;
            int priorLength = 0;
            int max = 0;
            for (int i = 0; i < count; i++)
            {
                int start = Starts[i];
                int newValue;
                if (start == priorStart)
                {
                    newValue = 0;
                }
                else
                {
                    // If not equal to prior start
                    // We store the delta from the end of the prior span + 1 (0 is reserved for starting at the same position as the prior segment)
                    // NOTE: This must be non-negative.
                    var priorEnd = priorStart + priorLength;
                    var priorEndOffset = start - priorEnd;

                    if (priorEndOffset < 0)
                    {
                        throw new InvalidOperationException(
                            $"priorEndOffset: {priorEndOffset} priorStart: {priorStart} priorLength: {priorLength} start: {start}");
                    }

                    newValue = priorEndOffset + 1;
                }

                Starts.SetIndexDirect(i, newValue);
                priorStart = start;
                priorLength = Lengths[i];
                max = Math.Max(newValue, max);
            }

            var newStartsMinByteWidth = NumberUtils.GetByteWidth(max);
            if (newStartsMinByteWidth > Starts.ValueByteWidth)
            {
                context.Stream.Position = 0;
                context.Stream.Read(Starts.Data, 0, startsDataLength);
                StartsExpanded = true;
            }
        }
    }

    internal class OptimizationContext
    {
        public MemoryStream Stream = new MemoryStream();

        internal byte[] Compress(byte[] data)
        {
            Stream.Position = 0;
            using (var deflateStream = new DeflateStream(Stream, CompressionLevel.Optimal, leaveOpen: true))
            {
                deflateStream.Write(data, 0, data.Length);
            }

            Stream.SetLength(Stream.Position);

            return Stream.ToArray();
        }
    }
}
