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
  auto& frame = frames_.emplace_back();
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
  peer->RegisterRemoteAudioFrameCallback(
      AudioFrameReadyCallback{&staticAudioFrameCallback, this});
}

AudioTrackReadBuffer::~AudioTrackReadBuffer() {
  peer_->RegisterRemoteAudioFrameCallback(AudioFrameReadyCallback{});
}

AudioTrackReadBuffer::Buffer::Buffer() {
  resampler_ = std::make_unique<webrtc::Resampler>();
}
AudioTrackReadBuffer::Buffer::~Buffer() {}

void AudioTrackReadBuffer::Buffer::addFrame(const Frame& frame,
                                            int dstSampleRate,
                                            int dstChannels) {
  // We may require up to 2 intermediate buffers
  // We always write into buffer_next and then swap front/back buffers
  std::vector<short> buffer_front;
  std::vector<short> buffer_back;

  // tmpData will eventually hold u16 data with the correct number of channels
  const short* srcData;
  size_t srcCount;

  // ensure source is 16 bit
  if (frame.bits_per_sample == 16) {
    srcData = (short*)frame.audio_data.data();
    srcCount = frame.number_of_frames * frame.number_of_channels;
  } else if (frame.bits_per_sample == 8) {
    buffer_front.resize(frame.audio_data.size());
    short* data = buffer_front.data();
    // 8 bit data is unsigned8, 16 bit is signed16
    for (int i = 0; i < (int)frame.audio_data.size(); ++i) {
      data[i] = ((int)frame.audio_data[i] * 256) - 32768;
    }
    srcData = data;
    srcCount = buffer_front.size();
    swap(buffer_front, buffer_back);
  } else {
    assert(false);
    return;
  }

  // match destination number of channels
  switch (frame.number_of_channels * 16 + dstChannels) {
    case 0x11:
    case 0x22:
      break;      // nop
    case 0x12: {  // duplicate
      buffer_front.resize(srcCount * 2);
      short* data = buffer_front.data();
      for (int i = 0; i < (int)srcCount; ++i) {
        data[2 * i + 0] = srcData[i];
        data[2 * i + 1] = srcData[i];
      }
      srcData = data;
      srcCount = buffer_front.size();
      swap(buffer_front, buffer_back);
      break;
    }
    case 0x21: {  // average L&R
      buffer_front.resize(srcCount / 2);
      short* data = buffer_front.data();
      for (int i = 0; i < (int)srcCount; ++i) {
        data[i] = (srcData[2 * i] + srcData[2 * i + 1]) / 2;
      }
      srcData = data;
      srcCount = buffer_front.size();
      swap(buffer_front, buffer_back);
      break;
    }
    default:
      assert(false);
      return;
  }

  // match sample rate
  if ((int)frame.sample_rate != dstSampleRate) {
    buffer_front.resize((srcCount * dstSampleRate / frame.sample_rate) + 1);
    short* data = buffer_front.data();

    resampler_->ResetIfNeeded(frame.sample_rate, dstSampleRate, dstChannels);
    size_t count;
    resampler_->Push(srcData, srcCount, data, buffer_front.size(), count);
    srcData = data;
    srcCount = count;
    swap(buffer_front, buffer_back);
  }

  // Convert s16 to f32
  data_.resize(srcCount);
  for (size_t i = 0; i < srcCount; ++i) {
    data_[i] = (float)srcData[i] / 32768.0f;
  }
  used_ = 0;
  channels_ = dstChannels;
  rate_ = dstSampleRate;
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
