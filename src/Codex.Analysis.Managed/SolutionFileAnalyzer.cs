using System;
using System.IO;
using Codex.Import;
using Codex.MSBuild;
using Codex.ObjectModel;
using Codex.Utilities;
using Microsoft.CodeAnalysis.Classification;

namespace Codex.Analysis.Files
{
    public class SolutionFileAnalyzer : RepoFileAnalyzer
    {
        private static readonly string[] supportedExtensions = new[] { ".sln" };

        public override string[] SupportedExtensions => supportedExtensions;

        public SolutionFileAnalyzer()
        {
        }

        protected override void AnnotateFile(BoundSourceFileBuilder binder)
        {
        }

        protected override void AnnotateFile(AnalysisServices services, RepoFile file, BoundSourceFileBuilder binder)
        {
            string solutionDirectory = Path.GetDirectoryName(file.FilePath);

            try
            {
                var defaultRepoProject = file.PrimaryProject.Repo.DefaultRepoProject;
                var repoRoot = defaultRepoProject.ProjectDirectory;
                var solutionFile = SolutionFile.Parse(new SourceTextReader(binder.SourceText));
                foreach (var projectBlock in solutionFile.ProjectBlocks)
                {
                    var projectPath = projectBlock.ParsedProjectPath.Value;
                    if (string.IsNullOrEmpty(projectPath))
                    {
                        continue;
                    }

                    var fullProjectPath = Path.GetFullPath(Path.Combine(solutionDirectory, projectPath));

                    var projectFileReference = BoundSourceFileBuilder.CreateFileReferenceSymbol(
                            defaultRepoProject.GetLogicalPath(fullProjectPath),
                            defaultRepoProject.ProjectId);

                    var span = projectBlock.ParsedProjectName.Span;
                    AnnotateReference(binder, projectFileReference, span);

                    span = projectBlock.ParsedProjectPath.Span;
                    AnnotateReference(binder, projectFileReference, span);
                }
            }
            catch (Exception ex)
            {
                services.Logger.LogExceptionError("AnnotateSolution", ex);
            }
        }

        private static void AnnotateReference(BoundSourceFileBuilder binder, ReferenceSymbol projectFileReference, Microsoft.CodeAnalysis.Text.TextSpan span)
        {
            binder.AnnotateClassification(span.Start, span.Length, ClassificationTypeNames.XmlLiteralAttributeValue);
            binder.AnnotateReferences(span.Start, span.Length, projectFileReference);
        }

        public override SourceFileInfo AugmentSourceFileInfo(SourceFileInfo info)
        {
            info.Language = "sln";
            return info;
        }
    }
}
