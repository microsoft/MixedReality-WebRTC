// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Runtime.InteropServices;
using System.Text;

namespace Microsoft.MixedReality.WebRTC.Interop
{
    /// <summary>
    /// Handle to a native audio track source object.
    /// </summary>
    internal sealed class AudioTrackSourceHandle : RefCountedObjectHandle { }

    internal class AudioTrackSourceInterop
    {
        /// <summary>
        /// Marshaling struct for initializing settings when opening a local audio device.
        /// </summary>
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
        internal ref struct LocalAudioDeviceMarshalInitConfig
        {
            public mrsOptBool AutoGainControl;

            /// <summary>
            /// Constructor for creating a local audio device initialization settings marshaling struct.
            /// </summary>
            /// <param name="settings">The settings to initialize the newly created marshaling struct.</param>
            /// <seealso cref="AudioTrackSource.CreateFromDeviceAsync(LocalAudioDeviceInitConfig)"/>
            public LocalAudioDeviceMarshalInitConfig(LocalAudioDeviceInitConfig settings)
            {
                AutoGainControl = (mrsOptBool)settings?.AutoGainControl;
            }
        }

        #region P/Invoke static functions

        [DllImport(Utils.dllPath, CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Ansi,
            EntryPoint = "mrsAudioTrackSourceCreateFromDevice")]
        public static unsafe extern uint AudioTrackSource_CreateFromDevice(
            in LocalAudioDeviceMarshalInitConfig config, out AudioTrackSourceHandle sourceHandle);

        #endregion
    }
}
