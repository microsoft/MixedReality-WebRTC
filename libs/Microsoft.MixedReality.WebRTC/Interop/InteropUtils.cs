using System;
using System.Runtime.InteropServices;

namespace Microsoft.MixedReality.WebRTC.Interop
{
    internal static class Utils
    {
#if MR_SHARING_WIN
        internal const string dllPath = "Microsoft.MixedReality.WebRTC.Native.dll";
#elif MR_SHARING_ANDROID
        internal const string dllPath = "Microsoft.MixedReality.WebRTC.Native.so";
#endif

        public static T ToWrapper<T>(IntPtr peer) where T : class
        {
            var handle = GCHandle.FromIntPtr(peer);
            var wrapper = (handle.Target as T);
            return wrapper;
        }
    }
}
