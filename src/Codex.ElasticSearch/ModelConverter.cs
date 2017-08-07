using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Linq;
using Codex.ObjectModel;
using Codex.Storage.DataModel;
using Codex.Utilities;

namespace Codex.Storage
{
    public static class ModelConverter
    {
        public static ProjectModel FromObjectModel(AnalyzedProject analyzedProject)
        {
            Contract.Requires(analyzedProject != null);
            Contract.Ensures(Contract.Result<ProjectModel>() != null);

            return
                new ProjectModel(analyzedProject.Id, analyzedProject.RepositoryName)
                {
                    ProjectKind = analyzedProject.ProjectKind
                }
                .AddReferencedProjects(FromObjectModel(analyzedProject.ReferencedProjects).ToArray());
        }

        public static IEnumerable<ReferencedProjectModel> FromObjectModel(List<ReferencedProject> items)
        {
            Contract.Requires(items != null);
            Contract.Ensures(Contract.Result<IEnumerable<ReferencedProjectModel>>() != null);

            var query = from rp in items
                        let rpm = new ReferencedProjectModel
                        {
                            ProjectId = rp.ProjectId,
                            Definitions = FromObjectModel(rp.Definitions),
                        }
                        select rpm;

            return query.ToList();
        }

        public static List<ReferencedProject> ToObjectModel(List<ReferencedProjectModel> items)
        {
            Contract.Requires(items != null);
            Contract.Ensures(Contract.Result<IEnumerable<ReferencedProject>>() != null);

            var query = from rpm in items
                        let rp = new ReferencedProject
                        {
                            ProjectId = rpm.ProjectId,
                            DisplayName = rpm.ProjectId,
                            Definitions = ToObjectModel(rpm.Definitions),
                        }
                        select rp;

            return query.ToList();
        }

        public static IEnumerable<SourceFileModel> FromObjectModel(List<BoundSourceFile> sourceFiles)
        {
            Contract.Requires(sourceFiles != null);
            Contract.Ensures(Contract.Result<IEnumerable<SourceFileModel>>() != null);

            List<SourceFileModel> sourceFileModels = new List<SourceFileModel>(sourceFiles.Count);
            foreach (var bsf in sourceFiles)
            {
                sourceFileModels.Add(FromObjectModel(bsf));
            }

            return sourceFileModels;
        }

        public static SourceFileModel FromObjectModel(BoundSourceFile boundSourceFile)
        {
            return new SourceFileModel
            {
                Uid = boundSourceFile.Uid,
                Content = boundSourceFile.SourceFile.GetContentsAsync().Result,
                Language = boundSourceFile.SourceFile.Info.Language,
                Path = boundSourceFile.SourceFile.Info.Path,
                ExcludeFromSearch = boundSourceFile.ExcludeFromSearch,
                RepoRelativePath = boundSourceFile.SourceFile.Info.RepoRelativePath,
                WebAddress = boundSourceFile.SourceFile.Info.WebAddress,
                ProjectId = boundSourceFile.ProjectId,
                Classifications = FromObjectModel(boundSourceFile.ClassificationSpans),
                Definitions = FromObjectModel(boundSourceFile.Definitions),
                References = new ReferenceListModel(boundSourceFile.References),
                SearchReferencesSource = FromObjectModel(boundSourceFile.References),
                Properties = boundSourceFile.SourceFile.Info.Properties
            };
        }

        public static ProjectContents ToProjectContents(ProjectModel project, List<DefinitionSearchSpanModel> sources)
        {
            Contract.Requires(project != null);
            Contract.Requires(sources != null);
            Contract.Ensures(Contract.Result<ProjectContents>() != null);

            return new ProjectContents
            {
                Id = project.Id,
                References = ToObjectModel(project.ReferencedProjects),
                Files = ToSourceFileInfo(sources),
                DateUploaded = project.DateUploaded
            };
        }

        private static List<SourceFileInfo> ToSourceFileInfo(List<DefinitionSearchSpanModel> sources)
        {
            Contract.Requires(sources != null);

            return sources.Select(x => new SourceFileInfo() { Language = x.Language, Path = x.FilePath }).ToList();
        }

