using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Codex.Analysis.Projects;
using Codex.Import;
using Codex.ObjectModel;
using Codex.Utilities;
using Microsoft.CodeAnalysis;

namespace Codex.Analysis.Managed
{
    public class MetadataAsSourceProjectAnalyzer : RepoProjectAnalyzer
    {
        public IEnumerable<string> assemblies;

        public ConcurrentDictionary<string, string> assemblyFileByMetadataAsSourceProjectPath
            = new ConcurrentDictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        public const string MetadataAsSourceRoot = @"\\MetadataAsSource\";

        public MetadataAsSourceProjectAnalyzer(IEnumerable<string> assemblies)
        {
            this.assemblies = assemblies.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        }

        //public FileSystem WrapFileSystem(FileSystem fs)
        //{
        //    return fs;
        //}

        public override void CreateProjects(Repo repo)
        {
            repo.AddMount("MetadataAsSource", MetadataAsSourceRoot);

            foreach (var assembly in assemblies)
            {
                var projectId = Path.GetFileNameWithoutExtension(assembly);
                var metadataAsSourceProjectDirectory = (MetadataAsSourceRoot + projectId).EnsureTrailingSlash();
                if (assemblyFileByMetadataAsSourceProjectPath.TryAdd(metadataAsSourceProjectDirectory, assembly))
                {
                    var project = repo.CreateRepoProject(projectId, metadataAsSourceProjectDirectory);
                    project.ProjectKind = nameof(ProjectKind.MetadataAsSource);
                    project.Analyzer = this;
                }
            }
        }

        public override void Analyze(RepoProject project)
        {
            project.Repo.AnalysisServices.TaskDispatcher.QueueInvoke(async () =>
            {
                var assembly = assemblyFileByMetadataAsSourceProjectPath[project.ProjectDirectory];

                try
                {
                    var solution = await MetadataAsSource.LoadMetadataAsSourceSolution(assembly, project.ProjectDirectory);
                    if (solution == null)
                    {
                        return;
                    }

                    var proj = solution.Projects.Single();

                    var projectInfo = ProjectInfo.Create(proj.Id, VersionStamp.Create(), project.ProjectId, project.ProjectId, LanguageNames.CSharp,
                        documents: proj.Documents.Select(d => DocumentInfo.Create(d.Id, d.Name, filePath: d.FilePath, folders: d.Folders)));

                    SolutionProjectAnalyzer.AddSolutionProject(
                        new Lazy<Task<Microsoft.CodeAnalysis.Solution>>(() => Task.FromResult(solution)),
                        projectInfo,
                        project.ProjectFile,
                        project,
                        csharpSemanticServices: new Lazy<SemanticServices>(() => new SemanticServices(solution.Workspace, LanguageNames.CSharp)));

                    if (project.Analyzer != this)
                    {
                        project.Analyzer.Analyze(project);
                    }
                }
                catch
                {
                }
            });
        }
    }
}
