// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license
// information.

#pragma once

#include <cassert>
#include <SDKDDKVer.h>

#if defined(MR_SHARING_WIN)

#define WEBRTC_WIN 1

#ifndef WIN32_LEAN_AND_MEAN
#define WIN32_LEAN_AND_MEAN
#endif

#ifndef NOMINMAX
#define NOMINMAX
#endif

#include <windows.h>

#else defined(MR_SHARING_ANDROID)

#define WEBRTC_POSIX 1

#endif

// Workaround https://github.com/google/googletest/issues/1111
#define GTEST_LANG_CXX11 1

#include "gtest/gtest.h"

#include "peer_connection_test_helpers.h"
