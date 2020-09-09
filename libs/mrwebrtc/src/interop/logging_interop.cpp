// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

// This is a precompiled header, it must be on its own, followed by a blank
// line, to prevent clang-format from reordering it with other headers.
#include "pch.h"

#include "callback.h"
#include "logging_interop.h"

using namespace Microsoft::MixedReality::WebRTC;

namespace {

class InteropLogSink : public rtc::LogSink {
 public:
  using MessageCallback = Callback<mrsLogSeverity, const char*>;

  static InteropLogSink* Create(MessageCallback callback) noexcept {
    try {
      auto sink = std::make_unique<InteropLogSink>(std::move(callback));
      InteropLogSink* const ret = sink.get();
      {
        rtc::CritScope lock(&GetCS());
        GetSinks().push_back(std::move(sink));
      }
      return ret;
    } catch (...) {
      return nullptr;
    }
  }

  static void Destroy(InteropLogSink* sink) noexcept {
    try {
      rtc::CritScope lock(&GetCS());
      auto& sinks = GetSinks();
      auto it = std::find_if(
          sinks.begin(), sinks.end(),
          [sink](const std::unique_ptr<InteropLogSink>& registeredSink) {
            return (registeredSink.get() == sink);
          });
      if (it != sinks.end()) {
        sinks.erase(it);
      }
    } catch (...) {
    }
  }

  InteropLogSink(MessageCallback callback) : callback_(std::move(callback)) {}

  void OnLogMessage(const std::string& msg,
                    rtc::LoggingSeverity severity,
                    const char* /*tag*/) override {
    callback_(static_cast<mrsLogSeverity>(severity), msg.c_str());
  }

  void OnLogMessage(const std::string& message,
                    rtc::LoggingSeverity severity) override {
    callback_((mrsLogSeverity)severity, message.c_str());
  }

  void OnLogMessage(const std::string& message) override {
    callback_(mrsLogSeverity::kUnknown, message.c_str());
  }

  mrsLogSinkHandle ToHandle() noexcept {
    return reinterpret_cast<mrsLogSinkHandle>(this);
  }

  static InteropLogSink* FromHandle(mrsLogSinkHandle handle) noexcept {
    return reinterpret_cast<InteropLogSink*>(handle);
  }

 private:
  MessageCallback callback_;
  static std::vector<std::unique_ptr<InteropLogSink>> s_sinks_;

 private:
  static rtc::CriticalSection& GetCS() {
    static rtc::CriticalSection cs;
    return cs;
  }
  static std::vector<std::unique_ptr<InteropLogSink>>& GetSinks() {
    static std::vector<std::unique_ptr<InteropLogSink>> sinks;
    return sinks;
  }
};

}  // namespace

mrsLogSinkHandle MRS_CALL mrsLoggingAddSink(mrsLogSeverity min_severity,
                                            mrsLogMessageCallback callback,
                                            void* user_data) noexcept {
  if ((min_severity == mrsLogSeverity::kNone) || !callback) {
    return nullptr;
  }
  InteropLogSink* const sink = InteropLogSink::Create({callback, user_data});
  // Doc says AddLogToStream() takes ownership, but RemoveLogToStream() doc says
  // it doesn't delete the sink, so it's not really taking ownership.
  rtc::LogMessage::AddLogToStream(
      sink, static_cast<rtc::LoggingSeverity>(min_severity));
  return sink->ToHandle();
}

void MRS_CALL mrsLoggingRemoveSink(mrsLogSinkHandle handle) noexcept {
  if (InteropLogSink* const sink = InteropLogSink::FromHandle(handle)) {
    rtc::LogMessage::RemoveLogToStream(sink);
    InteropLogSink::Destroy(sink);
  }
}

void MRS_CALL mrsLogMessage(mrsLogSeverity severity,
                            const char* message) noexcept {
  if ((severity >= mrsLogSeverity::kVerbose) &&
      (severity <= mrsLogSeverity::kError)) {
    const rtc::LoggingSeverity sev = (rtc::LoggingSeverity)severity;
    RTC_LOG_V(sev) << message;
  }
}
