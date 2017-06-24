using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;

namespace Codex.Utilities
{
    /// <summary>
    /// Allows executing a set of tasks with limited parallelism.
    /// </summary>
    public class ActionQueue
    {
        private ActionBlock<bool> operationBlock;
        private BlockingCollection<Func<Task>>[] priorityQueues;
        private CompletionTracker tracker;
        private AsyncLocal<Holder<int>> asyncLocalDepth = new AsyncLocal<Holder<int>>();

        /// <summary>
        /// Creates a new action queue.
        /// </summary>
        /// <param name="maxDegreeOfParallelism">the maximum number of parallel executing tasks</param>
        /// <param name="cancellationToken">the cancellation token</param>
        public ActionQueue(int maxDegreeOfParallelism = 1, 
            int priorityCount = 1,
            int boundedCapacity = int.MaxValue,
            CompletionTracker tracker = null, 
            CancellationToken cancellationToken = default(CancellationToken))
        {
            operationBlock = new ActionBlock<bool>(new Func<bool, Task>(Process), new ExecutionDataflowBlockOptions()
            {
                MaxDegreeOfParallelism = maxDegreeOfParallelism,
                CancellationToken = cancellationToken,
            });

            priorityQueues = Enumerable
                .Range(0, priorityCount * 2)
                .Select(_ => new BlockingCollection<Func<Task>>(boundedCapacity))
                .ToArray();

            this.tracker = tracker ?? new CompletionTracker();
        }

        private class Holder<T>
        {
            public T Value;
        }


        /// <summary>
        /// Returns a task the represents the completion of all current pending operations
        /// </summary>
        public Task PendingCompletion
        {
            get
            {
                return tracker.PendingCompletion;
            }
        }

        /// <summary>
        /// Processes a task generation function in the queue and waits on the result.
        /// </summary>
        private Task Process(bool unused)
        {
            Func<Task> asyncFactory;
            while (true)
            {
                for (int i = priorityQueues.Length - 1; i >= 0; i--)
                {
                    if (priorityQueues[i].TryTake(out asyncFactory))
                    {
                        return asyncFactory();
                    }
                }
            }
        }

        /// <summary>
        /// Executes the task on the action block.
        /// </summary>
        /// <typeparam name="T">the return type of the task</typeparam>
        /// <param name="taskFactory">the function which generates the task to execute</param>
        /// <returns>a task representing the result of the task execution</returns>
        public Task<T> Execute<T>(Func<Task<T>> taskFactory, int priority = 0)
        {
            int depth = 1;
            priority *= 2;

            if (asyncLocalDepth.Value != null)
            {
                priority++;
                depth = asyncLocalDepth.Value.Value +  1;
            }

            tracker.OnStart();
            TaskCompletionSource<T> completionSource = new TaskCompletionSource<T>();

            priorityQueues[priority].Add(new Func<Task>(async () =>
            {
                asyncLocalDepth.Value = new Holder<int>() { Value = depth };
                try
                {
                    var result = await taskFactory().ConfigureAwait(continueOnCapturedContext: false);

                    tracker.OnComplete();
                    completionSource.SetResult(result);
                }
                catch (OperationCanceledException)
                {
                    tracker.OnComplete();
                    completionSource.SetCanceled();
                }
                catch (Exception ex)
                {
                    tracker.OnComplete();
                    completionSource.SetException(ex);
                }
            }));

            operationBlock.Post(true);

            return completionSource.Task;
        }

        /// <summary>
        /// Executes the task on the action block.
        /// </summary>
        /// <param name="taskFactory">the function which generates the task to execute</param>
        /// <returns>a task representing the result of the task execution</returns>
        public Task Execute(Func<Task> taskFactory, int priority = 0)
        {
            return Execute<bool>(async () =>
            {
                await taskFactory().ConfigureAwait(continueOnCapturedContext: false);
                return true;
            }, priority);
        }

        /// <summary>
        /// Disposes of the action block, preventing any further operations from being queued.
        /// </summary>
        /// <returns>A task representing the completion of the action blocks outstanding tasks.</returns>
        public Task DisposeAsync()
        {
            this.operationBlock.Complete();
            return operationBlock.Completion;
        }
    }
}
