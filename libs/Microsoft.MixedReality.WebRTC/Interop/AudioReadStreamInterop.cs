using System;
using System.Runtime.InteropServices;

namespace Microsoft.MixedReality.WebRTC.Interop
{
    internal class AudioTrackReadBufferInterop
    {
        internal class Handle : SafeHandle
        {
            internal Handle(IntPtr h) : base(IntPtr.Zero, true) { handle = h; }
            public override bool IsInvalid => handle == IntPtr.Zero;
            protected override bool ReleaseHandle()
            {
                Destroy(this);
                return true;
            }
        }

        [DllImport(Utils.dllPath, CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Ansi,
            EntryPoint = "mrsAudioTrackReadBufferCreate")]
        public static extern uint Create(PeerConnectionHandle peerHandle, int bufferMs, out Handle audioTrackReadBufferOut);

        [DllImport(Utils.dllPath, CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Ansi,
            EntryPoint = "mrsAudioTrackReadBufferRead")]
        public static extern uint Read(Handle audioTrackReadBuffer, int sampleRate, float[] data, int dataLen, int numChannels);

        [DllImport(Utils.dllPath, CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Ansi,
            EntryPoint = "mrsAudioTrackReadBufferDestroy")]
        public static extern void Destroy(Handle audioTrackReadBuffer);
    }
}
