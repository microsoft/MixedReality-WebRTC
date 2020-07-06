// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using UnityEngine;

namespace Microsoft.MixedReality.WebRTC.Unity
{
    /// <summary>
    /// This component represents an audio track source generating audio frames for one or more
    /// audio tracks.
    /// </summary>
    /// <seealso cref="MicrophoneSource"/>
    public abstract class AudioTrackSource : MonoBehaviour, IMediaTrackSource, IMediaTrackSourceInternal
    {
        /// <summary>
        /// Audio track source object from the underlying C# library that this component encapsulates.
        ///
        /// The object is owned by this component, which will create it and dispose of it automatically.
        /// </summary>
        public WebRTC.AudioTrackSource Source { get; protected set; } = null;

        /// <summary>
        /// List of audio media lines using this source.
        /// </summary>
        public IReadOnlyList<MediaLine> MediaLines => _mediaLines;



        #region IMediaTrackSource

        /// <inheritdoc/>
        MediaKind IMediaTrackSource.MediaKind => MediaKind.Audio;

        #endregion


        private readonly List<MediaLine> _mediaLines = new List<MediaLine>();

        protected virtual void OnDisable()
        {
            if (Source != null)
            {
                // Notify media lines using that source. OnSourceDestroyed() calls
                // OnMediaLineRemoved() which will modify the collection.
                while (_mediaLines.Count > 0)
                {
                    _mediaLines[_mediaLines.Count - 1].OnSourceDestroyed();
                }

                // Audio track sources are disposable objects owned by the user (this component)
                Source.Dispose();
                Source = null;
            }
        }

        void IMediaTrackSourceInternal.OnAddedToMediaLine(MediaLine mediaLine)
        {
            Debug.Assert(!_mediaLines.Contains(mediaLine));
            _mediaLines.Add(mediaLine);
        }

        void IMediaTrackSourceInternal.OnRemoveFromMediaLine(MediaLine mediaLine)
        {
            bool removed = _mediaLines.Remove(mediaLine);
            Debug.Assert(removed);
        }
    }
}
