// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Runtime.InteropServices;

namespace Microsoft.MixedReality.WebRTC.Interop
{
    internal class AudioTrackInterop
    {
        #region Native callbacks

        // The callbacks below ideally would use 'in', but that generates an error with .NET Native:
        // "error : ILT0021: Could not resolve method 'EETypeRva:0x--------'".
        // So instead use 'ref' to ensure the signature is compatible with the C++ const& signature.

        [UnmanagedFunctionPointer(CallingConvention.StdCall, CharSet = CharSet.Ansi)]
        public delegate void AudioFrameUnmanagedCallback(IntPtr userData, ref AudioFrame frame);

        #endregion
    }
}
