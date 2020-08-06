// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Collections.Generic;
using System.Threading.Tasks;
using NUnit.Framework;
using NUnit.Framework.Internal;

#if !MRSW_EXCLUDE_DEVICE_TESTS

namespace Microsoft.MixedReality.WebRTC.Tests
{
    /// <summary>
    /// Test the static methods to enumerate the video capture devices and their capture formats.
    /// </summary>
    [TestFixture]
    internal class VideoEnum
    {
        /// <summary>
        /// Check that GetVideoCaptureDevicesAsync() successfully returns, and that if there is
        /// any device then its ID and name are not empty.
        /// </summary>
        [Test]
        public async Task EnumVideoDevices()
        {
            IReadOnlyList<VideoCaptureDevice> devices = await DeviceVideoTrackSource.GetCaptureDevicesAsync();
            foreach (var device in devices)
            {
                Assert.That(device.id.Length, Is.GreaterThan(0));
                Assert.That(device.name.Length, Is.GreaterThan(0));
            }
        }

        /// <summary>
        /// Check that, for all available video capture devices on the host device,
        /// GetVideoCaptureFormatsAsync() returns some valid formats.
        /// </summary>
        [Test]
        public async Task EnumVideoFormats()
        {
            IReadOnlyList<VideoCaptureDevice> devices = await DeviceVideoTrackSource.GetCaptureDevicesAsync();
            if (devices.Count == 0)
            {
                Assert.Inconclusive("Host device has no available video capture device.");
            }
            foreach (var device in devices)
            {
                IReadOnlyList<VideoCaptureFormat> formats = await DeviceVideoTrackSource.GetCaptureFormatsAsync(device.id);
                foreach (var format in formats)
                {
                    Assert.That(format.width, Is.GreaterThan(0));
                    Assert.That(format.height, Is.GreaterThan(0));
                    Assert.That(format.framerate, Is.GreaterThan(0.0));
                }
            }
        }
    }
}

#endif // !MRSW_EXCLUDE_DEVICE_TESTS
