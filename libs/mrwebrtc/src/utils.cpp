// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

#include "pch.h"

#include "interop_api.h"
#include "mrs_errors.h"
#include "result.h"
#include "remote_audio_track_interop.h"
#include "utils.h"

namespace Microsoft {
namespace MixedReality {
namespace WebRTC {

Result ResultFromRTCErrorType(webrtc::RTCErrorType type) {
  using namespace webrtc;
  switch (type) {
    case RTCErrorType::NONE:
      return Result::kSuccess;
    case RTCErrorType::UNSUPPORTED_OPERATION:
    case RTCErrorType::UNSUPPORTED_PARAMETER:
      return Result::kUnsupported;
    case RTCErrorType::INVALID_PARAMETER:
    case RTCErrorType::INVALID_RANGE:
      return Result::kInvalidParameter;
    case RTCErrorType::INVALID_STATE:
      return Result::kNotInitialized;
    default:
      return Result::kUnknownError;
  }
}

Error ErrorFromRTCError(const webrtc::RTCError& error) {
  return Error(ResultFromRTCErrorType(error.type()), error.message());
}

Error ErrorFromRTCError(webrtc::RTCError&& error) {
  // Ideally would move the std::string out of |error|, but doesn't look
  // possible at the moment.
  return Error(ResultFromRTCErrorType(error.type()), error.message());
}

mrsMediaKind MediaKindFromRtc(cricket::MediaType media_type) {
  switch (media_type) {
    case cricket::MediaType::MEDIA_TYPE_AUDIO:
      return mrsMediaKind::kAudio;
    case cricket::MediaType::MEDIA_TYPE_VIDEO:
      return mrsMediaKind::kVideo;
    default:
      RTC_LOG(LS_ERROR) << "Invalid media type, expected audio or video.";
      RTC_NOTREACHED();
      // Silence error about uninitialized variable when assigning the result of
      // this function, and return some visibly invalid value.
      return (mrsMediaKind)-1;
  }
}

cricket::MediaType MediaKindToRtc(mrsMediaKind media_kind) {
  switch (media_kind) {
    case mrsMediaKind::kAudio:
      return cricket::MediaType::MEDIA_TYPE_AUDIO;
    case mrsMediaKind::kVideo:
      return cricket::MediaType::MEDIA_TYPE_VIDEO;
    default:
      RTC_LOG(LS_ERROR) << "Unknown media kind, expected audio or video.";
      RTC_NOTREACHED();
      // Silence error about uninitialized variable when assigning the result of
      // this function, and return some visibly invalid value (mrsMediaKind is
      // audio or video only).
      return cricket::MediaType::MEDIA_TYPE_DATA;
  }
}

const char* ToString(cricket::MediaType media_type) {
  switch (media_type) {
    case cricket::MediaType::MEDIA_TYPE_AUDIO:
      return "audio";
    case cricket::MediaType::MEDIA_TYPE_VIDEO:
      return "video";
    case cricket::MediaType::MEDIA_TYPE_DATA:
      return "data";
    default:
      return "<unknown>";
  }
}

const char* ToString(webrtc::RtpTransceiverDirection dir) {
  switch (dir) {
    case webrtc::RtpTransceiverDirection::kSendRecv:
      return "kSendRecv";
    case webrtc::RtpTransceiverDirection::kSendOnly:
      return "kSendOnly";
    case webrtc::RtpTransceiverDirection::kRecvOnly:
      return "kRecvOnly";
    case webrtc::RtpTransceiverDirection::kInactive:
      return "kInactive";
    default:
      return "<unknown>";
  }
}

const char* ToString(bool value) {
  return (value ? "true" : "false");
}
bool IsValidAudioTrackBufferPadBehavior(
    mrsAudioTrackReadBufferPadBehavior pad_behavior) {
  return pad_behavior >= mrsAudioTrackReadBufferPadBehavior::kDoNotPad &&
         pad_behavior < mrsAudioTrackReadBufferPadBehavior::kCount;
}

}  // namespace WebRTC
}  // namespace MixedReality
}  // namespace Microsoft
