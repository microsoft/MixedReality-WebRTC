// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Runtime.CompilerServices;
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
        /// Marshaling struct for enumerating a video capture device.
        /// </summary>
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
        internal ref struct VideoCaptureDeviceMarshalInfo
        {
            public string Id;
            public string Name;
        }

        /// <summary>
        /// Marshaling struct for enumerating a video profile.
        /// </summary>
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
        internal ref struct VideoProfileMarshalInfo
        {
            public string Id;
        }

        /// <summary>
        /// Marshaling struct for enumerating a video capture format.
        /// </summary>
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
        internal ref struct VideoCaptureFormatMarshalInfo
        {
            public uint Width;
            public uint Height;
            public float Framerate;
            public uint FourCC;
        }

        // Callbacks for internal enumeration implementation only
        public delegate void VideoCaptureDeviceEnumCallbackImpl(in VideoCaptureDevice device);
        public delegate void VideoCaptureDeviceEnumCompletedCallbackImpl(Exception ex);
        public delegate void VideoProfileEnumCallbackImpl(in VideoProfile device);
        public delegate void VideoProfileEnumCompletedCallbackImpl(Exception ex);
        public delegate void VideoCaptureFormatEnumCallbackImpl(in VideoCaptureFormat format);
        public delegate void VideoCaptureFormatEnumCompletedCallbackImpl(Exception ex);

        public class EnumVideoCaptureDevicesWrapper
        {
            public VideoCaptureDeviceEnumCallbackImpl enumCallback;
            public VideoCaptureDeviceEnumCompletedCallbackImpl completedCallback;
            // Keep delegates alive!
            public VideoCaptureDeviceEnumCallback EnumTrampoline;
            public VideoCaptureDeviceEnumCompletedCallback CompletedTrampoline;
        }

        [MonoPInvokeCallback(typeof(VideoCaptureDeviceEnumCallback))]
        public static void VideoCaptureDevice_EnumCallback(IntPtr userData, in VideoCaptureDeviceMarshalInfo deviceInfo)
        {
            var wrapper = Utils.ToWrapper<EnumVideoCaptureDevicesWrapper>(userData);
            var device = new VideoCaptureDevice();
            device.id = deviceInfo.Id;
            device.name = deviceInfo.Name;
            wrapper.enumCallback(device); // this is mandatory, never null
        }

        [MonoPInvokeCallback(typeof(VideoCaptureDeviceEnumCompletedCallback))]
        public static void VideoCaptureDevice_EnumCompletedCallback(IntPtr userData, uint resultCode)
        {
            var exception = Utils.GetExceptionForErrorCode(resultCode);
            var wrapper = Utils.ToWrapper<EnumVideoCaptureDevicesWrapper>(userData);
            wrapper.completedCallback(exception); // this is optional, allows to be null
        }

        public class EnumVideoProfilesWrapper
        {
            public VideoProfileEnumCallbackImpl enumCallback;
            public VideoProfileEnumCompletedCallbackImpl completedCallback;
            // Keep delegates alive!
            public VideoProfileEnumCallback EnumTrampoline;
            public VideoProfileEnumCompletedCallback CompletedTrampoline;
        }

        [MonoPInvokeCallback(typeof(VideoProfileEnumCallback))]
        public static void VideoProfile_EnumCallback(IntPtr userData, in VideoProfileMarshalInfo profileInfo)
        {
            var wrapper = Utils.ToWrapper<EnumVideoProfilesWrapper>(userData);
            var profile = new VideoProfile();
            profile.uniqueId = profileInfo.Id;
            wrapper.enumCallback(profile); // this is mandatory, never null
        }

        [MonoPInvokeCallback(typeof(VideoProfileEnumCompletedCallback))]
        public static void VideoProfile_EnumCompletedCallback(IntPtr userData, uint resultCode)
        {
            var exception = Utils.GetExceptionForErrorCode(resultCode);
            var wrapper = Utils.ToWrapper<EnumVideoProfilesWrapper>(userData);
            wrapper.completedCallback(exception); // this is optional, allows to be null
        }

        public class EnumVideoCaptureFormatsWrapper
        {
            public VideoCaptureFormatEnumCallbackImpl enumCallback;
            public VideoCaptureFormatEnumCompletedCallbackImpl completedCallback;
            // Keep delegates alive!
            public VideoCaptureFormatEnumCallback EnumTrampoline;
            public VideoCaptureFormatEnumCompletedCallback CompletedTrampoline;
        }

        [MonoPInvokeCallback(typeof(VideoCaptureFormatEnumCallback))]
        public static void VideoCaptureFormat_EnumCallback(IntPtr userData, in VideoCaptureFormatMarshalInfo formatInfo)
        {
            var wrapper = Utils.ToWrapper<EnumVideoCaptureFormatsWrapper>(userData);
            var format = new VideoCaptureFormat();
            format.width = formatInfo.Width;
            format.height = formatInfo.Height;
            format.framerate = formatInfo.Framerate;
            format.fourcc = formatInfo.FourCC;
            wrapper.enumCallback(format); // this is mandatory, never null
        }

        [MonoPInvokeCallback(typeof(VideoCaptureFormatEnumCompletedCallback))]
        public static void VideoCaptureFormat_EnumCompletedCallback(IntPtr userData, uint resultCode)
        {
            var exception = Utils.GetExceptionForErrorCode(resultCode);
            var wrapper = Utils.ToWrapper<EnumVideoCaptureFormatsWrapper>(userData);
            wrapper.completedCallback(exception); // this is optional, allows to be null
        }

        /// <summary>
        /// Marshaling struct for initializing settings when opening a local video device.
        /// </summary>
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
        internal ref struct LocalVideoDeviceMarshalInitConfig
        {
            /// <summary>
            /// Video capture device unique identifier, as returned by <see cref="DeviceVideoTrackSource.GetCaptureDevicesAsync"/>.
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


        #region Reverse P/Invoke delegates

        // Note - none of those method arguments can be SafeHandle; use IntPtr instead.

        [UnmanagedFunctionPointer(CallingConvention.StdCall, CharSet = CharSet.Ansi)]
        public delegate void VideoCaptureDeviceEnumCallback(IntPtr userData, in VideoCaptureDeviceMarshalInfo deviceInfo);

        [UnmanagedFunctionPointer(CallingConvention.StdCall, CharSet = CharSet.Ansi)]
        public delegate void VideoCaptureDeviceEnumCompletedCallback(IntPtr userData, uint resultCode);

        [UnmanagedFunctionPointer(CallingConvention.StdCall, CharSet = CharSet.Ansi)]
        public delegate void VideoProfileEnumCallback(IntPtr userData, in VideoProfileMarshalInfo profileInfo);

        [UnmanagedFunctionPointer(CallingConvention.StdCall, CharSet = CharSet.Ansi)]
        public delegate void VideoProfileEnumCompletedCallback(IntPtr userData, uint resultCode);

        [UnmanagedFunctionPointer(CallingConvention.StdCall, CharSet = CharSet.Ansi)]
        public delegate void VideoCaptureFormatEnumCallback(IntPtr userData, in VideoCaptureFormatMarshalInfo formatInfo);

        [UnmanagedFunctionPointer(CallingConvention.StdCall, CharSet = CharSet.Ansi)]
        public delegate void VideoCaptureFormatEnumCompletedCallback(IntPtr userData, uint resultCode);

        #endregion


        #region P/Invoke static functions

        [DllImport(Utils.dllPath, CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Ansi,
            EntryPoint = "mrsEnumVideoCaptureDevicesAsync")]
        public static extern uint EnumVideoCaptureDevicesAsync(VideoCaptureDeviceEnumCallback enumCallback, IntPtr userData,
            VideoCaptureDeviceEnumCompletedCallback completedCallback, IntPtr completedUserData);

        [DllImport(Utils.dllPath, CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Ansi,
            EntryPoint = "mrsEnumVideoProfilesAsync")]
        public static extern uint EnumVideoProfilesAsync(string deviceId, VideoProfileKind profileKind,
            VideoProfileEnumCallback enumCallback, IntPtr userData,
            VideoProfileEnumCompletedCallback completedCallback, IntPtr completedUserData);

        [DllImport(Utils.dllPath, CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Ansi,
            EntryPoint = "mrsEnumVideoCaptureFormatsAsync")]
        public static extern uint EnumVideoCaptureFormatsAsync(string deviceId, string profileId, VideoProfileKind profileKind,
            VideoCaptureFormatEnumCallback enumCallback, IntPtr userData,
            VideoCaptureFormatEnumCompletedCallback completedCallback, IntPtr completedUserData);

        [DllImport(Utils.dllPath, CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Ansi,
            EntryPoint = "mrsDeviceVideoTrackSourceCreate")]
        public static unsafe extern uint DeviceVideoTrackSource_Create(
            in LocalVideoDeviceMarshalInitConfig config, out DeviceVideoTrackSourceHandle sourceHandle);

        #endregion
    }
}
