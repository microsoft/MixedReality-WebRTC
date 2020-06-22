// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Threading.Tasks;
using NUnit.Framework;
using NUnit.Framework.Internal;

namespace Microsoft.MixedReality.WebRTC.Tests
{
    [TestFixture(SdpSemantic.PlanB)]
    [TestFixture(SdpSemantic.UnifiedPlan)]
    internal class LocalAudioTrackTests : PeerConnectionTestBase
    {
        public LocalAudioTrackTests(SdpSemantic sdpSemantic) : base(sdpSemantic)
        {
        }

#if !MRSW_EXCLUDE_DEVICE_TESTS

        // TODO - Don't use device, run outside MRSW_EXCLUDE_DEVICE_TESTS
        [Test]
        public async Task CreateFromSource()
        {
            using (AudioTrackSource source = await DeviceAudioTrackSource.CreateAsync())
            {
                Assert.IsNotNull(source);

                var settings = new LocalAudioTrackInitConfig { trackName = "track_name" };
                using (LocalAudioTrack track = LocalAudioTrack.CreateFromSource(source, settings))
                {
                    Assert.IsNotNull(track);
                }
            }
        }

#endif // MRSW_EXCLUDE_DEVICE_TESTS
    }
}
