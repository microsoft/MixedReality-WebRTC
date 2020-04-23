// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license
// information.

#include "pch.h"

#include "str.h"

#if defined(MRS_USE_STR_WRAPPER)

namespace Microsoft::MixedReality::WebRTC {

str::str() = default;
str::str(const std::string& s) : str_(s) {}
str::str(std::string&& s) noexcept : str_(std::move(s)) {}
str::str(std::string_view view) : str_(view) {}
str::str(const char* s) : str_(s) {}
str::~str() = default;

str& str::operator=(const std::string& s) {
  str_ = s;
  return (*this);
}

str& str::operator=(std::string&& s) noexcept {
  str_ = std::move(s);
  return (*this);
}

bool str::empty() const noexcept {
  return str_.empty();
}

uint32_t str::size() const noexcept {
  return static_cast<uint32_t>(str_.size());
}

const char* str::data() const noexcept {
  return str_.data();
}

const char* str::c_str() const noexcept {
  return str_.c_str();
}

bool operator==(const str& lhs, const str& rhs) noexcept {
  return (lhs.str_ == rhs.str_);
}

bool operator!=(const str& lhs, const str& rhs) noexcept {
  return (lhs.str_ != rhs.str_);
}

bool operator==(const str& lhs, const std::string& rhs) noexcept {
  return (lhs.str_ == rhs);
}

bool operator==(const std::string& lhs, const str& rhs) noexcept {
  return (lhs == rhs.str_);
}

bool operator!=(const str& lhs, const std::string& rhs) noexcept {
  return (lhs.str_ != rhs);
}

bool operator!=(const std::string& lhs, const str& rhs) noexcept {
  return (lhs != rhs.str_);
}

}  // namespace Microsoft::MixedReality::WebRTC

#endif  // defined(MRS_USE_STR_WRAPPER)
