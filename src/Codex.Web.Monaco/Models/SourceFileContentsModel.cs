using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Web;
using Codex.ObjectModel;

namespace Codex.Web.Monaco.Models
{
    public class Span
    {
        public int position { get; set; }
        public int length { get; set; }
    }

    public class ResultModel
    {
        public string url { get; set; }
        public string symbolId { get; set; }
        public string projectId { get; set; }
    }

    public class SourceFileLocationModel
    {
        public string projectId { get; set; }
        public string filename { get; set; }
        public Span span { get; set; }
    }

    public class SegmentModel
    {
        public List<SymbolSpan> definitions { get; set; } = new List<SymbolSpan>(0);
        public List<SymbolSpan> references { get; set; } = new List<SymbolSpan>(0);
    }

    public class SourceFileContentsModel
    {
        public Span span;
        public string contents { get; set; }

        public string projectId { get; set; }
        public string filePath { get; set; }
        public string repoRelativePath { get; set; }
        public string webLink { get; set; }

        public List<SegmentModel> segments { get; set; } = new List<SegmentModel>();

        public int segmentLength { get; set; }

        public async Task Populate(BoundSourceFile boundSourceFile)
        {
            contents = await boundSourceFile.SourceFile.GetContentsAsync();
            repoRelativePath = boundSourceFile.SourceFile.Info.RepoRelativePath;
            webLink = boundSourceFile.SourceFile.Info.WebAddress;

            segmentLength = contents.Length;
            var segment = new SegmentModel();
            segments.Add(segment);

            foreach (var reference in boundSourceFile.References)
            {
                if (reference.Reference.IsImplicitlyDeclared)
                {
                    continue;
                }

                var symbolSpan = new SymbolSpan()
                {
                    symbol = reference.Reference.Id.Value,
                    projectId = reference.Reference.ProjectId,
                    span = new Span()
                    {
                        position = reference.Start,
                        length = reference.Length
                    }
                };

                if (reference.Reference.ReferenceKind == nameof(ReferenceKind.Definition))
                {
                    segment.definitions.Add(symbolSpan);
                }
                else
                {
                    segment.references.Add(symbolSpan);
                }
            }
        }
    }

    public class SymbolSpan
    {
        public string symbol { get; set; }
        public string projectId { get; set; }
        public Span span { get; set; }
    }
}