// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Runtime.InteropServices;

namespace Microsoft.MixedReality.WebRTC.Interop
{
    internal class AudioTrackInterop
    {
        #region Native callbacks

        [UnmanagedFunctionPointer(CallingConvention.StdCall, CharSet = CharSet.Ansi)]
        public delegate void AudioFrameUnmanagedCallback(IntPtr userData, in AudioFrame frame);

        #endregion
    }
}
