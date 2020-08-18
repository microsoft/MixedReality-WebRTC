// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

// This is a precompiled header, it must be on its own, followed by a blank
// line, to prevent clang-format from reordering it with other headers.
#include "pch.h"

#include "api/stats/rtcstats_objects.h"

#if defined(WINUWP)
// Stop WinRT from polluting the global namespace
// https://developercommunity.visualstudio.com/content/problem/859178/asyncinfoh-defines-the-error-symbol-at-global-name.html
// This is defined in pch.h but for some reason it must be here to work.
#define _HIDE_GLOBAL_ASYNC_STATUS 1
#include "third_party/winuwp_h264/H264Encoder/H264Encoder.h"
#endif

#include "audio_track_source_interop.h"
#include "data_channel.h"
#include "data_channel_interop.h"
#include "external_video_track_source_interop.h"
#include "interop/global_factory.h"
#include "interop_api.h"
#include "local_audio_track_interop.h"
#include "local_video_track_interop.h"
#include "media/audio_track_source.h"
#include "media/device_video_track_source.h"
#include "media/external_video_track_source.h"
#include "media/local_audio_track.h"
#include "media/local_video_track.h"
#include "peer_connection.h"
#include "peer_connection_interop.h"
#include "sdp_utils.h"
#include "utils.h"

using namespace Microsoft::MixedReality::WebRTC;

struct mrsEnumerator {
  virtual ~mrsEnumerator() = default;
  virtual void dispose() = 0;
};

namespace {

mrsResult RTCToAPIError(const webrtc::RTCError& error) {
  if (error.ok()) {
    return Result::kSuccess;
  }
  switch (error.type()) {
    case webrtc::RTCErrorType::INVALID_PARAMETER:
    case webrtc::RTCErrorType::INVALID_RANGE:
      return Result::kInvalidParameter;
    case webrtc::RTCErrorType::INVALID_STATE:
      return Result::kInvalidOperation;
    case webrtc::RTCErrorType::INTERNAL_ERROR:
    default:
      return Result::kUnknownError;
  }
}

#if defined(WINUWP)
using WebRtcFactoryPtr =
    std::shared_ptr<wrapper::impl::org::webRtc::WebRtcFactory>;
#endif  // defined(WINUWP)

}  // namespace

inline rtc::Thread* GetWorkerThread() {
  return GlobalFactory::InstancePtr()->GetWorkerThread();
}

uint32_t MRS_CALL mrsReportLiveObjects() noexcept {
  return GlobalFactory::StaticReportLiveObjects();
}

mrsResult MRS_CALL
mrsLibraryUseAudioDeviceModule(mrsAudioDeviceModule adm) noexcept {
  return GlobalFactory::UseAudioDeviceModule(adm);
}

mrsAudioDeviceModule MRS_CALL mrsLibraryGetAudioDeviceModule() noexcept {
  return GlobalFactory::GetAudioDeviceModule();
}

mrsShutdownOptions MRS_CALL mrsGetShutdownOptions() noexcept {
  return GlobalFactory::GetShutdownOptions();
}

void MRS_CALL mrsSetShutdownOptions(mrsShutdownOptions options) noexcept {
  GlobalFactory::SetShutdownOptions(options);
}

void MRS_CALL mrsForceShutdown() noexcept {
  GlobalFactory::ForceShutdown();
}

void MRS_CALL mrsCloseEnum(mrsEnumHandle* handleRef) noexcept {
  if (handleRef) {
    if (auto& handle = *handleRef) {
      handle->dispose();
      delete handle;
      handle = nullptr;
    }
  }
}

mrsResult MRS_CALL mrsEnumVideoCaptureDevicesAsync(
    mrsVideoCaptureDeviceEnumCallback enumCallback,
    void* enumCallbackUserData,
    mrsVideoCaptureDeviceEnumCompletedCallback completedCallback,
    void* completedCallbackUserData) noexcept {
  if (!enumCallback) {
    return Result::kInvalidParameter;
  }
  RTC_LOG(LS_INFO) << "Enumerating video capture devices";
  Error res = DeviceVideoTrackSource::GetVideoCaptureDevices(
      {enumCallback, enumCallbackUserData},
      {completedCallback, completedCallbackUserData});
  if (!res.ok()) {
    RTC_LOG(LS_ERROR) << "Failed to enumerate video capture devices: "
                      << res.message();
    return res.result();
  }
  return Result::kSuccess;
}

