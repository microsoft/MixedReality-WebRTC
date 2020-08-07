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
        /// <returns>
        /// The result of the initialization task if there is one and it has just successfully completed;
        /// <c>null</c> if there is no task or if it has thrown an exception.
        /// </returns>
        public T Result
        {
            get
            {
                T result = null;
                if (_initTask != null && _initTask.IsCompleted)
                {
                    if (!_initTask.IsFaulted)
                    {
                        result = _initTask.Result;
                    }
                    _initTask = null;
                    _cts?.Dispose();
                }
                return result;
            }
        }

        /// <summary>
        /// Cancel the initialization task and dispose its result when it completes.
        /// </summary>
        /// <remarks>
        /// This waits synchronously for the end of the initialization task. Note that this may
        /// cause deadlocks if the task requires the same thread to finish.
        ///
        /// Any exceptions from the initialization task are silently dropped; the task itself
        /// must take care of reporting.
        /// </remarks>
        public void AbortInitTask()
        {
            if (_initTask != null)
            {
                try
                {
                    _cts?.Cancel();
                    _initTask.Result?.Dispose();
                }
                catch(Exception)
                {
                    // Ignore; rely on _initTask itself to do the necessary reporting.
                }
            }
            _cts?.Dispose();
            _cts = null;
            _initTask = null;
        }
    }
}
