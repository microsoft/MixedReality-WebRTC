// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license
// information.

#pragma once

#include <cstdint>
#include <functional>
#include <string>
#include <string_view>

#include "export.h"

namespace Microsoft::MixedReality::WebRTC {

#if defined(MRS_USE_STR_WRAPPER)

/// Simple string wrapper to work around shared library issues when using
/// std::string. Used to pass strings across the API boundaries.
class str {
 public:
  str();
  explicit str(const std::string& s);
  explicit str(std::string&& s) noexcept;
  explicit str(std::string_view view);
  explicit str(const char* s);
  ~str();
  str& operator=(const std::string& s);
  str& operator=(std::string&& s) noexcept;
  [[nodiscard]] bool empty() const noexcept;
  [[nodiscard]] uint32_t size() const noexcept;
  [[nodiscard]] const char* data() const noexcept;
  [[nodiscard]] const char* c_str() const noexcept;

  // Do not use in API
  std::size_t hash() const noexcept { return std::hash<std::string>()(str_); }

 private:
  std::string str_;
  friend bool operator==(const str& lhs, const str& rhs) noexcept;
  friend bool operator!=(const str& lhs, const str& rhs) noexcept;
  friend bool operator==(const str& lhs, const std::string& rhs) noexcept;
  friend bool operator==(const std::string& lhs, const str& rhs) noexcept;
  friend bool operator!=(const str& lhs, const std::string& rhs) noexcept;
  friend bool operator!=(const std::string& lhs, const str& rhs) noexcept;
};

bool operator==(const str& lhs, const str& rhs) noexcept;
bool operator!=(const str& lhs, const str& rhs) noexcept;
bool operator==(const str& lhs, const std::string& rhs) noexcept;
bool operator==(const std::string& lhs, const str& rhs) noexcept;
bool operator!=(const str& lhs, const std::string& rhs) noexcept;
bool operator!=(const std::string& lhs, const str& rhs) noexcept;

#else  // defined(MRS_USE_STR_WRAPPER)

using str = std::string;

#endif  // defined(MRS_USE_STR_WRAPPER)

}  // namespace Microsoft::MixedReality::WebRTC

#if defined(MRS_USE_STR_WRAPPER)

namespace std {

/// Override std::hash<> for str to allow usage in unorderd_map and such.
template <>
struct hash<Microsoft::MixedReality::WebRTC::str> {
  std::size_t operator()(const Microsoft::MixedReality::WebRTC::str& k) const
      noexcept {
    return k.hash();
  }
};

}  // namespace std

#endif  // defined(MRS_USE_STR_WRAPPER)
