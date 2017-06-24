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
    public class RepoProject : IRepoProject
    {
        public readonly Repo Repo;
        public string ProjectId;
        private string projectDirectory;
        public string ProjectDirectory
        {
            get
            {
                return projectDirectory;
            }
            set
            {
                projectDirectory = value?.EnsureTrailingSlash();
            }
        }
        public RepoFile ProjectFile;
        public List<RepoFile> Files { get; private set; } = new List<RepoFile>();

        IRepo IRepoProject.Repo
        {
            get
            {
                return Repo;
            }
        }

        public string ProjectKind { get; set; } = nameof(ObjectModel.ProjectKind.Source);

        public RepoProjectAnalyzer Analyzer;

        public RepoProject(string projectId, Repo repo)
        {
            Repo = repo;
            ProjectId = projectId;
            Analyzer = RepoProjectAnalyzer.Default;
        }

        public void AddFile(RepoFile repoFile)
        {
            lock (Files)
            {
                Files.Add(repoFile);
            }
        }

        public RepoFile AddFile(string filePath, string logicalPath = null)
        {
            if (filePath == null)
            {
                throw new ArgumentNullException(nameof(filePath));
            }

            RepoFile file;
            if (!Repo.FilesByPath.TryGetValue(filePath, out file))
            {
                file = CreateNewProjectFile(filePath, logicalPath);
                file = Repo.FilesByPath.GetOrAdd(filePath, file);
            }
            else if (this != Repo.DefaultRepoProject)
            {
                var lockedFile = file;
                lock (lockedFile)
                {
                    if (file.PrimaryProject == Repo.DefaultRepoProject)
                    {
                        file.LogicalPath = logicalPath;
                        file.PrimaryProject = this;
                        AddFile(file);
                    }
                    else
                    {
                        file = Files
                            .Where(f => string.Equals(f.FilePath, filePath, StringComparison.OrdinalIgnoreCase))
                            .FirstOrDefault();
                        if (file == null && filePath != null)
                        {
                            file = CreateNewProjectFile(filePath, logicalPath);
                            Interlocked.Increment(ref Repo.UniqueFileCount);
                        }
                    }
                }
            }

            if (file == null)
            {
                throw new NullReferenceException(filePath);
            }

            return file;
        }

        private RepoFile CreateNewProjectFile(string filePath, string logicalPath)
        {
            var file = new RepoFile(this, filePath, logicalPath);
            AddFile(file);
            return file;
        }

        public string GetLogicalPath(string filePath)
        {
            return PathUtilities.GetRelativePath(ProjectDirectory, filePath)
                        ?? Repo.GetLogicalPath(filePath);
        }

        public bool InProjectDirectory(string filePath)
        {
            if (string.IsNullOrEmpty(ProjectDirectory))
            {
                return false;
            }

            if (!filePath.StartsWith(ProjectDirectory, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            return true;
        }
    }
}
