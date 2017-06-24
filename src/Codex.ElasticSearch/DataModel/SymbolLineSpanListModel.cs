using Codex.ObjectModel;
using Codex.Storage.Utilities;
using Nest;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static Codex.Storage.Utilities.NumberUtils;
using System.Collections;
using Codex.Utilities;
using System.Runtime.Serialization;
using Newtonsoft.Json;

namespace Codex.Storage.DataModel
{
    public class SymbolLineSpanListModel : SpanListModel<SymbolSpan, SpanListSegmentModel, SharedSymbolLineModel, int>
    {
        public static readonly IComparer<SharedSymbolLineModel> SharedSymbolLineModelComparer = new ComparerBuilder<SharedSymbolLineModel>()
            .CompareByAfter(s => s.LineSpanText);

        public SymbolLineSpanListModel()
        {
            Optimize = false;
        }

        public SymbolLineSpanListModel(IReadOnlyList<SymbolSpan> spans)
            : base(spans, sharedValueSorter: SharedSymbolLineModelComparer)
        {
            Optimize = false;
        }

        public override SpanListSegmentModel CreateSegment(ListSegment<SymbolSpan> segmentSpans)
        {
            return new SpanListSegmentModel();
        }

        public override SymbolSpan CreateSpan(int start, int length, SharedSymbolLineModel shared, SpanListSegmentModel segment, int segmentOffset)
        {
            return new SymbolSpan()
            {
                Start = shared.LineStart + start,
                LineSpanStart = start,
                Length = length,
                LineSpanText = shared.LineSpanText,
                LineNumber = shared.LineNumber
            };
        }

        public override SharedSymbolLineModel GetShared(SymbolSpan span)
        {
            return new SharedSymbolLineModel()
            {
                LineSpanText = span.LineSpanText,
                LineNumber = span.LineNumber,
                LineStart = span.Start - span.LineSpanStart
            };
        }

        public override int GetStart(SymbolSpan span, SharedSymbolLineModel shared)
        {
            return span.Start - shared.LineStart;
        }

        public override int GetSharedKey(SymbolSpan span)
        {
            return span.LineNumber;
        }

        [OnSerializing]
        public void PostProcessReferences(StreamingContext context)
        {
            string lineSpanText = null;
            foreach (var symbolLine in SharedValues)
            {
                symbolLine.LineSpanText = RemoveDuplicate(symbolLine.LineSpanText, ref lineSpanText);
            }
        }

        [OnDeserialized]
        public void MakeReferences(StreamingContext context)
        {
            string lineSpanText = null;
            foreach (var symbolLine in SharedValues)
            {
                symbolLine.LineSpanText = AssignDuplicate(symbolLine.LineSpanText, ref lineSpanText);
            }
        }
    }

    public class SharedSymbolLineModel
    {
        /// <summary>
        /// The absolute character position where the line starts in the text
        /// </summary>
        public int LineStart { get; set; }

        /// <summary>
        /// The character position where the span starts in the line text
        /// </summary>
        public int LineNumber { get; set; }

        /// <summary>
        /// The line text
        /// </summary>
        [String(Index = FieldIndexOption.No)]
        [JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore)]
        public string LineSpanText { get; set; }
    }
}
