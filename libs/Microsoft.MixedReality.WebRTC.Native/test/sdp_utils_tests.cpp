// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

#include "pch.h"

#include "interop_api.h"

// Copied from webrtc\pc\webrtcsdp_unittest.cc
static const char kSdpFullString[] =
    "v=0\r\n"
    "o=- 18446744069414584320 18446462598732840960 IN IP4 127.0.0.1\r\n"
    "s=-\r\n"
    "t=0 0\r\n"
    "a=msid-semantic: WMS local_stream_1\r\n"
    "m=audio 2345 RTP/SAVPF 111 103 104\r\n"
    "c=IN IP4 74.125.127.126\r\n"
    "a=rtcp:2347 IN IP4 74.125.127.126\r\n"
    "a=candidate:a0+B/1 1 udp 2130706432 192.168.1.5 1234 typ host "
    "generation 2\r\n"
    "a=candidate:a0+B/1 2 udp 2130706432 192.168.1.5 1235 typ host "
    "generation 2\r\n"
    "a=candidate:a0+B/2 1 udp 2130706432 ::1 1238 typ host "
    "generation 2\r\n"
    "a=candidate:a0+B/2 2 udp 2130706432 ::1 1239 typ host "
    "generation 2\r\n"
    "a=candidate:a0+B/3 1 udp 2130706432 74.125.127.126 2345 typ srflx "
    "raddr 192.168.1.5 rport 2346 "
    "generation 2\r\n"
    "a=candidate:a0+B/3 2 udp 2130706432 74.125.127.126 2347 typ srflx "
    "raddr 192.168.1.5 rport 2348 "
    "generation 2\r\n"
    "a=ice-ufrag:ufrag_voice\r\na=ice-pwd:pwd_voice\r\n"
    "a=mid:audio_content_name\r\n"
    "a=sendrecv\r\n"
    "a=rtcp-mux\r\n"
    "a=rtcp-rsize\r\n"
    "a=crypto:1 AES_CM_128_HMAC_SHA1_32 "
    "inline:NzB4d1BINUAvLEw6UzF3WSJ+PSdFcGdUJShpX1Zj|2^20|1:32 "
    "dummy_session_params\r\n"
    "a=rtpmap:111 opus/48000/2\r\n"
    "a=rtpmap:103 ISAC/16000\r\n"
    "a=rtpmap:104 ISAC/32000\r\n"
    "a=ssrc:1 cname:stream_1_cname\r\n"
    "a=ssrc:1 msid:local_stream_1 audio_track_id_1\r\n"
    "a=ssrc:1 mslabel:local_stream_1\r\n"
    "a=ssrc:1 label:audio_track_id_1\r\n"
    "m=video 3457 RTP/SAVPF 120\r\n"
    "c=IN IP4 74.125.224.39\r\n"
    "a=rtcp:3456 IN IP4 74.125.224.39\r\n"
    "a=candidate:a0+B/1 2 udp 2130706432 192.168.1.5 1236 typ host "
    "generation 2\r\n"
    "a=candidate:a0+B/1 1 udp 2130706432 192.168.1.5 1237 typ host "
    "generation 2\r\n"
    "a=candidate:a0+B/2 2 udp 2130706432 ::1 1240 typ host "
    "generation 2\r\n"
    "a=candidate:a0+B/2 1 udp 2130706432 ::1 1241 typ host "
    "generation 2\r\n"
    "a=candidate:a0+B/4 2 udp 2130706432 74.125.224.39 3456 typ relay "
    "generation 2\r\n"
    "a=candidate:a0+B/4 1 udp 2130706432 74.125.224.39 3457 typ relay "
    "generation 2\r\n"
    "a=ice-ufrag:ufrag_video\r\na=ice-pwd:pwd_video\r\n"
    "a=mid:video_content_name\r\n"
    "a=sendrecv\r\n"
    "a=crypto:1 AES_CM_128_HMAC_SHA1_80 "
    "inline:d0RmdmcmVCspeEc3QGZiNWpVLFJhQX1cfHAwJSoj|2^20|1:32\r\n"
    "a=rtpmap:120 VP8/90000\r\n"
    "a=ssrc-group:FEC 2 3\r\n"
    "a=ssrc:2 cname:stream_1_cname\r\n"
    "a=ssrc:2 msid:local_stream_1 video_track_id_1\r\n"
    "a=ssrc:2 mslabel:local_stream_1\r\n"
    "a=ssrc:2 label:video_track_id_1\r\n"
    "a=ssrc:3 cname:stream_1_cname\r\n"
    "a=ssrc:3 msid:local_stream_1 video_track_id_1\r\n"
    "a=ssrc:3 mslabel:local_stream_1\r\n"
    "a=ssrc:3 label:video_track_id_1\r\n";

