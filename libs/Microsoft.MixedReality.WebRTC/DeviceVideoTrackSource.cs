// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Threading.Tasks;
using Microsoft.MixedReality.WebRTC.Interop;
using Microsoft.MixedReality.WebRTC.Tracing;

namespace Microsoft.MixedReality.WebRTC
{
    /// <summary>
    /// Configuration to initialize capture on a local video device (webcam).
    /// </summary>
    public class LocalVideoDeviceInitConfig
    {
        /// <summary>
        /// Optional video capture device to use for capture.
        /// Use the default device if not specified.
        /// </summary>
        public VideoCaptureDevice videoDevice = default;

        /// <summary>
        /// Optional unique identifier of the video profile to use for capture,
        /// if the device supports video profiles, as retrieved by one of:
        /// - <see xref="MediaCapture.FindAllVideoProfiles"/>
        /// - <see xref="MediaCapture.FindKnownVideoProfiles"/>
        /// This requires <see cref="videoDevice"/> to be specified.
        /// </summary>
        public string videoProfileId = string.Empty;

        /// <summary>
        /// Optional video profile kind to restrict the list of video profiles to consider.
        /// Note that this is not exclusive with <see cref="videoProfileId"/>, although in
        /// practice it is recommended to specify only one or the other.
        /// This requires <see cref="videoDevice"/> to be specified.
        /// </summary>
        public VideoProfileKind videoProfileKind = VideoProfileKind.Unspecified;

        /// <summary>
        /// Enable Mixed Reality Capture (MRC) on devices supporting the feature.
        /// This setting is silently ignored on device not supporting MRC.
        /// </summary>
        /// <remarks>
        /// This is only supported on UWP.
        /// </remarks>
        public bool enableMrc = true;

        /// <summary>
        /// Display the on-screen recording indicator while MRC is enabled.
        /// This setting is silently ignored on device not supporting MRC, or if
        /// <see cref="enableMrc"/> is set to <c>false</c>.
        /// </summary>
        /// <remarks>
        /// This is only supported on UWP.
        /// </remarks>
        public bool enableMrcRecordingIndicator = true;

        /// <summary>
        /// Optional capture resolution width, in pixels.
        /// This must be a resolution width the device supports.
        /// </summary>
        public uint? width;

        /// <summary>
        /// Optional capture resolution height, in pixels.
        /// This must be a resolution width the device supports.
        /// </summary>
        public uint? height;

        /// <summary>
        /// Optional capture frame rate, in frames per second (FPS).
        /// This must be a capture framerate the device supports.
        /// </summary>
        /// <remarks>
        /// This is compared by strict equality, so is best left unspecified or to an exact value
        /// retrieved by <see cref="PeerConnection.GetVideoCaptureFormatsAsync"/>.
        /// </remarks>
        public double? framerate;
    }

    /// <summary>
    /// Implementation of a video track source producing frames captured from a video capture device (webcam).
    /// </summary>
    public class DeviceVideoTrackSource : VideoTrackSource
    {
        /// <summary>
        /// Create a video track source using a local video capture device (webcam).
        /// 
        /// The video track source produces raw video frames by capturing them from a capture device accessible
        /// from the local host machine, generally a USB webcam or built-in device camera. The video source
        /// initially starts in the capturing state, and will remain live for as long as the source is alive.
        /// Once the source is not live anymore (ended), it cannot be restarted. A new source must be created to
        /// use the same video capture device again.
        /// 
        /// The source can be used to create one or more local video tracks (<see cref="LocalVideoTrack"/>), which
        /// once added to a video transceiver allow the video frames to be sent to a remote peer. The source itself
        /// is not associated with any peer connection, and can be used to create local video tracks from multiple
        /// peer connections at once, thereby being shared amongst those peer connections.
        /// 
        /// The source is owned by the user, who must ensure it stays alive while being in use by at least one local
        /// video track. Once it is not used anymore, the user is in charge of disposing of the source. Disposing of
        /// a source still in use by a local video track is undefined behavior.
        /// </summary>
        /// <param name="initConfig">Optional configuration to initialize the video capture on the device.</param>
        /// <returns>The newly create video track source.</returns>
        /// <remarks>
        /// On UWP this requires the "webcam" capability.
        /// See <see href="https://docs.microsoft.com/en-us/windows/uwp/packaging/app-capability-declarations"/>
        /// for more details.
        /// 
        /// The video capture device may be accessed several times during the initializing process,
        /// generally once for listing and validating the capture format, and once for actually starting
        /// the video capture. This is a limitation of the OS and/or hardware.
        /// 
        /// Note that the capture device must support a capture format with the given constraints of profile
        /// ID or kind, capture resolution, and framerate, otherwise the call will fail. That is, there is no
        /// fallback mechanism selecting a closest match. Developers should use
        /// <see cref="PeerConnection.GetVideoCaptureFormatsAsync(string)"/> to list the supported formats ahead
        /// of calling <see cref="CreateAsync(LocalVideoDeviceInitConfig)"/>, and can build their own
        /// fallback mechanism on top of this call if needed.
        /// </remarks>
        /// <example>
        /// Create a video track source with Mixed Reality Capture (MRC) enabled.
        /// This assumes that the platform supports MRC. Note that if MRC is not available
        /// the call will still succeed, but will return a track without MRC enabled.
        /// <code>
        /// var initConfig = new LocalVideoDeviceInitConfig
        /// {
        ///     enableMrc = true
        /// };
        /// var videoSource = await VideoTrackSource.CreateFromDeviceAsync(initConfig);
        /// </code>
        /// Create a video track source from a local webcam, asking for a capture format suited for video conferencing,
        /// and a target framerate of 30 frames per second (FPS). The implementation will select an appropriate
        /// capture resolution. This assumes that the device supports video profiles, and has at least one capture
        /// format supporting exactly 30 FPS capture associated with the VideoConferencing profile. Otherwise the call
        /// will fail.
        /// <code>
        /// var initConfig = new LocalVideoDeviceInitConfig
        /// {
        ///     videoProfileKind = VideoProfileKind.VideoConferencing,
        ///     framerate = 30.0
        /// };
        /// var videoSource = await VideoTrackSource.CreateFromDeviceAsync(initConfig);
        /// </code>
        /// </example>
        /// <seealso cref="LocalVideoTrack.CreateFromSource(VideoTrackSource, LocalVideoTrackInitConfig)"/>
        public static Task<DeviceVideoTrackSource> CreateAsync(LocalVideoDeviceInitConfig initConfig = null)
        {
            // Ensure the logging system is ready before using PInvoke.
            MainEventSource.Log.Initialize();

            return Task.Run(() =>
            {
                // On UWP this cannot be called from the main UI thread, so always call it from
                // a background worker thread.

                var config = new DeviceVideoTrackSourceInterop.LocalVideoDeviceMarshalInitConfig(initConfig);
                uint ret = DeviceVideoTrackSourceInterop.DeviceVideoTrackSource_Create(in config, out DeviceVideoTrackSourceHandle handle);
                Utils.ThrowOnErrorCode(ret);
                return new DeviceVideoTrackSource(handle);
            });
        }

        internal DeviceVideoTrackSource(VideoTrackSourceHandle nativeHandle) : base(nativeHandle)
        {
        }

        /// <inheritdoc/>
        public override string ToString()
        {
            return $"(DeviceVideoTrackSource)\"{Name}\"";
        }
    }
}
