// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.MixedReality.WebRTC.Interop;

namespace Microsoft.MixedReality.WebRTC
{
    /// <summary>
    /// Log message severity.
    /// </summary>
    public enum LogSeverity : int
    {
        /// <summary>
        /// Unknown severity level, could not be retrieved/assigned.
        /// </summary>
        Unknown = -1,

        /// <summary>
        /// Diagnostic message for debugging.
        /// </summary>
        Verbose = 1,

        /// <summary>
        /// Informational message for diagnosing.
        /// </summary>
        Info = 2,

        /// <summary>
        /// Warning message about some event to potentially investigate.
        /// </summary>
        Warning = 3,

        /// <summary>
        /// Error message about unexpected action or result.
        /// </summary>
        Error = 4,

        /// <summary>
        /// Logging disabled.
        /// </summary>
        None = 5
    }

    /// <summary>
    /// Interface for a sink receiving log messages. The sink can be registered with
    /// <see cref="Logging.AddSink(ILogSink, LogSeverity)"/> to receive logging messages.
    /// </summary>
    /// <seealso cref="Logging.AddSink(ILogSink, LogSeverity)"/>
    /// <seealso cref="Logging.RemoveSink(ILogSink)"/>
    public interface ILogSink
    {
        /// <summary>
        /// Callback invoked when a log message is received.
        /// </summary>
        /// <param name="severity">Message severity.</param>
        /// <param name="message">Message description.</param>
        void LogMessage(LogSeverity severity, string message);
    }

    /// <summary>
    /// Logging utilities.
    /// </summary>
    public static class Logging
    {
        /// <summary>
        /// Add a log sink receiving messages.
        /// </summary>
        /// <param name="sink">The sink to register.</param>
        /// <param name="minimumSeverity">Minimum severity of messages to forward to the sink.</param>
        public static void AddSink(ILogSink sink, LogSeverity minimumSeverity)
        {
            LoggingInterop.AddSink(sink, minimumSeverity);
        }

        /// <summary>
        /// Remove a log sink receiving messages.
        /// </summary>
        /// <param name="sink">The sink to unregister.</param>
        public static void RemoveSink(ILogSink sink)
        {
            LoggingInterop.RemoveSink(sink);
        }

        /// <summary>
        /// Log a message with a given severity. The message will be logged alongside the messages generated
        /// by the implementation, and received by any registered sink callback like internal messages.
        /// </summary>
        /// <param name="severity">Message severity.</param>
        /// <param name="message">Message content.</param>
        public static void LogMessage(LogSeverity severity, string message)
        {
            LoggingInterop.Logging_LogMessage(severity, message);
        }
    }
}
