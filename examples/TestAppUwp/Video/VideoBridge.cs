// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.MixedReality.WebRTC;
using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading;
using Windows.Media.Core;

namespace TestAppUwp.Video
{
    /// <summary>
    /// Bridge between a WebRTC video source and a Media Foundation video sink.
    /// Helps feeding a raw video frame retrieved from WebRTC (local or remote)
    /// to a Media Foundation pipeline for video rendering.
    /// </summary>
    public class VideoBridge
    {
        private StreamSamplePool _streamSamplePool = new StreamSamplePool(10);
        private object _deferralLock = new object();
        private byte[] _deferralBuffer = Array.Empty<byte>();
        private MediaStreamSourceSampleRequest _request = null;
        private MediaStreamSourceSampleRequestDeferral _deferral = null;
        private long _frameCount = 0;
        private VideoFrameQueue<I420AVideoFrameStorage> _frameQueue;
        private float _lateFrameStat = 0f; // new FrameQueueStat(100);

        /// <summary>
        /// Statistics for loaded frames.
        /// This represents frame received from WebRTC for later rendering, and
        /// should ideally be equal to <see cref="FramePresent"/>  in an optimal
        /// pipeline scenario.
        /// </summary>
        public float FrameLoad
        {
            get { return _frameQueue.QueuedFramesPerSecond; }
        }

        /// <summary>
        /// Statistics for presented frames.
        /// This represents frames being popped for rendering, and should ideally be
        /// equal to <see cref="FrameLoad"/> in an optimal pipeline scenario.
        /// </summary>
        public float FramePresent
        {
            get { return _frameQueue.DequeuedFramesPerSecond; }
        }

        /// <summary>
        /// Statistics for skipped frames.
        /// This represents frames being pushed by WebRTc to the internal queue faster
        /// than they can be consumed by the Media Foundation sink.
        /// </summary>
        /// <remarks>
        /// This is the difference between <see cref="FrameLoad"/> and <see cref="FramePresent"/>.
        /// </remarks>
        public float FrameSkip
        {
            get { return _frameQueue.DroppedFramesPerSecond; }
        }

        /// <summary>
        /// Statistics for late frames.
        /// A late frame is a frame served via a deferral, that is a frame received
        /// by <see cref="HandleIncomingVideoFrame"/> after it has been requested by
        /// <see cref="TryServeVideoFrame"/>.
        /// </summary>
        ///
        /// If non-zero, this statistics indicates that the Media Foundation sink is
        /// requesting video frames faster than the WebRTC source can provide them.
        /// This is often the case though, as WebRTC uses a push model (source actively
        /// sends data downstream to sink) while Media Foundation uses a pull model
        /// (sink actively requests data from upstream), which makes the two difficult
        /// to synchronize.
        public float LateFrame
        {
            get { return _lateFrameStat; }
        }

        /// <summary>
        /// Construct a new video bridge with the given frame queue capacity.
        /// </summary>
        /// <param name="queueCapacity">Video frame queue initial capacity</param>
        public VideoBridge(int queueCapacity)
        {
            _frameQueue = new VideoFrameQueue<I420AVideoFrameStorage>(queueCapacity);
        }

        /// <summary>
        /// Clear the bridge of any pending frames and reset for reuse.
        /// </summary>
        public void Clear()
        {
            lock (_deferralLock)
            {
                _request = null;
                _deferral?.Complete();
                _deferral = null;
            }
            _frameQueue.Clear();
        }

        /// <summary>
        /// Handle an incoming raw video frame by either enqueuing it for serving
        /// a later request, or immediately serving a pending request.
        /// </summary>
        /// <param name="frame">The incoming video frame</param>
        public void HandleIncomingVideoFrame(I420AVideoFrame frame)
        {
            // If any pending request, serve it immediately
            lock (_deferralLock)
            {
                if (_deferral != null)
                {
                    _frameQueue.TrackLateFrame();
                    MakeSampleForPendingRequest(frame);
                    return;
                }
            }

            // Otherwise queue frame for later pulling by the MediaFoundation framework
            _frameQueue.Enqueue(frame);
        }

