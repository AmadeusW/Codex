using System;
using System.Collections.Generic;
using System.Linq;
using Codex.ObjectModel;

namespace Codex.Analysis.External
{
    public class CodexTypeAdapter
    {
        public static void Annotate(BoundSourceFileBuilder builder, CodexFile file)
        {
            builder.AddClassifications(FilterSpans(file.Spans.Select(span => ToClassification(file.Store, span)).Where(c => c != null)));

            foreach (var reference in FilterSpans(file.Spans.Select(span => ToReference(file.Store, builder, span)).Where(c => c != null)))
            {
                builder.AnnotateReferences(reference.Start, reference.Length, reference.Reference);
            }

            foreach (var definition in FilterSpans(file.Spans.Select(span => ToDefinition(file.Store, builder, span)).Where(c => c != null)))
            {
                builder.AnnotateDefinition(definition.Start, definition.Length, definition.Definition);
            }
        }

        public static IEnumerable<T> FilterSpans<T>(IEnumerable<T> spans)
            where T : Span
        {
            int minStart = 0;
            foreach (var span in spans)
            {
                if (span.Start >= minStart)
                {
                    minStart = span.Start + span.Length;
                    yield return span;
                }
            }
        }

        public static ClassificationSpan ToClassification(CodexSemanticStore store, CodexSpan span)
        {
            if (!span.Classification.IsValid && !span.Symbol.IsValid)
            {
                return null;
            }

            return new ClassificationSpan()
            {
                Start = span.Start,
                Length = span.Length,
                LocalGroupId = span.LocalId.IsValid ? span.LocalId.Id + 1 : 0,
                Classification = span.Classification.IsValid ? store.Classifications[span.Classification].Name : "text"
            };
        }

        public static ReferenceSpan ToReference(CodexSemanticStore store, BoundSourceFileBuilder builder, CodexSpan span)
        {
            if (!span.ReferenceKind.IsValid || !span.Symbol.IsValid)
            {
                return null;
            }

            var symbol = store.Symbols[span.Symbol];
            SymbolId symbolId = GetSpanSymbolId(symbol);

            var projectId = symbol.Project.IsValid ? store.Projects.Get(symbol.Project).Name : builder.ProjectId;

            return new ReferenceSpan()
            {
                Start = span.Start,
                Length = span.Length,
                Reference = new ReferenceSymbol()
                {
                    Id = symbolId,
                    ReferenceKind = store.ReferenceKinds[span.ReferenceKind].Name,
                    Kind = "symbol",
                    ProjectId = projectId,
                },
            };
        }

        public static DefinitionSpan ToDefinition(CodexSemanticStore store, BoundSourceFileBuilder builder, CodexSpan span)
        {
            if ((span.ReferenceKind.IsValid && !string.Equals(nameof(ReferenceKind.Definition), store.ReferenceKinds[span.ReferenceKind].Name,
                StringComparison.OrdinalIgnoreCase)) || !span.Symbol.IsValid)
            {
                return null;
            }

            var symbol = store.Symbols[span.Symbol];
            SymbolId symbolId = GetSpanSymbolId(symbol);

            var projectId = symbol.Project.IsValid ? store.Projects.Get(symbol.Project).Name : builder.ProjectId;

            return new DefinitionSpan()
            {
                Start = span.Start,
                Length = span.Length,
                Definition = new DefinitionSymbol()
                {
                    Id = symbolId,
                    ShortName = symbol.ShortName,
                    DisplayName = symbol.DisplayName,
                    ContainerQualifiedName = symbol.ContainerQualifiedName,
                    SymbolDepth = symbol.SymbolDepth,
                    ReferenceKind = nameof(ReferenceKind.Definition),
                    Kind = symbol.Kind,
                    ProjectId = projectId,
                },
            };
        }

        private static SymbolId GetSpanSymbolId(CodexSymbol symbol)
        {
            SymbolId symbolId;
            if (symbol.ExtensionData == null)
            {
                symbolId = SymbolId.CreateFromId(symbol.UniqueId);
                symbol.ExtensionData = symbolId;
            }
            else
            {
                symbolId = (SymbolId)symbol.ExtensionData;
            }

            return symbolId;
        }
    }

}
