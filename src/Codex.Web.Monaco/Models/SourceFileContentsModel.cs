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

    public class SourceFileContentsModel
    {
        public string contents { get; set; }

        public Span position { get; set; }

        public LineModel[] lines { get; set; }

        public string view { get; set; }

        public async Task Populate(BoundSourceFile boundSourceFile)
        {
            contents = await boundSourceFile.SourceFile.GetContentsAsync();

            lines = new LineModel[boundSourceFile.Lines];

            foreach (var reference in boundSourceFile.References)
            {
                if (reference.Reference.IsImplicitlyDeclared)
                {
                    continue;
                }

                var lineModel = lines[reference.LineNumber];
                if (lineModel == null)
                {
                    lineModel = new LineModel();
                    lines[reference.LineNumber] = lineModel;
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
                    lineModel.definitions.Add(symbolSpan);
                }
                else
                {
                    lineModel.references.Add(symbolSpan);
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

    public class LineModel
    {
        public List<SymbolSpan> definitions { get; set; } = new List<SymbolSpan>(0);
        public List<SymbolSpan> references { get; set; } = new List<SymbolSpan>(0);
    }
}