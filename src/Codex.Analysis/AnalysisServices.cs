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
using Codex.Logging;
using Codex.Utilities;
using static Codex.Utilities.TaskUtilities;

namespace Codex.Import
{
public class AnalysisServices
    {
        public Func<RepoProject, bool> IncludeRepoProject = (rp => true);
        public FileSystem FileSystem;
        public Logger Logger = Logger.Null;
        public string TargetIndex { get; }
        public IAnalysisTarget AnalysisTarget;
        public List<RepoFileAnalyzer> FileAnalyzers { get; set; } = new List<RepoFileAnalyzer>();
        public List<RepoProjectAnalyzer> ProjectAnalyzers { get; set; } = new List<RepoProjectAnalyzer>();
        public Dictionary<string, RepoFileAnalyzer> FileAnalyzerByExtension { get; set; } = new Dictionary<string, RepoFileAnalyzer>();
        public TaskDispatcher TaskDispatcher { get; set; } = new TaskDispatcher();
        public readonly List<NamedRoot> NamedRoots = new List<NamedRoot>();

        public ConcurrentDictionary<string, Repo> ReposByName = new ConcurrentDictionary<string, Repo>(StringComparer.OrdinalIgnoreCase);

        public static string GetTargetIndexName(string repoName)
        {
            return $"{repoName.ToLowerInvariant()}.{DateTime.UtcNow.ToString("yyMMdd.HH:mm:ss").Replace(":", "")}";
        }

        public AnalysisServices(string targetIndex, FileSystem fileSystem, RepoFileAnalyzer[] analyzers = null)
        {
            Debug.Assert(!string.IsNullOrEmpty(targetIndex));
            TargetIndex = targetIndex;
            FileSystem = fileSystem;
            if (analyzers != null)
            {
                FileAnalyzers.AddRange(analyzers);

                foreach (var analyzer in FileAnalyzers)
                {
                    foreach (var extension in analyzer.SupportedExtensions)
                    {
                        FileAnalyzerByExtension[extension] = analyzer;
                    }
                }
            }
        }

        public async virtual Task<Repo> CreateRepo(string name, string root = null)
        {
            Repo repo;
            bool added = false;

            if (!ReposByName.TryGetValue(name, out repo))
            {
                repo = ReposByName.GetOrAdd(name, k =>
                {
                    added = true;
                    return new Repo(name, root ?? $@"\\{name}\", this);
                });
            }

            if (added)
            {
                await AnalysisTarget.AddRepositiory(repo);
            }

            return repo;
        }

        public virtual RepoFileAnalyzer GetDefaultAnalyzer(string filePath)
        {
            var extension = Path.GetExtension(filePath);
            RepoFileAnalyzer fileAnalyzer;
            if (FileAnalyzerByExtension.TryGetValue(extension, out fileAnalyzer))
            {
                return fileAnalyzer;
            }

            return RepoFileAnalyzer.Default;
        }

        public string ReadAllText(string filePath)
        {
            using (var stream = FileSystem.OpenFile(filePath))
            using (StreamReader reader = new StreamReader(stream))
            {
                return reader.ReadToEnd();
            }
        }
    }

    public class TaskTracker
    {
        public bool IsTaskCompleted(string taskName) => false;

        public void CompleteTask(string taskName) { }
    }

    public enum TaskType
    {
        Analysis,
        Upload,
    }

    public class TaskDispatcher
    {
        private readonly ActionQueue actionQueue;
        private CompletionTracker tracker;

        public TaskDispatcher(int? maxParallelism = null)
        {
            tracker = new CompletionTracker();
            actionQueue = new ActionQueue(
                    maxDegreeOfParallelism: maxParallelism ?? Environment.ProcessorCount + 2,
                    tracker: tracker,
                    priorityCount: 2/*,
                    boundedCapacity: 128*/);
        }

        public CompletionTracker.CompletionHandle TrackScope()
        {
            return tracker.TrackScope();
        } 

        public Task OnCompletion()
        {
            return actionQueue.PendingCompletion;
        }

        public void QueueInvoke(Action action, TaskType type = TaskType.Analysis)
        {
            Invoke(action, type).IgnoreAsync();
        }

        public void QueueInvoke(Func<Task> asyncAction, TaskType type = TaskType.Analysis)
        {
            Invoke(asyncAction, type).IgnoreAsync();
        }

        public void QueueInvoke<T>(Func<Task<T>> asyncAction, TaskType type = TaskType.Analysis)
        {
            Invoke(asyncAction, type).IgnoreAsync();
        }

        public Task Invoke(Action action, TaskType type = TaskType.Analysis)
        {
            return actionQueue.Execute(() =>
                {
                    action();
                    return Task.FromResult(true);
                }, (int)type);
        }

        public Task Invoke(Func<Task> asyncAction, TaskType type = TaskType.Analysis)
        {
            return actionQueue.Execute(asyncAction, (int)type);
        }

        public Task<T> Invoke<T>(Func<Task<T>> asyncAction, TaskType type = TaskType.Analysis)
        {
            return actionQueue.Execute(asyncAction, (int)type);
        }
    }
}
