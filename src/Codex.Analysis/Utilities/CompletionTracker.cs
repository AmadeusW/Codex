using System;
using System.Threading;
using System.Threading.Tasks;

namespace Codex.Utilities
{
    public class CompletionTracker
    {
        private int pendingCount;

        private TaskCompletionSource<bool> pendingCompletionSource;
        private Task<bool> pendingCompletion = Task.FromResult(true);
        private object syncLock = new object();

        public CompletionTracker()
        {
        }

        /// <summary>
        /// Returns a task the represents the completion of all current pending operations
        /// </summary>
        public Task PendingCompletion
        {
            get
            {
                return GetCompletion();
            }
        }

        private async Task GetCompletion()
        {
            await pendingCompletion;
            await Task.Yield();
        }

        public void OnStart()
        {
            lock (syncLock)
            {
                if (Interlocked.Increment(ref pendingCount) == 1)
                {
                    if (pendingCompletionSource?.Task.IsCompleted == false)
                    {
                        return;
                    }

                    pendingCompletionSource = new TaskCompletionSource<bool>();
                    pendingCompletion = pendingCompletionSource.Task;
                }
            }
        }

        public void OnComplete()
        {
            lock (syncLock)
            {
                if (Interlocked.Decrement(ref pendingCount) == 0)
                {
                    pendingCompletionSource.SetResult(true);
                }
            }
        }

        public CompletionHandle TrackScope()
        {
            return new CompletionHandle(this);
        }

        public struct CompletionHandle : IDisposable
        {
            private CompletionTracker tracker;

            public CompletionHandle(CompletionTracker tracker)
            {
                this.tracker = tracker;
                tracker.OnStart();
            }

            public void Dispose()
            {
                tracker.OnComplete();
            }
        }
    }
}
