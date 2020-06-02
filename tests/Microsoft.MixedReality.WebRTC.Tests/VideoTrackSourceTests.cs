// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

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

#if !MRSW_EXCLUDE_DEVICE_TESTS

        [Test]
        public async Task CreateFromDevice()
        {
            using (VideoTrackSource source = await VideoTrackSource.CreateFromDeviceAsync())
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
            using (VideoTrackSource source = await VideoTrackSource.CreateFromDeviceAsync())
            {
                Assert.IsNotNull(source);

                const string kTestName = "test_video_track_source_name";
                source.Name = kTestName;
                Assert.AreEqual(kTestName, source.Name);
            }
        }

#endif // MRSW_EXCLUDE_DEVICE_TESTS
    }
}
