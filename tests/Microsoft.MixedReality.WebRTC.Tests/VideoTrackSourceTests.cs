// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using NUnit.Framework.Internal;

namespace Microsoft.MixedReality.WebRTC.Tests
{
    [TestFixture(SdpSemantic.PlanB)]
    [TestFixture(SdpSemantic.UnifiedPlan)]
    internal class VideoTrackSourceTests : PeerConnectionTestBase
    {
        public VideoTrackSourceTests(SdpSemantic sdpSemantic) : base(sdpSemantic)
        {
        }

        public static unsafe void CustomI420AFrameCallback(in FrameRequest request)
        {
            var data = stackalloc byte[32 * 16 + 16 * 8 * 2];
            int k = 0;
            // Y plane (full resolution)
            for (int j = 0; j < 16; ++j)
            {
                for (int i = 0; i < 32; ++i)
                {
                    data[k++] = 0x7F;
                }
            }
            // U plane (halved chroma in both directions)
            for (int j = 0; j < 8; ++j)
            {
                for (int i = 0; i < 16; ++i)
                {
                    data[k++] = 0x30;
                }
            }
            // V plane (halved chroma in both directions)
            for (int j = 0; j < 8; ++j)
            {
                for (int i = 0; i < 16; ++i)
                {
                    data[k++] = 0xB2;
                }
            }
            var dataY = new IntPtr(data);
            var frame = new I420AVideoFrame
            {
                dataY = dataY,
                dataU = dataY + (32 * 16),
                dataV = dataY + (32 * 16) + (16 * 8),
                dataA = IntPtr.Zero,
                strideY = 32,
                strideU = 16,
                strideV = 16,
                strideA = 0,
                width = 32,
                height = 16
            };
            request.CompleteRequest(frame);
        }

        public static unsafe void CustomArgb32FrameCallback(in FrameRequest request)
        {
            var data = stackalloc uint[32 * 16];
            int k = 0;
            // Create 2x2 checker pattern with 4 different colors
            for (int j = 0; j < 8; ++j)
            {
                for (int i = 0; i < 16; ++i)
                {
                    data[k++] = 0xFF0000FF;
                }
                for (int i = 16; i < 32; ++i)
                {
                    data[k++] = 0xFF00FF00;
                }
            }
            for (int j = 8; j < 16; ++j)
            {
                for (int i = 0; i < 16; ++i)
                {
                    data[k++] = 0xFFFF0000;
                }
                for (int i = 16; i < 32; ++i)
                {
                    data[k++] = 0xFF00FFFF;
                }
            }
            var frame = new Argb32VideoFrame
            {
                data = new IntPtr(data),
                stride = 128,
                width = 32,
                height = 16
            };
            request.CompleteRequest(frame);
        }

        public static void TestFrameReadyCallbacks(IVideoSource source)
        {
            bool shouldReceiveI420 = true;
            int i420Counter = 0;
            var enoughI420Frames = new ManualResetEventSlim(false);
            void i420Handler(I420AVideoFrame frame)
            {
                Assert.IsTrue(shouldReceiveI420);
                Assert.AreEqual(32, frame.width);
                Assert.AreEqual(16, frame.height);
                if (i420Counter == 10)
                {
                    enoughI420Frames.Set();
                }
                else if (i420Counter < 10)
                {
                    ++i420Counter;
                }
            }

            bool shouldReceiveArgb32 = true;
            int argb32Counter = 0;
            var enoughArgb32Frames = new ManualResetEventSlim(false);
            void argb32Handler(Argb32VideoFrame frame)
            {
                Assert.IsTrue(shouldReceiveArgb32);
                Assert.AreEqual(32, frame.width);
                Assert.AreEqual(16, frame.height);
                if (argb32Counter == 10)
                {
                    enoughArgb32Frames.Set();
                }
                else if (argb32Counter < 10)
                {
                    ++argb32Counter;
                }
            }

            // Receive I420 frames.
            source.I420AVideoFrameReady += i420Handler;
            Assert.IsTrue(enoughI420Frames.Wait(TimeSpan.FromSeconds(10)));

            // Receive both.
            source.Argb32VideoFrameReady += argb32Handler;
            enoughI420Frames.Reset();
            i420Counter = 0;
            Assert.IsTrue(enoughI420Frames.Wait(TimeSpan.FromSeconds(10)));
            Assert.IsTrue(enoughArgb32Frames.Wait(TimeSpan.FromSeconds(10)));

            // Receive ARGB32 frames.
            source.I420AVideoFrameReady -= i420Handler;
            enoughArgb32Frames.Reset();
            argb32Counter = 0;
            shouldReceiveI420 = false;
            Assert.IsTrue(enoughArgb32Frames.Wait(TimeSpan.FromSeconds(10)));

            // Stop all callbacks.
            source.Argb32VideoFrameReady -= argb32Handler;
            shouldReceiveArgb32 = false;
        }

#if !MRSW_EXCLUDE_DEVICE_TESTS

        [Test]
        public async Task CreateFromDevice()
        {
            using (VideoTrackSource source = await DeviceVideoTrackSource.CreateAsync())
            {
                Assert.IsNotNull(source);
                Assert.AreEqual(string.Empty, source.Name);
                Assert.AreEqual(0, source.Tracks.Count);
            }
        }

        // TODO - Don't use device, run outside MRSW_EXCLUDE_DEVICE_TESTS
        [Test]
        public async Task Name()
        {
            using (VideoTrackSource source = await DeviceVideoTrackSource.CreateAsync())
            {
                Assert.IsNotNull(source);

                const string kTestName = "test_video_track_source_name";
                source.Name = kTestName;
                Assert.AreEqual(kTestName, source.Name);
            }
        }

#endif // MRSW_EXCLUDE_DEVICE_TESTS

        [Test]
        public void FrameReadyCallbacks()
        {
            using (var source = ExternalVideoTrackSource.CreateFromI420ACallback(
                CustomI420AFrameCallback))
            {
                TestFrameReadyCallbacks(source);
            }
        }
    }
}
