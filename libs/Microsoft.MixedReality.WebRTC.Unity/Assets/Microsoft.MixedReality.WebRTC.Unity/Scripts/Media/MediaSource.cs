// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using UnityEngine;

namespace Microsoft.MixedReality.WebRTC.Unity
{
    /// <summary>
    /// This component represents a media receiver added as a media track to an
    /// existing WebRTC peer connection by a remote peer and received locally.
    /// The media track can optionally be displayed locally with a <see cref="MediaPlayer"/>.
    /// </summary>
    public abstract class MediaSource : MonoBehaviour
    {
        /// <summary>
        /// Is the source currently playing?
        /// The concept of _playing_ is described in the <see cref="Play"/> function.
        /// </summary>
        /// <seealso cref="Play"/>
        /// <seealso cref="Stop()"/>
        public bool IsPlaying { get; private set; }

        /// <summary>
        /// Internal queue used to marshal work back to the main Unity thread.
        /// </summary>
        protected ConcurrentQueue<Action> _mainThreadWorkQueue = new ConcurrentQueue<Action>();

        /// <summary>
        /// Manually start playback of the remote video feed by registering some listeners
        /// to the peer connection and starting to enqueue video frames as they become ready.
        /// 
        /// Because the WebRTC implementation uses a push model, calling <see cref="Play"/> does
        /// not necessarily start producing frames immediately. Instead, this starts listening for
        /// incoming frames from the remote peer. When a track is actually added by the remote peer
        /// and received locally, the <see cref="VideoSource.VideoStreamStarted"/> event is fired, and soon
        /// after frames will start being available for rendering in the internal frame queue. Note that
        /// this event may be fired before <see cref="Play"/> is called, in which case frames are
        /// produced immediately.
        /// 
        /// If <see cref="AutoPlayOnAdded"/> is <c>true</c> then this is called automatically
        /// as soon as the peer connection is initialized.
        /// </summary>
        /// <remarks>
        /// This is only valid while the peer connection is initialized, that is after the
        /// <see cref="PeerConnection.OnInitialized"/> event was fired.
        /// </remarks>
        /// <seealso cref="Stop()"/>
        /// <seealso cref="IsPlaying"/>
        public async Task PlayAsync()
        {
            if (!IsPlaying)
            {
                await DoStartMediaPlaybackAsync();
                IsPlaying = true;
            }
        }

        /// <summary>
        /// Stop playback of the remote video feed and unregister the handler listening to remote
        /// video frames.
        /// 
        /// Note that this is independent of whether or not a remote track is actually present.
        /// In particular this does not fire the <see cref="VideoSource.VideoStreamStopped"/>, which corresponds
        /// to a track being made available to the local peer by the remote peer.
        /// </summary>
        /// <seealso cref="Play()"/>
        /// <seealso cref="IsPlaying"/>
        public void Stop()
        {
            if (IsPlaying)
            {
                DoStopMediaPlayback();
                IsPlaying = false;
            }
        }

        protected abstract Task DoStartMediaPlaybackAsync();
        protected abstract void DoStopMediaPlayback();

        /// <summary>
        /// Implementation of <a href="https://docs.unity3d.com/ScriptReference/MonoBehaviour.Update.html">MonoBehaviour.Update</a>
        /// to execute from the current Unity main thread any background work enqueued from free-threaded callbacks.
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
