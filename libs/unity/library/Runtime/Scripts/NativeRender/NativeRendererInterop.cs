using System;
using System.Runtime.InteropServices;
using Microsoft.MixedReality.WebRTC.Interop;

namespace Microsoft.MixedReality.WebRTC.UnityPlugin.Interop
{
    internal class NativeRendererInterop
    {
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
        public struct TextureDesc
        {
            public IntPtr texture;
            public int width;
            public int height;
        }

        [DllImport(Utils.dllPath, CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Ansi,
            EntryPoint = "mrsNativeRenderer_Create")]
        public static extern uint Create(PeerConnectionHandle peerConnectionHandle);

        [DllImport(Utils.dllPath, CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Ansi,
            EntryPoint = "mrsNativeRenderer_Destroy")]
        public static extern uint Destroy(PeerConnectionHandle peerConnectionHandle);

        [DllImport(Utils.dllPath, CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Ansi,
            EntryPoint = "mrsNativeRenderer_EnableLocalVideo")]
        public static extern uint EnableLocalVideo(PeerConnectionHandle peerConnectionHandle, VideoKind format, TextureDesc[] textures, int textureCount);

        [DllImport(Utils.dllPath, CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Ansi,
            EntryPoint = "mrsNativeRenderer_DisableLocalVideo")]
        public static extern uint DisableLocalVideo(PeerConnectionHandle peerConnectionHandle);

        [DllImport(Utils.dllPath, CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Ansi,
            EntryPoint = "mrsNativeRenderer_EnableRemoteVideo")]
        public static extern uint EnableRemoteVideo(IntPtr videoHandle, PeerConnectionHandle peerConnectionHandle, VideoKind format, TextureDesc[] textures, int textureCount);

        [DllImport(Utils.dllPath, CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Ansi,
            EntryPoint = "mrsNativeRenderer_DisableRemoteVideo")]
        public static extern uint DisableRemoteVideo(PeerConnectionHandle peerConnectionHandle);

        [DllImport(Utils.dllPath, CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Ansi,
            EntryPoint = "mrsNativeRenderer_GetVideoUpdateMethod")]
        public static extern IntPtr GetVideoUpdateMethod();

        [DllImport(Utils.dllPath, CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Ansi,
            EntryPoint = "mrsNativeRenderer_SetLoggingFunctions")]
        public static extern void SetLoggingFunctions(LogCallback logDebugCallback, LogCallback logErrorCallback, LogCallback logWarningCallback);
    }
}