        /// <summary>
        /// Try to serve a video frame to the given Media Foundation sample request by
        /// dequeuing a video frame from the internal queue. If no frame is available,
        /// store the request and its deferral for later serving.
        /// </summary>
        /// <param name="args">The Media Foundation sample request to attempt to serve</param>
        public void TryServeVideoFrame(MediaStreamSourceSampleRequestedEventArgs args)
        {
            // Check if the local video stream is enabled
            if (_frameQueue == null) //< TODO - not the correct check (though also useful)
            {
                // End of stream
                args.Request.Sample = null;
                return;
            }

            // Try to read the next available frame packet
            I420AVideoFrameStorage frameStorage;
            lock (_deferralLock)
            {
                if (!_frameQueue.TryDequeue(out frameStorage))
                {
                    // Not available yet, wait for it
                    //_lateFrameStat.Track();
                    if (_deferral != null)
                    {
                        // Already a frame pending, and now another one.
                        // The earlier one will be skipped (we don't keep track of it for simplicity).
                        //_frameQueue.FrameSkip.Track();
                    }
                    args.Request.ReportSampleProgress(0);
                    _request = args.Request;
                    _deferral = _request.GetDeferral();
                    return;
                }
            }

            // Calculate frame timestamp
            TimeSpan timestamp = TimeSpan.FromSeconds(_frameCount / 30.0);
            ++_frameCount;

            // Get a sample
            uint pixelSize = frameStorage.Width * frameStorage.Height;
            uint byteSize = (pixelSize / 2 * 3); // I420 = 12 bits per pixel
            //Debug.Assert(byteSize == frame.Size);
            var sample = _streamSamplePool.Pop(byteSize, timestamp);
            sample.Duration = TimeSpan.FromSeconds(1.0 / 30.0);

            // Copy the frame data into the sample's buffer
            uint copySize = Math.Min((uint)frameStorage.Capacity, byteSize);
            frameStorage.Buffer.CopyTo(0, sample.Buffer, 0, (int)copySize);
            sample.Buffer.Length = copySize; // Somewhat surprisingly, this is not automatic

            // Recycle the frame storage itself
            _frameQueue.RecycleStorage(frameStorage);

            // Return the requested sample
            args.Request.Sample = sample;
        }

        /// <summary>
        /// Fulfill a pending Media Foundation video sample request with an incoming
        /// video frame packet, short-circuiting the internal frame queue.
        /// </summary>
        /// <param name="framePacket">The incoming video frame packet to consume.</param>
        /// <remarks>
        /// This must be called with the <see cref="_deferralLock"/> acquired.
        /// </remarks>
        private void MakeSampleForPendingRequest(I420AVideoFrame frame)
        {
            Debug.Assert(Monitor.IsEntered(_deferralLock));

            // Calculate frame timestamp
            TimeSpan timestamp = TimeSpan.FromSeconds(_frameCount / 30.0);
            ++_frameCount;

            // Get a sample
            // FIXME - There are some wrong assumptions around strides here, see MemCpyStride
            uint pixelSize = frame.width * frame.height;
            uint byteSize = (pixelSize / 2 * 3); // I420 = 12 bits per pixel
            //Debug.Assert(byteSize == frame.Size);
            var sample = _streamSamplePool.Pop(byteSize, timestamp);
            sample.Duration = TimeSpan.FromSeconds(1.0 / 30.0);

            // Copy the frame data into the sample's buffer.
            // Unfortunately the C# interface to Windows.Storage.Streams.Buffer seems to
            // only offer a copy from a byte[] buffer, so need to copy first into a temporary
            // one (packed YUV) before copying into the sample's Buffer object.
            if (byteSize != _deferralBuffer.Length)
            {
                // Reallocate the buffer. This will only happen if the resolution changes.
                _deferralBuffer = new byte[byteSize];
            }
            frame.CopyTo(_deferralBuffer);
            _deferralBuffer.CopyTo(0, sample.Buffer, 0, (int)byteSize);

            // Assign the sample
            _request.Sample = sample;
            _request.ReportSampleProgress(100);
            _deferral.Complete();
            _request = null;
            _deferral = null;
        }
    }
}
