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

class AudioTrackTests : public TestUtils::TestBase,
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
                        AudioTrackTests,
                        testing::ValuesIn(TestUtils::TestSemantics),
                        TestUtils::SdpSemanticToString);

//
// TODO : Those tests are currently partially disabled because
// - when not muted, the audio track needs some non-zero signal from the
// microphone for the test to pass, which requires someone or something to make
// some noise, and cannot be easily automated at that time.
// - when muted, the audio signal is still non-zero, possibly because of the way
// mute is implemented (no bool, only clears the buffer) and some minor rounding
// errors in subsequent processing, or other... in any case it is not exactly
// zero like for video. (NB: voice activation doesn't seem to have much effect).
//
// Note however that using headphones and microphone, one can clearly hear the
// first test (Simple) having the microphone enabled, and audio played back in
// the earphones speakers, while in the second test (Muted) the audio is clearly
// silent from a perceptual point of view.
//

TEST_P(AudioTrackTests, Simple) {
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

  // Register a callback on the remote track of #2
  uint32_t call_count = 0;
  AudioFrameCallback audio2_cb = [&call_count](const AudioFrame& frame) {
    ASSERT_NE(nullptr, frame.data_);
    ASSERT_LT(0u, frame.bits_per_sample_);
    ASSERT_LT(0u, frame.sampling_rate_hz_);
    ASSERT_LT(0u, frame.channel_count_);
    ASSERT_LT(0u, frame.sample_count_);
    // TODO - See comment above
    // if (bits_per_sample == 8) {
    //  uint8_t min, max;
    //  const bool is_silent =
    //      IsSilent_uint8((const uint8_t*)audio_data,
    //                     number_of_frames * number_of_channels, min, max);
    //  EXPECT_FALSE(is_silent)
    //      << "uint8 call #" << call_count << " min=" << min << " max=" << max;
    //} else if (bits_per_sample == 16) {
    //  int16_t min, max;
    //  const bool is_silent =
    //      IsSilent_int16((const int16_t*)audio_data,
    //                     number_of_frames * number_of_channels, min, max);
    //  EXPECT_FALSE(is_silent)
    //      << "int16 call #" << call_count << " min=" << min << " max=" << max;
    //} else {
    //  ASSERT_TRUE(false) << "Unkown bits per sample (not 8 nor 16)";
    //}
    ++call_count;
  };
  mrsRemoteAudioTrackRegisterFrameCallback(audio_track2, CB(audio2_cb));

  // Check several times this, because the audio "mute" is flaky, does not
  // really mute the audio, so check that the reported status is still
  // correct.
  ASSERT_NE(mrsBool::kFalse, mrsLocalAudioTrackIsEnabled(audio_track1));

  // Give the track some time to stream audio data
  Event ev;
  ev.WaitFor(3s);
  ASSERT_LT(30u, call_count) << "Expected at least 10 CPS";

  // Same as above
  ASSERT_NE(mrsBool::kFalse, mrsLocalAudioTrackIsEnabled(audio_track1));

  ASSERT_TRUE(pair.WaitExchangeCompletedFor(5s));

  // Clean-up
  mrsRemoteAudioTrackRegisterFrameCallback(audio_track2, nullptr, nullptr);
  mrsRefCountedObjectRemoveRef(audio_track1);
  mrsRefCountedObjectRemoveRef(audio_source1);
}

TEST_P(AudioTrackTests, Muted) {
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

  // Disable the audio track; it should output only silence
  ASSERT_EQ(Result::kSuccess,
            mrsLocalAudioTrackSetEnabled(audio_track1, mrsBool::kFalse));
  ASSERT_EQ(mrsBool::kFalse, mrsLocalAudioTrackIsEnabled(audio_track1));

  // Connect #1 and #2
  pair.ConnectAndWait();

  // Wait for remote track to be added on #2
  ASSERT_TRUE(track_added2_ev.WaitFor(5s));
  ASSERT_NE(nullptr, audio_track2);

  // Register a callback on the remote track of #2
  uint32_t call_count = 0;
  AudioFrameCallback audio_cb = [&call_count, &pair](const AudioFrame& frame) {
    ASSERT_NE(nullptr, frame.data_);
    ASSERT_LT(0u, frame.bits_per_sample_);
    ASSERT_LT(0u, frame.sampling_rate_hz_);
    ASSERT_LT(0u, frame.channel_count_);
    ASSERT_LT(0u, frame.sample_count_);
    // TODO - See comment above
    // if (bits_per_sample == 8) {
    //  uint8_t min, max;
    //  const bool is_silent =
    //      IsSilent_uint8((const uint8_t*)audio_data,
    //                     number_of_frames * number_of_channels, min, max);
    //  EXPECT_TRUE(is_silent) << "uint8 call #" << call_count
    //                         << " min=" << min << " max=" << max;
    //} else if (bits_per_sample == 16) {
    //  int16_t min, max;
    //  const bool is_silent =
    //      IsSilent_int16((const int16_t*)audio_data,
    //                     number_of_frames * number_of_channels, min, max);
    //  EXPECT_TRUE(is_silent) << "int16 call #" << call_count
    //                         << " min=" << min << " max=" << max;
    //} else {
    //  ASSERT_TRUE(false) << "Unkown bits per sample (not 8 nor 16)";
    //}
    ++call_count;
  };
  mrsRemoteAudioTrackRegisterFrameCallback(audio_track2, CB(audio_cb));

  // Check several times this, because the audio "mute" is flaky, does not
  // really mute the audio, so check that the reported status is still
  // correct.
  ASSERT_EQ(mrsBool::kFalse, mrsLocalAudioTrackIsEnabled(audio_track1));

  Event ev;
  ev.WaitFor(3s);
  ASSERT_LT(30u, call_count) << "Expected at least 10 CPS";

  // Same as above
  ASSERT_EQ(mrsBool::kFalse, mrsLocalAudioTrackIsEnabled(audio_track1));

  ASSERT_TRUE(pair.WaitExchangeCompletedFor(5s));

  // Clean-up
  mrsRemoteAudioTrackRegisterFrameCallback(audio_track2, nullptr, nullptr);
  mrsRefCountedObjectRemoveRef(audio_track1);
  mrsRefCountedObjectRemoveRef(audio_source1);
}

#endif  // MRSW_EXCLUDE_DEVICE_TESTS
