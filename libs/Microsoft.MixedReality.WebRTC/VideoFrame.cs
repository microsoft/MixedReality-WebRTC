// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using Microsoft.MixedReality.WebRTC.Interop;

namespace Microsoft.MixedReality.WebRTC
{
    /// <summary>
    /// Single video frame encoded in I420A format (triplanar YUV with optional alpha plane).
    /// See e.g. https://wiki.videolan.org/YUV/#I420 for details.
    ///
    /// The I420 format uses chroma downsampling in both directions, resulting in 12 bits per
    /// pixel. With the optional alpha plane, the size increases to 20 bits per pixel.
    /// </summary>
    /// <remarks>
    /// The use of <c>ref struct</c> is an optimization to avoid heap allocation on each frame while
    /// having a nicer-to-use container to pass a frame accross methods.
    ///
    /// The alpha plane is generically supported in this data structure, but actual support
    /// in the video tracks depend on the underlying implementation and the video codec used,
    /// and is generally not available.
    /// </remarks>
    public ref struct I420AVideoFrame
    {
        /// <summary>
        /// Frame width, in pixels.
        /// </summary>
        public uint width;

        /// <summary>
        /// Frame height, in pixels.
        /// </summary>
        public uint height;

        /// <summary>
        /// Pointer to the Y plane buffer.
        /// </summary>
        public IntPtr dataY;

        /// <summary>
        /// Pointer to the U plane buffer.
        /// </summary>
        public IntPtr dataU;

        /// <summary>
        /// Pointer to the V plane buffer.
        /// </summary>
        public IntPtr dataV;

        /// <summary>
        /// Optional pointer to the alpha plane buffer, if any, or <c>null</c> if the frame has no alpha plane.
        /// </summary>
        public IntPtr dataA;

        /// <summary>
        /// Stride in bytes between rows of the Y plane.
        /// </summary>
        public int strideY;

        /// <summary>
        /// Stride in bytes between rows of the U plane.
        /// </summary>
        public int strideU;

        /// <summary>
        /// Stride in bytes between rows of the V plane.
        /// </summary>
        public int strideV;

        /// <summary>
        /// Stride in bytes between rows of the A plane, if present.
        /// </summary>
        public int strideA;

        /// <summary>
        /// Copy the frame content to a <xref href="System.Byte"/>[] buffer as a contiguous block of memory
        /// containing the Y, U, and V planes one after another, and the alpha plane at the end if present.
        /// </summary>
        /// <param name="buffer">The destination buffer to copy the frame to.</param>
        public void CopyTo(byte[] buffer)
        {
            unsafe
            {
                fixed (void* ptr = buffer)
                {
                    // Destination buffer is packed and contiguous
                    ulong dstSizeYA = (ulong)width * height;
                    ulong dstSizeUV = dstSizeYA / 4;
                    int dstStrideYA = (int)width;
                    int dstStrideUV = dstStrideYA / 2;

                    // Note : System.Buffer.MemoryCopy() essentially does the same (without stride), but gets transpiled by IL2CPP
                    // into the C++ corresponding to the IL instead of a single memcpy() call. This results in a large overhead,
                    // especially in Debug config where one can lose 5-10 FPS just because of this.
                    void* dst = ptr;
                    Utils.MemCpyStride(dst, dstStrideYA, (void*)dataY, strideY, (int)width, (int)height);
                    dst = (void*)((ulong)dst + dstSizeYA);
                    Utils.MemCpyStride(dst, dstStrideUV, (void*)dataU, strideU, (int)width / 2, (int)height / 2);
                    dst = (void*)((ulong)dst + dstSizeUV);
                    Utils.MemCpyStride(dst, dstStrideUV, (void*)dataV, strideV, (int)width / 2, (int)height / 2);
                    if (dataA.ToPointer() != null)
                    {
                        dst = (void*)((ulong)dst + dstSizeUV);
                        Utils.MemCpyStride(dst, dstStrideYA, (void*)dataA, strideA, (int)width, (int)height);
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
    public delegate void I420AVideoFrameDelegate(I420AVideoFrame frame);

    /// <summary>
    /// Single video frame encoded in ARGB interleaved format (32 bits per pixel).
    ///
    /// The ARGB components are in the order of a little endian 32-bit integer, so
    /// 0xAARRGGBB, or (B, G, R, A) as a sequence of bytes in memory with B first
    /// and A last.
    /// </summary>
    /// <remarks>
    /// The use of <c>ref struct</c> is an optimization to avoid heap allocation on each frame while
    /// having a nicer-to-use container to pass a frame accross methods.
    /// </remarks>
    public ref struct Argb32VideoFrame
    {
        /// <summary>
        /// Frame width, in pixels.
        /// </summary>
        public uint width;

        /// <summary>
        /// Frame height, in pixels.
        /// </summary>
        public uint height;

        /// <summary>
        /// Pointer to the data buffer containing the ARBG data for each pixel.
        /// </summary>
        public IntPtr data;

        /// <summary>
        /// Stride in bytes between the ARGB rows.
        /// </summary>
        public int stride;
    }

    /// <summary>
    /// Delegate used for events when an ARGB-encoded video frame has been produced
    /// and is ready for consumption.
    /// </summary>
    /// <param name="frame">The newly available ARGB-encoded video frame.</param>
    public delegate void Argb32VideoFrameDelegate(Argb32VideoFrame frame);
}