mrsResult MRS_CALL mrsEnumVideoProfilesAsync(
    const char* device_id,
    mrsVideoProfileKind profile_kind,
    mrsVideoProfileEnumCallback enumCallback,
    void* enumCallbackUserData,
    mrsVideoProfileEnumCompletedCallback completedCallback,
    void* completedCallbackUserData) noexcept {
  if (IsStringNullOrEmpty(device_id)) {
    return Result::kInvalidParameter;
  }
  if (!enumCallback) {
    return Result::kInvalidParameter;
  }
  RTC_LOG(LS_INFO) << "Enumerating video profiles for device '" << device_id
                   << "'";
  Error res = DeviceVideoTrackSource::GetVideoProfiles(
      device_id, profile_kind, {enumCallback, enumCallbackUserData},
      {completedCallback, completedCallbackUserData});
  if (!res.ok()) {
    RTC_LOG(LS_ERROR) << "Failed to enumerate video profiles for device '"
                      << device_id << "': " << res.message();
    return res.result();
  }
  return Result::kSuccess;
}

mrsResult MRS_CALL mrsEnumVideoCaptureFormatsAsync(
    const char* device_id,
    const char* profile_id,
    mrsVideoProfileKind profile_kind,
    mrsVideoCaptureFormatEnumCallback enumCallback,
    void* enumCallbackUserData,
    mrsVideoCaptureFormatEnumCompletedCallback completedCallback,
    void* completedCallbackUserData) noexcept {
  if (IsStringNullOrEmpty(device_id)) {
    return Result::kInvalidParameter;
  }
  if (!enumCallback) {
    return Result::kInvalidParameter;
  }
  RTC_LOG(LS_INFO) << "Enumerating video capture formats for device '"
                   << device_id << "'";
  Error res = DeviceVideoTrackSource::GetVideoCaptureFormats(
      device_id, profile_id, profile_kind, {enumCallback, enumCallbackUserData},
      {completedCallback, completedCallbackUserData});
  if (!res.ok()) {
    RTC_LOG(LS_ERROR) << "Failed to enumerate video capture formats: "
                      << res.message();
    return res.result();
  }
  return Result::kSuccess;
}

mrsResult MRS_CALL
mrsPeerConnectionCreate(const mrsPeerConnectionConfiguration* config,
                        mrsPeerConnectionHandle* peer_handle_out) noexcept {
  if (!peer_handle_out) {
    return Result::kInvalidParameter;
  }
  *peer_handle_out = nullptr;

  // Create the new peer connection
  auto result = PeerConnection::create(*config);
  if (!result.ok()) {
    return result.error().result();
  }
  *peer_handle_out = (mrsPeerConnectionHandle)result.value().release();
  return Result::kSuccess;
}

void MRS_CALL mrsPeerConnectionRegisterConnectedCallback(
    mrsPeerConnectionHandle peer_handle,
    mrsPeerConnectionConnectedCallback callback,
    void* user_data) noexcept {
  if (auto peer = static_cast<PeerConnection*>(peer_handle)) {
    peer->RegisterConnectedCallback(Callback<>{callback, user_data});
  }
}

void MRS_CALL mrsPeerConnectionRegisterLocalSdpReadytoSendCallback(
    mrsPeerConnectionHandle peer_handle,
    mrsPeerConnectionLocalSdpReadytoSendCallback callback,
    void* user_data) noexcept {
  if (auto peer = static_cast<PeerConnection*>(peer_handle)) {
    peer->RegisterLocalSdpReadytoSendCallback(
        Callback<mrsSdpMessageType, const char*>{callback, user_data});
  }
}

void MRS_CALL mrsPeerConnectionRegisterIceCandidateReadytoSendCallback(
    mrsPeerConnectionHandle peer_handle,
    mrsPeerConnectionIceCandidateReadytoSendCallback callback,
    void* user_data) noexcept {
  if (auto peer = static_cast<PeerConnection*>(peer_handle)) {
    peer->RegisterIceCandidateReadytoSendCallback(
        Callback<const mrsIceCandidate*>{callback, user_data});
  }
}