// Same as kSdpFullString, after forcing the audio codec to "opus"
// This removes all "a=rtmap" audio codecs except #111 "a=rtpmap:111
// opus/48000/2", and change the "m=audio" line to list only codec #111.
static const char kSdpForcedAudioOpus[] =
    "v=0\r\n"
    "o=- 18446744069414584320 18446462598732840960 IN IP4 127.0.0.1\r\n"
    "s=-\r\n"
    "t=0 0\r\n"
    "a=msid-semantic: WMS local_stream_1\r\n"
    "m=audio 2345 RTP/SAVPF 111\r\n"
    "c=IN IP4 74.125.127.126\r\n"
    "a=rtcp:2347 IN IP4 74.125.127.126\r\n"
    "a=candidate:a0+B/1 1 udp 2130706432 192.168.1.5 1234 typ host "
    "generation 2\r\n"
    "a=candidate:a0+B/1 2 udp 2130706432 192.168.1.5 1235 typ host "
    "generation 2\r\n"
    "a=candidate:a0+B/2 1 udp 2130706432 ::1 1238 typ host "
    "generation 2\r\n"
    "a=candidate:a0+B/2 2 udp 2130706432 ::1 1239 typ host "
    "generation 2\r\n"
    "a=candidate:a0+B/3 1 udp 2130706432 74.125.127.126 2345 typ srflx "
    "raddr 192.168.1.5 rport 2346 "
    "generation 2\r\n"
    "a=candidate:a0+B/3 2 udp 2130706432 74.125.127.126 2347 typ srflx "
    "raddr 192.168.1.5 rport 2348 "
    "generation 2\r\n"
    "a=ice-ufrag:ufrag_voice\r\na=ice-pwd:pwd_voice\r\n"
    "a=mid:audio_content_name\r\n"
    "a=sendrecv\r\n"
    "a=rtcp-mux\r\n"
    "a=rtcp-rsize\r\n"
    "a=crypto:1 AES_CM_128_HMAC_SHA1_32 "
    "inline:NzB4d1BINUAvLEw6UzF3WSJ+PSdFcGdUJShpX1Zj|2^20|1:32 "
    "dummy_session_params\r\n"
    "a=rtpmap:111 opus/48000/2\r\n"
    "a=ssrc:1 cname:stream_1_cname\r\n"
    "a=ssrc:1 msid:local_stream_1 audio_track_id_1\r\n"
    "a=ssrc:1 mslabel:local_stream_1\r\n"
    "a=ssrc:1 label:audio_track_id_1\r\n"
    "m=video 3457 RTP/SAVPF 120\r\n"
    "c=IN IP4 74.125.224.39\r\n"
    "a=rtcp:3456 IN IP4 74.125.224.39\r\n"
    "a=candidate:a0+B/1 2 udp 2130706432 192.168.1.5 1236 typ host "
    "generation 2\r\n"
    "a=candidate:a0+B/1 1 udp 2130706432 192.168.1.5 1237 typ host "
    "generation 2\r\n"
    "a=candidate:a0+B/2 2 udp 2130706432 ::1 1240 typ host "
    "generation 2\r\n"
    "a=candidate:a0+B/2 1 udp 2130706432 ::1 1241 typ host "
    "generation 2\r\n"
    "a=candidate:a0+B/4 2 udp 2130706432 74.125.224.39 3456 typ relay "
    "generation 2\r\n"
    "a=candidate:a0+B/4 1 udp 2130706432 74.125.224.39 3457 typ relay "
    "generation 2\r\n"
    "a=ice-ufrag:ufrag_video\r\na=ice-pwd:pwd_video\r\n"
    "a=mid:video_content_name\r\n"
    "a=sendrecv\r\n"
    "a=crypto:1 AES_CM_128_HMAC_SHA1_80 "
    "inline:d0RmdmcmVCspeEc3QGZiNWpVLFJhQX1cfHAwJSoj|2^20|1:32\r\n"
    "a=rtpmap:120 VP8/90000\r\n"
    "a=ssrc-group:FEC 2 3\r\n"
    "a=ssrc:2 cname:stream_1_cname\r\n"
    "a=ssrc:2 msid:local_stream_1 video_track_id_1\r\n"
    "a=ssrc:2 mslabel:local_stream_1\r\n"
    "a=ssrc:2 label:video_track_id_1\r\n"
    "a=ssrc:3 cname:stream_1_cname\r\n"
    "a=ssrc:3 msid:local_stream_1 video_track_id_1\r\n"
    "a=ssrc:3 mslabel:local_stream_1\r\n"
    "a=ssrc:3 label:video_track_id_1\r\n";

