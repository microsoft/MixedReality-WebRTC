// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Linq;

namespace Microsoft.MixedReality.WebRTC.UnityPlugin
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
    /// High-performance, low-level, native video rendering for Microsoft.MixedReality.WebRTC.PeerConnection objects.
    /// </summary>
    public class NativeRenderer : IDisposable
    {
        private IntPtr _videoHandle;

        /// <summary>
        /// Creates a NativeRenderer for the provided PeerConnection.
        /// </summary>
        /// <param name="peerConnection"></param>
        public NativeRenderer(IntPtr videoHandle)
        {
            uint res = NativeRendererInterop.Create(videoHandle);
            WebRTC.Interop.Utils.ThrowOnErrorCode(res);
            _videoHandle = videoHandle;
            // _nativeHandle = peerConnection.NativeHandle;
        }

        /// <summary>
        /// Starts renderering the local video stream to the provided textures.
        /// </summary>
        /// <param name="format"></param>
        /// <param name="textures"></param>
        public void EnableLocalVideo(VideoKind format, TextureDesc[] textures)
        {
            var interopTextures = textures.Select(item => new NativeRendererInterop.TextureDesc
            {
                texture = item.texture,
                width = item.width,
                height = item.height
            }).ToArray();
            uint res = NativeRendererInterop.EnableLocalVideo(_videoHandle, format, interopTextures, interopTextures.Length);
            WebRTC.Interop.Utils.ThrowOnErrorCode(res);
        }

        /// <summary>
        /// Stops renderering the local video stream.
        /// </summary>
        public void DisableLocalVideo()
        {
            uint res = NativeRendererInterop.DisableLocalVideo(_videoHandle);
            WebRTC.Interop.Utils.ThrowOnErrorCode(res);
        }

        /// <summary>
        /// Starts rendering the remote video stream to the provided textures.
        /// </summary>
        /// <param name="format"></param>
        /// <param name="textures"></param>
        public void EnableRemoteVideo(VideoKind format, TextureDesc[] textures)
        {
            var interopTextures = textures.Select(item => new NativeRendererInterop.TextureDesc
            {
                texture = item.texture,
                width = item.width,
                height = item.height
            }).ToArray();
            
            uint res = NativeRendererInterop.EnableRemoteVideo(_videoHandle, format, interopTextures, interopTextures.Length);
            WebRTC.Interop.Utils.ThrowOnErrorCode(res);
        }

        /// <summary>
        /// Stops rendering the remote video stream.
        /// </summary>
        public void DisableRemoteVideo()
        {
            uint res = NativeRendererInterop.DisableRemoteVideo(_videoHandle);
            WebRTC.Interop.Utils.ThrowOnErrorCode(res);
        }

        /// <summary>
        /// Returns the native rendering update method to be called by Unity.
        /// </summary>
        /// <returns></returns>
        public static IntPtr GetVideoUpdateMethod()
        {
            return NativeRendererInterop.GetVideoUpdateMethod();
        }

        /// <summary>
        /// Sets callback handlers for the logging of debug, warning, and error messages.
        /// </summary>
        public static void SetLoggingFunctions(LogCallback logDebugCallback, LogCallback logErrorCallback, LogCallback logWarningCallback)
        {
            NativeRendererInterop.SetLoggingFunctions(logDebugCallback, logErrorCallback, logWarningCallback);
        }

        /// <summary>
        /// Disposes the underlying NativeRenderer.
        /// </summary>
        public void Dispose()
        {
            NativeRendererInterop.Destroy(_videoHandle);
            _videoHandle = IntPtr.Zero;
        }
    }
}
