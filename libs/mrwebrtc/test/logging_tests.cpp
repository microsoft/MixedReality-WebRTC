// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

#include "pch.h"

#include "interop_api.h"
#include "logging_interop.h"

#include "test_utils.h"

namespace {

class LoggingTests : public TestUtils::TestBase {};

void MRS_CALL LogCallback(void* /*user_data*/,
                          mrsLogSeverity /*severity*/,
                          const char* message) {
  OutputDebugStringA(message);
  OutputDebugStringA("\n");
}

/// Utility sink to log all messages and then check that specific keywords
/// appear in those messages, to confirm logging worked as intended.
struct CheckKeywordLogSink {
  static void MRS_CALL LogCallback(void* user_data,
                                   mrsLogSeverity severity,
                                   const char* message) {
    auto self = static_cast<CheckKeywordLogSink*>(user_data);
    self->LogMessage(severity, message);
  }

  struct Msg {
    mrsLogSeverity severity;
    std::string message;
  };

  void LogMessage(mrsLogSeverity severity, const char* message) {
    std::scoped_lock<std::mutex> lock(mutex_);
    messages_.push_back({severity, message ? message : ""});
  }

  bool HasKeyword(absl::string_view keyword) const {
    std::scoped_lock<std::mutex> lock(mutex_);
    auto it = std::find_if(
        messages_.begin(), messages_.end(), [keyword](const Msg& msg) {
          return (msg.message.find(keyword.data(), 0, keyword.size()) !=
                  std::string::npos);
        });
    return (it != messages_.end());
  }

  void Clear() {
    std::scoped_lock<std::mutex> lock(mutex_);
    messages_.clear();
  }

  mutable std::mutex mutex_;
  std::vector<Msg> messages_;
};

struct RaiiSinkHandle {
  RaiiSinkHandle(mrsLogSinkHandle handle) : handle_(handle) {}
  ~RaiiSinkHandle() noexcept {
    if (handle_) {
      mrsLoggingRemoveSink(handle_);
    }
  }
  mrsLogSinkHandle get() const { return handle_; }
  operator mrsLogSinkHandle() const { return handle_; }
  const mrsLogSinkHandle handle_{nullptr};
};

}  // namespace

TEST_F(LoggingTests, AddRemoveSink) {
  RaiiSinkHandle handle =
      mrsLoggingAddSink(mrsLogSeverity::kInfo, LogCallback, nullptr);
  ASSERT_NE(nullptr, handle.get());
}

TEST_F(LoggingTests, AddSinkInvalidArgs) {
  // Invalid severity
  ASSERT_EQ(nullptr,
            mrsLoggingAddSink(mrsLogSeverity::kNone, LogCallback, nullptr));
  // Invalid callback
  ASSERT_EQ(nullptr,
            mrsLoggingAddSink(mrsLogSeverity::kInfo, nullptr, nullptr));
}

TEST_F(LoggingTests, Severity) {
  constexpr const char kLogKeyword[] = "MR_SHARING_WEBRTC_TEST_LOG_KEYWORD";
  CheckKeywordLogSink sink;
  RaiiSinkHandle handle = mrsLoggingAddSink(
      mrsLogSeverity::kWarning, &CheckKeywordLogSink::LogCallback, &sink);
  ASSERT_NE(nullptr, handle.get());
  {
    sink.Clear();
    mrsLogMessage(mrsLogSeverity::kInfo, kLogKeyword);
    ASSERT_FALSE(sink.HasKeyword(kLogKeyword));
  }
  {
    sink.Clear();
    mrsLogMessage(mrsLogSeverity::kWarning, kLogKeyword);
    ASSERT_TRUE(sink.HasKeyword(kLogKeyword));
  }
  {
    sink.Clear();
    mrsLogMessage(mrsLogSeverity::kError, kLogKeyword);
    ASSERT_TRUE(sink.HasKeyword(kLogKeyword));
  }
  {
    sink.Clear();
    mrsLogMessage(mrsLogSeverity::kNone, kLogKeyword);
    ASSERT_FALSE(sink.HasKeyword(kLogKeyword));
  }
}
