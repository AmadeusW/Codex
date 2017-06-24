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
    public class Repo : IRepo
    {
        public readonly AnalysisServices AnalysisServices;
        public readonly RepoProject DefaultRepoProject;
        public BlockingCollection<RepoProject> Projects { get; private set; } = new BlockingCollection<RepoProject>();

        public int AnalyzeCount;
        public int UploadCount;
        public IEnumerable<RepoFile> Files => FilesByPath.Values;

        public int UniqueFileCount;
        public int FileCount => FilesByPath.Count + UniqueFileCount;

        public string Name { get; internal set; }

        public string RepositoryName
        {
            get
            {
                return Name;
            }
        }

        public string TargetIndex
        {
            get
            {
                return AnalysisServices.TargetIndex;
            }
        }

        public readonly ConcurrentDictionary<string, RepoFile> FilesByPath = new ConcurrentDictionary<string, RepoFile>(StringComparer.OrdinalIgnoreCase);

        public readonly List<NamedRoot> Roots = new List<NamedRoot>();

        public Repo(string repoName, string repoRoot, AnalysisServices analysisServices)
        {
            Debug.Assert(!string.IsNullOrEmpty(analysisServices.TargetIndex));
            Name = repoName;
            AnalysisServices = analysisServices;
            Roots.AddRange(analysisServices.NamedRoots);

            Roots.Add(new NamedRoot("RepoRoot", repoRoot));
            Roots.Add(new NamedRoot("ProgramFiles", Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles)));
            Roots.Add(new NamedRoot("ProgramFilesX86", Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86)));
            Roots.Add(new NamedRoot("System", Environment.GetFolderPath(Environment.SpecialFolder.System)));
            Roots.Add(new NamedRoot("SystemX86", Environment.GetFolderPath(Environment.SpecialFolder.SystemX86)));
            Roots.Add(new NamedRoot("Windows", Environment.GetFolderPath(Environment.SpecialFolder.Windows)));
            Roots.Add(new NamedRoot("Temp", Environment.GetEnvironmentVariable("Temp")));
            Roots.Add(new NamedRoot("UserProfile", Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)));
            Roots.Add(new NamedRoot("ProgramData", Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData)));
            Roots.Sort((m1, m2) => -m1.Path.Length.CompareTo(m2.Path.Length));
            Roots = Roots.Select(m => new NamedRoot(m.Name, PathUtilities.EnsureTrailingSlash(m.Path))).ToList();

            DefaultRepoProject = CreateRepoProject(GetRepoProjectName(repoName), repoRoot);
        }

        public void AddMount(string name, string path)
        {
            Roots.Add(new NamedRoot(name, PathUtilities.EnsureTrailingSlash(path)));
        }

        public static string GetRepoProjectName(string repoName)
        {
            return repoName + "(Repo)";
        }

        public string GetLogicalPath(string path)
        {
            foreach (var mount in Roots)
            {
                if (path.StartsWith(mount.Path, StringComparison.OrdinalIgnoreCase))
                {
                    return $@"[{mount.Name}]\{path.Substring(mount.Path.Length)}";
                }
            }

            return $@"[External]\{path.Replace(@":\", @"\")}";
        }

        public RepoProject CreateRepoProject(string projectId, string projectDirectory, RepoFile projectFile = null)
        {
            var project = new RepoProject(projectId, this)
            {
                ProjectDirectory = projectDirectory,
                ProjectFile = projectFile
            };

            if (projectFile != null)
            {
                projectFile.PrimaryProject = project;
                project.AddFile(projectFile);
            }

            Projects.Add(project);

            return project;
        }

        public RepoFile TryGetFile(string path)
        {
            RepoFile result = null;
            FilesByPath.TryGetValue(path, out result);
            return result;
        }
    }
}
