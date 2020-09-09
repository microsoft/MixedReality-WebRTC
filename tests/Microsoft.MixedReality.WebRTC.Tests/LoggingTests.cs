// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using NUnit.Framework;
using NUnit.Framework.Internal;

namespace Microsoft.MixedReality.WebRTC.Tests
{
    class CheckKeywordTestSink : ILogSink
    {
        public void LogMessage(LogSeverity severity, string message)
        {
            Messages.Add(new Msg { severity = severity, message = message });
        }

        public void Clear()
        {
            Messages.Clear();
        }

        public bool TryGetMessageByKeyword(string keyword, out Msg message)
        {
            foreach (var msg in Messages)
            {
                if (msg.message.Contains(keyword))
                {
                    message = msg;
                    return true;
                }
            }
            message = new Msg();
            return false;
        }

        public bool HasKeyword(string keyword)
        {
            foreach (var msg in Messages)
            {
                if (msg.message.Contains(keyword))
                {
                    return true;
                }
            }
            return false;
        }

        public struct Msg
        {
            public LogSeverity severity;
            public string message;
        }

        public List<Msg> Messages = new List<Msg>();
    }

    [TestFixture]
    internal class LoggingTests
    {
        [Test]
        public void AddRemoveSink()
        {
            var sink = new CheckKeywordTestSink();
            Logging.AddSink(sink, LogSeverity.Info);
            Logging.RemoveSink(sink);
        }

        [Test]
        public void Severity()
        {
            const string Keyword = "Dummy message for logging test";
            var sink = new CheckKeywordTestSink();
            Logging.AddSink(sink, LogSeverity.Warning);
            {
                sink.Clear();
                Logging.LogMessage(LogSeverity.Info, Keyword);
                Assert.IsFalse(sink.HasKeyword(Keyword));
            }
            {
                sink.Clear();
                Logging.LogMessage(LogSeverity.Warning, Keyword);
                Assert.IsTrue(sink.TryGetMessageByKeyword(Keyword, out CheckKeywordTestSink.Msg msg));
                Assert.AreEqual(LogSeverity.Warning, msg.severity);
            }
            {
                sink.Clear();
                Logging.LogMessage(LogSeverity.Error, Keyword);
                Assert.IsTrue(sink.TryGetMessageByKeyword(Keyword, out CheckKeywordTestSink.Msg msg));
                Assert.AreEqual(LogSeverity.Error, msg.severity);
            }
            {
                sink.Clear();
                Logging.LogMessage(LogSeverity.None, Keyword);
                Assert.IsFalse(sink.HasKeyword(Keyword));
            }
            Logging.RemoveSink(sink);
        }
    }
}
