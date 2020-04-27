// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Concurrent;
using UnityEngine;

namespace Microsoft.MixedReality.WebRTC.Unity
{
    /// <summary>
    /// Base class for media data source components producing some media frames
    /// 
    /// This component encapsulates one of 4 types of media tracks, depending on the actual
    /// class deriving from it:
    /// - <see cref="AudioSender"/> : produces audio frames locally, to be sent to the remote peer
    /// - <see cref="AudioReceiver"/> : receives audio frames from the remote peer
    /// - <see cref="VideoSender"/> : produces video frames locally, to be sent to the remote peer
    /// - <see cref="VideoReceiver"/> : receives video frames from the remote peer
    /// 
    /// The media source can be conveniently played using a <see cref="MediaPlayer"/>.
    /// </summary>
    /// <seealso cref="AudioSender"/>
    /// <seealso cref="AudioReceiver"/>
    /// <seealso cref="VideoSender"/>
    /// <seealso cref="VideoReceiver"/>
    /// <seealso cref="MediaPlayer"/>
    public abstract class MediaSource : MonoBehaviour
    {
        /// <summary>
        /// Kind of media (audio or video) the source produces.
        /// This field is immutable; a media source cannot change kind after its creation.
        /// </summary>
        public readonly MediaKind MediaKind;

        /// <summary>
        /// Internal queue used to marshal work back to the main Unity app thread, which is the
        /// only thread where access to Unity objects is allowed. This is used by free-threaded
        /// callbacks to defer some of their work, generally a final user notification via an event.
        /// </summary>
        protected ConcurrentQueue<Action> _mainThreadWorkQueue = new ConcurrentQueue<Action>();

        /// <summary>
        /// Create a new media source of the given <see cref="MediaKind"/>.
        /// </summary>
        /// <param name="mediaKind">The media kind of the source.</param>
        public MediaSource(MediaKind mediaKind)
        {
            MediaKind = mediaKind;
        }

        /// <summary>
        /// Implementation of <a href="https://docs.unity3d.com/ScriptReference/MonoBehaviour.Update.html">MonoBehaviour.Update</a>
        /// to execute from the main Unity app thread any background work enqueued from free-threaded callbacks.
        /// </summary>
        protected void Update()
        {
            // Execute any pending work enqueued by background tasks
            while (_mainThreadWorkQueue.TryDequeue(out Action workload))
            {
                workload();
            }
        }
    }
}
