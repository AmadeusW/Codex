using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Codex.ObjectModel;
using Codex.Utilities;

namespace Codex.Analysis
{
    public interface IAnalysisTarget
    {
        Task AddRepositiory(IRepo repo);

        Task UploadAsync(IRepoFile repoFile, BoundSourceFile boundSourceFile);

        Task AddProjectAsync(IRepoProject repoProject, AnalyzedProject analyzedProject);

        Task FinalizeRepository(IRepo repo);
    }
    public interface IRepo
    {
        string RepositoryName { get; }

        string TargetIndex { get; }
    }

    public interface IRepoProject
    {
        string ProjectKind { get; }
        IRepo Repo { get; }
    }

    public interface IRepoFile
    {
        IRepo Repo { get; }

        /// <summary>
        /// Repo relative path. May be null if file not under repo root.
        /// </summary>
        string RepoRelativePath { get; }
    }

    public class NullAnalysisTarget : IAnalysisTarget
    {
        public Task AddProjectAsync(IRepoProject repoProject, AnalyzedProject analyzedProject)
        {
            return Task.FromResult(true);
        }

        public Task AddRepositiory(IRepo repo)
        {
            return Task.FromResult(true);
        }

        public Task FinalizeRepository(IRepo repo)
        {
            return Task.FromResult(true);
        }

        public Task UploadAsync(IRepoFile repoFile, BoundSourceFile boundSourceFile)
        {
            return Task.FromResult(true);
        }
    }

    public class AnalysisTargetDecorator : IAnalysisTarget
    {
        private IAnalysisTarget analysisTarget;

        public AnalysisTargetDecorator(IAnalysisTarget analysisTarget)
        {
            this.analysisTarget = analysisTarget;
        }

        public virtual Task AddProjectAsync(IRepoProject repoProject, AnalyzedProject analyzedProject)
        {
            return analysisTarget.AddProjectAsync(repoProject, analyzedProject);
        }

        public virtual Task AddRepositiory(IRepo repo)
        {
            return analysisTarget.AddRepositiory(repo);
        }

        public Task FinalizeRepository(IRepo repo)
        {
            return analysisTarget.FinalizeRepository(repo);
        }

        public virtual Task UploadAsync(IRepoFile repoFile, BoundSourceFile boundSourceFile)
        {
            return analysisTarget.UploadAsync(repoFile, boundSourceFile);
        }
    }

    public class WebLinkAnalysisDecorator : AnalysisTargetDecorator
    {
        public Func<string, string> RepoUrlSelector;

        public WebLinkAnalysisDecorator(Func<string, string> webLinkSelector, IAnalysisTarget analysisTarget)
            : base(analysisTarget)
        {
            RepoUrlSelector = webLinkSelector;
        }

        public override Task UploadAsync(IRepoFile repoFile, BoundSourceFile boundSourceFile)
        {
            if (RepoUrlSelector != null && repoFile.RepoRelativePath != null)
            {
                boundSourceFile.SourceFile.Info.WebAddress = RepoUrlSelector(repoFile.RepoRelativePath);
            }

            return base.UploadAsync(repoFile, boundSourceFile);
        }
    }
}
