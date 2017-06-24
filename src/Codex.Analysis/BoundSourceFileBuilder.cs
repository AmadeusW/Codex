using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using Codex.ObjectModel;
using Codex.Utilities;
using Microsoft.CodeAnalysis.Text;

namespace Codex.Analysis
{
    public class BoundSourceFileBuilder
    {
        public readonly BoundSourceFile BoundSourceFile = new BoundSourceFile();
        private readonly List<ReferenceSpan> references;
        private readonly List<ClassificationSpan> classifications;
        private StringBuilder stringBuilder;
        public StringBuilder StringBuilder
        {
            get
            {
                if (stringBuilder == null)
                {
                    stringBuilder = new StringBuilder(SourceFile.Content);
                }

                return stringBuilder;
            }
        }

        public readonly SourceFile SourceFile = new SourceFile();
        public readonly string ProjectId;

        private SourceText sourceText;
        public SourceText SourceText
        {
            get
            {
                if (stringBuilder != null)
                {
                    throw new InvalidOperationException();
                }

                if (sourceText == null)
                {
                    sourceText = SourceText.From(SourceFile.Content);
                }

                return sourceText;
            }

            set
            {
                if (stringBuilder != null)
                {
                    throw new InvalidOperationException();
                }

                if (SourceFile.Content != null)
                {
                    throw new InvalidOperationException("SourceText can only be set if Content is not set in SourceFile");
                }

                sourceText = value;
                SourceFile.Content = sourceText.ToString();
            }
        }

        public HashSet<ReferenceSpan> ReferencesSet = new HashSet<ReferenceSpan>(new EqualityComparerBuilder<ReferenceSpan>()
            .CompareByAfter(s => s.LineSpanText)
            .CompareByAfter(s => s.Start));

        public BoundSourceFileBuilder(SourceFileInfo sourceFileInfo, string projectId)
            : this(new SourceFile() { Info = sourceFileInfo, Content = string.Empty }, projectId)
        {
        }

        public BoundSourceFileBuilder(SourceFile sourceFile, string projectId)
        {
            references = BoundSourceFile.References as List<ReferenceSpan>;
            classifications = BoundSourceFile.ClassificationSpans as List<ClassificationSpan>;
            if (references == null)
            {
                references = new List<ReferenceSpan>();
                BoundSourceFile.References = references;
            }

            if (classifications == null)
            {
                classifications = new List<ClassificationSpan>();
                BoundSourceFile.ClassificationSpans = classifications;
            }

            BoundSourceFile.SourceFile = sourceFile;
            BoundSourceFile.ProjectId = projectId;
            this.SourceFile = sourceFile;
            this.ProjectId = projectId;

            AnnotateDefinition(0, 0, CreateFileDefinitionSymbol(sourceFile.Info.Path, projectId));
        }

        public static DefinitionSymbol CreateFileDefinitionSymbol(string logicalPath, string projectId)
        {
            return new DefinitionSymbol()
            {
                Id = SymbolId.CreateFromId(logicalPath.ToLowerInvariant()),
                ShortName = PathUtilities.GetFileName(logicalPath),
                ContainerQualifiedName = PathUtilities.GetDirectoryName(logicalPath),
                ProjectId = projectId,
                ReferenceKind = nameof(ReferenceKind.Definition),
                Kind = nameof(SymbolKinds.File),
            };
        }

        public static ReferenceSymbol CreateFileReferenceSymbol(string logicalPath, string projectId, bool isDefinition = false)
        {
            return new ReferenceSymbol()
            {
                Id = SymbolId.CreateFromId(logicalPath.ToLowerInvariant()),
                ProjectId = projectId,
                ReferenceKind = isDefinition ? nameof(ReferenceKind.Definition) : nameof(ReferenceKind.Reference),
                Kind = nameof(SymbolKinds.File),
            };
        }

        public void AnnotateClassification(int start, int length, string classification)
        {
            classifications.Add(new ClassificationSpan()
            {
                Start = start,
                Length = length,
                Classification = classification
            });
        }

        public void AnnotateReferences(int start, int length, params ReferenceSymbol[] refs)
        {
            foreach (var reference in refs)
            {
                // NOTE: not all data is provided here! There is a post processing step to populate it.
                references.Add(new ReferenceSpan()
                {
                    Start = start,
                    Length = length,
                    Reference = reference
                });
            }
        }

        public void AddReferences(IEnumerable<ReferenceSpan> spans)
        {
            references.AddRange(spans);
        }

        public void AddClassifications(IEnumerable<ClassificationSpan> spans)
        {
            classifications.AddRange(spans);
        }