        private static List<DefinitionSpanModel> FromObjectModel(List<DefinitionSpan> spans)
        {
            if (spans == null)
            {
                return new List<DefinitionSpanModel>();
            }

            return spans.Select(s =>
                new DefinitionSpanModel
                {
                    Definition = FromObjectModel(s.Definition),
                    Start = s.Start,
                    Length = s.Length,
                    LineNumber = s.LineNumber,
                    LineSpanText = s.LineSpanText,
                    LineSpanStart = s.LineSpanStart,
                }).ToList();
        }

        private static List<DefinitionSymbolModel> FromObjectModel(List<DefinitionSymbol> symbols)
        {
            if (symbols == null)
            {
                return new List<DefinitionSymbolModel>();
            }

            return symbols.Select(s => FromObjectModel(s)).ToList();
        }

        private static List<DefinitionSymbol> ToObjectModel(List<DefinitionSymbolModel> symbols)
        {
            if (symbols == null)
            {
                return new List<DefinitionSymbol>();
            }

            return symbols.Select(s => ToObjectModel(s)).ToList();
        }

        private static ClassificationListModel FromObjectModel(IReadOnlyList<ClassificationSpan> spans)
        {
            if (spans == null)
            {
                return null;
            }

            return new ClassificationListModel(spans);
        }

        private static List<ReferenceSpanModel> FromObjectModel(IReadOnlyList<ReferenceSpan> spans)
        {
            if (spans == null)
            {
                return new List<ReferenceSpanModel>();
            }

            return spans.Select(s =>
                new ReferenceSpanModel
                {
                    Reference = FromObjectModel(s.Reference),
                    RelatedDefinition = s.RelatedDefinition.Value,
                    Start = s.Start,
                    Length = s.Length,
                    LineNumber = s.LineNumber,
                    LineSpanText = s.LineSpanText,
                    LineSpanStart = s.LineSpanStart
                }).ToList();
        }

        public static ReferenceSymbolModel FromObjectModel(ReferenceSymbol symbol)
        {
            var result = new ReferenceSymbolModel
            {
                ProjectId = symbol.ProjectId,
                Id = symbol.Id.Value,
                Kind = symbol.Kind,
                ReferenceKind = symbol.ReferenceKind,
                ExcludeFromDefaultSearch = symbol.ExcludeFromDefaultSearch,
                ExcludeFromSearch = symbol.ExcludeFromSearch,
                IsImplicitlyDeclared = symbol.IsImplicitlyDeclared
            };

            return result;
        }

        //public static AnalyzedProject ToOAnalyzedProject(ProjectModel project, List<SourceFileModel> sources)
        //{
        //    Contract.Requires(project != null);
        //    Contract.Requires(sources != null);
        //    Contract.Ensures(Contract.Result<AnalyzedProject>() != null);

        //    return new AnalyzedProject(project.RepositoryName, project.Id)
        //    {
        //        ReferencedProjects = project.ReferencedProjects,
        //        SourceFiles = ToBoundSourceFiles(sources),
        //    };
        //}

        private static List<BoundSourceFile> ToBoundSourceFiles(List<SourceFileModel> sources)
        {
            Contract.Requires(sources != null);

            return sources.Select(ToBoundSourceFile).ToList();
        }

        private static ReferenceSpan ToObjectModel(ReferenceSpanModel model)
        {
            Contract.Requires(model != null);

            return new ReferenceSpan
            {
                Reference = ToObjectModel(model.Reference),
                RelatedDefinition = SymbolId.UnsafeCreateWithValue(model.RelatedDefinition),
                Start = model.Start,
                Length = model.Length,
                LineNumber = model.LineNumber,
                LineSpanText = model.LineSpanText,
                LineSpanStart = model.LineSpanStart
            };
        }

        private static DefinitionSpan ToObjectModel(DefinitionSpanModel model)
        {
            Contract.Requires(model != null);

            return new DefinitionSpan
            {
                Definition = ToObjectModel(model.Definition),
                Start = model.Start,
                Length = model.Length,
                LineNumber = model.LineNumber,
                LineSpanText = model.LineSpanText,
                LineSpanStart = model.LineSpanStart
            };
        }

