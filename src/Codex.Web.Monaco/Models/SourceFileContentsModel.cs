using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Web;
using Codex.ObjectModel;
using WebUI.Rendering;

namespace Codex.Web.Monaco.Models
{
    public class Span
    {
        public int position { get; set; }
        public int length { get; set; }
    }

    public class LineSpan : Span
    {
        public int line { get; set; }
        public int column { get; set; }
    }

    public class ClassificationSpan : Span
    {
        public string name { get; set; }
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
        public LineSpan span { get; set; }
    }

    public class SegmentModel
    {
        public List<SymbolSpan> definitions { get; set; } = new List<SymbolSpan>(0);
        public List<SymbolSpan> references { get; set; } = new List<SymbolSpan>(0);
    }

    public class SymbolInformation
    {
        public string name { get; set; }
        public string containerName { get; set; }
        public string symbolKind { get; set; }
        public Span span { get; set; }
    }

    public class SourceFileContentsModel
    {
        public Span span;
        public string contents { get; set; }

        public string projectId { get; set; }
        public string filePath { get; set; }
        public string repoRelativePath { get; set; }
        public string webLink { get; set; }

        public List<ClassificationSpan> classifications { get; set; } = new List<ClassificationSpan>();
        public List<SymbolInformation> documentSymbols { get; set; } = new List<SymbolInformation>();
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

            classifications.AddRange(boundSourceFile.ClassificationSpans.Select(classification =>
            {
                return new ClassificationSpan()
                {
                    name = SourceFileRenderer.MapClassificationToCssClass(classification.Classification),
                    position = classification.Start,
                    length = classification.Length
                };
            }).Where(span => !string.IsNullOrEmpty(span.name)));

            documentSymbols.AddRange(boundSourceFile.Definitions.Select(definition =>
            {
                return new SymbolInformation()
                {
                    name = definition.Definition.ShortName,
                    containerName = definition.Definition.ContainerQualifiedName,
                    symbolKind = definition.Definition.Kind,
                    span = new Span()
                    {
                        position = definition.Start,
                        length = definition.Length
                    }
                };
            }));

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