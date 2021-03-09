// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Linq;

namespace Microsoft.MixedReality.WebRTC.Unity
{
    public delegate void LogCallback(string str);
    public delegate void TextureSizeChangeCallback(int width, int height, IntPtr videoHandle);

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
        /// <summary>
        /// Callback for when the texture size of stream changes in a NativeVideo.
        /// This callback will not occur on the main thread.
        /// Unity must handle this resize, it cannot be done entirely in DX11.
        /// </summary>
        public event TextureSizeChangeCallback TextureSizeChanged;

        private static Dictionary<IntPtr, NativeVideo> lookupDictionary = new Dictionary<IntPtr, NativeVideo>();
        
        private IntPtr _nativeVideoHandle;

        /// <summary>
        /// Creates a NativeRenderer for the provided PeerConnection.
        /// </summary>
        /// <param name="peerConnection"></param>
        public NativeVideo(IntPtr videoHandle)
        {
            _nativeVideoHandle = NativeVideoInterop.Create(videoHandle);
            if (lookupDictionary.ContainsKey(videoHandle)) lookupDictionary[videoHandle] = this;
            else lookupDictionary.Add(videoHandle, this);
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
            NativeVideoInterop.EnableLocalVideo(_nativeVideoHandle, format, interopTextures, interopTextures.Length);
        }

        /// <summary>
        /// Stops renderering the local video stream.
        /// </summary>
        public void DisableLocalVideo()
        {
            NativeVideoInterop.DisableLocalVideo(_nativeVideoHandle);
        }

        /// <summary>
        /// Starts rendering the remote video stream to the provided textures.
        /// Only kI420 is currently supported.
        ///
        /// Calling this will override anything that is currently
        /// subscribed to the FrameReady call back on the VideoTrack.
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

            NativeVideoInterop.EnableRemoteVideo(_nativeVideoHandle, format, interopTextures, interopTextures.Length);
        }
        
        public void UpdateTextures(TextureDesc[] textures)
        {
            var interopTextures = textures.Select(item => new NativeVideoInterop.TextureDesc
            {
                texture = item.texture,
                width = item.width,
                height = item.height
            }).ToArray();

            NativeVideoInterop.UpdateTextures(_nativeVideoHandle, interopTextures);
        }

        /// <summary>
        /// Stops rendering the remote video stream.
        /// </summary>
        public void DisableRemoteVideo()
        {
            NativeVideoInterop.DisableRemoteVideo(_nativeVideoHandle);
            if (lookupDictionary.ContainsKey(_nativeVideoHandle)) lookupDictionary.Remove(_nativeVideoHandle);
        }

        /// <summary>
        /// Returns the native rendering update method to be called by Unity.
        /// </summary>
        /// <returns></returns>
        public static IntPtr GetVideoUpdateMethod()
        {
            return NativeVideoInterop.GetVideoUpdateMethod();
        }
        
        /// Sets callback handlers for the logging of debug, warning, and error messages.
        /// </summary>
        public static void SetTextureChangeCallback()
        {
            NativeVideoInterop.SetTextureSizeChangeCallback(TextureSizeChangeCallback);
        }

        [AOT.MonoPInvokeCallback(typeof(LogCallback))]
        private static void TextureSizeChangeCallback(int width, int height, IntPtr videoHandle)
        {
            lookupDictionary.TryGetValue(videoHandle, out NativeVideo nativeVideo);
            nativeVideo?.TextureSizeChanged?.Invoke(width, height, videoHandle);
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
            NativeVideoInterop.Destroy(_nativeVideoHandle);
            _nativeVideoHandle = IntPtr.Zero;
        }
    }
}
