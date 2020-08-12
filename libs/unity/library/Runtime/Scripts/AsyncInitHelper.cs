// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.MixedReality.WebRTC.Unity
{
    /// <summary>
    /// Utility for resources in Unity components that need asynchronous initialization,
    /// and don't match well the Unity synchronous enable/disable workflow.
    /// </summary>
    /// <remarks>
    /// This keeps track of an initialization task, allows callers to poll for the
    /// initialized object, and handles cancellation/cleanup if the initialization is
    /// aborted before it has completed.
    /// </remarks>
    public class AsyncInitHelper<T> where T : class, IDisposable
    {
        private Task<T> _initTask;
        private CancellationTokenSource _cts;

        /// <summary>
        /// Starts tracking an initialization task for a resource.
        /// </summary>
        /// <param name="cts">
        /// This will be used to cancel the task if aborted before it has
        /// finished. Will be disposed at the end of the task.
        /// </param>
        public void TrackInitTask(Task<T> initTask, CancellationTokenSource cts = null)
        {
            _initTask = initTask;
            _cts = cts;
        }

        /// <summary>
        /// Check if the initialization task has generated a result.
        /// </summary>
        /// <remarks>
        /// If the initialization fails with an exception, this rethrows the exception.
        /// </remarks>
        /// <returns>
        /// The result of the initialization task if there is one and it has just successfully completed;
        /// <c>null</c> if there is no task.
        /// </returns>
        public T Result
        {
            get
            {
                if (_initTask != null && _initTask.IsCompleted)
                {
                    var task = _initTask;

                    // Reset the state.
                    _initTask = null;
                    _cts?.Dispose();

                    if (task.IsFaulted)
                    {
                        throw task.Exception.InnerException;
                    }
                    else
                    {
                        return task.Result;
                    }
                }
                return null;
            }
        }

        /// <summary>
        /// Cancel the initialization task and dispose its result when it completes.
        /// </summary>
        /// <remarks>
        /// Any exceptions from the initialization task are silently dropped.
        /// </remarks>
        /// <returns>
        /// A <see cref="Task"/> that will complete when the initialization task has ended
        /// and its result has been disposed.
        /// </returns>
        public Task AbortInitTask()
        {
            if (_initTask == null)
            {
                return Task.CompletedTask;
            }

            _cts?.Cancel();
            var capturedCts = _cts;
            var res = _initTask.ContinueWith(task =>
            {
                if (task.Status == TaskStatus.RanToCompletion)
                {
                    task.Result?.Dispose();
                }
                capturedCts?.Dispose();
            }
            );
            _initTask = null;
            _cts = null;
            return res;
        }
    }
}
