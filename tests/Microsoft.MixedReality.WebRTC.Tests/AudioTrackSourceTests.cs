// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Threading.Tasks;
using NUnit.Framework;
using NUnit.Framework.Internal;

namespace Microsoft.MixedReality.WebRTC.Tests
{
    [TestFixture(SdpSemantic.PlanB)]
    [TestFixture(SdpSemantic.UnifiedPlan)]
    internal class AudioTrackSourceTests : PeerConnectionTestBase
    {
        public AudioTrackSourceTests(SdpSemantic sdpSemantic) : base(sdpSemantic)
        {
        }

#if !MRSW_EXCLUDE_DEVICE_TESTS

        [Test]
        public async Task CreateFromDevice()
        {
            using (AudioTrackSource source = await DeviceAudioTrackSource.CreateAsync())
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
            using (AudioTrackSource source = await DeviceAudioTrackSource.CreateAsync())
            {
                Assert.IsNotNull(source);

                const string kTestName = "test_audio_track_source_name";
                source.Name = kTestName;
                Assert.AreEqual(kTestName, source.Name);
            }
        }

#endif // MRSW_EXCLUDE_DEVICE_TESTS
    }
}
