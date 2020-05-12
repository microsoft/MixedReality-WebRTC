// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

#include "pch.h"

#include "audio_frame.h"
#include "audio_frame_observer.h"
#include "audio_track_read_buffer.h"
#include "peer_connection.h"

namespace Microsoft {
namespace MixedReality {
namespace WebRTC {

void AudioTrackReadBuffer::audioFrameCallback(const void* audio_data,
                                              const uint32_t bits_per_sample,
                                              const uint32_t sample_rate,
                                              const uint32_t number_of_channels,
                                              const uint32_t number_of_frames) {
  std::lock_guard<std::mutex> lock(frames_mutex_);
  // maintain buffering limits, after adding this frame
  const size_t maxFrames = std::max(buffer_ms_ / 10, 1);
  while (frames_.size() > maxFrames) {
    frames_.pop_front();
  }
  // add the new frame
  frames_.emplace_back();
  auto& frame = frames_.back();
  frame.bits_per_sample = bits_per_sample;
  frame.sample_rate = sample_rate;
  frame.number_of_channels = number_of_channels;
  frame.number_of_frames = number_of_frames;
  size_t size =
      (size_t)(bits_per_sample / 8) * number_of_channels * number_of_frames;
  auto src_bytes = static_cast<const std::byte*>(audio_data);
  frame.audio_data.insert(frame.audio_data.begin(), src_bytes,
                          src_bytes + size);
}

void AudioTrackReadBuffer::staticAudioFrameCallback(void* user_data,
                                                    const AudioFrame& frame) {
  auto ars = static_cast<AudioTrackReadBuffer*>(user_data);
  ars->audioFrameCallback(frame.data_, frame.bits_per_sample_,
                          frame.sampling_rate_hz_, frame.channel_count_,
                          frame.sample_count_);
}

AudioTrackReadBuffer::AudioTrackReadBuffer(PeerConnection* peer, int bufferMs)
    : peer_(peer),
      buffer_ms_(bufferMs >= 10 ? bufferMs : 500 /*TODO good value?*/) {
    // FIXME
  //peer->RegisterRemoteAudioFrameCallback(
  //    AudioFrameReadyCallback{&staticAudioFrameCallback, this});
}

AudioTrackReadBuffer::~AudioTrackReadBuffer() {
    // FIXME
  //peer_->RegisterRemoteAudioFrameCallback(AudioFrameReadyCallback{});
}

AudioTrackReadBuffer::Buffer::Buffer() {
  resampler_ = std::make_unique<webrtc::Resampler>();
}
AudioTrackReadBuffer::Buffer::~Buffer() {}

void AudioTrackReadBuffer::Buffer::addFrame(const Frame& frame,
                                            int dst_sample_rate,
                                            int dst_channels) {
  assert(frame.number_of_channels == 1 || frame.number_of_channels == 2);
  assert(dst_channels == 1 || dst_channels == 2);

  // We may require up to 2 intermediate buffers
  // We always write into buffer_next and then swap front/back buffers
  std::vector<short> buffer_front;
  std::vector<short> buffer_back;

  const short* curr_data; //< Current version of the processed data.
  size_t src_count;  //< Includes samples from *all* channels.
  int curr_channels = frame.number_of_channels;

  // ensure source is 16 bit
  if (frame.bits_per_sample == 16) {
    curr_data = (short*)frame.audio_data.data();
    src_count = frame.number_of_frames * frame.number_of_channels;
  } else if (frame.bits_per_sample == 8) {
    buffer_front.resize(frame.audio_data.size());
    short* data = buffer_front.data();
    // 8 bit data is unsigned8, 16 bit is signed16
    for (int i = 0; i < (int)frame.audio_data.size(); ++i) {
      data[i] = ((int)frame.audio_data[i] * 256) - 32768;
    }
    curr_data = data;
    src_count = buffer_front.size();
    swap(buffer_front, buffer_back);
  } else {
    FATAL();
    return;
  }

  if (frame.number_of_channels == 2 && dst_channels == 1) {
    // average L&R
    buffer_front.resize(src_count / 2);
    short* data = buffer_front.data();
    for (int i = 0; i < (int)src_count; ++i) {
      data[i] = (curr_data[2 * i] + curr_data[2 * i + 1]) / 2;
    }

    curr_data = data;
    src_count = buffer_front.size();
    curr_channels = 1;
    swap(buffer_front, buffer_back);
  }

  if ((int)frame.sample_rate != dst_sample_rate) {
    // match sample rate
    buffer_front.resize((src_count * dst_sample_rate / frame.sample_rate) + 1);
    short* data = buffer_front.data();
    resampler_->ResetIfNeeded(frame.sample_rate, dst_sample_rate, curr_channels);
    size_t count;
    int res = resampler_->Push(curr_data, src_count, data, buffer_front.size(), count);
    RTC_DCHECK(res == 0);

    curr_data = data;
    src_count = count;
    swap(buffer_front, buffer_back);
  }

  // Convert s16 to f32
  if (curr_channels == 1 && dst_channels == 2) {
    // duplicate
    data_.resize(src_count * 2);
    for (int i = 0; i < (int)src_count; ++i) {
      float val = (float)curr_data[i] / 32768.0f;
      data_[2 * i + 0] = val;
      data_[2 * i + 1] = val;
    }
  } else {
    data_.resize(src_count);
    for (size_t i = 0; i < src_count; ++i) {
      data_[i] = (float)curr_data[i] / 32768.0f;
    }
  }
  used_ = 0;
  channels_ = dst_channels;
  rate_ = dst_sample_rate;
}

void AudioTrackReadBuffer::Read(int sampleRate,
                                float dataOrig[],
                                int dataLenOrig,
                                int channels) noexcept {
  float* dst = dataOrig;
  int dstLen = dataLenOrig;  // number of points remaining

  while (dstLen > 0) {
    if (sampleRate == buffer_.rate_ && channels == buffer_.channels_ &&
        buffer_.available()) {
      // format matches, fill some from the buffer. If the format doesn't
      // match we will fall through and ensure the next frame matches. This
      // may drop some data but will only happen when the output
      // sampleRate/channels change (i.e. rarely)
      int len = buffer_.readSome(dst, dstLen);
      dst += len;
      dstLen -= len;
    } else {
      Frame frame;
      {
        std::unique_lock<std::mutex> lock(frames_mutex_);
        if (frames_.empty()) {  // no more input! fill with sin wave
          lock.unlock();
          constexpr float freq = 2 * 222 * float(M_PI);
          for (int i = 0; i < dstLen; ++i) {
            dst[i] = 0.15f * sinf((freq * (sinwave_iter_ + i)) /
                                  (sampleRate * channels));
          }
          sinwave_iter_ = (sinwave_iter_ + dstLen) % 628318530 /*twopi*/;
          sinwave_iter_ += dstLen;
          return;  // and return
        }
        Frame& f = frames_.front();
        frame.audio_data.swap(f.audio_data);
        frame.bits_per_sample = f.bits_per_sample;
        frame.sample_rate = f.sample_rate;
        frame.number_of_channels = f.number_of_channels;
        frame.number_of_frames = f.number_of_frames;
        frames_.pop_front();
      }
      buffer_.addFrame(frame, sampleRate, channels);
    }
  }
}

}  // namespace WebRTC
}  // namespace MixedReality
}  // namespace Microsoft
