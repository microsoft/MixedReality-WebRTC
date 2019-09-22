// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license
// information.

#pragma once

#include <cassert>
#include <cstdint>
#include <functional>
#include <string>
#include <string_view>

#include "targetver.h"

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

// Prevent external headers from triggering warnings that would break compiling
// due to warning-as-error.
#pragma warning(push, 2)
#pragma warning(disable : 4100)
#pragma warning(disable : 4244)

// Core WebRTC
#include "absl/memory/memory.h"
#include "api/audio_codecs/builtin_audio_decoder_factory.h"
#include "api/audio_codecs/builtin_audio_encoder_factory.h"
#include "api/datachannelinterface.h"
#include "api/mediaconstraintsinterface.h"
#include "api/mediastreaminterface.h"
#include "api/peerconnectioninterface.h"
#include "api/video/i420_buffer.h"
#include "api/videosourceproxy.h"
#include "media/engine/internaldecoderfactory.h"
#include "media/engine/internalencoderfactory.h"
#include "media/engine/multiplexcodecfactory.h"
#include "media/engine/webrtcvideocapturerfactory.h"
#include "media/engine/webrtcvideodecoderfactory.h"
#include "media/engine/webrtcvideoencoderfactory.h"
#include "modules/audio_device/include/audio_device.h"
#include "modules/audio_processing/include/audio_processing.h"
#include "modules/video_capture/video_capture_factory.h"
#include "rtc_base/memory/aligned_malloc.h"

// libyuv from WebRTC repository for color conversion
#include "libyuv.h"

// UWP wrappers
#if defined(WINUWP)
#include <winrt/windows.applicationmodel.core.h>
#include "impl_org_webRtc_EventQueue.h"
#include "impl_org_webRtc_VideoCapturer.h"
#include "impl_org_webRtc_VideoCapturerCreationParameters.h"
#include "impl_org_webRtc_VideoDeviceInfo.h"
#include "impl_org_webRtc_VideoFormat.h"
#include "impl_org_webRtc_WebRtcFactory.h"
#include "impl_org_webRtc_WebRtcFactoryConfiguration.h"
#include "impl_org_webRtc_WebRtcLib.h"
#include "impl_org_webRtc_WebRtcLibConfiguration.h"
#endif

#pragma warning(pop)
