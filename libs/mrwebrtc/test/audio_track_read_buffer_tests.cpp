// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

#include "pch.h"

#include "audio_frame.h"
#include "device_audio_track_source_interop.h"
#include "interop_api.h"
#include "local_audio_track_interop.h"
#include "remote_audio_track_interop.h"
#include "transceiver_interop.h"

#include "test_utils.h"

namespace {

class AudioTrackReadBufferTests
    : public TestUtils::TestBase,
      public testing::WithParamInterface<mrsSdpSemantic> {};

}  // namespace

#if !defined(MRSW_EXCLUDE_DEVICE_TESTS)

namespace {

// PeerConnectionAudioTrackAddedCallback
using AudioTrackAddedCallback =
    InteropCallback<const mrsRemoteAudioTrackAddedInfo*>;

// PeerConnectionAudioFrameCallback
using AudioFrameCallback = InteropCallback<const AudioFrame&>;

bool IsSilent_uint8(const uint8_t* data,
                    uint32_t size,
                    uint8_t& min,
                    uint8_t& max) noexcept {
  // 8bpp in [0:255] range, UINT8
  const uint8_t* const s = data;
  const uint8_t* const e = s + size;
  // Currently "mute" on audio does not mute completely, so the frame is
  // not *exactly* zero. So check if it's close enough.
  min = 255;
  max = 0;
  for (const uint8_t* p = s; p < e; ++p) {
    min = std::min(min, *p);
    max = std::max(max, *p);
  }
  const bool is_silent = (min >= 126) && (max <= 129);  // ~1%
  return is_silent;
}

bool IsSilent_int16(const int16_t* data,
                    uint32_t size,
                    int16_t& min,
                    int16_t& max) noexcept {
  // 16bpp in [-32768:32767] range, SINT16
  const int16_t* const s = data;
  const int16_t* const e = s + size;
  // Currently "mute" on audio does not mute completely, so the frame is
  // not *exactly* zero. So check if it's close enough.
  min = 32767;
  max = -32768;
  for (const int16_t* p = s; p < e; ++p) {
    min = std::min(min, *p);
    max = std::max(max, *p);
  }
  const bool is_silent = (min >= -5) && (max <= 5);  // ~1.5e-4 = 0.015%
  return is_silent;
}

}  // namespace

INSTANTIATE_TEST_CASE_P(,
                        AudioTrackReadBufferTests,
                        testing::ValuesIn(TestUtils::TestSemantics),
                        TestUtils::SdpSemanticToString);

