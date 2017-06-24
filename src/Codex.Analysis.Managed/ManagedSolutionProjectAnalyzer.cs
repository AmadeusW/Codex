using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Codex.Analysis.Files;
using Codex.Import;
using Codex.MSBuild;
using Codex.Utilities;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Host.Mef;
using Microsoft.CodeAnalysis.MSBuild;
using Microsoft.CodeAnalysis.Text;

namespace Codex.Analysis.Projects
{
    public class ManagedSolutionProjectAnalyzer : SolutionProjectAnalyzer
    {
        public ManagedSolutionProjectAnalyzer(string[] includedSolutions = null)
            : base(includedSolutions)
        {
        }

        protected override async Task<SolutionInfo> GetSolutionInfoAsync(RepoFile repoFile)
        {
            var repo = repoFile.PrimaryProject.Repo;
            var dispatcher = repo.AnalysisServices.TaskDispatcher;
            var services = repo.AnalysisServices;
            var logger = repo.AnalysisServices.Logger;

            var solutionFile = SolutionFile.Parse(new SourceTextReader(SourceText.From(services.ReadAllText(repoFile.FilePath))));

            SolutionInfoBuilder solutionInfo = new SolutionInfoBuilder(repoFile.FilePath, repo);

            foreach (var projectBlock in solutionFile.ProjectBlocks)
            {
                solutionInfo.StartLoadProjectAsync(projectBlock.ProjectPath);
            }

            await solutionInfo.WaitForProjects();

            if (solutionInfo.HasProjects)
            {
                repoFile.HasExplicitAnalyzer = true;
            }

            return solutionInfo.Build();
        }

        private class SolutionInfoBuilder
        {
            private string solutionPath;
            private string solutionDirectory;
            private AdhocWorkspace workspace;
            public ConcurrentDictionary<string, ProjectInfoBuilder> ProjectInfoByAssemblyNameMap = new ConcurrentDictionary<string, ProjectInfoBuilder>(StringComparer.OrdinalIgnoreCase);
            private Repo repo;
            private string extractionDirectory;

            public bool HasProjects => ProjectInfoByAssemblyNameMap.Count != 0;

            private CompletionTracker completionTracker = new CompletionTracker();

            public SolutionInfoBuilder(string filePath, Repo repo)
            {
                this.solutionPath = filePath;
                this.solutionDirectory = Path.GetDirectoryName(solutionPath);
                this.repo = repo;
                extractionDirectory = Path.Combine(repo.DefaultRepoProject.ProjectDirectory, @".vs\Codex\projects");
                workspace = new AdhocWorkspace(DesktopMefHostServices.DefaultServices);
            }

            public Task WaitForProjects()
            {
                return completionTracker.PendingCompletion;
            }

            public async void StartLoadProjectAsync(string projectPath)
            {
                projectPath = Path.GetFullPath(Path.Combine(solutionDirectory, projectPath));
                var projectName = Path.GetFileNameWithoutExtension(projectPath);

                using (completionTracker.TrackScope())
                {
                    var dispatcher = repo.AnalysisServices.TaskDispatcher;
                    var services = repo.AnalysisServices;
                    var logger = repo.AnalysisServices.Logger;

                    string languageName = projectPath.EndsWith(".csproj") ? LanguageNames.CSharp : LanguageNames.VisualBasic;
                    string argsName = languageName == LanguageNames.CSharp ? "csc.args.txt" : "vbc.args.txt";
                    string argsPath = Path.Combine(extractionDirectory, projectName, argsName);
                    if (!File.Exists(argsPath))
                    {
                        return;
                    }

                    ProjectInfoBuilder info = null;

                    await dispatcher.Invoke(() =>
                    {
                        var args = File.ReadAllLines(argsPath);

                        ProjectInfo projectInfo = GetCommandLineProject(projectPath, projectName, languageName, args);
                        var assemblyName = Path.GetFileNameWithoutExtension(projectInfo.OutputFilePath);
                        info = GetProjectInfo(assemblyName);
                        info.ProjectInfo = projectInfo;
                    });
                }
            }

