// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

#pragma once

namespace Microsoft::MixedReality::WebRTC {

/// Can mix selected audio sources only.
class ToggleAudioMixer : public webrtc::AudioMixer {
 public:
  ToggleAudioMixer();

  // AudioMixer implementation.
  bool AddSource(Source* audio_source) override;
  void RemoveSource(Source* audio_source) override;
  void Mix(size_t number_of_channels,
           webrtc::AudioFrame* audio_frame_for_mixing) override;

  // Select if the source with the given id must be played on the audio device.
  void RenderSource(int ssrc, bool render);

 private:
  struct KnownSource {
    Source* source;
    bool is_rendered;
  };

  void TryAddToBaseImpl(KnownSource& audio_source);

  rtc::CriticalSection crit_;
  rtc::scoped_refptr<webrtc::AudioMixerImpl> base_impl_;
  std::map<int, KnownSource> source_from_id_;
};

}  // namespace Microsoft::MixedReality::WebRTC
