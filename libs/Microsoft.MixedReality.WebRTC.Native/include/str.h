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
  MRS_API str();
  MRS_API explicit str(const std::string& s);
  MRS_API explicit str(std::string&& s) noexcept;
  MRS_API explicit str(std::string_view view);
  MRS_API explicit str(const char* s);
  MRS_API ~str();
  MRS_API str& operator=(const std::string& s);
  MRS_API str& operator=(std::string&& s) noexcept;
  [[nodiscard]] MRS_API bool empty() const noexcept;
  [[nodiscard]] MRS_API uint32_t size() const noexcept;
  [[nodiscard]] MRS_API const char* data() const noexcept;
  [[nodiscard]] MRS_API const char* c_str() const noexcept;

  // Do not use in API
  std::size_t hash() const noexcept { return std::hash<std::string>()(str_); }

 private:
  std::string str_;
  friend MRS_API bool operator==(const str& lhs, const str& rhs) noexcept;
  friend MRS_API bool operator!=(const str& lhs, const str& rhs) noexcept;
  friend MRS_API bool operator==(const str& lhs,
                                 const std::string& rhs) noexcept;
  friend MRS_API bool operator==(const std::string& lhs,
                                 const str& rhs) noexcept;
  friend MRS_API bool operator!=(const str& lhs,
                                 const std::string& rhs) noexcept;
  friend MRS_API bool operator!=(const std::string& lhs,
                                 const str& rhs) noexcept;
};

MRS_API bool operator==(const str& lhs, const str& rhs) noexcept;
MRS_API bool operator!=(const str& lhs, const str& rhs) noexcept;
MRS_API bool operator==(const str& lhs, const std::string& rhs) noexcept;
MRS_API bool operator==(const std::string& lhs, const str& rhs) noexcept;
MRS_API bool operator!=(const str& lhs, const std::string& rhs) noexcept;
MRS_API bool operator!=(const std::string& lhs, const str& rhs) noexcept;

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
