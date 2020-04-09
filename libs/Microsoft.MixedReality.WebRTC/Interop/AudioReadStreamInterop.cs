using System;
using System.Runtime.InteropServices;

namespace Microsoft.MixedReality.WebRTC.Interop
{
    internal class AudioTrackReadBufferInterop
    {
        [DllImport(Utils.dllPath, CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Ansi,
            EntryPoint = "mrsAudioTrackReadBufferCreate")]
        public static extern uint Create(PeerConnectionHandle peerHandle, int bufferMs, out IntPtr audioTrackReadBuffer);

        [DllImport(Utils.dllPath, CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Ansi,
            EntryPoint = "mrsAudioTrackReadBufferRead")]
        public static extern uint Read(IntPtr audioTrackReadBuffer, int sampleRate, float[] data, int dataLen, int numChannels);

        [DllImport(Utils.dllPath, CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Ansi,
            EntryPoint = "mrsAudioTrackReadBufferDestroy")]
        public static extern void Destroy(IntPtr audioTrackReadBuffer);
    }
}
