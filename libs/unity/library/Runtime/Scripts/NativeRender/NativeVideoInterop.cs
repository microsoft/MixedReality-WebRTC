// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Runtime.InteropServices;

namespace Microsoft.MixedReality.WebRTC.Unity
{
    internal static class NativeVideoInterop
    {
        private const string _dllPath = "mrwebrtc-unityplugin";

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
        public struct TextureDesc
        {
            public IntPtr texture;
            public int width;
            public int height;
        }

        [DllImport(_dllPath, CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Ansi, EntryPoint = "mrsNativeRenderer_Create")]
        public static extern IntPtr Create(IntPtr videoTrackHandle);

        [DllImport(_dllPath, CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Ansi, EntryPoint = "mrsNativeRenderer_Destroy")]
        public static extern uint Destroy(IntPtr nativeVideoHandle);

        [DllImport(_dllPath, CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Ansi, EntryPoint = "mrsNativeRenderer_EnableLocalVideo")]
        public static extern uint EnableLocalVideo(IntPtr nativeVideoHandle, VideoKind format, TextureDesc[] textures, int textureCount);

        [DllImport(_dllPath, CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Ansi, EntryPoint = "mrsNativeRenderer_DisableLocalVideo")]
        public static extern uint DisableLocalVideo(IntPtr nativeVideoHandle);

        [DllImport(_dllPath, CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Ansi, EntryPoint = "mrsNativeRenderer_EnableRemoteVideo")]
        public static extern uint EnableRemoteVideo(IntPtr nativeVideoHandle, VideoKind format);
        
        [DllImport(_dllPath, CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Ansi, EntryPoint = "mrsNativeRenderer_UpdateRemoteTextures")]
        public static extern uint UpdateRemoteTextures(IntPtr nativeVideoHandle, VideoKind format, TextureDesc[] textures, int textureCount);

        [DllImport(_dllPath, CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Ansi, EntryPoint = "mrsNativeRenderer_DisableRemoteVideo")]
        public static extern uint DisableRemoteVideo(IntPtr nativeVideoHandle);

        [DllImport(_dllPath, CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Ansi, EntryPoint = "mrsNativeRenderer_GetVideoUpdateMethod")]
        public static extern IntPtr GetVideoUpdateMethod();

        [DllImport(_dllPath, CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Ansi, EntryPoint = "mrsNativeRenderer_SetLoggingFunctions")]
        public static extern void SetLoggingFunctions(LogCallback logDebugCallback, LogCallback logErrorCallback, LogCallback logWarningCallback);
        
        [DllImport(_dllPath, CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Ansi, EntryPoint = "mrsNativeRenderer_SetTextureSizeChanged")]
        public static extern void SetTextureSizeChanged(TextureSizeChangeCallback logDebugCallback);
    }
}
