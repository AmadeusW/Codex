using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Codex.Import;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Host.Mef;
using Microsoft.CodeAnalysis.MSBuild;

namespace Codex.Analysis.Projects
{
    public class MSBuildSolutionProjectAnalyzer : SolutionProjectAnalyzer
    {
        MSBuildProjectLoader loader;

        public MSBuildSolutionProjectAnalyzer(string[] includedSolutions = null)
            : base(includedSolutions)
        {
            loader = new MSBuildProjectLoader(new AdhocWorkspace(DesktopMefHostServices.DefaultServices))
            {
                SkipUnrecognizedProjects = true,
            };
        }

        protected override Task<SolutionInfo> GetSolutionInfoAsync(RepoFile repoFile)
        {
            return loader.LoadSolutionInfoAsync(repoFile.FilePath);
        }
    }
}
