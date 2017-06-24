// --------------------------------------------------------------------
//
// Copyright (c) Microsoft Corporation.  All rights reserved.
//
// --------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Diagnostics.Contracts;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace Codex.Utilities
{
    /// <summary>
    /// Static utilities related to <see cref="Task" />.
    /// </summary>
    public static class TaskUtilities
    {
        public static void IgnoreAsync(this Task t)
        {
        }

        /// <summary>
        /// Returns a faulted task containing the given exception.
        /// This is the failure complement of <see cref="Task.FromResult{TResult}" />.
        /// </summary>
        [ContractOption("runtime", "checking", false)]
        public static Task<T> FromException<T>(Exception ex)
        {
            Contract.Requires(ex != null);
            Contract.Ensures(Contract.Result<Task<T>>() != null);

            var failureSource = new TaskCompletionSource<T>();
            failureSource.SetException(ex);
            return failureSource.Task;
        }

        /// <summary>
        /// Provides await functionality for ordinary <see cref="WaitHandle"/>s.
        /// </summary>
        /// <param name="handle">The handle to wait on.</param>
        /// <returns>The awaiter.</returns>
        public static TaskAwaiter GetAwaiter(this WaitHandle handle)
        {
            Contract.Requires(handle != null);

            return handle.ToTask().GetAwaiter();
        }

        /// <summary>
        /// Provides await functionality for an array of ordinary <see cref="WaitHandle"/>s.
        /// </summary>
        /// <param name="handles">The handles to wait on.</param>
        /// <returns>The awaiter.</returns>
        public static TaskAwaiter<int> GetAwaiter(this WaitHandle[] handles)
        {
            Contract.Requires(handles != null);
            Contract.Requires(Contract.ForAll(handles, handle => handles != null));

            return handles.ToTask().GetAwaiter();
        }

        /// <summary>
        /// Creates a TPL Task that is marked as completed when a <see cref="WaitHandle"/> is signaled.
        /// </summary>
        /// <param name="handle">The handle whose signal triggers the task to be completed.  Do not use a <see cref="Mutex"/> here.</param>
        /// <param name="timeout">The timeout (in milliseconds) after which the task will fault with a <see cref="TimeoutException"/> if the handle is not signaled by that time.</param>
        /// <returns>A Task that is completed after the handle is signaled.</returns>
        /// <remarks>
        /// There is a (brief) time delay between when the handle is signaled and when the task is marked as completed.
        /// </remarks>
        public static Task ToTask(this WaitHandle handle, int timeout = Timeout.Infinite)
        {
            Contract.Requires(handle != null);

            return ToTask(new WaitHandle[1] { handle }, timeout);
        }

        /// <summary>
        /// Creates a TPL Task that is marked as completed when any <see cref="WaitHandle"/> in the array is signaled.
        /// </summary>
        /// <param name="handles">The handles whose signals triggers the task to be completed.  Do not use a <see cref="Mutex"/> here.</param>
        /// <param name="timeout">The timeout (in milliseconds) after which the task will return a value of WaitTimeout.</param>
        /// <returns>A Task that is completed after any handle is signaled.</returns>
        /// <remarks>
        /// There is a (brief) time delay between when the handles are signaled and when the task is marked as completed.
        /// </remarks>
        public static Task<int> ToTask(this WaitHandle[] handles, int timeout = Timeout.Infinite)
        {
            Contract.Requires(handles != null);
            Contract.Requires(Contract.ForAll(handles, handle => handles != null));

            var tcs = new TaskCompletionSource<int>();
            int signalledHandle = WaitHandle.WaitAny(handles, 0);
            if (signalledHandle != WaitHandle.WaitTimeout)
            {
                // An optimization for if the handle is already signaled
                // to return a completed task.
                tcs.SetResult(signalledHandle);
            }
            else
            {
                var localVariableInitLock = new object();
                lock (localVariableInitLock)
                {
                    RegisteredWaitHandle[] callbackHandles = new RegisteredWaitHandle[handles.Length];
                    for (int i = 0; i < handles.Length; i++)
                    {
                        callbackHandles[i] = ThreadPool.RegisterWaitForSingleObject(
                            handles[i],
                            (state, timedOut) =>
                            {
                                int handleIndex = (int)state;
                                if (timedOut)
                                {
                                    tcs.TrySetResult(WaitHandle.WaitTimeout);
                                }
                                else
                                {
                                    tcs.TrySetResult(handleIndex);
                                }

                                // We take a lock here to make sure the outer method has completed setting the local variable callbackHandles contents.
                                lock (localVariableInitLock)
                                {
                                    foreach (var handle in callbackHandles)
                                    {
                                        handle.Unregister(null);
                                    }
                                }
                            },
                            state: i,
                            millisecondsTimeOutInterval: timeout,
                            executeOnlyOnce: true);
                    }
                }
            }

            return tcs.Task;
        }

        /// <summary>
        /// Creates a new <see cref="SemaphoreSlim"/> representing a mutex which can only be entered once.
        /// </summary>
        /// <returns>the semaphore</returns>
        public static SemaphoreSlim CreateMutex()
        {
            return new SemaphoreSlim(initialCount: 1, maxCount: 1);
        }

        /// <summary>
        /// Asynchronously acquire a semaphore
        /// </summary>
        /// <param name="semaphore">The semaphore to acquire</param>
        /// <param name="cancellationToken">The cancellation token</param>
        /// <returns>A disposable which will release the semaphore when it is disposed.</returns>
        public static async Task<SemaphoreReleaser> AcquireAsync(this SemaphoreSlim semaphore, CancellationToken cancellationToken = default(CancellationToken))
        {
            Contract.Requires(semaphore != null);
            await semaphore.WaitAsync(cancellationToken);
            return new SemaphoreReleaser(semaphore);
        }

        /// <summary>
        /// Synchronously acquire a semaphore
        /// </summary>
        /// <param name="semaphore">The semaphore to acquire</param>
        public static SemaphoreReleaser AcquireSemaphore(this SemaphoreSlim semaphore)
        {
            Contract.Requires(semaphore != null);
            semaphore.Wait();
            return new SemaphoreReleaser(semaphore);
        }

        /// <summary>
        /// Consumes a task and doesn't do anything with it.  Useful for fire-and-forget calls to async methods within async methods.
        /// </summary>
        /// <param name="task">The task whose result is to be ignored.</param>
        [SuppressMessage("Microsoft.Usage", "CA1801:ReviewUnusedParameters", MessageId = "task")]
        public static void Forget(this Task task)
        {
        }

        /// <summary>
        /// Allows an IDisposable-conforming release of an acquired semaphore
        /// </summary>
        [SuppressMessage("Microsoft.Performance", "CA1815:OverrideEqualsAndOperatorEqualsOnValueTypes")]
        public struct SemaphoreReleaser : IDisposable
        {
            private readonly SemaphoreSlim m_semaphore;

            /// <summary>
            /// Creates a new releaser.
            /// </summary>
            /// <param name="semaphore">The semaphore to release when Dispose is invoked.</param>
            /// <remarks>
            /// Assumes the semaphore is already acquired.
            /// </remarks>
            internal SemaphoreReleaser(SemaphoreSlim semaphore)
            {
                Contract.Requires(semaphore != null);
                this.m_semaphore = semaphore;
            }

            /// <summary>
            /// IDispoaable.Dispose()
            /// </summary>
            public void Dispose()
            {
                m_semaphore.Release();
            }

            /// <summary>
            /// Whether this semaphore releaser is valid (and not the default value)
            /// </summary>
            public bool IsValid
            {
                get { return m_semaphore != null; }
            }

            /// <summary>
            /// Gets the number of threads that will be allowed to enter the semaphore.
            /// </summary>
            public int CurrentCount
            {
                get { return m_semaphore.CurrentCount; }
            }
        }
    }
}