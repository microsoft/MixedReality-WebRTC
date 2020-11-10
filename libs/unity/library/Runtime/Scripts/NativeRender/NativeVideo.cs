// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Linq;

namespace Microsoft.MixedReality.WebRTC.Unity
{
    public delegate void LogCallback(string str);

    public enum VideoKind : int
    {
        None = 0,
        I420 = 1,
        ARGB = 2
    }

    public class TextureDesc
    {
        public IntPtr texture;
        public int width;
        public int height;
    }

    /// <summary>
    /// Managed object which holds reference to a video in the native UnityPlugin.
    /// </summary>
    internal class NativeVideo : IDisposable
    {
        private IntPtr _videoHandle;

        /// <summary>
        /// Creates a NativeRenderer for the provided PeerConnection.
        /// </summary>
        /// <param name="peerConnection"></param>
        public NativeVideo(IntPtr videoHandle)
        {
            uint res = NativeVideoInterop.Create(videoHandle);
            WebRTC.Interop.Utils.ThrowOnErrorCode(res);
            _videoHandle = videoHandle;
            // _nativeHandle = peerConnection.NativeHandle;
        }

        /// <summary>
        /// Starts renderering the local video stream to the provided textures.
        /// Only kI420 is currently supported.
        /// </summary>
        /// <param name="format"></param>
        /// <param name="textures"></param>
        public void EnableLocalVideo(VideoKind format, TextureDesc[] textures)
        {
            var interopTextures = textures.Select(item => new NativeVideoInterop.TextureDesc
            {
                texture = item.texture,
                width = item.width,
                height = item.height
            }).ToArray();
            uint res = NativeVideoInterop.EnableLocalVideo(_videoHandle, format, interopTextures, interopTextures.Length);
            WebRTC.Interop.Utils.ThrowOnErrorCode(res);
        }

        /// <summary>
        /// Stops renderering the local video stream.
        /// </summary>
        public void DisableLocalVideo()
        {
            uint res = NativeVideoInterop.DisableLocalVideo(_videoHandle);
            WebRTC.Interop.Utils.ThrowOnErrorCode(res);
        }

        /// <summary>
        /// Starts rendering the remote video stream to the provided textures.
        /// Only kI420 is currently supported.
        /// </summary>
        /// <param name="format"></param>
        /// <param name="textures"></param>
        public void EnableRemoteVideo(VideoKind format, TextureDesc[] textures)
        {
            var interopTextures = textures.Select(item => new NativeVideoInterop.TextureDesc
            {
                texture = item.texture,
                width = item.width,
                height = item.height
            }).ToArray();

            uint res = NativeVideoInterop.EnableRemoteVideo(_videoHandle, format, interopTextures, interopTextures.Length);
            WebRTC.Interop.Utils.ThrowOnErrorCode(res);
        }

        /// <summary>
        /// Stops rendering the remote video stream.
        /// </summary>
        public void DisableRemoteVideo()
        {
            uint res = NativeVideoInterop.DisableRemoteVideo(_videoHandle);
            WebRTC.Interop.Utils.ThrowOnErrorCode(res);
        }

        /// <summary>
        /// Returns the native rendering update method to be called by Unity.
        /// </summary>
        /// <returns></returns>
        public static IntPtr GetVideoUpdateMethod()
        {
            return NativeVideoInterop.GetVideoUpdateMethod();
        }

        /// <summary>
        /// Sets callback handlers for the logging of debug, warning, and error messages.
        /// </summary>
        public static void SetLoggingFunctions(LogCallback logDebugCallback, LogCallback logErrorCallback, LogCallback logWarningCallback)
        {
            NativeVideoInterop.SetLoggingFunctions(logDebugCallback, logErrorCallback, logWarningCallback);
        }

        /// <summary>
        /// Disposes the underlying NativeRenderer.
        /// </summary>
        public void Dispose()
        {
            NativeVideoInterop.Destroy(_videoHandle);
            _videoHandle = IntPtr.Zero;
        }
    }
}