/// Helper raw buffer deallocated automatically on scope leave.
struct RaiiBuffer {
  RaiiBuffer(size_t size) { _data = new char[size]; }
  ~RaiiBuffer() { delete[] _data; }
  char* _data{};
};

// Check mrsSdpForceCodecs() forces the audio codec, without adding an
// unsupported video codec.
TEST(SdpUtils, ForceCodecs) {
  uint64_t len = sizeof(kSdpFullString) * 2;
  RaiiBuffer buffer((size_t)len);
  ASSERT_NE(nullptr, buffer._data);
  // Force audio to "opus" only. Don't change video because "h264" is not
  // advertized as supported in the input message
  SdpFilter audio_filter{"opus", ""};
  SdpFilter video_filter{"h264", ""};
  ASSERT_EQ(Result::kSuccess,
            mrsSdpForceCodecs(kSdpFullString, audio_filter, video_filter,
                              buffer._data, &len));
  ASSERT_EQ(sizeof(kSdpForcedAudioOpus), len);
  ASSERT_EQ(0, memcmp(kSdpForcedAudioOpus, buffer._data, (size_t)len));
}

// No-op if codecs are not supported
TEST(SdpUtils, ForceCodecsNotSupported) {
  uint64_t len = sizeof(kSdpFullString) * 2;
  RaiiBuffer buffer((size_t)len);
  ASSERT_NE(nullptr, buffer._data);
  SdpFilter audio_filter{"random_non_existing_audio_codec", ""};
  SdpFilter video_filter{"random_non_existing_video_codec", ""};
  ASSERT_EQ(Result::kSuccess,
            mrsSdpForceCodecs(kSdpFullString, audio_filter, video_filter,
                              buffer._data, &len));
  ASSERT_EQ(sizeof(kSdpFullString), len);
  ASSERT_EQ(0, memcmp(kSdpFullString, buffer._data, (size_t)len));
}

// Buffer too small
TEST(SdpUtils, ForceCodecsShortBuffer) {
  uint64_t len = 32;  // too short on purpose
  char buffer[32];
  SdpFilter audio_filter{"opus", ""};
  SdpFilter video_filter{"h264", ""};
  ASSERT_EQ(Result::kInvalidParameter,
            mrsSdpForceCodecs(kSdpFullString, audio_filter, video_filter,
                              buffer, &len));
  ASSERT_EQ(sizeof(kSdpForcedAudioOpus), len);
}

TEST(SdpUtils, IsValidToken) {
  ASSERT_EQ(mrsBool::kFalse, mrsSdpIsValidToken(nullptr));
  ASSERT_EQ(mrsBool::kFalse, mrsSdpIsValidToken(""));
  ASSERT_EQ(mrsBool::kFalse, mrsSdpIsValidToken(" "));
  ASSERT_EQ(mrsBool::kTrue, mrsSdpIsValidToken("a"));
  ASSERT_EQ(mrsBool::kFalse, mrsSdpIsValidToken("a z"));
  for (auto c : std::string_view{"!#$%'*+-.^_`{|}~"}) {
    const char str[2] = {c, '\0'};
    ASSERT_EQ(mrsBool::kTrue, mrsSdpIsValidToken(str));
  }
}
