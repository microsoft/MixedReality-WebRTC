// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Collections.Generic;
using UnityEngine;

namespace Microsoft.MixedReality.WebRTC.Unity
{
    public abstract class AudioReceiver : WorkQueue, IMediaReceiver, IMediaReceiverInternal
    {
        /// <summary>
        /// List of audio media lines using this source.
        /// </summary>
        public IReadOnlyList<MediaLine> MediaLines => _mediaLines;

        /// <summary>
        /// Event raised when the audio stream started.
        ///
        /// When this event is raised, the followings are true:
        /// - The <see cref="Track"/> property is a valid remote audio track.
        /// - The <see cref="MediaReceiver.IsLive"/> property is <c>true</c>.
        /// - The <see cref="IsStreaming"/> will become <c>true</c> just after the event
        ///   is raised, by design.
        /// </summary>
        /// <remarks>
        /// This event is raised from the main Unity thread to allow Unity object access.
        /// </remarks>
        public readonly AudioStreamStartedEvent AudioStreamStarted = new AudioStreamStartedEvent();

        /// <summary>
        /// Event raised when the audio stream stopped.
        ///
        /// When this event is raised, the followings are true:
        /// - The <see cref="MediaReceiver.IsLive"/> property is <c>false</c>.
        /// - The <see cref="IsStreaming"/> has just become <c>false</c> right before the event
        ///   was raised, by design.
        /// </summary>
        /// <remarks>
        /// This event is raised from the main Unity thread to allow Unity object access.
        /// </remarks>
        public readonly AudioStreamStoppedEvent AudioStreamStopped = new AudioStreamStoppedEvent();


        #region IMediaReceiver interface

        /// <inheritdoc/>
        MediaKind IMediaReceiver.MediaKind => MediaKind.Audio;

        /// <inheritdoc/>
        bool IMediaReceiver.IsLive => _isLive;

        /// <inheritdoc/>
        Transceiver IMediaReceiver.Transceiver => _transceiver;

        #endregion

        protected bool _isLive = false;
        private Transceiver _transceiver = null;
        private readonly List<MediaLine> _mediaLines = new List<MediaLine>();


        #region IMediaReceiverInternal interface

        /// <inheritdoc/>
        void IMediaReceiverInternal.OnAddedToMediaLine(MediaLine mediaLine)
        {
            Debug.Assert(!_mediaLines.Contains(mediaLine));
            _mediaLines.Add(mediaLine);
        }

        /// <inheritdoc/>
        void IMediaReceiverInternal.OnRemoveFromMediaLine(MediaLine mediaLine)
        {
            bool removed = _mediaLines.Remove(mediaLine);
            Debug.Assert(removed);
        }

        void IMediaReceiverInternal.OnPaired(MediaTrack track) => OnPaired(track);
        void IMediaReceiverInternal.OnUnpaired(MediaTrack track) => OnUnpaired(track);

        /// <inheritdoc/>
        internal abstract void OnPaired(MediaTrack track);

        /// <inheritdoc/>
        internal abstract void OnUnpaired(MediaTrack track);

        /// <inheritdoc/>
        void IMediaReceiverInternal.AttachToTransceiver(Transceiver transceiver)
        {
            Debug.Assert((_transceiver == null) || (_transceiver == transceiver));
            _transceiver = transceiver;
        }

        /// <inheritdoc/>
        void IMediaReceiverInternal.DetachFromTransceiver(Transceiver transceiver)
        {
            Debug.Assert((_transceiver == null) || (_transceiver == transceiver));
            _transceiver = null;
        }

        #endregion
    }
}
