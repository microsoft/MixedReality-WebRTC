// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;

namespace Microsoft.MixedReality.WebRTC
{
    /// <summary>
    /// Single video frame encoded in I420A format (triplanar YUV + alpha, 18 bits per pixel).
    /// See https://wiki.videolan.org/YUV/#I420 for details.
    /// </summary>
    /// 
    /// <remarks>
    /// Note: ref struct is C# 7.2, available from .NET Core 2.1 (apparently).
    /// And MRTK supports Unity 2018.3 which comes with the Roslyn compiler
    /// and C# 7.3 support.
    /// https://blogs.unity3d.com/2018/12/13/introducing-unity-2018-3/
    /// This is an optimization to avoid heap allocation on each frame while
    /// having a nicer-to-use container for a frame. It can probably be removed
    /// and/or refactored out if this is an issue.
    /// </remarks>
    public ref struct I420AVideoFrame
    {
        public uint width;
        public uint height;
        public IntPtr dataY;
        public IntPtr dataU;
        public IntPtr dataV;
        public IntPtr dataA;
        public int strideY;
        public int strideU;
        public int strideV;
        public int strideA;
    }

    /// <summary>
    /// Delegate used for events when an I420-encoded video frame has been produced
    /// and is ready for consumption.
    /// </summary>
    /// <param name="frame">The newly available I420-encoded video frame.</param>
    public delegate void I420VideoFrameDelegate(I420AVideoFrame frame);

    /// <summary>
    /// Single video frame encoded in ARGB interleaved format (32 bits per pixel).
    /// </summary>
    public ref struct ARGBVideoFrame
    {
        public uint width;
        public uint height;
        public IntPtr data;
        public int stride;
    }

    /// <summary>
    /// Delegate used for events when an ARGB-encoded video frame has been produced
    /// and is ready for consumption.
    /// </summary>
    /// <param name="frame">The newly available ARGB-encoded video frame.</param>
    public delegate void ARGBVideoFrameDelegate(ARGBVideoFrame frame);
}
