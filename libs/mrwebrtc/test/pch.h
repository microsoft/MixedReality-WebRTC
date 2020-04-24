// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

#pragma once

#include <SDKDDKVer.h>
#include <cassert>

#include <condition_variable>
#include <functional>
#include <mutex>

using namespace std::chrono_literals;

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

#pragma warning(push)
#pragma warning(disable : 4100 4127 4244)
#include "api/datachannelinterface.h"
#include "api/peerconnectioninterface.h"
#include "modules/audio_mixer/audio_mixer_impl.h"
#include "rtc_base/memory/aligned_malloc.h"
#include "rtc_base/thread_annotations.h"
#pragma warning(pop)

#include "peer_connection_test_helpers.h"