        public void AddDefinition(DefinitionSpan span)
        {
            BoundSourceFile.Definitions.Add(span);
        }

        public void AddDefinitions(IEnumerable<DefinitionSpan> spans)
        {
            BoundSourceFile.Definitions.AddRange(spans);
        }

        public void AnnotateDefinition(int start, int length, DefinitionSymbol definition)
        {
            BoundSourceFile.Definitions.Add(new DefinitionSpan()
            {
                Definition = definition,
                Start = start,
                Length = length
            });

            references.Add(new ReferenceSpan()
            {
                Start = start,
                Length = length,
                Reference = definition
            });
        }

        public void AppendReferences(string text, params ReferenceSymbol[] referenceSymbols)
        {
            SymbolSpan symbolSpan = new SymbolSpan()
            {
                Start = StringBuilder.Length,
                Length = text.Length,
            };

            StringBuilder.Append(text);
            foreach (var reference in referenceSymbols)
            {
                references.Add(symbolSpan.CreateReference(reference));
            }
        }

        public void AppendDefinition(string text, DefinitionSymbol definition)
        {
            SymbolSpan symbolSpan = new SymbolSpan()
            {
                Start = StringBuilder.Length,
                Length = text.Length,
            };

            StringBuilder.Append(text);
            BoundSourceFile.Definitions.Add(symbolSpan.CreateDefinition(definition));
            references.Add(symbolSpan.CreateReference(definition));
        }

        public BoundSourceFile Build()
        {
            if (stringBuilder != null)
            {
                SourceFile.Content = stringBuilder.ToString();
                stringBuilder = null;
            }

            SourceText text = SourceText;
            var info = BoundSourceFile.SourceFile.Info;

            var checksumKey = GetChecksumKey(text.ChecksumAlgorithm);
            if (checksumKey != null)
            {
                var checksum = text.GetChecksum().ToHex();
                info.Properties[checksumKey] = checksum;

                AnnotateDefinition(0, 0,
                    new DefinitionSymbol()
                    {
                        Id = SymbolId.CreateFromId($"{checksumKey}|{checksum}"),
                        ShortName = checksum,
                        ContainerQualifiedName = checksumKey,
                        ProjectId = BoundSourceFile.ProjectId,
                        ReferenceKind = nameof(ReferenceKind.Definition),
                        Kind = checksumKey,
                    });
            }

            classifications.Sort((cs1, cs2) => cs1.Start.CompareTo(cs2.Start));
            references.Sort((cs1, cs2) => cs1.Start.CompareTo(cs2.Start));
            BoundSourceFile.Definitions.Sort((cs1, cs2) => cs1.Start.CompareTo(cs2.Start));

            ReferenceSpan lastReference = null;

            foreach (var reference in references)
            {
                if (lastReference?.Start == reference.Start)
                {
                    reference.LineNumber = lastReference.LineNumber;
                    reference.LineSpanStart = lastReference.LineSpanStart;
                    reference.LineSpanText = lastReference.LineSpanText;
                    continue;
                }

                var line = text.Lines.GetLineFromPosition(reference.Start);
                if (lastReference?.LineNumber == line.LineNumber)
                {
                    reference.LineNumber = line.LineNumber;
                    reference.LineSpanStart = lastReference.LineSpanStart + (reference.Start - lastReference.Start);
                    reference.LineSpanText = lastReference.LineSpanText;
                    continue;
                }

                reference.LineNumber = line.LineNumber;
                reference.LineSpanStart = reference.Start - line.Start;
                reference.LineSpanText = line.ToString();
                reference.Trim();
            }

            foreach (var definitionSpan in BoundSourceFile.Definitions)
            {
                definitionSpan.Definition.ProjectId = definitionSpan.Definition.ProjectId ?? ProjectId;
                var line = text.Lines.GetLineFromPosition(definitionSpan.Start);
                definitionSpan.LineNumber = line.LineNumber;
                definitionSpan.LineSpanStart = definitionSpan.Start - line.Start;
            }

            return BoundSourceFile;
        }

        private static string GetChecksumKey(SourceHashAlgorithm algorithm)
        {
            switch (algorithm)
            {
                case SourceHashAlgorithm.Sha1:
                    return IndexingUtilities.GetChecksumKey(ChecksumAlgorithm.Sha1);
                case SourceHashAlgorithm.Sha256:
                    return IndexingUtilities.GetChecksumKey(ChecksumAlgorithm.Sha256);
                default:
                    return null;
            }
        }
    }
}