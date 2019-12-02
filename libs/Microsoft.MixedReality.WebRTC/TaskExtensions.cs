// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.MixedReality.WebRTC
{
    /// <summary>
    /// Collection of extension methods for <xref href="System.Threading.Tasks.Task"/>.
    /// </summary>
    public static class TaskExtensions
    {
        ///// <summary>
        ///// Gracefully allows a task to continue running without loosing any exceptions thrown or requireing to await it.
        ///// </summary>
        ///// <param name="task">The task that should be wrapped.</param>
        //public static async void FireAndForget(this Task task)
        //{
        //    try
        //    {
        //        await task.IgnoreCancellation().ConfigureAwait(false);
        //    }
        //    catch (Exception ex)
        //    {
        //        UnityEngine.Debug.LogError("Encountered an exception with a FireAndForget task.");
        //        UnityEngine.Debug.LogException(ex);
        //    }
        //}


        ///// <summary>
        ///// Gracefully allows a task to continue running without loosing any exceptions thrown or requireing to await it.
        ///// </summary>
        ///// <param name="task">The task that should be wrapped.</param>
        //public static async void FireAndForget<T>(this Task<T> task)
        //{
        //    try
        //    {
        //        await task.IgnoreCancellation().ConfigureAwait(false);
        //    }
        //    catch (Exception ex)
        //    {
        //        UnityEngine.Debug.LogError("Encountered an exception with a FireAndForget task.");
        //        UnityEngine.Debug.LogException(ex);
        //    }
        //}


        /// <summary>
        /// Prevents <see xref="TaskCanceledException"/> or <see xref="OperationCanceledException"/> from trickling up.
        /// </summary>
        /// <param name="task">The task to ignore exceptions for.</param>
        /// <returns>A wrapping task for the given task.</returns>
        public static Task IgnoreCancellation(this Task task)
        {
            return task.ContinueWith(t =>
            {
                if (t.IsFaulted)
                {
                    // This will rethrow any remaining exceptions, if any.
                    t.Exception.Handle(ex => ex is OperationCanceledException);
                } // else do nothing
            }, TaskContinuationOptions.ExecuteSynchronously);
        }

        /// <summary>
        /// Prevents <see xref="TaskCanceledException"/> or <see xref="OperationCanceledException"/> from trickling up.
        /// </summary>
        /// <typeparam name="T">The result type of the Task.</typeparam>
        /// <param name="task">The task to ignore exceptions for.</param>
        /// <param name="defaultCancellationReturn">The default value to return in case the task is cancelled.</param>
        /// <returns>A wrapping task for the given task.</returns>
        public static Task<T> IgnoreCancellation<T>(this Task<T> task, T defaultCancellationReturn = default(T))
        {
            return task.ContinueWith(t =>
            {
                if (t.IsFaulted)
                {
                    // This will rethrow any remaining exceptions, if any.
                    t.Exception.Handle(ex => ex is OperationCanceledException);
                    return defaultCancellationReturn;
                }

                return t.IsCanceled ? defaultCancellationReturn : t.Result;
            }, TaskContinuationOptions.ExecuteSynchronously);
        }

        /// <summary>
        /// A simple helper to enable "awaiting" a <see xref="CancellationToken"/> by creating a task wrapping it.
        /// </summary>
        /// <param name="cancellationToken">The <see xref="CancellationToken"/> to await.</param>
        /// <returns>The task that can be awaited.</returns>
        public static Task AsTask(this CancellationToken cancellationToken) => Task.Delay(-1, cancellationToken);

        /// <summary>
        /// The task will be awaited until the cancellation token is triggered. (await task unless cancelled).
        /// </summary>
        /// <remarks>This is different from cancelling the task. The use case is to enable a calling method 
        /// bow out of the await that it can't cancel, but doesn't require completion/cancellation in order to cancel it's own execution.</remarks>
        /// <param name="task">The task to await.</param>
        /// <param name="cancellationToken">The cancellation token to stop awaiting.</param>
        /// <returns>The task that can be awaited unless the cancellation token is triggered.</returns>
        public static Task Unless(this Task task, CancellationToken cancellationToken) => Task.WhenAny(task, cancellationToken.AsTask());
    }
}
