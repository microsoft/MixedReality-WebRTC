// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Collections.Generic;
using NUnit.Framework;
using NUnit.Framework.Internal;

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
        public void EnumVideoDevices()
        {
            PeerConnection.GetVideoCaptureDevicesAsync().ContinueWith((enumTask) =>
            {
                Assert.IsNull(enumTask.Exception);
                List<VideoCaptureDevice> devices = enumTask.Result;
                foreach (var device in devices)
                {
                    Assert.That(device.id.Length, Is.GreaterThan(0));
                    Assert.That(device.name.Length, Is.GreaterThan(0));
                }
            });
        }

        /// <summary>
        /// Check that, for all available video capture devices on the host device,
        /// GetVideoCaptureFormatsAsync() returns some valid formats.
        /// </summary>
        [Test]
        public void EnumVideoFormats()
        {
            PeerConnection.GetVideoCaptureDevicesAsync().ContinueWith((enumDeviceTask) =>
            {
                Assert.IsNull(enumDeviceTask.Exception);
                List<VideoCaptureDevice> devices = enumDeviceTask.Result;
                if (devices.Count == 0)
                {
                    Assert.Inconclusive("Host device has no available video capture device.");
                }

                foreach (var device in devices)
                {
                    PeerConnection.GetVideoCaptureFormatsAsync(device.id).ContinueWith((enumFormatTask) =>
                    {
                        Assert.IsNull(enumFormatTask.Exception);
                        List<VideoCaptureFormat> formats = enumFormatTask.Result;
                        foreach (var format in formats)
                        {
                            Assert.That(format.width, Is.GreaterThan(0));
                            Assert.That(format.height, Is.GreaterThan(0));
                            Assert.That(format.framerate, Is.GreaterThan(0.0));
                        }
                    });
                }
            });
        }
    }
}
