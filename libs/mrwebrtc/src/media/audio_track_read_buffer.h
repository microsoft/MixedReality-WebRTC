// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

#pragma once

#include "export.h"
#include "common_audio/resampler/include/resampler.h"

namespace Microsoft {
namespace MixedReality {
namespace WebRTC {

struct AudioFrame;
class PeerConnection;

/// High level interface for consuming WebRTC audio tracks.
/// The implementation builds on top of the low-level AudioFrame callbacks
/// and handles all buffering and resampling.
class AudioTrackReadBuffer {
 public:
  /// Create a new stream which buffers 'bufferMs' milliseconds of audio.
  /// WebRTC delivers audio at 10ms intervals so pass a multiple of 10.
  /// Or pass -1 for default buffer size (currently 0.5 seconds).
  /// This registers a remote audio callback on |peer|.
  AudioTrackReadBuffer(PeerConnection* peer, int bufferMs);

  /// Destructs the stream.
  /// Unregister the remote audio callback on the associated PeerConnection.
  ~AudioTrackReadBuffer();

  /// Fill data with samples at the given sampleRate and number of channels.
  /// If the internal buffer overruns, the oldest data will be dropped.
  /// If the internal buffer is exhausted, the data is padded with white noise.
  /// In any case the entire data array is filled.
  void Read(int sampleRate,
            float data[],
            int dataLen,
            int numChannels) noexcept;

 private:
  static void MRS_CALL staticAudioFrameCallback(void* user_data,
                                                const AudioFrame& frame);
  void audioFrameCallback(const void* audio_data,
                          const uint32_t bits_per_sample,
                          const uint32_t sample_rate,
                          const uint32_t number_of_channels,
                          const uint32_t number_of_frames);

  PeerConnection* peer_ = nullptr;
  struct Frame {
    std::vector<std::uint8_t> audio_data;
    uint32_t bits_per_sample;
    uint32_t sample_rate;
    uint32_t number_of_channels;
    uint32_t number_of_frames;
  };
  // Incoming frames received from webrtc - see also buffer_
  std::deque<Frame> frames_;
  // protects frames_ in Read() and audioFrameCallback()
  std::mutex frames_mutex_;
  // max ms of audio data stored in frames_
  int buffer_ms_ = 0;
  // for debugging, we emit a sin on underrun.
  int sinwave_iter_ = 0;

  // Outgoing data resamples to f32 format
  struct Buffer {
    std::unique_ptr<webrtc::Resampler> resampler_ = nullptr;
    std::vector<float> data_;
    int used_ = 0;
    int channels_ = 0;
    int rate_ = 0;

    Buffer();
    ~Buffer();
    int available() const { return (int)data_.size() - used_; }
    int readSome(float* dst, int dstLen) {
      int take = std::min(available(), dstLen);
      memcpy(dst, data_.data() + used_, take * sizeof(float));
      used_ += take;
      return take;
    }
    // Extract/resample data from frame and add it to our buffer.
    void addFrame(const Frame& frame, int dstSampleRate, int dstChannels);
  };
  // Only accessed from callers of Read - no locking needed.
  Buffer buffer_;
};
}  // namespace WebRTC
}  // namespace MixedReality
}  // namespace Microsoft
