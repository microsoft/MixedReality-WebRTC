// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license
// information.

#include "pch.h"

#include "sdp_utils.h"

#include "api/jsepsessiondescription.h"
#include "pc/sessiondescription.h"
#include "pc/webrtcsdp.h"

namespace {

/// Assign a preferred audio or video codec to the media content description,
/// and optionally add some extra codec parameters on top of the default one,
/// overwritting any previous value.
template <typename C>
bool SetPreferredCodec(
    const std ::string& codec_name,
    cricket::MediaContentDescriptionImpl<C>* desc,
    const std::map<std::string, std::string>& extra_codec_params) {
  // Find the preferred codec, if available
  const std::vector<C>& codecs = desc->codecs();
  auto it = std::find_if(
      codecs.begin(), codecs.end(),
      [&codec_name](const C& codec) { return (codec.name == codec_name); });
  if (it == codecs.end()) {
    return false;
  }

  // Assign the codec to the media content description
  std::vector<C> new_codecs;
  new_codecs.reserve(1);
  if (extra_codec_params.empty()) {
    // Add preferred codec with default parameters
    new_codecs.push_back(*it);
  } else {
    // Make a copy to modify the parameters
    C preferred_codec = *it;
    for (auto&& param : extra_codec_params) {
      preferred_codec.SetParam(param.first, param.second);
    }
    new_codecs.push_back(preferred_codec);
  }
  desc->set_codecs(new_codecs);
  return true;
}

}  // namespace

namespace Microsoft::MixedReality::WebRTC {

void SdpParseCodecParameters(const std::string& param_string,
                             std::map<std::string, std::string>& params) {
  std::vector<std::string> key_values;
  rtc::split(param_string, ';', &key_values);
  for (auto&& kv : key_values) {
    std::vector<std::string> param(2);
    if (rtc::split(kv, '=', &param) == 2) {
      params[std::move(param[0])] = std::move(param[1]);
    }
  }
}

std::string SdpForceCodecs(
    const std::string& message,
    const std::string& audio_codec_name,
    const std::map<std::string, std::string>& extra_audio_codec_params,
    const std::string& video_codec_name,
    const std::map<std::string, std::string>& extra_video_codec_params) {
  // Deserialize the SDP message
  webrtc::JsepSessionDescription jdesc(webrtc::SdpType::kOffer);
  webrtc::SdpParseError error;
  if (!webrtc::SdpDeserialize(message, &jdesc, &error)) {
    RTC_LOG(LS_WARNING)
        << "Failed to deserialize SDP message to force codecs. Error line "
        << error.line << ": " << error.description;
    return message;
  }
  if (jdesc.GetType() != webrtc::SdpType::kOffer) {
    RTC_LOG(LS_WARNING) << "Cannot force codecs on non-offer SDP message.";
    return message;
  }

  // Remove codecs not wanted, add extra parameters if needed
  {
    // Loop over the session contents to find the audio and video ones
    const cricket::SessionDescription* const desc = jdesc.description();
    const cricket::ContentInfos& contents = desc->contents();
    for (auto&& content : contents) {
      cricket::MediaContentDescription* media_desc = content.description;
      switch (media_desc->type()) {
        case cricket::MediaType::MEDIA_TYPE_AUDIO:
          // Only try modify the audio codecs if asked for
          if (!audio_codec_name.empty()) {
            cricket::AudioContentDescription* const audio_desc =
                media_desc->as_audio();
            SetPreferredCodec<cricket::AudioCodec>(audio_codec_name, audio_desc,
                                                   extra_audio_codec_params);
          }
          break;
        case cricket::MediaType::MEDIA_TYPE_VIDEO:
          // Only try modify the audio codecs if asked for
          if (!video_codec_name.empty()) {
            cricket::VideoContentDescription* const video_desc =
                media_desc->as_video();
            SetPreferredCodec<cricket::VideoCodec>(video_codec_name, video_desc,
                                                   extra_video_codec_params);
          }
          break;
        case cricket::MediaType::MEDIA_TYPE_DATA:
          continue;
      }
    }
  }

  // Re-serialize the SDP modified message
  return webrtc::SdpSerialize(jdesc);
}

}  // namespace Microsoft::MixedReality::WebRTC