void MRS_CALL mrsPeerConnectionRegisterIceStateChangedCallback(
    mrsPeerConnectionHandle peer_handle,
    mrsPeerConnectionIceStateChangedCallback callback,
    void* user_data) noexcept {
  if (auto peer = static_cast<PeerConnection*>(peer_handle)) {
    peer->RegisterIceStateChangedCallback(
        Callback<mrsIceConnectionState>{callback, user_data});
  }
}

void MRS_CALL mrsPeerConnectionRegisterRenegotiationNeededCallback(
    mrsPeerConnectionHandle peer_handle,
    mrsPeerConnectionRenegotiationNeededCallback callback,
    void* user_data) noexcept {
  if (auto peer = static_cast<PeerConnection*>(peer_handle)) {
    peer->RegisterRenegotiationNeededCallback(Callback<>{callback, user_data});
  }
}

void MRS_CALL mrsPeerConnectionRegisterAudioTrackAddedCallback(
    mrsPeerConnectionHandle peer_handle,
    mrsPeerConnectionAudioTrackAddedCallback callback,
    void* user_data) noexcept {
  if (auto peer = static_cast<PeerConnection*>(peer_handle)) {
    peer->RegisterAudioTrackAddedCallback(
        Callback<const mrsRemoteAudioTrackAddedInfo*>{callback, user_data});
  }
}

void MRS_CALL mrsPeerConnectionRegisterAudioTrackRemovedCallback(
    mrsPeerConnectionHandle peer_handle,
    mrsPeerConnectionAudioTrackRemovedCallback callback,
    void* user_data) noexcept {
  if (auto peer = static_cast<PeerConnection*>(peer_handle)) {
    peer->RegisterAudioTrackRemovedCallback(
        Callback<mrsRemoteAudioTrackHandle, mrsTransceiverHandle>{callback,
                                                                  user_data});
  }
}

void MRS_CALL mrsPeerConnectionRegisterVideoTrackAddedCallback(
    mrsPeerConnectionHandle peer_handle,
    mrsPeerConnectionVideoTrackAddedCallback callback,
    void* user_data) noexcept {
  if (auto peer = static_cast<PeerConnection*>(peer_handle)) {
    peer->RegisterVideoTrackAddedCallback(
        Callback<const mrsRemoteVideoTrackAddedInfo*>{callback, user_data});
  }
}

void MRS_CALL mrsPeerConnectionRegisterVideoTrackRemovedCallback(
    mrsPeerConnectionHandle peer_handle,
    mrsPeerConnectionVideoTrackRemovedCallback callback,
    void* user_data) noexcept {
  if (auto peer = static_cast<PeerConnection*>(peer_handle)) {
    peer->RegisterVideoTrackRemovedCallback(
        Callback<mrsRemoteVideoTrackHandle, mrsTransceiverHandle>{callback,
                                                                  user_data});
  }
}

void MRS_CALL mrsPeerConnectionRegisterDataChannelAddedCallback(
    mrsPeerConnectionHandle peer_handle,
    mrsPeerConnectionDataChannelAddedCallback callback,
    void* user_data) noexcept {
  if (auto peer = static_cast<PeerConnection*>(peer_handle)) {
    peer->RegisterDataChannelAddedCallback(
        Callback<const mrsDataChannelAddedInfo*>{callback, user_data});
  }
}

void MRS_CALL mrsPeerConnectionRegisterDataChannelRemovedCallback(
    mrsPeerConnectionHandle peer_handle,
    mrsPeerConnectionDataChannelRemovedCallback callback,
    void* user_data) noexcept {
  if (auto peer = static_cast<PeerConnection*>(peer_handle)) {
    peer->RegisterDataChannelRemovedCallback(
        Callback<mrsDataChannelHandle>{callback, user_data});
  }
}

mrsResult MRS_CALL mrsPeerConnectionAddDataChannel(
    mrsPeerConnectionHandle peer_handle,
    const mrsDataChannelConfig* config,
    mrsDataChannelHandle* data_channel_handle_out) noexcept

