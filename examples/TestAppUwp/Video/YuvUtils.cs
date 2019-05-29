// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Numerics;
using System.Runtime.InteropServices;

namespace TestAppUwp.Video
{
    public static class YuvUtils
    {
        public static Vector3 Yuv2rgb(Vector3 yuv)
        {
            // The YUV to RBA conversion, please refer to: http://en.wikipedia.org/wiki/YUV
            // Y'UV420sp (NV21) to RGB conversion (Android) section.
            float y_value = yuv.X;
            float u_value = yuv.Y;
            float v_value = yuv.Z;
            float r = y_value + 1.370705f * (v_value - 0.5f);
            float g = y_value - 0.698001f * (v_value - 0.5f) - (0.337633f * (u_value - 0.5f));
            float b = y_value + 1.732446f * (u_value - 0.5f);
            return new Vector3(r, g, b);
        }

        //public static void Yuv2rgb(byte y, byte u, byte v, out byte r, out byte g, out byte b)
        //{
        //    // The YUV to RBA conversion, please refer to: http://en.wikipedia.org/wiki/YUV
        //    // Y'UV420sp (NV21) to RGB conversion (Android) section.
        //    float uf = u - 128.0f;
        //    float vf = 
        //    float r = y + 1.370705f * (v - 128);
        //    float g = y - 0.698001f * (v - 128) - (0.337633f * (u - 0.5f));
        //    float b = y + 1.732446f * (u - 128);
        //    return new Vector3(r, g, b);
        //}

        public static void CopyYuvToBufferAsRgb(IntPtr dataY, IntPtr dataU, IntPtr dataV, int strideY, int strideU, int strideV, uint width, uint height, byte[] buffer)
        {
            unsafe
            {
                byte* ptrY = (byte*)dataY.ToPointer();
                byte* ptrU = (byte*)dataU.ToPointer();
                byte* ptrV = (byte*)dataV.ToPointer();
                int srcOffsetY = 0;
                int srcOffsetU = 0;
                int srcOffsetV = 0;
                int destOffset = 0;
                for (int i = 0; i < height; i++)
                {
                    srcOffsetY = i * strideY;
                    srcOffsetU = (i / 2) * strideU;
                    srcOffsetV = (i / 2) * strideV;
                    destOffset = i * (int)width * 4;
                    for (int j = 0; j < width; j += 2)
                    {
                        {
                            byte y = ptrY[srcOffsetY];
                            byte u = ptrU[srcOffsetU];
                            byte v = ptrV[srcOffsetV];
                            srcOffsetY++;
                            srcOffsetU++;
                            srcOffsetV++;
                            destOffset += 4;
                            Vector3 yuvF = new Vector3(y / 255.0f, u / 255.0f, v / 255.0f);
                            Vector3 rgbF = Yuv2rgb(yuvF);
                            byte r = (byte)(rgbF.X * 255.0f);
                            byte g = (byte)(rgbF.Y * 255.0f);
                            byte b = (byte)(rgbF.Z * 255.0f);
                            buffer[destOffset] = r;
                            buffer[destOffset + 1] = g;
                            buffer[destOffset + 2] = b;
                            buffer[destOffset + 3] = 0xff;

                            // use same u, v values
                            byte y2 = ptrY[srcOffsetY];
                            srcOffsetY++;
                            destOffset += 4;
                            Vector3 yuv2F = new Vector3(y2 / 255.0f, u / 255.0f, v / 255.0f);
                            Vector3 rgb2F = Yuv2rgb(yuv2F);
                            byte r2 = (byte)(rgb2F.X * 255.0f);
                            byte g2 = (byte)(rgb2F.Y * 255.0f);
                            byte b2 = (byte)(rgb2F.Z * 255.0f);
                            buffer[destOffset] = r2;
                            buffer[destOffset + 1] = g2;
                            buffer[destOffset + 2] = b2;
                            buffer[destOffset + 3] = 0xff;
                        }
                    }
                }
            }
        }

