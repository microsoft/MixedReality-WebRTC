// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.MixedReality.WebRTC.Interop;
using System;

namespace Microsoft.MixedReality.WebRTC
{
    /// <summary>
    /// Audio device module for Windows Desktop platform.
    /// </summary>
    public enum AudioDeviceModule : byte
    {
        /// <summary>
        /// Legacy audio device module (ADM1); not recommended unless there is an issue
        /// with the new default one.
        /// </summary>
        LegacyModule = 1,

        /// <summary>
        /// New CoreAudio based audio device module (ADM2). This is the default.
        /// </summary>
        DefaultModule = 2
    }

    /// <summary>
    /// Container for library-wise global settings of MixedReality-WebRTC.
    /// </summary>
    public static class Library
    {
        /// <summary>
        /// Report all objects currently alive and tracked by the native implementation.
        /// This is a live report, which generally gets outdated as soon as the function
        /// returned, as new objects are created and others destroyed. Nonetheless this
        /// is may be helpful to diagnose issues with disposing objects.
        /// </summary>
        /// <returns>Returns the number of live objects at the time of the call.</returns>
        public static uint ReportLiveObjects()
        {
            return Utils.LibraryReportLiveObjects();
        }

        /// <summary>
        /// Audio device module to use on Windows Desktop.
        /// </summary>
        /// <remarks>
        /// The ADM can only be changed before any peer connection is created/initialized;
        /// otherwise an <see cref="InvalidOperationException"/> is raised.
        ///
        /// This has no effect on UWP or non-Windows platforms.
        /// </remarks>
        /// <exception cref="InvalidOperationException">
        /// The peer connection factory was already initialized, therefore the audio device
        /// module cannot be changed anymore.
        /// </exception>
        public static AudioDeviceModule UsedAudioDeviceModule
        {
            get
            {
                return Utils.LibraryGetAudioDeviceModule();
            }
            set
            {
                uint res = Utils.LibraryUseAudioDeviceModule(value);
                Utils.ThrowOnErrorCode(res);
            }
        }

        /// <summary>
        /// Options for library shutdown.
        /// </summary>
        [Flags]
        public enum ShutdownOptionsFlags : uint
        {
            /// <summary>
            /// Do nothing specific.
            /// </summary>
            None = 0,

            /// <summary>
            /// Log with <see cref="ReportLiveObjects"/> all objects still alive, to help debugging.
            /// This is recommended to prevent deadlocks during shutdown.
            /// </summary>
            LogLiveObjects = 0x1,

            /// <summary>
            /// When forcing shutdown, either because <see cref="ForceShutdown"/> is called or
            /// because the program terminates, and some objects are still alive, attempt
            /// to break into the debugger. This is not available for all platforms.
            /// </summary>
            DebugBreakOnForceShutdown = 0x2,

            /// <summary>
            /// Default options.
            /// </summary>
            Default = LogLiveObjects
        }

        /// <summary>
        /// Options used when shutting down the MixedReality-WebRTC library.
        /// disposed, to shutdown the internal threads and release the global resources, and allow the
        /// library's module to be unloaded.
        /// </summary>
        public static ShutdownOptionsFlags ShutdownOptions
        {
            get { return Utils.LibraryGetShutdownOptions(); }
            set { Utils.LibrarySetShutdownOptions(value); }
        }

        /// <summary>
        /// Forcefully shutdown the MixedReality-WebRTC library. This shall not be used under normal
        /// circumstances, but can be useful e.g. in the Unity editor when a test fails and proper
        /// clean-up is not ensured (in particular, disposing objects), to allow the shared module to
        /// shutdown and terminate its native threads, and be unloaded.
        /// </summary>
        public static void ForceShutdown()
        {
            Utils.LibraryForceShutdown();
        }
    }
}
