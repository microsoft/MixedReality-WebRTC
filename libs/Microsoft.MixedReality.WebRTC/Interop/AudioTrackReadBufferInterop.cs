using System;
using System.Runtime.InteropServices;

namespace Microsoft.MixedReality.WebRTC.Interop
{
    internal class AudioTrackReadBufferInterop
    {
        internal class Handle : SafeHandle
        {
            /// <summary>
            /// Used internally by <see cref="Create(PeerConnectionHandle, int, out Handle)"/>.
            /// </summary>
            internal Handle() : base(IntPtr.Zero, true) {}

            public override bool IsInvalid => handle == IntPtr.Zero;
            protected override bool ReleaseHandle()
            {
                Destroy(handle);
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
        public static extern void Destroy(IntPtr audioTrackReadBuffer);
    }
}
