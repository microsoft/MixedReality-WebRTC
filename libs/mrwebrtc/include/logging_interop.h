// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

#pragma once

#include "interop_api.h"

extern "C" {

/// Severity of a log message.
enum class mrsLogSeverity : int32_t {
  kUnknown = -1,  // Could not assign a severity level
  kVerbose = 1,
  kInfo = 2,
  kWarning = 3,
  kError = 4,
  kNone = 5,
};

struct mrsLogSinkHandleImpl {};

/// Handle to a registered log sink, used to unregister it.
using mrsLogSinkHandle = mrsLogSinkHandleImpl*;

/// Callback invoked when a log message is received. The callback receives the
/// log message severity and a string with the message content. The first
/// argument is the opaque |user_data| parameter passed during registration.
using mrsLogMessageCallback = void(MRS_CALL*)(void*,
                                              mrsLogSeverity,
                                              const char*);

/// Register a log message sink in the form of a callback invoked when a log
/// message is produced. The callback is invoked for any message with a severity
/// great than or equal to the specified |min_severity|.
MRS_API mrsLogSinkHandle MRS_CALL
mrsLoggingAddSink(mrsLogSeverity min_severity,
                  mrsLogMessageCallback callback,
                  void* user_data) noexcept;

/// Unregister a previously registered log message sink.
MRS_API void MRS_CALL mrsLoggingRemoveSink(mrsLogSinkHandle handle) noexcept;

/// Log a message with a given severity.
MRS_API void MRS_CALL mrsLogMessage(mrsLogSeverity severity,
                                    const char* message) noexcept;
}
