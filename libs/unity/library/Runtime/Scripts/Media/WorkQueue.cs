// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Threading;
using UnityEngine;

namespace Microsoft.MixedReality.WebRTC.Unity
{
    /// <summary>
    /// Base class providing some utility work queue to dispatch free-threaded actions
    /// to the main Unity application thread, where the handler(s) can safely access
    /// Unity objects.
    /// </summary>
    public class WorkQueue : MonoBehaviour
    {
        /// <summary>
        /// Check if the current thread is the main Unity application thread where
        /// it is safe to access Unity objects.
        /// </summary>
        /// <remarks>
        /// Should be only called once the object is awake.
        /// </remarks>
        public bool IsMainAppThread
        {
            get
            {
                UnityEngine.Debug.Assert(_mainAppThread != null, "This method can only be called once the object is awake.");
                return Thread.CurrentThread == _mainAppThread;
            }
        }

        /// <summary>
        /// Ensure the current method is running on the main Unity application thread.
        /// </summary>
        /// <remarks>
        /// Should be only called once the object is awake.
        /// </remarks>
        [Conditional("UNITY_ASSERTIONS")]
        public void EnsureIsMainAppThread()
        {
            UnityEngine.Debug.Assert(IsMainAppThread, "This method can only be called from the main Unity application thread.");
        }

        /// <summary>
        /// Invoke the specified action on the main Unity app thread.
        /// </summary>
        /// <param name="action">The action to execute.</param>
        /// <remarks>
        /// If this object is awake, and this method is called from the main Unity app thread,
        /// <paramref name="action"/> will be executed synchronously. Otherwise,
        /// <paramref name="action"/> will be called during the next call to this object's <see cref="Update"/>.
        /// </remarks>
        public void InvokeOnAppThread(Action action)
        {
            if (_mainAppThread != null && IsMainAppThread)
            {
                action();
            }
            else
            {
                _mainThreadWorkQueue.Enqueue(action);
            }
        }

        protected virtual void Awake()
        {
            // Awake() is always called from the main Unity app thread
            _mainAppThread = Thread.CurrentThread;
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

        /// <summary>
        /// Reference to the main Unity application thread where it is safe to access Unity objects.
        /// </summary>
        private Thread _mainAppThread = null;
    }
}
