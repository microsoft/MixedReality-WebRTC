// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Runtime.InteropServices;
using System.Text;

namespace Microsoft.MixedReality.WebRTC.Interop
{
    /// <summary>
    /// Handle to a native video track source object.
    /// </summary>
    internal sealed class VideoTrackSourceHandle : SafeHandle
    {
        /// <summary>
        /// Check if the current handle is invalid, which means it is not referencing
        /// an actual native object. Note that a valid handle only means that the internal
        /// handle references a native object, but does not guarantee that the native
        /// object is still accessible. It is only safe to access the native object if
        /// the handle is not closed, which implies it being valid.
        /// </summary>
        public override bool IsInvalid => (handle == IntPtr.Zero);

        /// <summary>
        /// Default constructor for an invalid handle.
        /// </summary>
        public VideoTrackSourceHandle() : base(IntPtr.Zero, ownsHandle: true)
        {
        }

        /// <summary>
        /// Constructor for a valid handle referencing the given native object.
        /// </summary>
        /// <param name="handle">The valid internal handle to the native object.</param>
        public VideoTrackSourceHandle(IntPtr handle) : base(IntPtr.Zero, ownsHandle: true)
        {
            SetHandle(handle);
        }

        /// <summary>
        /// Release the native object while the handle is being closed.
        /// </summary>
        /// <returns>Return <c>true</c> if the native object was successfully released.</returns>
        protected override bool ReleaseHandle()
        {
            VideoTrackSourceInterop.VideoTrackSource_RemoveRef(handle);
            return true;
        }
    }

    internal class VideoTrackSourceInterop
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
            /// <seealso cref="VideoTrackSource.CreateFromDeviceAsync(LocalVideoDeviceInitConfig)"/>
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

        #region Unmanaged delegates

        // Note - none of those method arguments can be SafeHandle; use IntPtr instead.

        [UnmanagedFunctionPointer(CallingConvention.StdCall, CharSet = CharSet.Ansi)]
        public delegate void I420AVideoFrameUnmanagedCallback(IntPtr userData, in I420AVideoFrame frame);

        #endregion

        #region P/Invoke static functions

        [DllImport(Utils.dllPath, CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Ansi,
            EntryPoint = "mrsVideoTrackSourceAddRef")]
        public static unsafe extern void VideoTrackSource_AddRef(VideoTrackSourceHandle handle);

        // Note - This is used during SafeHandle.ReleaseHandle(), so cannot use VideoTrackSourceHandle
        [DllImport(Utils.dllPath, CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Ansi,
            EntryPoint = "mrsVideoTrackSourceRemoveRef")]
        public static unsafe extern void VideoTrackSource_RemoveRef(IntPtr handle);

        [DllImport(Utils.dllPath, CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Ansi,
            EntryPoint = "mrsVideoTrackSourceSetName")]
        public static unsafe extern void VideoTrackSource_SetName(VideoTrackSourceHandle handle, string name);

        [DllImport(Utils.dllPath, CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Ansi,
            EntryPoint = "mrsVideoTrackSourceGetName")]
        public static unsafe extern uint VideoTrackSource_GetName(VideoTrackSourceHandle handle, StringBuilder buffer,
            ref ulong bufferCapacity);

        [DllImport(Utils.dllPath, CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Ansi,
            EntryPoint = "mrsVideoTrackSourceSetUserData")]
        public static unsafe extern void VideoTrackSource_SetUserData(VideoTrackSourceHandle handle, IntPtr userData);

        [DllImport(Utils.dllPath, CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Ansi,
            EntryPoint = "mrsVideoTrackSourceGetUserData")]
        public static unsafe extern IntPtr VideoTrackSource_GetUserData(VideoTrackSourceHandle handle);

        [DllImport(Utils.dllPath, CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Ansi,
            EntryPoint = "mrsVideoTrackSourceCreateFromDevice")]
        public static unsafe extern uint VideoTrackSource_CreateFromDevice(
            in LocalVideoDeviceMarshalInitConfig config, out VideoTrackSourceHandle sourceHandle);

        [DllImport(Utils.dllPath, CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Ansi,
            EntryPoint = "mrsVideoTrackSourceRegisterFrameCallback")]
        public static extern void VideoTrackSource_RegisterFrameCallback(VideoTrackSourceHandle trackHandle,
            I420AVideoFrameUnmanagedCallback callback, IntPtr userData);

        #endregion

        public class InteropCallbackArgs
        {
            public VideoTrackSource Source;
            public I420AVideoFrameUnmanagedCallback I420AFrameCallback;
        }

        [MonoPInvokeCallback(typeof(I420AVideoFrameUnmanagedCallback))]
        public static void I420AFrameCallback(IntPtr userData, in I420AVideoFrame frame)
        {
            var source = Utils.ToWrapper<VideoTrackSource>(userData);
            source.OnI420AFrameReady(frame);
        }
    }
}