{
  if (!data_channel_handle_out || !config) {
    return Result::kInvalidParameter;
  }
  *data_channel_handle_out = nullptr;
  auto peer = static_cast<PeerConnection*>(peer_handle);
  if (!peer) {
    return Result::kInvalidNativeHandle;
  }
  const bool ordered = (config->flags & mrsDataChannelConfigFlags::kOrdered);
  const bool reliable = (config->flags & mrsDataChannelConfigFlags::kReliable);
  const absl::string_view label = (config->label ? config->label : "");
  ErrorOr<std::shared_ptr<DataChannel>> data_channel =
      peer->AddDataChannel(config->id, label, ordered, reliable);
  if (data_channel.ok()) {
    *data_channel_handle_out = data_channel.value().operator->();
  }
  return data_channel.error().result();
}

mrsResult MRS_CALL mrsPeerConnectionRemoveDataChannel(
    mrsPeerConnectionHandle peer_handle,
    mrsDataChannelHandle data_channel_handle) noexcept {
  auto peer = static_cast<PeerConnection*>(peer_handle);
  if (!peer) {
    return Result::kInvalidNativeHandle;
  }
  auto data_channel = static_cast<DataChannel*>(data_channel_handle);
  if (!data_channel) {
    return Result::kInvalidNativeHandle;
  }
  peer->RemoveDataChannel(*data_channel);
  return Result::kSuccess;
}

mrsResult MRS_CALL
mrsPeerConnectionAddIceCandidate(mrsPeerConnectionHandle peer_handle,
                                 const mrsIceCandidate* candidate) noexcept {
  if (!candidate || IsStringNullOrEmpty(candidate->sdp_mid) ||
      IsStringNullOrEmpty(candidate->content) ||
      (candidate->sdp_mline_index < 0)) {
    return mrsResult::kInvalidParameter;
  }
  auto const peer = static_cast<PeerConnection*>(peer_handle);
  if (!peer) {
    return Result::kInvalidNativeHandle;
  }
  Error result = peer->AddIceCandidate(*candidate);
  if (!result.ok()) {
    RTC_LOG(LS_ERROR) << result.message();
  }
  return result.result();
}

mrsResult MRS_CALL
mrsPeerConnectionCreateOffer(mrsPeerConnectionHandle peer_handle) noexcept {
  if (auto peer = static_cast<PeerConnection*>(peer_handle)) {
    return (peer->CreateOffer() ? Result::kSuccess : Result::kUnknownError);
  }
  return Result::kInvalidNativeHandle;
}

mrsResult MRS_CALL
mrsPeerConnectionCreateAnswer(mrsPeerConnectionHandle peer_handle) noexcept {
  if (auto peer = static_cast<PeerConnection*>(peer_handle)) {
    return (peer->CreateAnswer() ? Result::kSuccess : Result::kUnknownError);
  }
  return Result::kInvalidNativeHandle;
}

mrsResult MRS_CALL
mrsPeerConnectionSetBitrate(mrsPeerConnectionHandle peer_handle,
                            int min_bitrate_bps,
                            int start_bitrate_bps,
                            int max_bitrate_bps) noexcept {
  if (auto peer = static_cast<PeerConnection*>(peer_handle)) {
    BitrateSettings settings{};
    if (min_bitrate_bps >= 0) {
      settings.min_bitrate_bps = min_bitrate_bps;
    }
    if (start_bitrate_bps >= 0) {
      settings.start_bitrate_bps = start_bitrate_bps;
    }
    if (max_bitrate_bps >= 0) {
      settings.max_bitrate_bps = max_bitrate_bps;
    }
    return peer->SetBitrate(settings);
  }
  return Result::kInvalidNativeHandle;
}

mrsResult MRS_CALL mrsPeerConnectionSetRemoteDescriptionAsync(
    mrsPeerConnectionHandle peer_handle,
    mrsSdpMessageType type,
    const char* sdp,
    mrsRemoteDescriptionAppliedCallback callback,
    void* user_data) noexcept {
  if (IsStringNullOrEmpty(sdp)) {
    return mrsResult::kInvalidParameter;
  }
  auto peer = static_cast<PeerConnection*>(peer_handle);
  if (!peer) {
    return Result::kInvalidNativeHandle;
  }
  Error result = peer->SetRemoteDescriptionAsync(
      type, sdp,
      PeerConnection::RemoteDescriptionAppliedCallback{callback, user_data});
  if (!result.ok()) {
    RTC_LOG(LS_ERROR) << result.message();
  }
  return result.result();
}

