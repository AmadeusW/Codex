using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Codex.Analysis;
using Codex.Analysis.Files;
using Codex.Analysis.FileSystems;
using Codex.ObjectModel;
using Codex.Utilities;

namespace Codex.Import
{
    public class RepositoryImporter
    {
        public readonly string RepoName;
        public string RootDirectory;
        private FileSystem FileSystem;
        public readonly AnalysisServices AnalysisServices;
        public List<AnalyzerData> AnalyzerDatas = new List<AnalyzerData>();

        public RepositoryImporter(string repoName, string rootDirectory, AnalysisServices analysisServices)
        {
            AnalysisServices = analysisServices;
            FileSystem = analysisServices.FileSystem;
            RepoName = repoName;
            RootDirectory = rootDirectory;
        }

        public async Task<bool> Import(string repoProjectName = null, bool finalizeImport = true)
        {
            var logger = AnalysisServices.Logger;
            var repo = await AnalysisServices.CreateRepo(RepoName, RootDirectory);

            if (repoProjectName != null)
            {
                repo.DefaultRepoProject.ProjectId = Repo.GetRepoProjectName(repoProjectName);
            }

            Stopwatch watch = Stopwatch.StartNew();
            Stopwatch totalWatch = Stopwatch.StartNew();

            foreach (var analyzer in AnalysisServices.FileAnalyzers)
            {
                analyzer.Initialize(repo);
            }

            await AnalysisServices.TaskDispatcher.OnCompletion();

            logger.WriteLine($"Initialized analyzers. Operation: {watch.Elapsed} Total: {totalWatch.Elapsed}");
            watch.Restart();

            foreach (var analyzerData in AnalyzerDatas)
            {
                analyzerData.Analyzer.CreateProjects(repo);

                // Wait for analysis of all projects for the given analyzer before moving on to
                // next analyzer. This allows solution files to handle analysis of all constituent
                // projects and prevent the project analyzer from being used.
                await AnalysisServices.TaskDispatcher.OnCompletion();
            }

            logger.WriteLine($"Created repo projects. Operation: {watch.Elapsed} Total: {totalWatch.Elapsed}");
            watch.Restart();

            int i = 0;
            foreach (var file in FileSystem.GetFiles())
            {
                if (file == null)
                {
                    throw new ArgumentNullException(nameof(file));
                }

                logger.LogMessage($"Found file: {file}", Logging.MessageKind.Diagnostic);

                i++;
                if ((i % 1000) == 0)
                {
                    logger.WriteLine($"Detected {i} files");
                }

                var repoFile = repo.DefaultRepoProject.AddFile(file);

                if (!repoFile.HasExplicitAnalyzer)
                {
                    foreach (var analyzerData in AnalyzerDatas)
                    {
                        if (analyzerData.Analyzer.IsCandidateProjectFile(repoFile))
                        {
                            logger.LogMessage($"Candidate project file: {file}\nAnalyzer: {analyzerData.Analyzer.GetType().FullName}", Logging.MessageKind.Diagnostic);

                            analyzerData.CandidateProjectFiles.Add(repoFile);
                        }
                    }
                }
            }

            //File.WriteAllLines("output\\files.txt", FileSystem.GetFiles());

            logger.WriteLine($"Computed candidate project files. Operation: {watch.Elapsed} Total: {totalWatch.Elapsed}");
            watch.Restart();

            foreach (var analyzerData in AnalyzerDatas)
            {
                i = 0;
                foreach (var candidateProjectFile in analyzerData.CandidateProjectFiles)
                {
                    if (candidateProjectFile.HasExplicitAnalyzer)
                    {
                        continue;
                    }

                    analyzerData.Analyzer.CreateProjects(candidateProjectFile);

                    i++;
                    logger.WriteLine($"Create project {i} of {analyzerData.CandidateProjectFiles.Count} from candidate project file: {candidateProjectFile.LogicalPath}");
                }

                // Wait for analysis of all projects for the given analyzer before moving on to
                // next analyzer. This allows solution files to handle analysis of all constituent
                // projects and prevent the project analyzer from being used.
                await AnalysisServices.TaskDispatcher.OnCompletion();
            }

            logger.WriteLine($"Created projects from candidates files. Operation: {watch.Elapsed} Total: {totalWatch.Elapsed}");
            watch.Restart();

            logger.WriteLine("Sorting files");

            var defaultFiles = repo.DefaultRepoProject.Files;
            var fileList = defaultFiles.Where(f => f != null).ToList();
            if (fileList.Count < defaultFiles.Count)
            {
                logger.WriteLine($"{defaultFiles.Count - fileList.Count} files in DefaultRepoProject are null");
                Debugger.Break();
            }

            foreach (var f in fileList)
            {
                if (f.FilePath == null)
                {
                    logger.WriteLine($"FilePath for file {f.ToString()} is null");
                    Debugger.Break();
                }
            }

            fileList.Sort((p1, p2) => StringComparer.OrdinalIgnoreCase.Compare(p1.FilePath, p2.FilePath));

            var projects = repo.Projects.ToList();
            projects.Remove(repo.DefaultRepoProject);
            projects.Sort((p1, p2) => -p1.ProjectDirectory.Length.CompareTo(p2.ProjectDirectory.Length));

            logger.WriteLine($"Sorted files. Operation: {watch.Elapsed} Total: {totalWatch.Elapsed}");
            watch.Restart();

            HashSet<RepoFile> projectFiles = new HashSet<RepoFile>();
            foreach (var project in projects)
            {
                if (project == repo.DefaultRepoProject)
                {
                    continue;
                }

                projectFiles.Clear();
                projectFiles.UnionWith(project.Files);

                foreach (var file in fileList.GetPrefixRange(project.ProjectDirectory, rf => rf.FilePath))
                {
                    if (file.PrimaryProject == repo.DefaultRepoProject && file.FilePath.StartsWith(project.ProjectDirectory, StringComparison.OrdinalIgnoreCase))
                    {
                        file.PrimaryProject = project;
                        if (projectFiles.Add(file))
                        {
                            project.AddFile(file);
                        }

                        logger.WriteLine($"Reassigning {file.FilePath} to {project.ProjectId} in '{project.ProjectDirectory}'");
                    }
                }
            }

            var unassignedFiles = fileList.Where(rf => rf.PrimaryProject == repo.DefaultRepoProject).ToList();
            repo.DefaultRepoProject.Files.Clear();
            repo.DefaultRepoProject.Files.AddRange(unassignedFiles);

            logger.WriteLine($"Reassigned files. Operation: {watch.Elapsed} Total: {totalWatch.Elapsed}");

            watch.Restart();

            foreach (var project in repo.Projects)
            {
                if (AnalysisServices.IncludeRepoProject(project))
                {
                    project.Analyzer.Analyze(project);
                }
            }
            // TODO(LANCEC): Explicitly analyze default repo project. Not why I don't
            // add this to the projects list but I remember there being a reason.
            //repo.DefaultRepoProject.Analyzer.Analyze(repo.DefaultRepoProject);

            await AnalysisServices.TaskDispatcher.OnCompletion();

            logger.WriteLine($"Analyzed Projects. Operation: {watch.Elapsed} Total: {totalWatch.Elapsed}");
            watch.Restart();

            foreach (var analyzer in AnalysisServices.FileAnalyzers)
            {
                analyzer.Finalize(repo);
            }

            await AnalysisServices.TaskDispatcher.OnCompletion();

            logger.WriteLine($"Finalized Analyzers. Operation: {watch.Elapsed} Total: {totalWatch.Elapsed}");

            if (finalizeImport)
            {
                await AnalysisServices.AnalysisTarget.FinalizeRepository(repo);

                logger.WriteLine($"Finalized Repository. Operation: {watch.Elapsed} Total: {totalWatch.Elapsed}");
            }

            return true;
        }

        private static int Compare(string projectDirectory, string filePath)
        {
            if (filePath.StartsWith(projectDirectory, StringComparison.OrdinalIgnoreCase))
            {
                return 0;
            }

            return StringComparer.OrdinalIgnoreCase.Compare(projectDirectory, filePath);
        }
    }

    public class AnalyzerData
    {
        public RepoProjectAnalyzer Analyzer { get; set; }
        public List<RepoFile> CandidateProjectFiles { get; } = new List<RepoFile>();
    }
}