        private static ClassificationSpan ToObjectModel(ClassificationSpanModel model)
        {
            Contract.Requires(model != null);

            return new ClassificationSpan
            {
                Classification = model.Classification,
                Start = model.Start,
                Length = model.Length,
            };
        }

        private const string ShortNameToken = "{sn}";
        private const string ContainerQualifiedNameToken = "{cqn}";

        private static DefinitionSymbolModel FromObjectModel(DefinitionSymbol symbol)
        {
            if (symbol == null)
                return null;

            var tokenizedDisplayName = symbol.DisplayName;
            if (tokenizedDisplayName != null)
            {
                if (!string.IsNullOrEmpty(symbol.ContainerQualifiedName))
                {
                    tokenizedDisplayName = tokenizedDisplayName.Replace(symbol.ContainerQualifiedName, ContainerQualifiedNameToken);
                }

                tokenizedDisplayName = tokenizedDisplayName.Replace(symbol.ShortName, ShortNameToken);
            }

            var result = new DefinitionSymbolModel
            {
                ProjectId = symbol.ProjectId,
                Id = symbol.Id.Value,
                Uid = symbol.Uid,
                DisplayName = symbol.DisplayName,
                ContainerQualifiedName = symbol.ContainerQualifiedName,
                ShortName = symbol.ShortName,
                Kind = symbol.Kind,
                ExcludeFromSearch = symbol.ExcludeFromSearch,
                ExcludeFromDefaultSearch = symbol.ExcludeFromDefaultSearch,
                Glyph = symbol.Glyph == Glyph.Unknown ? null : symbol.Glyph.ToString(),
                SymbolDepth = symbol.SymbolDepth,
                IsImplicitlyDeclared = symbol.IsImplicitlyDeclared,
                ShortDisplayName =  symbol.DeclarationName,
                Comment =  symbol.Comment,
                TypeName = symbol.TypeName
            };

            return result;
        }

        private static DefinitionSymbol ToObjectModel(DefinitionSymbolModel symbolModel)
        {
            if (symbolModel == null)
                return null;

            var displayName = symbolModel.DisplayName;
            if (displayName != null)
            {
                if (!string.IsNullOrEmpty(symbolModel.ContainerQualifiedName))
                {
                    displayName = displayName.Replace(ContainerQualifiedNameToken, symbolModel.ContainerQualifiedName);
                }

                if (!string.IsNullOrEmpty(symbolModel.ShortName))
                {
                    displayName = displayName.Replace(ShortNameToken, symbolModel.ShortName);
                }
            }

            return new DefinitionSymbol
            {
                ProjectId = symbolModel.ProjectId,
                Id = SymbolId.UnsafeCreateWithValue(symbolModel.Id),
                Uid = symbolModel.Uid,
                DisplayName = displayName,
                ContainerQualifiedName = symbolModel.ContainerQualifiedName,
                ShortName = symbolModel.ShortName,
                ExcludeFromDefaultSearch = symbolModel.ExcludeFromDefaultSearch,
                ExcludeFromSearch = symbolModel.ExcludeFromSearch,
                Kind = symbolModel.Kind,
                Glyph = ParseEnumOrDefault(symbolModel.Glyph, Glyph.Unknown),
                SymbolDepth = symbolModel.SymbolDepth,
                IsImplicitlyDeclared = symbolModel.IsImplicitlyDeclared,
                DeclarationName = symbolModel.ShortDisplayName,
                Comment = symbolModel.Comment,
                TypeName = symbolModel.TypeName
            };
        }

