using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Codex.Import;
using Microsoft.CodeAnalysis;

namespace Codex.Analysis.Projects
{
    public class ProjectCollectionAnalyzer : RepoProjectAnalyzerBase
    {
        private readonly List<ProjectInfo> projects;

        public ProjectCollectionAnalyzer(IEnumerable<ProjectInfo> projects)
        {
            this.projects = projects.ToList();
        }

        public override void CreateProjects(Repo repo)
        {
            var dispatcher = repo.AnalysisServices.TaskDispatcher;

            int i = 0;
            foreach (var projectInfo in projects)
            {
                i++;
                SolutionProjectAnalyzer.AddSolutionProjects(
                    repo,
                    () => LoadSolutionInfo(projectInfo),
                    requireProjectExists: false);

                Console.WriteLine($"Created project {i} of {projects.Count}");
            }
        }

        private Task<SolutionInfo> LoadSolutionInfo(ProjectInfo projectInfo)
        {
            return Task.FromResult(SolutionInfo.Create(SolutionId.CreateNewId(), VersionStamp.Create(),
                projects: new[] { projectInfo }));
        }
    }
}
