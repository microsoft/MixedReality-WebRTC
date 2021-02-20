// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

#include "pch.h"

#include "../include/api.h"
#include "log_helpers.h"

void LogDebugString(LogLevel level,
                           const char* file,
                           int line,
                           const char* format,
                           ...) {
  constexpr uint32_t BufferSize = 8192;
  char buffer[BufferSize];

  const char* fileEnd = strrchr(file, '\\');
  if (fileEnd == nullptr) {
    fileEnd = file;
  } else {
    fileEnd = fileEnd + 1;
  }

  // Unity takes the level as an argument. For raw printing, we need to specify
  // it here.
  const char* levelName = "";
  if (!UnityLogger::LoggersSet()) {
    switch (level) {
      default:
      case LogLevel::Error:
        levelName = "[  ERROR] ";
        break;
      case LogLevel::Warning:
        levelName = "[WARNING] ";
        break;
      case LogLevel::Info:
        levelName = "[   INFO] ";
        break;
      case LogLevel::Debug:
        levelName = "[  DEBUG] ";
        break;
    }
  }

  // Reserve space for the newline and terminating character.
  constexpr size_t ReservedBufferSize = BufferSize - 2;

  int writeCount = snprintf(buffer, ReservedBufferSize, "%s[%s:%i] ", levelName,
                            fileEnd, line);  // sprintf_s

  va_list list;
  va_start(list, format);
  vsnprintf(buffer + writeCount, ReservedBufferSize - writeCount, format,
            list);  // vsprintf_s
  va_end(list);

#if __GNUC__
  strcat(buffer, "\n");
#else
  strcat_s(buffer, BufferSize, "\n");
#endif

  if (UnityLogger::LoggersSet()) {
    switch (level) {
      default:
      case LogLevel::Error:
        UnityLogger::LogError(buffer);
        break;
      case LogLevel::Warning:
        UnityLogger::LogWarning(buffer);
        break;
      case LogLevel::Info:
        // Use LogDebug because unity does not have have a loginfo concept
        // TODO: nit: It's 'debug' it doesn't support, not 'info'.
        UnityLogger::LogDebug(buffer);
        break;
      case LogLevel::Debug:
        UnityLogger::LogDebug(buffer);
        break;
    }
  } else {
#if __GNUC__
    printf("%s", buffer);
#else
    OutputDebugStringA(buffer);
#endif
  }
}

UnityLogger::LogFunction UnityLogger::LogDebugFunc = nullptr;
UnityLogger::LogFunction UnityLogger::LogErrorFunc = nullptr;
UnityLogger::LogFunction UnityLogger::LogWarningFunc = nullptr;

void UnityLogger::LogDebug(const char* str) {
  if (LogDebugFunc != nullptr) {
    LogDebugFunc(str);
  }
}

void UnityLogger::LogError(const char* str) {
  if (LogErrorFunc != nullptr) {
    LogErrorFunc(str);
  }
}

void UnityLogger::LogWarning(const char* str) {
  if (LogWarningFunc != nullptr) {
    LogWarningFunc(str);
  }
}

bool UnityLogger::LoggersSet() {
  return LogDebugFunc != nullptr && LogWarningFunc != nullptr && LogErrorFunc != nullptr;
}

void UnityLogger::SetLoggingFunctions(LogFunction logDebug,
                                      LogFunction logError,
                                      LogFunction logWarning) {
  LogDebugFunc = logDebug;
  LogErrorFunc = logError;
  LogWarningFunc = logWarning;
}
