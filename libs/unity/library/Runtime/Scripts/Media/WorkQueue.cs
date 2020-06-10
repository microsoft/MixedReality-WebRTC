// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Concurrent;
using UnityEngine;

namespace Microsoft.MixedReality.WebRTC.Unity
{
    /// <summary>
    /// Base class providing some utility work queue to dispatch free-threaded actions
    /// to the main Unity application thread, where the handler(s) can safely access
    /// Unity objects.
    /// </summary>
    public abstract class WorkQueue : MonoBehaviour
    {
        /// <summary>
        /// Invoke the specified action on the main Unity app thread.
        /// </summary>
        /// <param name="action">The action to execute.</param>
        protected void InvokeOnAppThread(Action action)
        {
            _mainThreadWorkQueue.Enqueue(action);
        }

        /// <summary>
        /// Implementation of <a href="https://docs.unity3d.com/ScriptReference/MonoBehaviour.Update.html">MonoBehaviour.Update</a>
        /// to execute from the main Unity app thread any background work enqueued from free-threaded callbacks.
        /// </summary>
        protected virtual void Update()
        {
            // Execute any pending work enqueued by background tasks
            while (_mainThreadWorkQueue.TryDequeue(out Action workload))
            {
                workload();
            }
        }

        /// <summary>
        /// Internal queue used to marshal work back to the main Unity app thread, which is the
        /// only thread where access to Unity objects is allowed. This is used by free-threaded
        /// callbacks to defer some of their work, generally a final user notification via an event.
        /// </summary>
        private readonly ConcurrentQueue<Action> _mainThreadWorkQueue = new ConcurrentQueue<Action>();
    }
}