mrsResult MRS_CALL
mrsPeerConnectionClose(mrsPeerConnectionHandle peer_handle) noexcept {
  if (auto peer = static_cast<PeerConnection*>(peer_handle)) {
    peer->Close();
    return Result::kSuccess;
  }
  return Result::kInvalidNativeHandle;
}

mrsResult MRS_CALL mrsSdpForceCodecs(const char* message,
                                     SdpFilter audio_filter,
                                     SdpFilter video_filter,
                                     char* buffer,
                                     uint64_t* buffer_size) noexcept {
  RTC_CHECK(message);
  RTC_CHECK(buffer);
  RTC_CHECK(buffer_size);
  std::string message_str(message);
  std::string audio_codec_name_str;
  std::string video_codec_name_str;
  std::map<std::string, std::string> extra_audio_params;
  std::map<std::string, std::string> extra_video_params;
  if (audio_filter.codec_name) {
    audio_codec_name_str.assign(audio_filter.codec_name);
  }
  if (video_filter.codec_name) {
    video_codec_name_str.assign(video_filter.codec_name);
  }
  // Only assign extra parameters if codec name is not empty
  if (!audio_codec_name_str.empty() && audio_filter.params) {
    SdpParseCodecParameters(audio_filter.params, extra_audio_params);
  }
  if (!video_codec_name_str.empty() && video_filter.params) {
    SdpParseCodecParameters(video_filter.params, extra_video_params);
  }
  std::string out_message =
      SdpForceCodecs(message_str, audio_codec_name_str, extra_audio_params,
                     video_codec_name_str, extra_video_params);
  const size_t capacity = static_cast<size_t>(*buffer_size);
  const size_t size = out_message.size();
  *buffer_size = size + 1;
  if (capacity < size + 1) {
    return Result::kInvalidParameter;
  }
  memcpy(buffer, out_message.c_str(), size);
  buffer[size] = '\0';
  return Result::kSuccess;
}

mrsBool MRS_CALL mrsSdpIsValidToken(const char* token) noexcept {
  return ((token != nullptr) && SdpIsValidToken(token) ? mrsBool::kTrue
                                                       : mrsBool::kFalse);
}

void MRS_CALL mrsSetFrameHeightRoundMode(FrameHeightRoundMode value) {
  PeerConnection::SetFrameHeightRoundMode(value);
}

void MRS_CALL mrsMemCpy(void* dst, const void* src, uint64_t size) noexcept {
  memcpy(dst, src, static_cast<size_t>(size));
}

void MRS_CALL mrsMemCpyStride(void* dst,
                              int32_t dst_stride,
                              const void* src,
                              int32_t src_stride,
                              int32_t elem_size,
                              int32_t elem_count) noexcept {
  RTC_CHECK(dst);
  RTC_CHECK(dst_stride >= elem_size);
  RTC_CHECK(src);
  RTC_CHECK(src_stride >= elem_size);
  if ((dst_stride == elem_size) && (src_stride == elem_size)) {
    // If tightly packed, do a single memcpy() for performance
    const size_t total_size = (size_t)elem_size * elem_count;
    memcpy(dst, src, total_size);
  } else {
    // Otherwise, copy row by row
    for (int i = 0; i < elem_count; ++i) {
      memcpy(dst, src, elem_size);
      dst = (char*)dst + dst_stride;
      src = (const char*)src + src_stride;
    }
  }
}

namespace {
template <class T>
T& FindOrInsert(std::vector<std::pair<std::string, T>>& vec,
                absl::string_view id) {
  auto it = std::find_if(vec.begin(), vec.end(),
                         [&](auto&& pair) { return pair.first == id; });
  if (it != vec.end()) {
    return it->second;
  }
  vec.emplace_back(id, T{});
  return vec.back().second;
}

}  // namespace