        public static ReferenceSymbol ToObjectModel(ReferenceSymbolModel symbolModel)
        {
            if (symbolModel == null)
                return null;

            return new ReferenceSymbol
            {
                ProjectId = symbolModel.ProjectId,
                Id = SymbolId.UnsafeCreateWithValue(symbolModel.Id),
                Kind = symbolModel.Kind,
                ExcludeFromDefaultSearch = symbolModel.ExcludeFromDefaultSearch,
                ExcludeFromSearch = symbolModel.ExcludeFromSearch,
                ReferenceKind = symbolModel.ReferenceKind ?? nameof(ReferenceKind.Reference),
                IsImplicitlyDeclared = symbolModel.IsImplicitlyDeclared
            };
        }

        private static List<SourceFileInfo> ToObjectModel(List<SourceFileModel> sources)
        {
            Contract.Requires(sources != null);

            return sources.Select(x => new SourceFileInfo() { Language = x.Language, Path = x.Path }).ToList();
        }

        public static BoundSourceFile ToBoundSourceFile(SourceFileModel sourceFile)
        {
            Contract.Requires(sourceFile != null);
            Contract.Ensures(Contract.Result<BoundSourceFile>() != null);

            return new BoundSourceFile
            {
                SourceFile = new SourceFile
                {
                    Content = sourceFile.Content,
                    Info = new SourceFileInfo
                    {
                        Language = sourceFile.Language,
                        Path = sourceFile.Path,
                        WebAddress = sourceFile.WebAddress,
                        RepoRelativePath = sourceFile.RepoRelativePath
                    }
                },
                Uid = sourceFile.Uid,
                ProjectId = sourceFile.ProjectId,
                ExcludeFromSearch = sourceFile.ExcludeFromSearch,
                // TODO: Possible NRE, Spans could be null
                ClassificationSpans = IndexableListAdapter.GetSpanList(sourceFile.Classifications),
                Definitions = sourceFile.Definitions?.Select(s => ToObjectModel(s)).ToList() ?? new List<DefinitionSpan>(0),
                References = IndexableListAdapter.GetSpanList(sourceFile.References) ?? IndexableSpans.Empty<ReferenceSpan>()
            };
        }

        public static List<SymbolSpan> ToObjectModel(this IEnumerable<SymbolSpanModel> symbolSpanModels)
        {
            List<SymbolSpan> spans = new List<SymbolSpan>(symbolSpanModels.Select(s => ToObjectModel(s)));
            return spans;
        }

        private static SymbolSpan ToObjectModel(SymbolSpanModel model)
        {
            Contract.Requires(model != null);

            return new SymbolSpan
            {
                Start = model.Start,
                Length = model.Length,
                LineNumber = model.LineNumber,
                LineSpanText = model.LineSpanText,
                LineSpanStart = model.LineSpanStart
            };
        }

        private static DefinitionSpan SetUid(this DefinitionSpan span, string uid)
        {
            span.Definition.Uid = uid;
            return span;
        }

        public static IList<GetDefinitionResult> ToDefinitionResult(List<DefinitionSearchSpanModel> definitions)
        {
            Contract.Requires(definitions != null);

            return definitions.Select(
                d => new GetDefinitionResult
                {
                    Span = ToObjectModel(d.Span).SetUid(d.Uid),
                    File = new SourceFileInfo
                    {
                        Language = d.Language,
                        Path = d.FilePath,
                    }
                }).ToList();
        }

        public static List<SymbolReferenceEntry> ToSymbolReferences(List<SymbolReferenceModel> symbols)
        {
            Contract.Requires(symbols != null);
            return symbols.Select(s => new SymbolReferenceEntry
            {
                ReferringFilePath = s.ReferringFilePath,
                ReferringProjectId = s.ReferringProjectId,
                ReferringSpan = ToObjectModel(s.ReferringSpan),
            }).ToList();
        }

        public static List<SymbolSearchResultEntry> ToSymbolSearchResults(List<DefinitionSearchSpanModel> results)
        {
            Contract.Requires(results != null);

            return results.Select(s => new SymbolSearchResultEntry
            {
                File = s.FilePath,
                ReferenceKind = s.ReferenceKind,
                Span = ToObjectModel(s.Span).SetUid(s.Uid)
            }.Process()).ToList();
        }

