// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Runtime.InteropServices;

namespace Microsoft.MixedReality.WebRTC.Interop
{
    /// <summary>
    /// Handle to a native device video track source object.
    /// </summary>
    internal class DeviceVideoTrackSourceHandle : VideoTrackSourceHandle { }

    internal class DeviceVideoTrackSourceInterop
    {
        /// <summary>
        /// Marshaling struct for initializing settings when opening a local video device.
        /// </summary>
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
        internal ref struct LocalVideoDeviceMarshalInitConfig
        {
            /// <summary>
            /// Video capture device unique identifier, as returned by <see cref="PeerConnection.GetVideoCaptureDevicesAsync"/>.
            /// </summary>
            public string VideoDeviceId;

            /// <summary>
            /// Optional video profile unique identifier to use.
            /// Ignored if the video capture device specified by <see cref="VideoDeviceId"/> does not
            /// support video profiles.
            /// </summary>
            /// <remarks>
            /// This is generally preferred over <see cref="VideoProfileKind"/> to get full
            /// control over the video profile selection. Specifying both this and <see cref="VideoProfileKind"/>
            /// is discouraged, as it over-constraints the selection algorithm.
            /// </remarks>
            /// <seealso xref="MediaCapture.IsVideoProfileSupported(string)"/>
            public string VideoProfileId;

            /// <summary>
            /// Optional video profile kind to select a video profile from.
            /// Ignored if the video capture device specified by <see cref="VideoDeviceId"/> does not
            /// support video profiles.
            /// </summary>
            /// <remarks>
            /// This is generally preferred over <see cref="VideoProfileId"/> to find a matching
            /// capture format (resolution and/or framerate) when one does not care about which video
            /// profile provides this capture format. Specifying both this and <see cref="VideoProfileId"/>
            /// is discouraged, as it over-constraints the selection algorithm.
            /// </remarks>
            /// <seealso xref="MediaCapture.IsVideoProfileSupported(string)"/>
            public VideoProfileKind VideoProfileKind;

            /// <summary>
            /// Optional capture resolution width, in pixels, or zero for no constraint.
            /// </summary>
            public uint Width;

            /// <summary>
            /// Optional capture resolution height, in pixels, or zero for no constraint.
            /// </summary>
            public uint Height;

            /// <summary>
            /// Optional capture framerate, in frames per second (FPS), or zero for no constraint.
            /// </summary>
            public double Framerate;

            /// <summary>
            /// Enable Mixed Reality Capture (MRC). This flag is ignored if the platform doesn't support MRC.
            /// </summary>
            public mrsBool EnableMixedRealityCapture;

            /// <summary>
            /// When MRC is enabled, enable the on-screen recording indicator.
            /// </summary>
            public mrsBool EnableMRCRecordingIndicator;

            /// <summary>
            /// Constructor for creating a local video device initialization settings marshaling struct.
            /// </summary>
            /// <param name="settings">The settings to initialize the newly created marshaling struct.</param>
            /// <seealso cref="DeviceVideoTrackSource.CreateAsync(LocalVideoDeviceInitConfig)"/>
            public LocalVideoDeviceMarshalInitConfig(LocalVideoDeviceInitConfig settings)
            {
                if (settings != null)
                {
                    VideoDeviceId = settings.videoDevice.id;
                    VideoProfileId = settings.videoProfileId;
                    VideoProfileKind = settings.videoProfileKind;
                    Width = settings.width.GetValueOrDefault(0);
                    Height = settings.height.GetValueOrDefault(0);
                    Framerate = settings.framerate.GetValueOrDefault(0.0);
                    EnableMixedRealityCapture = (mrsBool)settings.enableMrc;
                    EnableMRCRecordingIndicator = (mrsBool)settings.enableMrcRecordingIndicator;
                }
                else
                {
                    VideoDeviceId = string.Empty;
                    VideoProfileId = string.Empty;
                    VideoProfileKind = VideoProfileKind.Unspecified;
                    Width = 0;
                    Height = 0;
                    Framerate = 0.0;
                    EnableMixedRealityCapture = mrsBool.True;
                    EnableMRCRecordingIndicator = mrsBool.True;
                }
            }
        }

        #region P/Invoke static functions

        [DllImport(Utils.dllPath, CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Ansi,
            EntryPoint = "mrsDeviceVideoTrackSourceCreate")]
        public static unsafe extern uint DeviceVideoTrackSource_Create(
            in LocalVideoDeviceMarshalInitConfig config, out DeviceVideoTrackSourceHandle sourceHandle);

        #endregion
    }
}