mrsResult MRS_CALL mrsPeerConnectionGetSimpleStats(
    mrsPeerConnectionHandle peer_handle,
    mrsPeerConnectionGetSimpleStatsCallback callback,
    void* user_data) {
  if (auto peer = static_cast<PeerConnection*>(peer_handle)) {
    struct Collector : webrtc::RTCStatsCollectorCallback {
      Collector(mrsPeerConnectionGetSimpleStatsCallback callback,
                void* user_data)
          : callback_(callback), user_data_(user_data) {}

      mrsPeerConnectionGetSimpleStatsCallback callback_;
      void* user_data_;
      void OnStatsDelivered(
          const rtc::scoped_refptr<const webrtc::RTCStatsReport>& report)
          override {
        // Return a wrapper for the RTCStatsReport.
        // mrsStatsReportRemoveRef removes the reference.
        report->AddRef();
        (*callback_)(user_data_, report.get());
      }
    };
    rtc::scoped_refptr<Collector> collector =
        new rtc::RefCountedObject<Collector>(callback, user_data);

    peer->GetStats(collector);
    return Result::kSuccess;
  }
  return Result::kInvalidNativeHandle;
}

namespace {
template <class T>
void GetCommonValues(T& lhs, const webrtc::RTCOutboundRTPStreamStats& rhs) {
  lhs.rtp_stats_timestamp_us = rhs.timestamp_us();
  lhs.packets_sent = *rhs.packets_sent;
  lhs.bytes_sent = *rhs.bytes_sent;
}
template <class T>
void GetCommonValues(T& lhs, const webrtc::RTCInboundRTPStreamStats& rhs) {
  lhs.rtp_stats_timestamp_us = rhs.timestamp_us();
  lhs.packets_received = *rhs.packets_received;
  lhs.bytes_received = *rhs.bytes_received;
}

template <class T>
T GetValueIfDefined(const webrtc::RTCStatsMember<T>& member) {
  return member.is_defined() ? *member : 0;
}

}  // namespace

