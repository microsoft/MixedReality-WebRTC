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
    internal sealed class AudioTrackSourceHandle : SafeHandle
    {
        /// <summary>
        /// Check if the current handle is invalid, which means it is not referencing
        /// an actual native object. Note that a valid handle only means that the internal
        /// handle references a native object, but does not guarantee that the native
        /// object is still accessible. It is only safe to access the native object if
        /// the handle is not closed, which implies it being valid.
        /// </summary>
        public override bool IsInvalid => (handle == IntPtr.Zero);

        /// <summary>
        /// Default constructor for an invalid handle.
        /// </summary>
        public AudioTrackSourceHandle() : base(IntPtr.Zero, ownsHandle: true)
        {
        }

        /// <summary>
        /// Constructor for a valid handle referencing the given native object.
        /// </summary>
        /// <param name="handle">The valid internal handle to the native object.</param>
        public AudioTrackSourceHandle(IntPtr handle) : base(IntPtr.Zero, ownsHandle: true)
        {
            SetHandle(handle);
        }

        /// <summary>
        /// Release the native object while the handle is being closed.
        /// </summary>
        /// <returns>Return <c>true</c> if the native object was successfully released.</returns>
        protected override bool ReleaseHandle()
        {
            AudioTrackSourceInterop.AudioTrackSource_RemoveRef(handle);
            return true;
        }
    }

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
                AutoGainControl = new mrsOptBool(settings?.AutoGainControl);
            }
        }

        #region P/Invoke static functions

        [DllImport(Utils.dllPath, CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Ansi,
            EntryPoint = "mrsAudioTrackSourceAddRef")]
        public static unsafe extern void AudioTrackSource_AddRef(AudioTrackSourceHandle handle);

        // Note - This is used during SafeHandle.ReleaseHandle(), so cannot use AudioTrackSourceHandle
        [DllImport(Utils.dllPath, CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Ansi,
            EntryPoint = "mrsAudioTrackSourceRemoveRef")]
        public static unsafe extern void AudioTrackSource_RemoveRef(IntPtr handle);

        [DllImport(Utils.dllPath, CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Ansi,
            EntryPoint = "mrsAudioTrackSourceSetName")]
        public static unsafe extern void AudioTrackSource_SetName(AudioTrackSourceHandle handle, string name);

        [DllImport(Utils.dllPath, CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Ansi,
            EntryPoint = "mrsAudioTrackSourceGetName")]
        public static unsafe extern uint AudioTrackSource_GetName(AudioTrackSourceHandle handle, StringBuilder buffer,
            ref ulong bufferCapacity);

        [DllImport(Utils.dllPath, CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Ansi,
            EntryPoint = "mrsAudioTrackSourceSetUserData")]
        public static unsafe extern void AudioTrackSource_SetUserData(AudioTrackSourceHandle handle, IntPtr userData);

        [DllImport(Utils.dllPath, CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Ansi,
            EntryPoint = "mrsAudioTrackSourceGetUserData")]
        public static unsafe extern IntPtr AudioTrackSource_GetUserData(AudioTrackSourceHandle handle);

        [DllImport(Utils.dllPath, CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Ansi,
            EntryPoint = "mrsAudioTrackSourceCreateFromDevice")]
        public static unsafe extern uint AudioTrackSource_CreateFromDevice(
            in LocalAudioDeviceMarshalInitConfig config, out AudioTrackSourceHandle sourceHandle);

        #endregion
    }
}
