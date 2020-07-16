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
    public abstract class AudioTrackSource : MediaTrackSource
    {
        /// <summary>
        /// Audio track source object from the underlying C# library that this component encapsulates.
        ///
        /// The object is owned by this component, which will create it and dispose of it automatically.
        /// </summary>
        public WebRTC.AudioTrackSource Source { get; private set; } = null;

        /// <inheritdoc/>
        public override MediaKind MediaKind => MediaKind.Audio;

        /// <inheritdoc/>
        public override bool IsLive => Source != null;

        protected void AttachSource(WebRTC.AudioTrackSource source)
        {
            Source = source;
            AttachToMediaLines();
        }

        protected void DisposeSource()
        {
            if (Source != null)
            {
                DetachFromMediaLines();

                // Audio track sources are disposable objects owned by the user (this component)
                Source.Dispose();
                Source = null;
            }
        }
    }
}