TEST_P(AudioTrackReadBufferTests, Resample) {
  mrsPeerConnectionConfiguration pc_config{};
  pc_config.sdp_semantic = GetParam();
  LocalPeerPairRaii pair(pc_config);

  // Grab the handle of the remote track from the remote peer (#2) via the
  // AudioTrackAdded callback.
  mrsTransceiverHandle audio_transceiver2{};
  mrsRemoteAudioTrackHandle audio_track2{};
  Event track_added2_ev;
  AudioTrackAddedCallback track_added2_cb =
      [&audio_track2, &audio_transceiver2,
       &track_added2_ev](const mrsRemoteAudioTrackAddedInfo* info) {
        audio_track2 = info->track_handle;
        audio_transceiver2 = info->audio_transceiver_handle;
        track_added2_ev.Set();
      };
  mrsPeerConnectionRegisterAudioTrackAddedCallback(pair.pc2(),
                                                   CB(track_added2_cb));

  // Create an audio transceiver on #1
  mrsTransceiverHandle audio_transceiver1{};
  mrsTransceiverInitConfig transceiver_config{};
  transceiver_config.name = "transceiver1";
  transceiver_config.media_kind = mrsMediaKind::kAudio;
  ASSERT_EQ(Result::kSuccess,
            mrsPeerConnectionAddTransceiver(pair.pc1(), &transceiver_config,
                                            &audio_transceiver1));
  ASSERT_NE(nullptr, audio_transceiver1);

  // Create the audio source #1
  mrsLocalAudioDeviceInitConfig device_config{};
  mrsDeviceAudioTrackSourceHandle audio_source1{};
  ASSERT_EQ(Result::kSuccess,
            mrsDeviceAudioTrackSourceCreate(&device_config, &audio_source1));
  ASSERT_NE(nullptr, audio_source1);

  // Create the local audio track #1
  mrsLocalAudioTrackInitSettings init_settings{};
  init_settings.track_name = "test_audio_track";
  mrsLocalAudioTrackHandle audio_track1{};
  ASSERT_EQ(Result::kSuccess,
            mrsLocalAudioTrackCreateFromSource(&init_settings, audio_source1,
                                               &audio_track1));
  ASSERT_NE(nullptr, audio_track1);

  // Audio tracks start enabled
  ASSERT_NE(mrsBool::kFalse, mrsLocalAudioTrackIsEnabled(audio_track1));

  // Check transceiver #1 consistency
  {
    // Local track is NULL
    mrsLocalVideoTrackHandle track_handle_local{};
    ASSERT_EQ(Result::kSuccess, mrsTransceiverGetLocalAudioTrack(
                                    audio_transceiver1, &track_handle_local));
    ASSERT_EQ(nullptr, track_handle_local);

    // Remote track is NULL
    mrsRemoteVideoTrackHandle track_handle_remote{};
    ASSERT_EQ(Result::kSuccess, mrsTransceiverGetRemoteAudioTrack(
                                    audio_transceiver1, &track_handle_remote));
    ASSERT_EQ(nullptr, track_handle_remote);
  }

  // Add the local audio track on the transceiver #1
  ASSERT_EQ(Result::kSuccess,
            mrsTransceiverSetLocalAudioTrack(audio_transceiver1, audio_track1));

  // Check transceiver #1 consistency
  {
    // Local track is audio_track1
    mrsLocalVideoTrackHandle track_handle_local{};
    ASSERT_EQ(Result::kSuccess, mrsTransceiverGetLocalAudioTrack(
                                    audio_transceiver1, &track_handle_local));
    ASSERT_EQ(audio_track1, track_handle_local);

    // Remote track is NULL
    mrsRemoteVideoTrackHandle track_handle_remote{};
    ASSERT_EQ(Result::kSuccess, mrsTransceiverGetRemoteAudioTrack(
                                    audio_transceiver1, &track_handle_remote));
    ASSERT_EQ(nullptr, track_handle_remote);
  }

  // Connect #1 and #2
  pair.ConnectAndWait();

  // Wait for remote track to be added on #2
  ASSERT_TRUE(track_added2_ev.WaitFor(5s));
  ASSERT_NE(nullptr, audio_track2);
  ASSERT_NE(nullptr, audio_transceiver2);

  // Check transceiver #2 consistency
  {
    // Local track is NULL
    mrsLocalVideoTrackHandle track_handle_local{};
    ASSERT_EQ(Result::kSuccess, mrsTransceiverGetLocalAudioTrack(
                                    audio_transceiver2, &track_handle_local));
    ASSERT_EQ(nullptr, track_handle_local);

    // Remote track is audio_track2
    mrsRemoteVideoTrackHandle track_handle_remote{};
    ASSERT_EQ(Result::kSuccess, mrsTransceiverGetRemoteAudioTrack(
                                    audio_transceiver2, &track_handle_remote));
    ASSERT_EQ(audio_track2, track_handle_remote);
  }

  // Create read buffer
  mrsAudioTrackReadBufferHandle read_buffer2{};
  ASSERT_EQ(Result::kSuccess,
            mrsRemoteAudioTrackCreateReadBuffer(audio_track2, &read_buffer2));

  // Check several times this, because the audio "mute" is flaky, does not
  // really mute the audio, so check that the reported status is still
  // correct.
  ASSERT_NE(mrsBool::kFalse, mrsLocalAudioTrackIsEnabled(audio_track1));

  // Try some dummy resampling with some improbable frequency that the internal
  // resampler surely does not support, whatever the input frequency from the
  // audio device may be (generally 48kHz).
  constexpr int kImprobableSampleRate = 7919;  // prime number
  int num_samples_read = 0;
  mrsBool has_overrun = mrsBool::kFalse;
  std::vector<float> buffer(30 * 24000 *
                            2);  // 30fps * 24000samples * 2 channels = 1 second
  ASSERT_EQ(mrsResult::kAudioResamplingNotSupported,
            mrsAudioTrackReadBufferRead(
                read_buffer2, kImprobableSampleRate, 1,
                mrsAudioTrackReadBufferPadBehavior::kPadWithZero, buffer.data(),
                (int)buffer.size(), &num_samples_read, &has_overrun));

  // Give the track some time to stream audio data, and during this time use the
  // read buffer to read incoming data (and exercise the resampler).
  size_t total_samples_read = 0;
  const auto start_time = std::chrono::system_clock::now();
  const auto end_time = start_time + std::chrono::seconds(3);
  auto cur_time = start_time;
  while (cur_time < end_time) {
    // Read some data
    mrsAudioTrackReadBufferRead(
        read_buffer2, 24000, 2,
        mrsAudioTrackReadBufferPadBehavior::kPadWithZero, buffer.data(),
        (int)buffer.size(), &num_samples_read, &has_overrun);
    ASSERT_EQ(mrsBool::kFalse, has_overrun);
    total_samples_read += num_samples_read;

    // Check data
    // TODO - See comment in audio_track_tests.cpp, this is flaky because it
    //        relies on the microphone and some noise gate. This would be best
    //        tested if we had external audio tracks...

    cur_time = std::chrono::system_clock::now();
  }
  ASSERT_GT(total_samples_read, 0);

  // Same as above
  ASSERT_NE(mrsBool::kFalse, mrsLocalAudioTrackIsEnabled(audio_track1));

  ASSERT_TRUE(pair.WaitExchangeCompletedFor(5s));

  // Clean-up
  mrsAudioTrackReadBufferDestroy(read_buffer2);
  mrsRefCountedObjectRemoveRef(audio_track1);
  mrsRefCountedObjectRemoveRef(audio_source1);
}

#endif  // MRSW_EXCLUDE_DEVICE_TESTS
