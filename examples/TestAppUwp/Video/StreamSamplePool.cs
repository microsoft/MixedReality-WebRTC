// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Collections.Generic;
using Windows.Media.Core;
using Windows.Storage.Streams;

namespace TestAppUwp.Video
{
    /// <summary>
    /// Pool of <see cref="Windows.Media.Core.MediaStreamSample"/> for video frames,
    /// which handles the <see cref="Windows.Media.Core.MediaStreamSample.Processed">
    /// event to recycle the underlying <see cref="Windows.Storage.Streams.Buffer"/>
    /// automatically once used.
    /// </summary>
    /// <remarks>
    /// This should be reimplemented in unmanaged code to benefit from low-level buffer
    /// access and avoid reallocating a new <see cref="Windows.Media.Core.MediaStreamSample"/>
    /// each time, as current managed wrapper does not allow changing the timestamp of
    /// a sample after creation. Also even trying to recycle the samples once processed
    /// throws a WinRT exception, presumably because the sample is still used after the
    /// event is fired, as hinted by the heap profiler showing some pinned object.
    /// This might explain why the sample API doesn't allow changing the timestamp to
    /// force re-creation and avoid recycling. Unmanaged code would fix that and prevent
    /// GC every 0.3 second or so.
    /// </remarks>
    public class StreamSamplePool
    {
        /// <summary>
        /// Queue of buffers in use by a sample.
        /// </summary>
        /// <remarks>
        /// Newly used buffers are added on the back of the queue, and removed
        /// from the front once the <see cref="Windows.Media.Core.MediaStreamSample.Processed"/>
        /// signal is fired. Because some users report out-of-order or missing
        /// calls, all earlier buffers are also removed, so order of the buffers
        /// in the queue reflects the chronology and matters.
        /// </remarks>
        Queue<Buffer> _usedBuffers;

        /// <summary>
        /// Stack of free buffers available for recycling by a new sample.
        /// </summary>
        /// <remarks>
        /// Since buffer resize shall correspond to video resize and thus be infrequent,
        /// favor reusing the last released buffer, which is most likely to have the same
        /// capacity as the new frame, by using a stack.
        /// </remarks>
        Stack<Buffer> _freeBuffers;

        /// <summary>
        /// Construct a new pool of buffers.
        /// </summary>
        /// <param name="capacity">Initial capacity of both the used and free collections of buffers</param>
        public StreamSamplePool(int capacity)
        {
            this._usedBuffers = new Queue<Buffer>(capacity);
            this._freeBuffers = new Stack<Buffer>(capacity);
        }

        /// <summary>
        /// Get a sample from the pool which has a buffer with a given capacity
        /// and with the associated timestamp.
        /// </summary>
        /// <param name="byteSize">The exact size in bytes that the sample buffer needs to accomodate.</param>
        /// <param name="timestamp">The sample presentation timestamp.</param>
        /// <returns>The newly created sample</returns>
        /// <remarks>
        /// The returned sample's buffer has a <see cref="Windows.Storage.Streams.Buffer.Length"/> property
        /// set to the input <see cref="byteSize"/>. This is required to be set before creating the sample,
        /// and should not be modified once the sample was created.
        /// </remarks>
        public MediaStreamSample Pop(uint byteSize, System.TimeSpan timestamp)
        {
            Buffer buffer;
            lock (this)
            {
                if (_freeBuffers.Count > 0)
                {
                    buffer = _freeBuffers.Pop();
                    if (buffer.Capacity < byteSize)
                    {
                        buffer = new Buffer(byteSize);
                    }
                }
                else
                {
                    buffer = new Buffer(byteSize);
                }
                _usedBuffers.Enqueue(buffer);

                // This must be set before calling CreateFromBuffer() below otherwise
                // the Media Foundation pipeline throws an exception.
                buffer.Length = byteSize;
            }

            // Because the managed wrapper does not allow modifying the timestamp,
            // need to recreate the sample each time with the correct timestamp.
            var sample = MediaStreamSample.CreateFromBuffer(buffer, timestamp);
            sample.Processed += OnSampleProcessed;
            return sample;
        }

        /// <summary>
        /// Callback fired by MediaFoundation when a <see cref="Windows.Media.Core.MediaStreamSample"/>
        /// has been processed by the pipeline and its buffer can be reused.
        /// </summary>
        /// <param name="sample">The sample which has been processed.</param>
        /// <param name="args"></param>
        private void OnSampleProcessed(MediaStreamSample sample, object args)
        {
            lock (this)
            {
                // This does a linear search from front, which generally finds
                // the first object (oldest) or at worse one very close to front,
                // so is optimal anyway.
                // Remove this sample and all earlier ones too. Some users report that
                // the Processed event is not always reported for earlier samples, which
                // would result in memory leaks. This may be due to out-of-order reporting.
                while (_usedBuffers.TryDequeue(out Buffer buffer))
                {
                    // Save the buffer for later reuse
                    _freeBuffers.Push(buffer);

                    if (buffer == sample.Buffer)
                    {
                        break;
                    }
                }
            }
        }
    }
}
