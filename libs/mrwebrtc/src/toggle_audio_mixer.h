// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

#pragma once

namespace Microsoft {
namespace MixedReality {
namespace WebRTC {

/// Can mix selected audio sources only.
class ToggleAudioMixer : public webrtc::AudioMixer {
 public:
  ToggleAudioMixer();

  // AudioMixer implementation.
  bool AddSource(Source* audio_source) override;
  void RemoveSource(Source* audio_source) override;
  void Mix(size_t number_of_channels,
           webrtc::AudioFrame* audio_frame_for_mixing) override;

  // Select if the source with the given id must be output to the audio device.
  void OutputSource(int ssrc, bool output);

 private:
  struct KnownSource {
    Source* source;
    bool is_output;
  };

  void TryAddToBaseImpl(KnownSource& audio_source);

  rtc::CriticalSection crit_;
  rtc::scoped_refptr<webrtc::AudioMixerImpl> base_impl_;
  std::map<int, KnownSource> source_from_id_;
};

}  // namespace WebRTC
}  // namespace MixedReality
}  // namespace Microsoft
