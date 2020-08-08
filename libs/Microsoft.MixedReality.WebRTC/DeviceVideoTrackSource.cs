// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading;
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
        /// retrieved by <see cref="DeviceVideoTrackSource.GetCaptureFormatsAsync(string)"/>.
        /// </remarks>
        public double? framerate;
    }

    /// <summary>
    /// Video profile.
    /// </summary>
    public class VideoProfile
    {
        /// <summary>
        /// Unique identifier of the video profile.
        /// </summary>
        public string uniqueId;
    }

    /// <summary>
    /// Implementation of a video track source producing frames captured from a video capture device (webcam).
    /// </summary>
    public class DeviceVideoTrackSource : VideoTrackSource
    {
        /// <inheritdoc/>
        public override VideoEncoding FrameEncoding => VideoEncoding.I420A;

        /// <summary>
        /// Get the list of video capture devices available on the local host machine.
        /// </summary>
        /// <returns>The list of available video capture devices.</returns>
        /// <remarks>
        /// Assign one of the returned <see cref="VideoCaptureDevice"/> to the <see cref="LocalVideoDeviceInitConfig.videoDevice"/>
        /// field to force a local video track to use that device when creating it with
        /// <see cref="CreateAsync(LocalVideoDeviceInitConfig)"/>.
        /// </remarks>
        public static Task<IReadOnlyList<VideoCaptureDevice>> GetCaptureDevicesAsync()
        {
            // Ensure the logging system is ready before using PInvoke.
            MainEventSource.Log.Initialize();

            // Always call this on a background thread, this is possibly the first call to the library so needs
            // to initialize the global factory, and that cannot be done from the main UI thread on UWP.
            return Task.Run(() =>
            {
                var devices = new List<VideoCaptureDevice>();
                var eventWaitHandle = new ManualResetEventSlim(initialState: false);
                Exception resultException = null;
                var wrapper = new DeviceVideoTrackSourceInterop.EnumVideoCaptureDevicesWrapper()
                {
                    enumCallback = (in VideoCaptureDevice device) => devices.Add(device),
                    completedCallback = (Exception ex) =>
                    {
                        resultException = ex;

                        // On enumeration end, signal the caller thread
                        eventWaitHandle.Set();
                    },
                    // Keep delegates alive
                    EnumTrampoline = DeviceVideoTrackSourceInterop.VideoCaptureDevice_EnumCallback,
                    CompletedTrampoline = DeviceVideoTrackSourceInterop.VideoCaptureDevice_EnumCompletedCallback
                };

                // Prevent garbage collection of the wrapper delegates until the enumeration is completed.
                var handle = GCHandle.Alloc(wrapper, GCHandleType.Normal);
                IntPtr userData = GCHandle.ToIntPtr(handle);

                // Execute the native async callback
                uint res = DeviceVideoTrackSourceInterop.EnumVideoCaptureDevicesAsync(
                    wrapper.EnumTrampoline, userData, wrapper.CompletedTrampoline, userData);
                if (res != Utils.MRS_SUCCESS)
                {
                    resultException = Utils.GetExceptionForErrorCode(res);
                }
                else
                {
                    // Wait for end of enumerating
                    eventWaitHandle.Wait();
                }

                // Clean-up and release the wrapper delegates
                handle.Free();

                if (resultException != null)
                {
                    throw resultException;
                }

                return (IReadOnlyList<VideoCaptureDevice>)devices;
            });
        }

        /// <summary>
        /// Enumerate all the video profiles associated with the specified video capture device, if any.
        /// </summary>
        /// <param name="deviceId">Unique identifier of the video capture device to enumerate the
        /// capture formats of, as retrieved from the <see cref="VideoCaptureDevice.id"/> field of
        /// a capture device enumerated with <see cref="GetCaptureDevicesAsync"/>.</param>
        /// <returns>The list of available video profiles for the specified video capture device.</returns>
        /// <remarks>
        /// If the video capture device does not support video profiles, the function succeeds
        /// and returns an empty list.
        /// 
        /// This is equivalent to:
        /// <code>
        /// GetCaptureProfilesAsync(deviceId, VideoProfileKind.Unspecified);
        /// </code>
        /// </remarks>
        /// <seealso cref="GetCaptureProfilesAsync(string, VideoProfileKind)"/>
        public static Task<IReadOnlyList<VideoProfile>> GetCaptureProfilesAsync(string deviceId)
        {
            return GetCaptureProfilesAsync(deviceId, VideoProfileKind.Unspecified);
        }

        /// <summary>
        /// Enumerate the video profiles associated with the specified video capture device, if any,
        /// and restricted to the specified video profile kind.
        /// </summary>
        /// <param name="deviceId">Unique identifier of the video capture device to enumerate the
        /// capture formats of, as retrieved from the <see cref="VideoCaptureDevice.id"/> field of
        /// a capture device enumerated with <see cref="GetCaptureDevicesAsync"/>.</param>
        /// <param name="profileKind">Kind of video profile to enumerate. Specify
        /// <see cref="VideoProfileKind.Unspecified"/> to enumerate all profiles.</param>
        /// <returns>The list of available video profiles for the specified video capture device.</returns>
        /// <remarks>If the video capture device does not support video profiles, the function succeeds
        /// and returns an empty list.</remarks>
        /// <seealso cref="GetCaptureProfilesAsync(string)"/>
        public static Task<IReadOnlyList<VideoProfile>> GetCaptureProfilesAsync(string deviceId, VideoProfileKind profileKind)
        {
            // Ensure the logging system is ready before using PInvoke.
            MainEventSource.Log.Initialize();

            // Always call this on a background thread, this is possibly the first call to the library so needs
            // to initialize the global factory, and that cannot be done from the main UI thread on UWP.
            return Task.Run(() => {
                var profiles = new List<VideoProfile>();
                var eventWaitHandle = new ManualResetEventSlim(initialState: false);
                Exception resultException = null;
                var wrapper = new DeviceVideoTrackSourceInterop.EnumVideoProfilesWrapper()
                {
                    enumCallback = (in VideoProfile profile) => profiles.Add(profile),
                    completedCallback = (Exception ex) => {
                        resultException = ex;

                        // On enumeration end, signal the caller thread
                        eventWaitHandle.Set();
                    },
                    // Keep delegates alive
                    EnumTrampoline = DeviceVideoTrackSourceInterop.VideoProfile_EnumCallback,
                    CompletedTrampoline = DeviceVideoTrackSourceInterop.VideoProfile_EnumCompletedCallback
                };

                // Prevent garbage collection of the wrapper delegates until the enumeration is completed.
                var handle = GCHandle.Alloc(wrapper, GCHandleType.Normal);
                IntPtr userData = GCHandle.ToIntPtr(handle);

                // Execute the native async callback.
                uint res = DeviceVideoTrackSourceInterop.EnumVideoProfilesAsync(deviceId, profileKind,
                    wrapper.EnumTrampoline, userData, wrapper.CompletedTrampoline, userData);
                if (res != Utils.MRS_SUCCESS)
                {
                    resultException = Utils.GetExceptionForErrorCode(res);
                }
                else
                {
                    // Wait for end of enumerating
                    eventWaitHandle.Wait();
                }

                // Clean-up and release the wrapper delegates
                handle.Free();

                if (resultException != null)
                {
                    throw resultException;
                }

                return (IReadOnlyList<VideoProfile>)profiles;
            });
        }

        /// <summary>
        /// Enumerate the video capture formats for the specified video capture device.
        /// </summary>
        /// <param name="deviceId">Unique identifier of the video capture device to enumerate the
        /// capture formats of, as retrieved from the <see cref="VideoCaptureDevice.id"/> field of
        /// a capture device enumerated with <see cref="GetCaptureDevicesAsync"/>.</param>
        /// <returns>The list of available video capture formats for the specified video capture device.</returns>
        public static Task<IReadOnlyList<VideoCaptureFormat>> GetCaptureFormatsAsync(string deviceId)
        {
            return GetCaptureFormatsAsyncImpl(deviceId, string.Empty, VideoProfileKind.Unspecified);
        }

        /// <summary>
        /// Enumerate the video capture formats for the specified video capture device and video profile.
        /// </summary>
        /// <param name="deviceId">Unique identifier of the video capture device to enumerate the
        /// capture formats of, as retrieved from the <see cref="VideoCaptureDevice.id"/> field of
        /// a capture device enumerated with <see cref="GetCaptureDevicesAsync"/>.</param>
        /// <param name="profileId">Unique identifier of the video profile to enumerate the capture formats of,
        /// as retrieved from <see cref="VideoCaptureDevice.id"/> field of a capture device enumerated with
        /// <see cref="GetCaptureDevicesAsync"/>.</param>
        /// <returns>The list of available video capture formats for the specified video capture device.</returns>
        public static Task<IReadOnlyList<VideoCaptureFormat>> GetCaptureFormatsAsync(
            string deviceId, string profileId)
        {
            return GetCaptureFormatsAsyncImpl(deviceId, profileId, VideoProfileKind.Unspecified);
        }

        /// <summary>
        /// Enumerate the video capture formats for the specified video capture device and video profile.
        /// </summary>
        /// <param name="deviceId">Unique identifier of the video capture device to enumerate the
        /// capture formats of, as retrieved from the <see cref="VideoCaptureDevice.id"/> field of
        /// a capture device enumerated with <see cref="GetCaptureDevicesAsync"/>.</param>
        /// <param name="profileKind">Kind of video profile to enumerate the capture formats of.</param>
        /// <returns>The list of available video capture formats for the specified video capture device.</returns>
        public static Task<IReadOnlyList<VideoCaptureFormat>> GetCaptureFormatsAsync(
            string deviceId, VideoProfileKind profileKind)
        {
            return GetCaptureFormatsAsyncImpl(deviceId, string.Empty, profileKind);
        }

        private static Task<IReadOnlyList<VideoCaptureFormat>> GetCaptureFormatsAsyncImpl(
            string deviceId, string profileId, VideoProfileKind profileKind)
        {
            // Ensure the logging system is ready before using PInvoke.
            MainEventSource.Log.Initialize();

            // Always call this on a background thread, this is possibly the first call to the library so needs
            // to initialize the global factory, and that cannot be done from the main UI thread on UWP.
            return Task.Run(() =>
            {
                var formats = new List<VideoCaptureFormat>();
                var eventWaitHandle = new ManualResetEventSlim(initialState: false);
                Exception resultException = null;
                var wrapper = new DeviceVideoTrackSourceInterop.EnumVideoCaptureFormatsWrapper()
                {
                    enumCallback = (in VideoCaptureFormat format) => formats.Add(format),
                    completedCallback = (Exception ex) =>
                    {
                        resultException = ex;

                        // On enumeration end, signal the caller thread
                        eventWaitHandle.Set();
                    },
                    // Keep delegates alive
                    EnumTrampoline = DeviceVideoTrackSourceInterop.VideoCaptureFormat_EnumCallback,
                    CompletedTrampoline = DeviceVideoTrackSourceInterop.VideoCaptureFormat_EnumCompletedCallback,
                };

                // Prevent garbage collection of the wrapper delegates until the enumeration is completed.
                var handle = GCHandle.Alloc(wrapper, GCHandleType.Normal);
                IntPtr userData = GCHandle.ToIntPtr(handle);

                // Execute the native async callback.
                uint res = DeviceVideoTrackSourceInterop.EnumVideoCaptureFormatsAsync(deviceId, profileId, profileKind,
                    wrapper.EnumTrampoline, userData, wrapper.CompletedTrampoline, userData);
                if (res != Utils.MRS_SUCCESS)
                {
                    resultException = Utils.GetExceptionForErrorCode(res);
                }
                else
                {
                    // Wait for end of enumerating
                    eventWaitHandle.Wait();
                }

                // Clean-up and release the wrapper delegates
                handle.Free();

                if (resultException != null)
                {
                    throw resultException;
                }

                return (IReadOnlyList<VideoCaptureFormat>)formats;
            });
        }

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
        /// fallback mechanism selecting a closest match. Developers should use <see cref="GetCaptureFormatsAsync(string)"/>
        /// to list the supported formats ahead of calling <see cref="CreateAsync(LocalVideoDeviceInitConfig)"/>, and can
        /// build their own fallback mechanism on top of this call if needed.
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