mrsResult MRS_CALL
mrsStatsReportGetObjects(mrsStatsReportHandle report_handle,
                         const char* stats_type,
                         mrsStatsReportGetObjectCallback callback,
                         void* user_data) {
  if (!report_handle) {
    return Result::kInvalidNativeHandle;
  }
  auto report = static_cast<const webrtc::RTCStatsReport*>(report_handle);

  if (!strcmp(stats_type, "DataChannelStats")) {
    for (auto&& stats : *report) {
      if (!strcmp(stats.type(), "data-channel")) {
        const auto& dc_stats = stats.cast_to<webrtc::RTCDataChannelStats>();
        mrsDataChannelStats simple_stats{
            dc_stats.timestamp_us(),     *dc_stats.datachannelid,
            *dc_stats.messages_sent,     *dc_stats.bytes_sent,
            *dc_stats.messages_received, *dc_stats.bytes_received};
        (*callback)(user_data, &simple_stats);
      }
    }
  } else if (!strcmp(stats_type, "AudioSenderStats")) {
    std::vector<std::pair<std::string, mrsAudioSenderStats>> pending_stats;
    // Get values from both RTCOutboundRTPStreamStats and
    // RTCMediaStreamTrackStats objects. Match them together by track ID.
    for (auto&& stats : *report) {
      if (!strcmp(stats.type(), "outbound-rtp")) {
        const auto& ortp_stats =
            stats.cast_to<webrtc::RTCOutboundRTPStreamStats>();
        if (*ortp_stats.kind == "audio" &&
            // Removing a track will leave a "trackless" RTP stream. Ignore it.
            ortp_stats.track_id.is_defined()) {
          auto& dest_stats = FindOrInsert(pending_stats, *ortp_stats.track_id);
          GetCommonValues(dest_stats, ortp_stats);
        }
      } else if (!strcmp(stats.type(), "track")) {
        const auto& track_stats =
            stats.cast_to<webrtc::RTCMediaStreamTrackStats>();
        if (*track_stats.kind == "audio") {
          if (!(*track_stats.remote_source)) {
            auto& dest_stats = FindOrInsert(pending_stats, track_stats.id());
            dest_stats.track_stats_timestamp_us = track_stats.timestamp_us();
            dest_stats.track_identifier = track_stats.track_identifier->c_str();
            dest_stats.audio_level = GetValueIfDefined(track_stats.audio_level);
            dest_stats.total_audio_energy = *track_stats.total_audio_energy;
            dest_stats.total_samples_duration =
                *track_stats.total_samples_duration;
          }
        }
      }
    }
    for (auto&& stats : pending_stats) {
      (*callback)(user_data, &stats.second);
    }
  } else if (!strcmp(stats_type, "AudioReceiverStats")) {
    std::vector<std::pair<std::string, mrsAudioReceiverStats>> pending_stats;
    // Get values from both RTCInboundRTPStreamStats and
    // RTCMediaStreamTrackStats objects. Match them together by track ID.
    for (auto&& stats : *report) {
      if (!strcmp(stats.type(), "inbound-rtp")) {
        const auto& irtp_stats =
            stats.cast_to<webrtc::RTCInboundRTPStreamStats>();
        if (*irtp_stats.kind == "audio") {
          auto& dest_stats = FindOrInsert(pending_stats, *irtp_stats.track_id);
          GetCommonValues(dest_stats, irtp_stats);
        }
      } else if (!strcmp(stats.type(), "track")) {
        const auto& track_stats =
            stats.cast_to<webrtc::RTCMediaStreamTrackStats>();
        if (*track_stats.kind == "audio") {
          if (*track_stats.remote_source) {
            auto& dest_stats = FindOrInsert(pending_stats, track_stats.id());
            dest_stats.track_stats_timestamp_us = track_stats.timestamp_us();
            dest_stats.track_identifier = track_stats.track_identifier->c_str();
            // This seems to be undefined in some not well specified cases.
            dest_stats.audio_level = GetValueIfDefined(track_stats.audio_level);
            dest_stats.total_audio_energy = *track_stats.total_audio_energy;
            dest_stats.total_samples_received =
                GetValueIfDefined(track_stats.total_samples_received);
            dest_stats.total_samples_duration =
                *track_stats.total_samples_duration;
          }
        }
      }
    }
    for (auto&& stats : pending_stats) {
      (*callback)(user_data, &stats.second);
    }
  } else if (!strcmp(stats_type, "VideoSenderStats")) {
    std::vector<std::pair<std::string, mrsVideoSenderStats>> pending_stats;
    // Get values from both RTCOutboundRTPStreamStats and
    // RTCMediaStreamTrackStats objects. Match them together by track ID.
    for (auto&& stats : *report) {
      if (!strcmp(stats.type(), "outbound-rtp")) {
        const auto& ortp_stats =
            stats.cast_to<webrtc::RTCOutboundRTPStreamStats>();
        if (*ortp_stats.kind == "video" &&
            // Removing a track will leave a "trackless" RTP stream. Ignore it.
            ortp_stats.track_id.is_defined()) {
          auto& dest_stats = FindOrInsert(pending_stats, *ortp_stats.track_id);
          GetCommonValues(dest_stats, ortp_stats);
          dest_stats.frames_encoded = *ortp_stats.frames_encoded;
        }
      } else if (!strcmp(stats.type(), "track")) {
        const auto& track_stats =
            stats.cast_to<webrtc::RTCMediaStreamTrackStats>();
        if (*track_stats.kind == "video") {
          if (!(*track_stats.remote_source)) {
            auto& dest_stats = FindOrInsert(pending_stats, track_stats.id());
            dest_stats.track_stats_timestamp_us = track_stats.timestamp_us();
            dest_stats.track_identifier = track_stats.track_identifier->c_str();
            dest_stats.frames_sent = GetValueIfDefined(track_stats.frames_sent);
            dest_stats.huge_frames_sent =
                GetValueIfDefined(track_stats.huge_frames_sent);
          }
        }
      }
    }
    for (auto&& stats : pending_stats) {
      (*callback)(user_data, &stats.second);
    }
  } else if (!strcmp(stats_type, "VideoReceiverStats")) {
    std::vector<std::pair<std::string, mrsVideoReceiverStats>> pending_stats;
    // Get values from both RTCInboundRTPStreamStats and
    // RTCMediaStreamTrackStats objects. Match them together by track ID.
    for (auto&& stats : *report) {
      if (!strcmp(stats.type(), "inbound-rtp")) {
        const auto& irtp_stats =
            stats.cast_to<webrtc::RTCInboundRTPStreamStats>();
        if (*irtp_stats.kind == "video") {
          auto& dest_stats = FindOrInsert(pending_stats, *irtp_stats.track_id);
          GetCommonValues(dest_stats, irtp_stats);
          dest_stats.frames_decoded = *irtp_stats.frames_decoded;
        }
      } else if (!strcmp(stats.type(), "track")) {
        const auto& track_stats =
            stats.cast_to<webrtc::RTCMediaStreamTrackStats>();
        if (*track_stats.kind == "video") {
          if (*track_stats.remote_source) {
            auto& dest_stats = FindOrInsert(pending_stats, track_stats.id());
            dest_stats.track_stats_timestamp_us = track_stats.timestamp_us();
            dest_stats.track_identifier = track_stats.track_identifier->c_str();
            dest_stats.frames_received =
                GetValueIfDefined(track_stats.frames_received);
            dest_stats.frames_dropped =
                GetValueIfDefined(track_stats.frames_dropped);
          }
        }
      }
    }
    for (auto&& stats : pending_stats) {
      (*callback)(user_data, &stats.second);
    }
  } else if (!strcmp(stats_type, "TransportStats")) {
    for (auto&& stats : *report) {
      if (!strcmp(stats.type(), "transport")) {
        const auto& dc_stats = stats.cast_to<webrtc::RTCTransportStats>();
        mrsTransportStats simple_stats{dc_stats.timestamp_us(),
                                       *dc_stats.bytes_sent,
                                       *dc_stats.bytes_received};
        (*callback)(user_data, &simple_stats);
      }
    }
  }
  return Result::kSuccess;
}

