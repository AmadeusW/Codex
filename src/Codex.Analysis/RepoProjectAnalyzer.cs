using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Codex.Analysis;
using Codex.Analysis.Files;
using Codex.Analysis.FileSystems;
using Codex.Import;
using Codex.ObjectModel;
using Codex.Utilities;
using static Codex.Utilities.TaskUtilities;

namespace Codex.Analysis
{

    public class RepoProjectAnalyzerBase : RepoProjectAnalyzer
    {
        public override void UploadProject(RepoProject project)
        {
        }
    }

    public class NullRepoProjectAnalzyer : RepoProjectAnalyzer
    {
        public override void UploadProject(RepoProject project)
        {
        }

        public override void Analyze(RepoProject project)
        {
        }
    }

    public class RepoProjectAnalyzer
    {
        public static readonly RepoProjectAnalyzer Default = new RepoProjectAnalyzer();

        public static readonly RepoProjectAnalyzer Null = new NullRepoProjectAnalzyer();

        public virtual void CreateProjects(Repo repo) { }

        public virtual void Analyze(RepoProject project)
        {
            project.Repo.AnalysisServices.Logger.WriteLine($"Analyzing project {project.ProjectId}");

            foreach (var file in project.Files)
            {
                if (file.PrimaryProject == project)
                {
                    file.Analyze();
                }
            }

            UploadProject(project);
        }

        public virtual void CreateProjects(RepoFile repoFile) { }

        public virtual bool IsCandidateProjectFile(RepoFile repoFile) => false;

        public virtual void UploadProject(RepoProject project)
        {
            AnalyzedProject analyzedProject = new AnalyzedProject(project.Repo.Name, project.ProjectId);

            UploadProject(project, analyzedProject);
        }

        protected static void UploadProject(RepoProject project, AnalyzedProject analyzedProject)
        {
            analyzedProject.ProjectKind = project.ProjectKind;
            project.Repo.AnalysisServices.TaskDispatcher.QueueInvoke(() =>
                project.Repo.AnalysisServices.AnalysisTarget.AddProjectAsync(project, analyzedProject), TaskType.Upload);
        }
    }
}
