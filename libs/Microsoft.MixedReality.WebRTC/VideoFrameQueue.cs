// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Collections.Concurrent;

namespace Microsoft.MixedReality.WebRTC
{
    public interface IVideoFrameStorage
    {
        ulong Capacity { get; set; }
        uint Width { get; set; }
        uint Height { get; set; }
        byte[] Buffer { get; }
    }

    public class I420VideoFrameStorage : IVideoFrameStorage
    {
        public ulong Capacity { get { return _capacity; } set { Resize(value); } }
        public uint Width { get; set; }
        public uint Height { get; set; }
        public byte[] Buffer { get; private set; }

        private ulong _capacity = 0;

        private void Resize(ulong capacity)
        {
            if (capacity > _capacity)
            {
                Buffer = new byte[capacity];
                _capacity = capacity;
            }
        }
    }

    public interface IVideoFrameQueue
    {
        float QueuedFramesPerSecond { get; }
        float DequeuedFramesPerSecond { get; }
        float DroppedFramesPerSecond { get; }
    }

    /// <summary>
    /// Small queue of video frames received from a source and pending delivery to a sink.
    /// Used as temporary buffer between the WebRTC callback (push model) and the video
    /// player rendering (pull model). This also handles dropping frames when the source
    /// is faster than the sink, by limiting the maximum queue length.
    /// </summary>
    /// <typeparam name="T">The type of video frame</typeparam>
    public class VideoFrameQueue<T> : IVideoFrameQueue
        where T : class, IVideoFrameStorage, new()
    {
        public float QueuedFramesPerSecond { get { return 0f; } }
        public float DequeuedFramesPerSecond { get { return 0f; } }
        public float DroppedFramesPerSecond { get { return 0f; } }

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
        /// Create a new queue with a maximum frame length.
        /// </summary>
        /// <param name="maxQueueLength">Maxmimum number of frames to enqueue before starting to drop incoming frames</param>
        public VideoFrameQueue(int maxQueueLength)
        {
            _maxQueueLength = maxQueueLength;
        }

        /// <summary>
        /// Enqueue a new video frame encoded in I420 format.
        /// If the internal queue reached its maximum capacity, do nothing and drop the frame.
        /// </summary>
        /// <param name="frame">The new video frame from the video source</param>
        public bool Enqueue(I420AVideoFrame frame)
        {
            ulong byteSize = (ulong)(frame.strideY + frame.strideA) * frame.height + (ulong)(frame.strideU + frame.strideV) * frame.height / 2;
            T storage = GetStorageFor(byteSize);
            if (storage == null)
            {
                // Too many frames in queue, drop the current one
                return false;
            }
            frame.CopyTo(storage.Buffer);
            storage.Width = frame.width;
            storage.Height = frame.height;
            _frameQueue.Enqueue(storage);
            return true;
        }

        /// <summary>
        /// Try to enqueue a new video frame encoded in raw ARGB format.
        /// If the internal queue reached its maximum capacity, do nothing and drop the frame.
        /// </summary>
        /// <param name="frame">The new video frame from the video source</param>
        public bool Enqueue(ARGBVideoFrame frame)
        {
            ulong byteSize = (ulong)frame.stride * frame.height * 4;
            T storage = GetStorageFor(byteSize);
            if (storage == null)
            {
                // Too many frames in queue, drop the current one
                return false;
            }
            unsafe
            {
                fixed (void* dst = storage.Buffer)
                {
                    void* src = (void*)frame.data;
                    PeerConnection.MemCpyStride(dst, frame.stride, (void*)frame.data, frame.stride, (int)frame.width * 4, (int)frame.height);
                }
            }
            storage.Width = frame.width;
            storage.Height = frame.height;
            _frameQueue.Enqueue(storage);
            return true;
        }

        /// <summary>
        /// Try to dequeue a video frame to be consumed by a video sink (video player).
        /// </summary>
        /// <param name="frame">On success, the dequeue frame</param>
        /// <returns>Return true on success or false if no frame is available</returns>
        public bool TryDequeue(out T frame)
        {
            return _frameQueue.TryDequeue(out frame);
        }

        /// <summary>
        /// Recycle a frame storage, putting it back into the internal pool for later reuse.
        /// </summary>
        /// <param name="frame">The unused frame to recycle</param>
        public void RecycleStorage(T frame)
        {
            _unusedFramePool.Push(frame);
        }

        /// <summary>
        /// Get some video frame storage for a frame of the given byte size.
        /// </summary>
        /// <param name="byteSize">The byte size of the frame that will be copied into the storage</param>
        /// <returns>A new or recycled storage if possible, or null if the queue reached its maximum capacity</returns>
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
