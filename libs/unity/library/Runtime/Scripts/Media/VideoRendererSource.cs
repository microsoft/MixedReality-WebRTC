// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Microsoft.MixedReality.WebRTC.Unity
{
    /// <summary>
    /// Base class for video-producing entities which can be rendered using a <see cref="VideoRenderer"/>.
    /// 
    /// This class is only used because Unity requires a common base class between <see cref="VideoTrackSource"/>
    /// and <see cref="VideoReceiver"/> to be able to serialize a polymorphic property to either of those producers
    /// in <see cref="VideoRenderer"/>. Ideally <see cref="VideoRenderer"/> would use an <see cref="IVideoSource"/>
    /// property instead, but polymorphic serialization of interfaces is not supported (there is partial support in
    /// Unity 2019.3 but some things like the component picking window are still broken).
    /// </summary>
    /// <seealso cref="VideoTrackSource"/>
    /// <seealso cref="VideoReceiver"/>
    /// <seealso cref="VideoRenderer"/>
    public abstract class VideoRendererSource : WorkQueue
    {
    }
}
