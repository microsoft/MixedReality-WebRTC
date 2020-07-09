// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace Microsoft.MixedReality.WebRTC.Interop
{
    internal static class LoggingInterop
    {
        internal sealed class SinkWrapper
        {
            public ILogSink Sink;
            public IntPtr SinkHandle;
        }

        /// <summary>
        /// Add a new log message sink.
        /// </summary>
        /// <param name="sink">The sink to register.</param>
        /// <param name="minimumSeverity">The minimum severity of messages to receive with that sink.</param>
        public static void AddSink(ILogSink sink, LogSeverity minimumSeverity)
        {
            if (minimumSeverity == LogSeverity.None)
            {
                return;
            }
            var wrapper = new SinkWrapper { Sink = sink };
            IntPtr wrapperHandle = Utils.MakeWrapperRef(wrapper);
            lock (lock_)
            {
                wrapper.SinkHandle = Logging_AddSink(minimumSeverity, InteropLogMessageCallback, wrapperHandle);
                if (wrapper.SinkHandle != IntPtr.Zero)
                {
                    sinks_.Add(sink, wrapper);
                }
            }
        }

        /// <summary>
        /// Remove a sink previously registered to stop receiving messages.
        /// </summary>
        /// <param name="sink">The sink to unregister.</param>
        public static void RemoveSink(ILogSink sink)
        {
            lock (lock_)
            {
                if (sinks_.TryGetValue(sink, out SinkWrapper wrapper))
                {
                    sinks_.Remove(sink);
                    Logging_RemoveSink(wrapper.SinkHandle);
                }
            }
        }

        private static readonly object lock_ = new object();
        private static readonly Dictionary<ILogSink, SinkWrapper> sinks_ = new Dictionary<ILogSink, SinkWrapper>();


        #region Native functions

        [DllImport(Utils.dllPath, CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Ansi,
            EntryPoint = "mrsLoggingAddSink")]
        public static extern IntPtr Logging_AddSink(LogSeverity minSeverity, InteropLogMessageDelegate callback, IntPtr userData);

        [DllImport(Utils.dllPath, CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Ansi,
            EntryPoint = "mrsLoggingRemoveSink")]
        public static extern void Logging_RemoveSink(IntPtr sinkHandle);

        [DllImport(Utils.dllPath, CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Ansi,
            EntryPoint = "mrsLogMessage")]
        public static extern void Logging_LogMessage(LogSeverity severity, string message);

        #endregion


        #region Native callbacks

        [UnmanagedFunctionPointer(CallingConvention.StdCall, CharSet = CharSet.Ansi)]
        public delegate void InteropLogMessageDelegate(IntPtr sinkHandle, LogSeverity severity, string message);

        // Keep alive a delegate object encapsulating the static function used as trampoline.
        public static readonly InteropLogMessageDelegate InteropLogMessageCallback = LogMessageCallback;

        [MonoPInvokeCallback(typeof(InteropLogMessageDelegate))]
        private static void LogMessageCallback(IntPtr sinkHandle, LogSeverity severity, string message)
        {
            var sinkWrapper = Utils.ToWrapper<SinkWrapper>(sinkHandle);
            sinkWrapper.Sink.LogMessage(severity, message);
        }

        #endregion
    }
}
