// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Microsoft.MixedReality.WebRTC.Unity
{
    /// <summary>
    /// Interface for media track source components producing some media frames locally.
    /// </summary>
    /// <seealso cref="AudioTrackSource"/>
    /// <seealso cref="VideoTrackSource"/>
    public interface IMediaTrackSource
    {
        /// <summary>
        /// Media kind of the track source.
        /// </summary>
        MediaKind MediaKind { get; }
    }

    internal interface IMediaTrackSourceInternal
    {
        void OnAddedToMediaLine(MediaLine mediaLine);
        void OnRemoveFromMediaLine(MediaLine mediaLine);
    }
}
