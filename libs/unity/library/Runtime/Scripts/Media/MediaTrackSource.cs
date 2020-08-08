// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Collections.Generic;
using UnityEngine;

namespace Microsoft.MixedReality.WebRTC.Unity
{
    /// <summary>
    /// Base class for media track source components producing some media frames locally.
    /// </summary>
    /// <seealso cref="AudioTrackSource"/>
    /// <seealso cref="VideoTrackSource"/>
    public abstract class MediaTrackSource : MonoBehaviour
    {
        /// <summary>
        /// Media kind of the track source.
        /// </summary>
        public abstract MediaKind MediaKind { get; }

        /// <summary>
        /// Indicates if the source is currently producing frames.
        /// </summary>
        public abstract bool IsLive { get; }

        /// <summary>
        /// List of audio media lines using this source.
        /// </summary>
        /// <remarks>
        /// Note that a connected <see cref="MediaLine"/> will be added to this only if the owning
        /// <see cref="PeerConnection"/> is awake. A <see cref="MediaLine"/> will be automatically
        /// removed if the owning <see cref="PeerConnection"/> is destroyed.
        /// </remarks>
        public IReadOnlyList<MediaLine> MediaLines => _mediaLines;
        private readonly List<MediaLine> _mediaLines = new List<MediaLine>();

        internal void OnAddedToMediaLine(MediaLine mediaLine)
        {
            Debug.Assert(!_mediaLines.Contains(mediaLine));
            _mediaLines.Add(mediaLine);
        }

        internal void OnRemovedFromMediaLine(MediaLine mediaLine)
        {
            bool removed = _mediaLines.Remove(mediaLine);
            Debug.Assert(removed);
        }

        protected void AttachToMediaLines()
        {
            foreach (var ml in _mediaLines)
            {
                ml.AttachSource();
            }
        }

        protected void DetachFromMediaLines()
        {
            foreach (var ml in _mediaLines)
            {
                ml.DetachSource();
            }
        }
    }
}