        private static readonly Dictionary<string, int> glyphs = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
        {
            { "Class", 0 },
            { "Constant", 6 },
            { "Delegate", 12 },
            { "Enum", 18 },
            { "EnumMember", 24 },
            { "Event", 30 },
            { "Exception", 36 },
            { "Field", 42 },
            { "Interface", 48 },
            { "Macro", 54 },
            { "Map", 60 },
            { "MapItem", 66 },
            { "Method", 72 },
            { "Overload", 78 },
            { "Module", 84 },
            { "Namespace", 90 },
            { "Operator", 96 },
            { "Property", 102 },
            { "Struct", 108 },
            { "TypeParameter", 114 },
            { "ExtensionMethod", 220 },
        };

        public static SymbolSearchResultEntry Process(this SymbolSearchResultEntry s)
        {
            s.Glyph = GetGlyph(s.Span, s.File);
            return s;
        }

        public static string GetGlyph(this DefinitionSpan s, string filePath = null)
        {
            return GetGlyphCore(s, filePath) + ".png";
        }

        private static string GetGlyphCore(DefinitionSpan s, string filePath)
        {
            var glyph = s.Definition.Glyph;
            if (glyph != Glyph.Unknown)
            {
                return glyph.GetGlyphNumber().ToString();
            }

            int result = 0;
            if (glyphs.TryGetValue(s.Definition.Kind, out result))
            {
                return result.ToString();
            }

            if (!string.IsNullOrEmpty(filePath))
            {
                if (string.Equals(s.Definition.Kind, nameof(SymbolKinds.File), StringComparison.OrdinalIgnoreCase))
                {
                    return GetFileNameGlyph(filePath);
                }
            }

            return "0";
        }

        public static T ParseEnumOrDefault<T>(string value, T defaultValue = default(T)) where T : struct
        {
            if (string.IsNullOrEmpty(value))
            {
                return defaultValue;
            }

            T result;
            if (!Enum.TryParse<T>(value, ignoreCase: true, result: out result))
            {
                result = defaultValue;
            }

            return result;
        }

        public static string GetFileNameGlyph(string fileName)
        {
            if (fileName.EndsWith(".cs", StringComparison.OrdinalIgnoreCase))
            {
                return "csharp";
            }
            else if (fileName.EndsWith(".vb", StringComparison.OrdinalIgnoreCase))
            {
                return "vb";
            }
            else if (fileName.EndsWith(".ts", StringComparison.OrdinalIgnoreCase))
            {
                return "TypeScript";
            }
            else if (fileName.EndsWith(".xaml", StringComparison.OrdinalIgnoreCase))
            {
                return "xaml";
            }

            return "212";
        }

        public static IEnumerable<SymbolReferenceModel> ToSymbolReferences(ReferenceSearchResultModel searchResult)
        {
            var reference = ToObjectModel(searchResult.Reference);

            if (searchResult.SymbolLineSpanList != null)
            {
                foreach (var span in searchResult.SymbolLineSpanList.GetReadOnlyList())
                {
                    yield return new SymbolReferenceModel()
                    {
                        ReferringSpan = new ReferenceSpanModel()
                        {
                            Reference = searchResult.Reference,
                            Start = span.Start,
                            LineSpanStart = span.LineSpanStart,
                            LineSpanText = span.LineSpanText,
                            Length = span.Length,
                            LineNumber = span.LineNumber
                        },

                        ReferringFilePath = searchResult.FilePath,
                        ReferringProjectId = searchResult.ProjectId
                    };
                }
            }

            if (searchResult.SymbolLineSpans != null)
            {
                foreach (var span in searchResult.SymbolLineSpans)
                {
                    yield return new SymbolReferenceModel()
                    {
                        ReferringSpan = new ReferenceSpanModel()
                        {
                            Reference = searchResult.Reference,
                            Start = span.Start,
                            LineSpanStart = span.LineSpanStart,
                            LineSpanText = span.LineSpanText,
                            Length = span.Length,
                            LineNumber = span.LineNumber
                        },

                        ReferringFilePath = searchResult.FilePath,
                        ReferringProjectId = searchResult.ProjectId
                    };
                }
            }
        }
    }
}