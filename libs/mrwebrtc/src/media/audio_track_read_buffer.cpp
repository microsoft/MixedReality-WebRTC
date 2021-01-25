// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

#include "pch.h"

#include "audio_frame.h"
#include "audio_frame_observer.h"
#include "audio_track_read_buffer.h"
#include "interop/global_factory.h"
#include "peer_connection.h"
#include "remote_audio_track_interop.h"

namespace Microsoft {
namespace MixedReality {
namespace WebRTC {

void AudioTrackReadBuffer::OnData(const void* audio_data,
                                  int bits_per_sample,
                                  int sample_rate,
                                  size_t number_of_channels,
                                  size_t number_of_frames) {
  std::lock_guard<std::mutex> lock(frames_mutex_);
  // maintain buffering limits, after adding this frame
  const size_t maxFrames = std::max(buffer_size_ms_ / 10, 1);
  while (frames_.size() > maxFrames) {
    frames_.pop_front();
    has_overrun_ = true;
  }
  // add the new frame
  frames_.emplace_back();
  auto& frame = frames_.back();
  frame.bits_per_sample = bits_per_sample;
  frame.sample_rate = sample_rate;
  frame.number_of_channels = rtc::checked_cast<uint32_t>(number_of_channels);
  frame.number_of_frames = rtc::checked_cast<uint32_t>(number_of_frames);
  size_t size =
      (size_t)(bits_per_sample / 8) * number_of_channels * number_of_frames;
  auto src_bytes = static_cast<const std::uint8_t*>(audio_data);
  frame.audio_data.insert(frame.audio_data.begin(), src_bytes,
                          src_bytes + size);
}

AudioTrackReadBuffer::AudioTrackReadBuffer(
    RefPtr<GlobalFactory> global_factory,
    rtc::scoped_refptr<webrtc::AudioTrackInterface> track,
    int bufferMs)
    : TrackedObject(std::move(global_factory),
                    ObjectType::kAudioTrackReadBuffer),
      track_(std::move(track)),
      buffer_size_ms_(bufferMs >= 10 ? bufferMs : 500) {
  track_->AddSink(this);
}

AudioTrackReadBuffer::~AudioTrackReadBuffer() {
  track_->RemoveSink(this);
}

AudioTrackReadBuffer::Buffer::Buffer() {
  resampler_ = std::make_unique<webrtc::Resampler>();
}
AudioTrackReadBuffer::Buffer::~Buffer() {}

Result AudioTrackReadBuffer::Buffer::addFrame(const Frame& frame,
                                              int dst_sample_rate,
                                              int dst_channels) {
  assert(frame.number_of_channels == 1 || frame.number_of_channels == 2);
  assert(dst_channels == 1 || dst_channels == 2);

  // We may require up to 2 intermediate buffers
  // We always write into buffer_next and then swap front/back buffers
  std::vector<short> buffer_front;
  std::vector<short> buffer_back;

  const short* curr_data;  //< Current version of the processed data.
  size_t src_count;        //< Includes samples from *all* channels.
  int curr_channels = frame.number_of_channels;

  // Ensure source is s16 (16-bit signed)
  if (frame.bits_per_sample == 16) {
    curr_data = (short*)frame.audio_data.data();
    src_count = frame.number_of_frames * frame.number_of_channels;
  } else if (frame.bits_per_sample == 8) {
    buffer_front.resize(frame.audio_data.size());
    short* data = buffer_front.data();
    // 8 bit data is unsigned8, 16 bit is signed16
    for (size_t i = 0; i < frame.audio_data.size(); ++i) {
      //   0 * 257 - 32768 == -32768
      // 255 * 257 - 32768 ==  32767
      data[i] = ((int)frame.audio_data[i] * 257) - 32768;
    }
    curr_data = data;
    src_count = buffer_front.size();
    swap(buffer_front, buffer_back);
  } else {
    RTC_LOG(LS_ERROR) << "Unsupported audio bit size (not 8-bit nor 16-bit). "
                         "Dropping audio frame.";
    return Result::kInvalidParameter;
  }

  // Stereo -> Mono
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

  // Resample
  if ((int)frame.sample_rate != dst_sample_rate) {
    buffer_front.resize((src_count * dst_sample_rate / frame.sample_rate) + 1);
    short* data = buffer_front.data();
    int res = resampler_->ResetIfNeeded(frame.sample_rate, dst_sample_rate,
                                        curr_channels);
    if (res != 0) {
      RTC_LOG(LS_ERROR)
          << "Resampler does not implement conversion of sample rate "
          << frame.sample_rate << " -> " << dst_sample_rate
          << ". Dropping audio frame.";
      data_.clear();
      used_ = 0;
      return Result::kAudioResamplingNotSupported;
    }
    size_t count;
    res = resampler_->Push(curr_data, src_count, data, buffer_front.size(),
                           count);
    if (res != 0) {
      RTC_LOG(LS_ERROR) << "Resampler failed to adjust for sample rate ("
                        << frame.sample_rate << " -> " << dst_sample_rate
                        << "). Dropping audio frame.";
      data_.clear();
      used_ = 0;
      return Result::kUnknownError;
    }

    curr_data = data;
    src_count = count;
    swap(buffer_front, buffer_back);
  }

  // Convert s16 to f32
  if (curr_channels == 1 && dst_channels == 2) {
    // duplicate
    data_.resize(src_count * 2);
    for (size_t i = 0; i < src_count; ++i) {
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
  return Result::kSuccess;
}

Result AudioTrackReadBuffer::Read(
    int sample_rate,
    int num_channels,
    mrsAudioTrackReadBufferPadBehavior pad_behavior,
    float* samples_out,
    int num_samples_max,
    int* num_samples_read_out,
    bool* has_overrun_out) noexcept {
  float* dst = samples_out;
  int dst_len = num_samples_max;  // number of points remaining

  *has_overrun_out = false;

  while (dst_len > 0) {
    if (sample_rate == buffer_.rate_ && num_channels == buffer_.channels_ &&
        buffer_.available()) {
      // There is still data in the buffer and the format matches, read some.
      int len = buffer_.readSome(dst, dst_len);
      dst += len;
      dst_len -= len;
    } else {
      // Buffer is empty or the format has changed.
      // If the format doesn't match we drop what's left in the buffer and
      // ensure the next frame matches. This may drop some data but will only
      // happen when the output sample rate/channels change (i.e. rarely)

      absl::optional<Frame> frame;
      {
        std::unique_lock<std::mutex> lock(frames_mutex_);

        // Read and reset the overrun flag.
        *has_overrun_out = *has_overrun_out || has_overrun_;
        has_overrun_ = false;

        // Pop the next frame.
        if (!frames_.empty()) {
          frame = std::move(frames_.front());
          frames_.pop_front();
        }
      }

      if (frame) {
        Result res = buffer_.addFrame(*frame, sample_rate, num_channels);
        if (res != Result::kSuccess) {
          *num_samples_read_out = num_samples_max - dst_len;
          return res;
        }
      } else {
        // No more input.
        // Pad output buffer if requested by caller.
        constexpr float freq = 2 * 222 * float(M_PI);
        switch (pad_behavior) {
          case mrsAudioTrackReadBufferPadBehavior::kDoNotPad:
            break;
          case mrsAudioTrackReadBufferPadBehavior::kPadWithZero:
            std::memset(dst, 0, dst_len * sizeof(float));
            break;
          case mrsAudioTrackReadBufferPadBehavior::kPadWithSine:
            for (int i = 0; i < dst_len; ++i) {
              dst[i] = 0.15f * sinf((freq * (sinwave_iter_ + i)) /
                                    (sample_rate * num_channels));
            }
            sinwave_iter_ = (sinwave_iter_ + dst_len) % 628318530 /*twopi*/;
            sinwave_iter_ += dst_len;
            break;
          default:
            RTC_NOTREACHED();
            break;
        }
        *num_samples_read_out = num_samples_max - dst_len;
        return Result::kSuccess;  // and return
      }
    }
  }
  *num_samples_read_out = num_samples_max;
  return Result::kSuccess;
}

}  // namespace WebRTC
}  // namespace MixedReality
}  // namespace Microsoft
