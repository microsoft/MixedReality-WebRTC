// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;

namespace Microsoft.MixedReality.WebRTC
{
    /// <summary>
    /// Single video frame encoded in I420A format (triplanar YUV + alpha, 18 bits per pixel).
    /// See e.g. https://wiki.videolan.org/YUV/#I420 for details.
    /// </summary>
    /// <remarks>
    /// The use of ref struct is an optimization to avoid heap allocation on each frame while
    /// having a nicer-to-use container for a frame.
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

        public void CopyTo(byte[] buffer)
        {
            unsafe
            {
                fixed (void* ptr = buffer)
                {
                    // Note : System.Buffer.MemoryCopy() essentially does the same (without stride), but gets transpiled by IL2CPP
                    // into the C++ corresponding to the IL instead of a single memcpy() call. This results in a large overhead,
                    // especially in Debug config where one can lose 5-10 FPS just because of this.
                    void* dst = ptr;
                    ulong sizeY = (ulong)strideY * height;
                    PeerConnection.MemCpyStride(dst, strideY, (void*)dataY, strideY, (int)width, (int)height);
                    dst = (void*)((ulong)dst + sizeY);
                    ulong sizeU = (ulong)strideU * height / 2;
                    PeerConnection.MemCpyStride(dst, strideU, (void*)dataU, strideU, (int)width / 2, (int)height / 2);
                    dst = (void*)((ulong)dst + sizeU);
                    ulong sizeV = (ulong)strideV * height / 2;
                    PeerConnection.MemCpyStride(dst, strideV, (void*)dataV, strideV, (int)width / 2, (int)height / 2);
                    if (dataA.ToPointer() != null)
                    {
                        dst = (void*)((ulong)dst + sizeV);
                        PeerConnection.MemCpyStride(dst, strideA, (void*)dataA, strideA, (int)width, (int)height);
                    }
                }
            }
        }
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
    /// <remarks>
    /// The use of ref struct is an optimization to avoid heap allocation on each frame while
    /// having a nicer-to-use container for a frame.
    /// </remarks>
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