        public static void CopyYuvToBuffer(IntPtr dataY, IntPtr dataU, IntPtr dataV, int strideY, int strideU, int strideV, uint width, uint height, byte[] buffer)
        {
            unsafe
            {
                byte* ptrY = (byte*)dataY.ToPointer();
                byte* ptrU = (byte*)dataU.ToPointer();
                byte* ptrV = (byte*)dataV.ToPointer();
                int srcOffsetY = 0;
                int srcOffsetU = 0;
                int srcOffsetV = 0;
                int destOffset = 0;
                for (int i = 0; i < height; i++)
                {
                    srcOffsetY = i * strideY;
                    srcOffsetU = (i / 2) * strideU;
                    srcOffsetV = (i / 2) * strideV;
                    destOffset = i * (int)width * 4;
                    for (int j = 0; j < width; j += 2)
                    {
                        {
                            byte y = ptrY[srcOffsetY];
                            byte u = ptrU[srcOffsetU];
                            byte v = ptrV[srcOffsetV];
                            srcOffsetY++;
                            srcOffsetU++;
                            srcOffsetV++;
                            destOffset += 4;
                            buffer[destOffset] = y;
                            buffer[destOffset + 1] = u;
                            buffer[destOffset + 2] = v;
                            buffer[destOffset + 3] = 0xff;

                            // use same u, v values
                            byte y2 = ptrY[srcOffsetY];
                            srcOffsetY++;
                            destOffset += 4;
                            buffer[destOffset] = y2;
                            buffer[destOffset + 1] = u;
                            buffer[destOffset + 2] = v;
                            buffer[destOffset + 3] = 0xff;
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Copy YUV values from an I420 frame into an NV12 buffer.
        /// </summary>
        /// <param name="dataY">Raw pointer to the Y luma plane.</param>
        /// <param name="dataU">Raw pointer to the U chroma plane.</param>
        /// <param name="dataV">Raw pointer to the V chroma plane.</param>
        /// <param name="strideY">Row stride in bytes of the Y luma.</param>
        /// <param name="strideU">Row stride in bytes of the U chroma.</param>
        /// <param name="strideV">Row stride in bytes of the V chroma.</param>
        /// <param name="width">Width of the frame in pixels.</param>
        /// <param name="height">Height of the frame in pixels.</param>
        /// <param name="buffer">Output buffer to write the frame in NV12 format to.</param>
        /// <returns>The number of bytes written to the buffer.</returns>
        /// <remarks>
        /// See https://wiki.videolan.org/YUV#I420 and https://wiki.videolan.org/YUV#NV12.2FNV21 for details.
        /// Essentially I420 is a planar encoding with sequential planes for Y first, U next, and V last, while
        /// NV12 is semi-planar for 8-pixel blocks, interleaving YYYYYYYYUVUV.
        /// </remarks>
        public static uint CopyI420FrameToNv12Buffer(IntPtr dataY, IntPtr dataU, IntPtr dataV, int strideY, int strideU, int strideV, uint width, uint height, byte[] buffer)
        {
            uint numBlocks = width * height / 8;

            unsafe
            {
                byte* ptrY = (byte*)dataY.ToPointer();
                byte* ptrU = (byte*)dataU.ToPointer();
                byte* ptrV = (byte*)dataV.ToPointer();

                fixed (byte* ptrBuffer = buffer)
                {
                    byte* dst = ptrBuffer;

                    // Copy 8 Y values
                    //for (int i = 0; i < numBlocks; ++i)
                    //{
                    //    *dst++ = *ptrY++;
                    //    *dst++ = *ptrY++;
                    //    *dst++ = *ptrY++;
                    //    *dst++ = *ptrY++;
                    //    *dst++ = *ptrY++;
                    //    *dst++ = *ptrY++;
                    //    *dst++ = *ptrY++;
                    //    *dst++ = *ptrY++;
                    //}
                    {
                        // Optimized : 8 bytes at a time
                        ulong* dstUlong = (ulong*)dst;
                        ulong* srcUlong = (ulong*)ptrY;
                        for (uint i = 0; i < numBlocks; ++i)
                        {
                            *dstUlong++ = *srcUlong++;
                        }
                        dst = (byte*)dstUlong;
                    }

                    // Copy UVUV values
                    for (int i = 0; i < numBlocks; ++i)
                    {
                        *dst++ = *ptrU++;
                        *dst++ = *ptrV++;
                        *dst++ = *ptrU++;
                        *dst++ = *ptrV++;
                    }
                }
            }

            return (numBlocks * 12); // == (width * height * 3 / 2)
        }

        public unsafe static uint CopyI420FrameToI420Buffer(IntPtr dataY, IntPtr dataU, IntPtr dataV, int strideY, int strideU, int strideV, uint width, uint height, byte[] buffer)
        {
            uint pixelCount = width * height;
            uint byteSize = (pixelCount * 12 / 8);

            bool hasCompactY = (strideY == width);
            bool hasCompactU = (strideU == width / 2);
            bool hasCompactV = (strideV == width / 2);
            bool hasSequentialYU = (dataU.ToInt64() == dataY.ToInt64() + pixelCount);
            bool hasSequentialUV = (dataV.ToInt64() == dataU.ToInt64() + pixelCount / 4);

            // Fast path - compact layout
            if (hasCompactY && hasCompactU && hasCompactV && hasSequentialYU && hasSequentialUV)
            {
                Marshal.Copy(dataY, buffer, 0, (int)byteSize);
                return byteSize;
            }

            // Slow path - individual copies
            if (hasCompactY && hasCompactU && hasCompactV)
            {
                // "Fast-slow" path - still compact buffers, just not sequential
                int yByteSize = (int)pixelCount;
                int uvByteSize = (int)(pixelCount / 4);
                Marshal.Copy(dataY, buffer, 0, yByteSize);
                Marshal.Copy(dataU, buffer, yByteSize, uvByteSize);
                Marshal.Copy(dataV, buffer, yByteSize + uvByteSize, uvByteSize);
            }
            else
            {
                // Really slow path - copy each row byte by byte
                fixed (byte* ptrBuffer = buffer)
                {
                    byte* dst = ptrBuffer;
                    byte* src;
                    uint yWidth = width;
                    uint yHeight = height;
                    uint uvWidth = width / 2;
                    uint uvHeight = height / 2;

                    // Y
                    src = (byte*)dataY.ToPointer();
                    for (uint j = 0; j < yHeight; ++j)
                    {
                        byte* rowSrc = src;
                        for (uint i = 0; i < yWidth; ++i)
                        {
                            *dst++ = *rowSrc++;
                        }
                        src += strideY;
                    }

                    // U
                    src = (byte*)dataU.ToPointer();
                    for (uint j = 0; j < uvHeight; ++j)
                    {
                        byte* rowSrc = src;
                        for (uint i = 0; i < uvWidth; ++i)
                        {
                            *dst++ = *rowSrc++;
                        }
                        src += strideU;
                    }

                    // V
                    src = (byte*)dataV.ToPointer();
                    for (uint j = 0; j < uvHeight; ++j)
                    {
                        byte* rowSrc = src;
                        for (uint i = 0; i < uvWidth; ++i)
                        {
                            *dst++ = *rowSrc++;
                        }
                        src += strideV;
                    }
                }
            }

            return byteSize;
        }
    }

    // For debugging : uniform color frame sample
    //public /*async*/ void Debug_GenerateUniformSampleNv12(Windows.Storage.Streams.Buffer buffer, MediaStreamSourceSampleRequestedEventArgs args, uint argb)
    //{
    //    // For debugging : random samples
    //    var sampleRequest = args.Request;

    //    using (var stream = new Windows.Storage.Streams.InMemoryRandomAccessStream())
    //    {
    //        using (var dataWriter = new Windows.Storage.Streams.DataWriter(stream))
    //        {
    //            dataWriter.UnicodeEncoding = Windows.Storage.Streams.UnicodeEncoding.Utf8;
    //            dataWriter.ByteOrder = Windows.Storage.Streams.ByteOrder.LittleEndian;
    //            var rand = new Random();

    //            // Y
    //            for (int j = 0; j < 480; ++j)
    //            {
    //                for (int i = 0; i < 640; ++i)
    //                {
    //                    //uint val = (uint)rand.Next();
    //                    //dataWriter.WriteByte((byte)(val & 0xFF));
    //                    dataWriter.WriteByte((byte)(((j / 480.0f) * 0.5f + (i / 640.0f) * 0.5f) * 255.0f));
    //                }
    //            }

    //            // U
    //            for (int j = 0; j < 480 / 2; ++j)
    //            {
    //                for (int i = 0; i < 640 / 2; ++i)
    //                {
    //                    //uint val = (uint)rand.Next();
    //                    //dataWriter.WriteByte((byte)(val & 0xFF));
    //                    dataWriter.WriteByte(128);
    //                }
    //            }

    //            // V
    //            for (int j = 0; j < 480 / 2; ++j)
    //            {
    //                for (int i = 0; i < 640 / 2; ++i)
    //                {
    //                    //uint val = (uint)rand.Next();
    //                    //dataWriter.WriteByte((byte)(val & 0xFF));
    //                    dataWriter.WriteByte(128);
    //                }
    //            }

    //            dataWriter.StoreAsync().AsTask().Wait();
    //            dataWriter.FlushAsync().AsTask().Wait();
    //            dataWriter.DetachStream();
    //        }
    //        stream.FlushAsync().AsTask().Wait();
    //        stream.Seek(0);
    //        uint byteSize = (uint)(640 * 480 + 640 * 480 / 2);
    //        stream.ReadAsync(buffer, byteSize, Windows.Storage.Streams.InputStreamOptions.None).AsTask().Wait();
    //        stream.FlushAsync().AsTask().Wait();
    //    }

    //    TimeSpan timestamp = TimeSpan.FromSeconds(frameCount / 30.0);
    //    var sample = MediaStreamSample.CreateFromBuffer(buffer, timestamp);
    //    sample.Duration = TimeSpan.FromSeconds(1.0 / 30.0);
    //    sampleRequest.Sample = sample;
    //    ++frameCount;
    //}

    public enum SampleKind
    {
        /// <summary>
        /// Uniform ARGB color (supports transparency).
        /// </summary>
        Uniform,

        /// <summary>
        /// Random opaque color (alpha = 1.0).
        /// </summary>
        Random
    }

    /// <summary>
    /// Utility to programatically generate a video frame sample, for debugging purpose.
    /// </summary>
    /// <param name="args">The sample request from the Media Foundation pipeline to serve with the generated sample.</param>
    /// <param name="sampleKind">The kind of sample to generate.</param>
    /// <param name="argb">The pixel color to use, if applicable (depends on <see cref="sampleKind"/>).</param>
    //public unsafe void GenerateFrameSample(MediaStreamSourceSampleRequestedEventArgs args, SampleKind sampleKind, uint argb)
    //{
    //    // Retrieve the size of the frame to generate
    //    var videoStreamDescriptor = (args.Request.StreamDescriptor as VideoStreamDescriptor);
    //    uint width = videoStreamDescriptor.EncodingProperties.Width;
    //    uint height = videoStreamDescriptor.EncodingProperties.Height;

    //    // Generate the frame content
    //    uint pixelSize = (width * height);
    //    uint byteSize = (pixelSize * 4);
    //    var byteBuffer = new byte[byteSize];
    //    switch (sampleKind)
    //    {
    //        case SampleKind.Uniform:
    //            fixed (byte* byteBufferPtr = byteBuffer)
    //            {
    //                uint* ptr = (uint*)byteBufferPtr;
    //                for (uint i = 0; i < pixelSize; ++i)
    //                {
    //                    *ptr++ = argb;
    //                }
    //            }
    //            break;

    //        case SampleKind.Random:
    //            var rand = new Random();
    //            fixed (byte* byteBufferPtr = byteBuffer)
    //            {
    //                uint* ptr = (uint*)byteBufferPtr;
    //                for (uint i = 0; i < pixelSize; ++i)
    //                {
    //                    // Random opaque color
    //                    int val = rand.Next();
    //                    *ptr++ = ((uint)val & 0xFFFFFFu) | 0xFF000000u;
    //                }
    //            }
    //            break;
    //    }

    //    // Calculate the timestamp of the sample
    //    TimeSpan timestamp = TimeSpan.FromSeconds(frameCount / 30.0);
    //    ++frameCount;

    //    // Get a sample
    //    var sample = streamBufferPool.Pop(byteSize, timestamp);
    //    sample.Duration = TimeSpan.FromSeconds(1.0 / 30.0);

    //    // Copy the frame data into the sample's buffer
    //    byteBuffer.CopyTo(0, sample.Buffer, 0, (int)byteSize);

    //    // Return the requested sample
    //    args.Request.Sample = sample;
    //}
}
