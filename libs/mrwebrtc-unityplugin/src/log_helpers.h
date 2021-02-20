// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

#pragma once

class UnityLogger {
 public:
  typedef void (*LogFunction)(const char*);

  static void LogDebug(const char* str);
  static void LogError(const char* str);
  static void LogWarning(const char* str);
  static void SetLoggingFunctions(LogFunction logDebug,
                                  LogFunction logError,
                                  LogFunction logWarning);
  static bool LoggersSet();

 private:
  static LogFunction LogDebugFunc;
  static LogFunction LogErrorFunc;
  static LogFunction LogWarningFunc;
};

///
/// Log helpers.
///
enum class LogLevel { Error, Warning, Info, Debug };

// GNUC's __VA_ARGS__ doesn't work if no arguments are given, but the ##
// extension supports it.
#if defined(__GNUC__)
#define Log_Error(_FORMAT, ...) \
  LogDebugString(LogLevel::Error, __FILE__, __LINE__, _FORMAT, ##__VA_ARGS__)

#define Log_Warning(_FORMAT, ...) \
  LogDebugString(LogLevel::Warning, __FILE__, __LINE__, _FORMAT, ##__VA_ARGS__)

#define Log_Debug(_FORMAT, ...) \
  LogDebugString(LogLevel::Debug, __FILE__, __LINE__, _FORMAT, ##__VA_ARGS__)

#define Log_Info(_FORMAT, ...) \
  LogDebugString(LogLevel::Info, __FILE__, __LINE__, _FORMAT, ##__VA_ARGS__)

#else
#define Log_Error(_FORMAT, ...) \
  LogDebugString(LogLevel::Error, __FILE__, __LINE__, _FORMAT, __VA_ARGS__)

#define Log_Warning(_FORMAT, ...) \
  LogDebugString(LogLevel::Warning, __FILE__, __LINE__, _FORMAT, __VA_ARGS__)

#define Log_Debug(_FORMAT, ...) \
  LogDebugString(LogLevel::Debug, __FILE__, __LINE__, _FORMAT, __VA_ARGS__)

#define Log_Info(_FORMAT, ...) \
  LogDebugString(LogLevel::Info, __FILE__, __LINE__, _FORMAT, __VA_ARGS__)

#endif

#define Warn_If(_COND, _FORMAT, ...)     \
  do {                                   \
    if (_COND) {                         \
      Log_Warning(_FORMAT, __VA_ARGS__); \
    }                                    \
  } while (0, 0)

void LogDebugString(LogLevel level,
                    const char* file,
                    int line,
                    const char* format,
                    ...);
