// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license
// information.

#include "pch.h"

#include "sdp_utils.h"

#include "api/jsep_session_description.h"
#include "pc/session_description.h"
#include "pc/webrtc_sdp.h"

namespace Microsoft::MixedReality::WebRTC {

std::string SdpForceCodecs(const std::string& message,
                           const std::string& audio_codec_name,
                           const std::string& video_codec_name) {
  // Deserialize the SDP message
  webrtc::JsepSessionDescription jdesc(webrtc::SdpType::kOffer);
  webrtc::SdpParseError error;
  if (!webrtc::SdpDeserialize(message, &jdesc, &error)) {
    RTC_LOG(LS_WARNING)
        << "Failed to deserialize SDP message to force codecs. Error line "
        << error.line << ": " << error.description;
    return message;
  }

  // Remove codecs not wanted
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
            const std::vector<cricket::AudioCodec>& codecs =
                audio_desc->codecs();
            auto it = std::find_if(
                codecs.begin(), codecs.end(),
                [&audio_codec_name](const cricket::AudioCodec& codec) {
                  return (codec.name == audio_codec_name);
                });
            if (it == codecs.end()) {
              break;
            }
            std::vector<cricket::AudioCodec> new_codecs;
            new_codecs.push_back(*it);
            audio_desc->set_codecs(new_codecs);
          }
          break;
        case cricket::MediaType::MEDIA_TYPE_VIDEO:
          // Only try modify the audio codecs if asked for
          if (!video_codec_name.empty()) {
            cricket::VideoContentDescription* const video_desc =
                media_desc->as_video();
            const std::vector<cricket::VideoCodec>& codecs =
                video_desc->codecs();
            auto it = std::find_if(
                codecs.begin(), codecs.end(),
                [&video_codec_name](const cricket::VideoCodec& codec) {
                  return (codec.name == video_codec_name);
                });
            if (it == codecs.end()) {
              break;
            }
            std::vector<cricket::VideoCodec> new_codecs;
            new_codecs.push_back(*it);
            video_desc->set_codecs(new_codecs);
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
