// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.MixedReality.WebRTC.Interop;
using System;

namespace Microsoft.MixedReality.WebRTC
{
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
            /// </summary>
            LogLiveObjects = 1,

            /// <summary>
            /// Default options.
            /// </summary>
            Default = LogLiveObjects
        }

        /// <summary>
        /// Options used when shutting down the MixedReality-WebRTC library.
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
