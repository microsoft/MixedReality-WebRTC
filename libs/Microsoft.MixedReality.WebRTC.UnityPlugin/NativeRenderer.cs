using System;
using System.Diagnostics;
using System.Linq;
using Microsoft.MixedReality.WebRTC.UnityPlugin.Interop;

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
        private PeerConnection _peerConnection;
        private WebRTC.Interop.PeerConnectionHandle _nativeHandle;

        /// <summary>
        /// Creates a NativeRenderer for the provided PeerConnection.
        /// </summary>
        /// <param name="peerConnection"></param>
        public NativeRenderer(PeerConnection peerConnection)
        {
            uint res = NativeRendererInterop.Create(peerConnection.NativeHandle);
            WebRTC.Interop.Utils.ThrowOnErrorCode(res);
            _peerConnection = peerConnection;
            _nativeHandle = peerConnection.NativeHandle;
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
            uint res = NativeRendererInterop.EnableLocalVideo(_nativeHandle, format, interopTextures, interopTextures.Length);
            WebRTC.Interop.Utils.ThrowOnErrorCode(res);
        }

        /// <summary>
        /// Stops renderering the local video stream.
        /// </summary>
        public void DisableLocalVideo()
        {
            uint res = NativeRendererInterop.DisableLocalVideo(_nativeHandle);
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

            if (_peerConnection.Transceivers.Count > 0 && _peerConnection.Transceivers[0].RemoteVideoTrack != null)
            {
                uint res = NativeRendererInterop.EnableRemoteVideo(_peerConnection.Transceivers[0].RemoteVideoTrack.NativeHandle, _peerConnection.NativeHandle, format, interopTextures, interopTextures.Length);
                WebRTC.Interop.Utils.ThrowOnErrorCode(res);
            }
        }

        /// <summary>
        /// Stops rendering the remote video stream.
        /// </summary>
        public void DisableRemoteVideo()
        {
            uint res = NativeRendererInterop.DisableRemoteVideo(_nativeHandle);
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
            NativeRendererInterop.Destroy(_nativeHandle);
            _nativeHandle = null;
        }
    }
}