mrsResult MRS_CALL mrsStatsReportRemoveRef(mrsStatsReportHandle stats_report) {
  if (auto rep = static_cast<const webrtc::RTCStatsReport*>(stats_report)) {
    rep->Release();
    return Result::kSuccess;
  }
  return Result::kInvalidNativeHandle;
}

mrsResult MRS_CALL mrsSetH264Config(const mrsH264Config* config) {
#if defined(WINUWP)
#define CHECK_ENUM_VALUE(NAME)                                        \
  static_assert((int)webrtc::H264::NAME == (int)mrsH264Profile::NAME, \
                "webrtc::H264::Profile does not match mrsH264Profile")
  CHECK_ENUM_VALUE(kProfileConstrainedBaseline);
  CHECK_ENUM_VALUE(kProfileBaseline);
  CHECK_ENUM_VALUE(kProfileMain);
  CHECK_ENUM_VALUE(kProfileConstrainedHigh);
  CHECK_ENUM_VALUE(kProfileHigh);
#undef CHECK_ENUM_VALUE

#define CHECK_ENUM_VALUE(NAME)                                           \
  static_assert((int)mrsH264RcMode::k##NAME ==                           \
                    (int)webrtc::WinUWPH264EncoderImpl::RcMode::k##NAME, \
                "WinUWPH264EncoderImpl::RcMode does not match mrsH264RcMode")
  CHECK_ENUM_VALUE(Unset);
  CHECK_ENUM_VALUE(CBR);
  CHECK_ENUM_VALUE(VBR);
  CHECK_ENUM_VALUE(Quality);
#undef CHECK_ENUM_VALUE

  webrtc::WinUWPH264EncoderImpl::global_profile.store(
      (webrtc::H264::Profile)config->profile);
  webrtc::WinUWPH264EncoderImpl::global_rc_mode.store(
      (webrtc::WinUWPH264EncoderImpl::RcMode)config->rc_mode);
  webrtc::WinUWPH264EncoderImpl::global_max_qp.store(config->max_qp);
  webrtc::WinUWPH264EncoderImpl::global_quality.store(config->quality);
  return mrsResult::kSuccess;
#else   // defined(WINUWP)
  (void)config;
  RTC_LOG(LS_ERROR) << "Setting H.264 configuration is not supported on "
                       "non-UWP platforms";
  return mrsResult::kUnsupported;
#endif  // defined(WINUWP)
}
