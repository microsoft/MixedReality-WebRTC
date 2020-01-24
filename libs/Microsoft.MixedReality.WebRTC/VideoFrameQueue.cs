// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using Microsoft.MixedReality.WebRTC.Interop;
using Microsoft.MixedReality.WebRTC.Tracing;

namespace Microsoft.MixedReality.WebRTC
{
    /// <summary>
    /// Interface for a storage of a single video frame.
    /// </summary>
    public interface IVideoFrameStorage
    {
        /// <summary>
        /// Storage capacity, in bytes.
        /// </summary>
        ulong Capacity { get; set; }

        /// <summary>
        /// Frame width, in pixels.
        /// </summary>
        uint Width { get; set; }

        /// <summary>
        /// Frame height, in pixels.
        /// </summary>
        uint Height { get; set; }

        /// <summary>
        /// Raw storage buffer of capacity <see cref="Capacity"/>.
        /// </summary>
        byte[] Buffer { get; }
    }

    /// <summary>
    /// Storage for a video frame encoded in I420+Alpha format.
    /// </summary>
    public class I420AVideoFrameStorage : IVideoFrameStorage
    {
        /// <summary>
        /// Total capacity of the storage, in bytes.
        /// This can be assigned to resize the storage.
        /// </summary>
        /// <remarks>
        /// Reading this property is equivalent to reading the <see xref="System.Array.LongLength"/>
        /// property of <see cref="Buffer"/>.
        /// </remarks>
        public ulong Capacity
        {
            get { return (ulong)Buffer.LongLength; }
            set { Resize(value); }
        }

        /// <summary>
        /// Frame width, in pixels.
        /// </summary>
        public uint Width { get; set; }

        /// <summary>
        /// Frame height, in pixels.
        /// </summary>
        public uint Height { get; set; }

        /// <summary>
        /// Raw byte buffer containing the frame data.
        /// </summary>
        public byte[] Buffer { get; private set; }

        /// <summary>
        /// Resize the internal buffer to the given capacity.
        /// This has no effect if the new capacity is smaller than the current one.
        /// </summary>
        /// <param name="capacity">The new desired capacity, in bytes.</param>
        private void Resize(ulong capacity)
        {
            if ((Buffer == null) || (capacity > (ulong)Buffer.LongLength))
            {
                Buffer = new byte[capacity];
            }
        }
    }

    /// <summary>
    /// Storage for a video frame encoded in ARGB format.
    /// </summary>
    public class Argb32VideoFrameStorage : IVideoFrameStorage
    {
        /// <summary>
        /// Total capacity of the storage, in bytes.
        /// This can be assigned to resize the storage.
        /// </summary>
        /// <remarks>
        /// Reading this property is equivalent to reading the <see xref="System.Array.LongLength"/>
        /// property of <see cref="Buffer"/>.
        /// </remarks>
        public ulong Capacity
        {
            get { return (ulong)Buffer.LongLength; }
            set { Resize(value); }
        }

        /// <summary>
        /// Frame width, in pixels.
        /// </summary>
        public uint Width { get; set; }

        /// <summary>
        /// Frame height, in pixels.
        /// </summary>
        public uint Height { get; set; }

        /// <summary>
        /// Raw byte buffer containing the frame data.
        /// </summary>
        public byte[] Buffer { get; private set; }

        /// <summary>
        /// Resize the internal buffer to the given capacity.
        /// This has no effect if the new capacity is smaller than the current one.
        /// </summary>
        /// <param name="capacity">The new desired capacity, in bytes.</param>
        private void Resize(ulong capacity)
        {
            if ((Buffer == null) || (capacity > (ulong)Buffer.LongLength))
            {
                Buffer = new byte[capacity];
            }
        }
    }

    /// <summary>
    /// Interface for a queue of video frames.
    /// </summary>
    public interface IVideoFrameQueue
    {
        /// <summary>
        /// Get the number of frames enqueued per seconds.
        /// This is generally an average statistics representing how fast a video source
        /// produces some video frames.
        /// </summary>
        float QueuedFramesPerSecond { get; }

