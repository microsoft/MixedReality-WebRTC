// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.MixedReality.Toolkit.Networking;
using Microsoft.MixedReality.Toolkit.Networking.WebRTC.Marshaling;
using System;
using System.Diagnostics;
using System.Runtime.InteropServices.WindowsRuntime;
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
        private MediaStreamSourceSampleRequest _request = null;
        private MediaStreamSourceSampleRequestDeferral _deferral = null;
        private long _frameCount = 0;
        private FrameQueue _frameQueue;
        private FrameQueueStat _lateFrameStat = new FrameQueueStat(100);

        /// <summary>
        /// Statistics for loaded frames.
        /// This represents frame received from WebRTC for later rendering, and
        /// should ideally be equal to <see cref="FramePresent"/>  in an optimal
        /// pipeline scenario.
        /// </summary>
        public IReadonlyFrameQueueStat FrameLoad
        {
            get { return _frameQueue.FrameLoad; }
        }

        /// <summary>
        /// Statistics for presented frames.
        /// This represents frames being popped for rendering, and should ideally be
        /// equal to <see cref="FrameLoad"/> in an optimal pipeline scenario.
        /// </summary>
        public IReadonlyFrameQueueStat FramePresent
        {
            get { return _frameQueue.FramePresent; }
        }

        /// <summary>
        /// Statistics for skipped frames.
        /// This represents frames being pushed by WebRTc to the internal queue faster
        /// than they can be consumed by the Media Foundation sink.
        /// </summary>
        /// <remarks>
        /// This is the difference between <see cref="FrameLoad"/> and <see cref="FramePresent"/>.
        /// </remarks>
        public IReadonlyFrameQueueStat FrameSkip
        {
            get { return _frameQueue.FrameSkip; }
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
        public IReadonlyFrameQueueStat LateFrame
        {
            get { return _lateFrameStat; }
        }

        /// <summary>
        /// Construct a new video bridge with the given frame queue capacity.
        /// </summary>
        /// <param name="queueCapacity">Video frame queue initial capacity</param>
        public VideoBridge(int queueCapacity)
        {
            _frameQueue = new FrameQueue(queueCapacity);
        }

        /// <summary>
        /// Handle an incoming raw video frame by either enqueuing it for serving
        /// a later request, or immediately serving a pending request.
        /// </summary>
        /// <param name="frame">The incoming video frame</param>
        public void HandleIncomingVideoFrame(I420AVideoFrame frame)
        {
            // Create a new packet with the incoming data
            FramePacket packet = _frameQueue.GetDataBufferWithoutContents((int)(frame.width * frame.height * 4));
            if (packet == null)
            {
                return;
            }
            packet._size = YuvUtils.CopyI420FrameToI420Buffer(frame.dataY, frame.dataU, frame.dataV,
                frame.strideY, frame.strideU, frame.strideV, frame.width, frame.height, packet.Buffer);
            packet.width = (int)frame.width;
            packet.height = (int)frame.height;

            lock (_deferralLock)
            {
                // If any pending request, serve it
                if (_deferral != null)
                {
                    _frameQueue.FrameLoad.Track();
                    _frameQueue.FramePresent.Track();
                    MakeSampleForPendingRequest(packet);
                    _frameQueue.Pool(packet);
                    return;
                }

                // Otherwise queue frame for later pulling by the MediaFoundation framework
                _frameQueue.Push(packet);
            }
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
            FramePacket framePacket;
            lock (_deferralLock)
            {
                framePacket = _frameQueue.Pop();
                if (framePacket == null)
                {
                    // Not available yet, wait for it
                    _lateFrameStat.Track();
                    if (_deferral != null)
                    {
                        // Already a frame pending, and now another one.
                        // The earlier one will be skipped (we don't keep track of it for simplicity).
                        _frameQueue.FrameSkip.Track();
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
            uint pixelSize = (uint)(framePacket.width * framePacket.height);
            uint byteSize = (pixelSize / 2 * 3); // I420 = 12 bits per pixel
            Debug.Assert(byteSize == framePacket.Size);
            var sample = _streamSamplePool.Pop(byteSize, timestamp);
            sample.Duration = TimeSpan.FromSeconds(1.0 / 30.0);

            // Copy the frame data into the sample's buffer
            framePacket.Buffer.CopyTo(0, sample.Buffer, 0, (int)framePacket.Size);
            sample.Buffer.Length = framePacket.Size; // Somewhat surprisingly, this is not automatic

            // Recycle the framePacket itself
            _frameQueue.Pool(framePacket);

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
        private void MakeSampleForPendingRequest(FramePacket framePacket)
        {
            // Calculate frame timestamp
            TimeSpan timestamp = TimeSpan.FromSeconds(_frameCount / 30.0);
            ++_frameCount;

            // Get a sample
            uint pixelSize = (uint)(framePacket.width * framePacket.height);
            uint byteSize = (pixelSize / 2 * 3); // I420 = 12 bits per pixel
            Debug.Assert(byteSize == framePacket.Size);
            var sample = _streamSamplePool.Pop(byteSize, timestamp);
            sample.Duration = TimeSpan.FromSeconds(1.0 / 30.0);

            // Copy the frame data into the sample's buffer
            framePacket.Buffer.CopyTo(0, sample.Buffer, 0, (int)framePacket.Size);

            // Assign the sample
            _request.Sample = sample;
            _request.ReportSampleProgress(100);
            _deferral.Complete();
            _request = null;
            _deferral = null;
        }
    }
}
