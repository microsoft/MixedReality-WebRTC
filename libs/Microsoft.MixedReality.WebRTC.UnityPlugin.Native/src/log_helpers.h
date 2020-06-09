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

inline void LogDebugString(LogLevel level,
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