        /// <summary>
        /// Get the number of frames enqueued per seconds.
        /// This is generally an average statistics representing how fast a video sink
        /// consumes some video frames, typically to render them.
        /// </summary>
        float DequeuedFramesPerSecond { get; }

        /// <summary>
        /// Get the number of frames dropped per seconds.
        /// This is generally an average statistics representing how many frames were
        /// enqueued by a video source but not dequeued fast enough by a video sink,
        /// meaning the video sink renders at a slower framerate than the source can produce.
        /// </summary>
        float DroppedFramesPerSecond { get; }
    }

    /// <summary>
    /// Small queue of video frames received from a source and pending delivery to a sink.
    /// Used as temporary buffer between the WebRTC callback (push model) and the video
    /// player rendering (pull model). This also handles dropping frames when the source
    /// is faster than the sink, by limiting the maximum queue length.
    /// </summary>
    /// <typeparam name="T">The type of video frame storage</typeparam>
    public class VideoFrameQueue<T> : IVideoFrameQueue
        where T : class, IVideoFrameStorage, new()
    {
        /// <inheritdoc/>
        public float QueuedFramesPerSecond => 1000f / _queuedFrameTimeAverage.Average;

        /// <inheritdoc/>
        public float DequeuedFramesPerSecond => 1000f / _dequeuedFrameTimeAverage.Average;

        /// <inheritdoc/>
        public float DroppedFramesPerSecond => 1000f / _droppedFrameTimeAverage.Average;

        /// <summary>
        /// Queue of frames pending delivery to sink.
        /// </summary>
        private ConcurrentQueue<T> _frameQueue = new ConcurrentQueue<T>();

        /// <summary>
        /// Pool of unused frames available for reuse, to avoid memory allocations.
        /// </summary>
        private ConcurrentStack<T> _unusedFramePool = new ConcurrentStack<T>();

        /// <summary>
        /// Maximum queue length in number of frames.
        /// </summary>
        private int _maxQueueLength = 3;

        /// <summary>
        /// Unique identifier representing the current object instance, to discriminate ETW events.
        /// </summary>
        /// <remarks>
        /// Ideally this would be based on ActivityID and ProcessID, but the former is not easy to use
        /// with the C# API, while also not fitting really well with the use case, and the latter doesn't
        /// seem to be recorded at all. So using an explicit GUID works around this, as the only critical
        /// point is being able to discriminate events from several instances to avoid mixing their results.
        /// </remarks>
        private Guid _traceId = Guid.NewGuid();


        #region Statistics

        /// <summary>
        /// Shared clock for all frame statistics.
        /// </summary>
        private Stopwatch _stopwatch = new Stopwatch();

        /// <summary>
        /// Time in milliseconds since last frame was enqueued, as reported by <see cref="_stopwatch"/>.
        /// </summary>
        private double _lastQueuedTimeMs = 0f;

        /// <summary>
        /// Time in milliseconds since last frame was dequeued, as reported by <see cref="_stopwatch"/>.
        /// </summary>
        private double _lastDequeuedTimeMs = 0f;

        /// <summary>
        /// Time in milliseconds since last frame was dropped, as reported by <see cref="_stopwatch"/>.
        /// </summary>
        private double _lastDroppedTimeMs = 0f;

        /// <summary>
        /// Moving average of the queued frame time, in frames per second.
        /// </summary>
        private MovingAverage _queuedFrameTimeAverage = new MovingAverage(30);

        /// <summary>
        /// Moving average of the dequeued frame time, in frames per second.
        /// </summary>
        private MovingAverage _dequeuedFrameTimeAverage = new MovingAverage(30);

        /// <summary>
        /// Moving average of the dropped frame time, in frames per second.
        /// </summary>
        private MovingAverage _droppedFrameTimeAverage = new MovingAverage(30);

        #endregion


        /// <summary>
        /// Create a new queue with a maximum frame length.
        /// </summary>
        /// <param name="maxQueueLength">Maxmimum number of frames to enqueue before starting to drop incoming frames</param>
        public VideoFrameQueue(int maxQueueLength)
        {
            _maxQueueLength = maxQueueLength;
            _stopwatch.Start();
        }

        /// <summary>
        /// Clear the queue and drop all frames currently pending.
        /// </summary>
        public void Clear()
        {
            MainEventSource.Log.VideoFrameQueueClear(_traceId);

            _lastQueuedTimeMs = 0f;
            _lastDequeuedTimeMs = 0f;
            _lastDroppedTimeMs = 0f;
            _queuedFrameTimeAverage.Clear();
            _dequeuedFrameTimeAverage.Clear();
            _droppedFrameTimeAverage.Clear();
            while (_frameQueue.TryDequeue(out T frame))
            {
                _unusedFramePool.Push(frame);
            }
            _stopwatch.Restart();
        }

        /// <summary>
        /// Enqueue a new video frame encoded in I420+Alpha format.
        /// If the internal queue reached its maximum capacity, do nothing and drop the frame.
        /// </summary>
        /// <param name="frame">The video frame to enqueue</param>
        /// <returns>Return <c>true</c> if the frame was enqueued successfully, or <c>false</c> if it was dropped</returns>
        /// <remarks>This should only be used if the queue has storage for a compatible video frame encoding.</remarks>
        public bool Enqueue(I420AVideoFrame frame)
        {
            MainEventSource.Log.VideoFrameQueueEnqueueI420(_traceId, (int)frame.width, (int)frame.height);

            double curTime = _stopwatch.Elapsed.TotalMilliseconds;

            // Always update queued time, which refers to calling Enqueue(), even
            // if the queue is full and the frame is dropped.
            float queuedDt = (float)(curTime - _lastQueuedTimeMs);
            _lastQueuedTimeMs = curTime;

            // Try to get some storage for that new frame
            ulong byteSize = (ulong)(frame.strideY + frame.strideA) * frame.height + (ulong)(frame.strideU + frame.strideV) * frame.height / 2;
            T storage = GetStorageFor(byteSize);
            if (storage == null)
            {
                // Too many frames in queue, drop the current one
                MainEventSource.Log.VideoFrameQueueDropI420(_traceId, (int)frame.width, (int)frame.height);
                float droppedDt = (float)(curTime - _lastDroppedTimeMs);
                _lastDroppedTimeMs = curTime;
                _droppedFrameTimeAverage.Push(droppedDt);
                return false;
            }

            // Copy the new frame to its storage
            frame.CopyTo(storage.Buffer);
            storage.Width = frame.width;
            storage.Height = frame.height;

            // Enqueue for later delivery
            _frameQueue.Enqueue(storage);
            _queuedFrameTimeAverage.Push(queuedDt);
            _droppedFrameTimeAverage.Push((float)(curTime - _lastDroppedTimeMs));
            return true;
        }

        /// <summary>
        /// Try to enqueue a new video frame encoded in raw ARGB format.
        /// If the internal queue reached its maximum capacity, do nothing and drop the frame.
        /// </summary>
        /// <param name="frame">The video frame to enqueue</param>
        /// <returns>Return <c>true</c> if the frame was enqueued successfully, or <c>false</c> if it was dropped</returns>
        /// <remarks>This should only be used if the queue has storage for a compatible video frame encoding.</remarks>
        public bool Enqueue(Argb32VideoFrame frame)
        {
            MainEventSource.Log.VideoFrameQueueEnqueueArgb32(_traceId, (int)frame.width, (int)frame.height);

            Debug.Assert(frame.stride >= frame.width * 4);

            double curTime = _stopwatch.Elapsed.TotalMilliseconds;

            // Always update queued time, which refers to calling Enqueue(), even
            // if the queue is full and the frame is dropped.
            float queuedDt = (float)(curTime - _lastQueuedTimeMs);
            _lastQueuedTimeMs = curTime;

            // Try to get some storage for that new frame
            ulong byteSize = (ulong)frame.stride * frame.height;
            T storage = GetStorageFor(byteSize);
            if (storage == null)
            {
                // Too many frames in queue, drop the current one
                MainEventSource.Log.VideoFrameQueueDropArgb32(_traceId, (int)frame.width, (int)frame.height);
                float droppedDt = (float)(curTime - _lastDroppedTimeMs);
                _lastDroppedTimeMs = curTime;
                _droppedFrameTimeAverage.Push(droppedDt);
                return false;
            }

            // Copy the new frame to its storage
            unsafe
            {
                fixed (void* dst = storage.Buffer)
                {
                    void* src = (void*)frame.data;
                    Utils.MemCpyStride(dst, frame.stride, (void*)frame.data, frame.stride, (int)frame.width * 4, (int)frame.height);
                }
            }
            storage.Width = frame.width;
            storage.Height = frame.height;

            // Enqueue for later delivery
            _frameQueue.Enqueue(storage);
            _queuedFrameTimeAverage.Push(queuedDt);
            _droppedFrameTimeAverage.Push((float)(curTime - _lastDroppedTimeMs));
            return true;
        }

        /// <summary>
        /// Try to dequeue a video frame, usually to be consumed by a video sink (video player).
        /// </summary>
        /// <param name="frame">On success, returns the dequeued frame.</param>
        /// <returns>Return <c>true</c> on success or <c>false</c> if the queue is empty.</returns>
        public bool TryDequeue(out T frame)
        {
            if (_frameQueue.TryDequeue(out frame))
            {
                // Only track dequeued time if actually dequeued. Otherwise this will generate
                // duplicate timings in the buffer (twice or more per frame) and will result in
                // completely unreliable averages.
                double curTime = _stopwatch.Elapsed.TotalMilliseconds;
                float dequeuedDt = (float)(curTime - _lastDequeuedTimeMs);
                _lastDequeuedTimeMs = curTime;
                _dequeuedFrameTimeAverage.Push(dequeuedDt);
                MainEventSource.Log.VideoFrameQueueDequeue(_traceId, dequeuedDt);
                return true;
            }
            return false;
        }

        /// <summary>
        /// Recycle a frame storage, putting it back into the internal pool for later reuse.
        /// This prevents deallocation and reallocation of a frame, and decreases pressure on
        /// the garbage collector.
        /// </summary>
        /// <param name="frame">The unused frame storage to recycle for a later new frame</param>
        public void RecycleStorage(T frame)
        {
            MainEventSource.Log.VideoFrameQueueRecycleStorage(_traceId);
            _unusedFramePool.Push(frame);
        }

        /// <summary>
        /// Track statistics for a late frame, which short-circuits the queue and is delivered
        /// as soon as it is received.
        /// </summary>
        public void TrackLateFrame()
        {
            double curTime = _stopwatch.Elapsed.TotalMilliseconds;

            float queuedDt = (float)(curTime - _lastQueuedTimeMs);
            _lastQueuedTimeMs = curTime;
            _queuedFrameTimeAverage.Push(queuedDt);

            float dequeuedDt = (float)(curTime - _lastDequeuedTimeMs);
            _lastDequeuedTimeMs = curTime;
            _dequeuedFrameTimeAverage.Push(dequeuedDt);

            MainEventSource.Log.VideoFrameQueueTrackLateFrame(_traceId, queuedDt, dequeuedDt);

            _droppedFrameTimeAverage.Push((float)(curTime - _lastDroppedTimeMs));
        }

        /// <summary>
        /// Get some video frame storage for a frame of the given byte size.
        /// </summary>
        /// <param name="byteSize">The byte size of the frame that the storage should accomodate</param>
        /// <returns>A new or recycled storage if possible, or <c>null</c> if the queue reached its maximum capacity</returns>
        private T GetStorageFor(ulong byteSize)
        {
            if (_unusedFramePool.TryPop(out T storage))
            {
                if (storage.Capacity < byteSize)
                {
                    storage.Capacity = byteSize;
                }
                return storage;
            }
            if (_frameQueue.Count >= _maxQueueLength)
            {
                // Too many frames in queue, drop the current one
                return null;
            }
            var newStorage = new T();
            newStorage.Capacity = byteSize;
            return newStorage;
        }
    }
}