            private ProjectInfoBuilder GetProjectInfo(string assemblyName)
            {
                return ProjectInfoByAssemblyNameMap.GetOrAdd(assemblyName, k => new ProjectInfoBuilder(k));
            }

            private ProjectInfo GetCommandLineProject(string projectPath, string projectName, string languageName, string[] args)
            {
                var projectDirectory = Path.GetDirectoryName(projectPath);
                string outputPath;
                if (languageName == LanguageNames.VisualBasic)
                {
                    var vbArgs = Microsoft.CodeAnalysis.VisualBasic.VisualBasicCommandLineParser.Default.Parse(args, projectPath, sdkDirectory: null);
                    outputPath = Path.Combine(vbArgs.OutputDirectory, vbArgs.OutputFileName);
                }
                else
                {
                    var csArgs = Microsoft.CodeAnalysis.CSharp.CSharpCommandLineParser.Default.Parse(args, projectPath, sdkDirectory: null);
                    outputPath = Path.Combine(csArgs.OutputDirectory, csArgs.OutputFileName);
                }

                var projectInfo = CommandLineProject.CreateProjectInfo(
                    projectName: projectName, 
                    language: languageName, 
                    commandLineArgs: args, 
                    projectDirectory: projectDirectory, 
                    workspace: workspace);

                projectInfo = projectInfo.WithOutputFilePath(outputPath);
                return projectInfo;
            }

            internal SolutionInfo Build()
            {
                List<ProjectInfo> projects = new List<ProjectInfo>();
                foreach (var project in ProjectInfoByAssemblyNameMap.Values)
                {
                    project.Finalize(this);
                    if (project.IsProject)
                    {
                        projects.Add(project.ProjectInfo);
                    }
                }

                return SolutionInfo.Create(SolutionId.CreateNewId(), VersionStamp.Default, solutionPath, projects);
            }
        }

        private class ProjectInfoBuilder
        {
            public bool IsProject => ProjectInfo != null;
            public ProjectInfo ProjectInfo;
            public string AssemblyName;

            public ProjectInfoBuilder(string assemblyName)
            {
                AssemblyName = assemblyName;
            }

            public void Finalize(SolutionInfoBuilder solutionInfo)
            {
                if (ProjectInfo != null)
                {
                    List<ProjectReference> projectReferences = new List<ProjectReference>();
                    List<MetadataReference> metadataReferences = new List<MetadataReference>();
                    foreach (var reference in ProjectInfo.MetadataReferences)
                    {
                        string path = null;
                        if (reference is PortableExecutableReference)
                        {
                            path = ((PortableExecutableReference)reference).FilePath;
                        }
                        else if (reference is UnresolvedMetadataReference)
                        {
                            path = ((UnresolvedMetadataReference)reference).Reference;
                        }

                        if (string.IsNullOrEmpty(path))
                        {
                            metadataReferences.Add(reference);
                            continue;
                        }

                        var assemblyName = Path.GetFileNameWithoutExtension(path);

                        ProjectInfoBuilder referencedProject;
                        if (solutionInfo.ProjectInfoByAssemblyNameMap.TryGetValue(assemblyName, out referencedProject) && referencedProject.IsProject)
                        {
                            projectReferences.Add(new ProjectReference(
                                referencedProject.ProjectInfo.Id,
                                reference.Properties.Aliases,
                                reference.Properties.EmbedInteropTypes));
                        }
                        else
                        {
                            metadataReferences.Add(reference);
                        }
                    }

                    ProjectInfo = ProjectInfo.WithMetadataReferences(metadataReferences);
                    ProjectInfo = ProjectInfo.WithProjectReferences(projectReferences);
                }
            }
        }

        private class SolutionAnalyzer : RepoFileAnalyzer
        {
            protected override void AnnotateFile(BoundSourceFileBuilder binder)
            {


                base.AnnotateFile(binder);
            }
        }

    }
}
