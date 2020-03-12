// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text;
using System.Text.RegularExpressions;
using Microsoft.MixedReality.WebRTC.Interop;
using NUnit.Framework;
using NUnit.Framework.Internal;

namespace Microsoft.MixedReality.WebRTC.Tests
{
    [TestFixture]
    internal class SdpForceCodecs
    {
        #region Constants

        const string kSdpFullString =
            "v=0\r\n" +
            "o=- 18446744069414584320 18446462598732840960 IN IP4 127.0.0.1\r\n" +
            "s=-\r\n" +
            "t=0 0\r\n" +
            "a=msid-semantic: WMS local_stream_1\r\n" +
            "m=audio 2345 RTP/SAVPF 111 103 104\r\n" +
            "c=IN IP4 74.125.127.126\r\n" +
            "a=rtcp:2347 IN IP4 74.125.127.126\r\n" +
            "a=candidate:a0+B/1 1 udp 2130706432 192.168.1.5 1234 typ host " +
            "generation 2\r\n" +
            "a=candidate:a0+B/1 2 udp 2130706432 192.168.1.5 1235 typ host " +
            "generation 2\r\n" +
            "a=candidate:a0+B/2 1 udp 2130706432 ::1 1238 typ host " +
            "generation 2\r\n" +
            "a=candidate:a0+B/2 2 udp 2130706432 ::1 1239 typ host " +
            "generation 2\r\n" +
            "a=candidate:a0+B/3 1 udp 2130706432 74.125.127.126 2345 typ srflx " +
            "raddr 192.168.1.5 rport 2346 " +
            "generation 2\r\n" +
            "a=candidate:a0+B/3 2 udp 2130706432 74.125.127.126 2347 typ srflx " +
            "raddr 192.168.1.5 rport 2348 " +
            "generation 2\r\n" +
            "a=ice-ufrag:ufrag_voice\r\na=ice-pwd:pwd_voice\r\n" +
            "a=mid:audio_content_name\r\n" +
            "a=sendrecv\r\n" +
            "a=rtcp-mux\r\n" +
            "a=rtcp-rsize\r\n" +
            "a=crypto:1 AES_CM_128_HMAC_SHA1_32 " +
            "inline:NzB4d1BINUAvLEw6UzF3WSJ+PSdFcGdUJShpX1Zj|2^20|1:32 " +
            "dummy_session_params\r\n" +
            "a=rtpmap:111 opus/48000/2\r\n" +
            "a=rtpmap:103 ISAC/16000\r\n" +
            "a=rtpmap:104 ISAC/32000\r\n" +
            "a=ssrc:1 cname:stream_1_cname\r\n" +
            "a=ssrc:1 msid:local_stream_1 audio_track_id_1\r\n" +
            "a=ssrc:1 mslabel:local_stream_1\r\n" +
            "a=ssrc:1 label:audio_track_id_1\r\n" +
            "m=video 3457 RTP/SAVPF 120\r\n" +
            "c=IN IP4 74.125.224.39\r\n" +
            "a=rtcp:3456 IN IP4 74.125.224.39\r\n" +
            "a=candidate:a0+B/1 2 udp 2130706432 192.168.1.5 1236 typ host " +
            "generation 2\r\n" +
            "a=candidate:a0+B/1 1 udp 2130706432 192.168.1.5 1237 typ host " +
            "generation 2\r\n" +
            "a=candidate:a0+B/2 2 udp 2130706432 ::1 1240 typ host " +
            "generation 2\r\n" +
            "a=candidate:a0+B/2 1 udp 2130706432 ::1 1241 typ host " +
            "generation 2\r\n" +
            "a=candidate:a0+B/4 2 udp 2130706432 74.125.224.39 3456 typ relay " +
            "generation 2\r\n" +
            "a=candidate:a0+B/4 1 udp 2130706432 74.125.224.39 3457 typ relay " +
            "generation 2\r\n" +
            "a=ice-ufrag:ufrag_video\r\na=ice-pwd:pwd_video\r\n" +
            "a=mid:video_content_name\r\n" +
            "a=sendrecv\r\n" +
            "a=crypto:1 AES_CM_128_HMAC_SHA1_80 " +
            "inline:d0RmdmcmVCspeEc3QGZiNWpVLFJhQX1cfHAwJSoj|2^20|1:32\r\n" +
            "a=rtpmap:120 VP8/90000\r\n" +
            "a=ssrc-group:FEC 2 3\r\n" +
            "a=ssrc:2 cname:stream_1_cname\r\n" +
            "a=ssrc:2 msid:local_stream_1 video_track_id_1\r\n" +
            "a=ssrc:2 mslabel:local_stream_1\r\n" +
            "a=ssrc:2 label:video_track_id_1\r\n" +
            "a=ssrc:3 cname:stream_1_cname\r\n" +
            "a=ssrc:3 msid:local_stream_1 video_track_id_1\r\n" +
            "a=ssrc:3 mslabel:local_stream_1\r\n" +
            "a=ssrc:3 label:video_track_id_1\r\n";

        #endregion

        [Test]
        public void NoOp()
        {
            ulong len = (ulong)kSdpFullString.Length + 1; // exact size + null terminator
            StringBuilder buffer = new StringBuilder((int)len);
            var audioFilter = new Utils.SdpFilter();
            var videoFilter = new Utils.SdpFilter();
            uint res = Utils.SdpForceCodecs(kSdpFullString, audioFilter, videoFilter, buffer, ref len);
            Assert.AreEqual(Utils.MRS_SUCCESS, res);
            Assert.AreEqual(kSdpFullString, buffer.ToString());
        }

        [Test]
        public void ShortBuffer()
        {
            ulong len = (ulong)kSdpFullString.Length; // missing space for null terminator
            StringBuilder buffer = new StringBuilder((int)len);
            var audioFilter = new Utils.SdpFilter();
            var videoFilter = new Utils.SdpFilter();
            uint res = Utils.SdpForceCodecs(kSdpFullString, audioFilter, videoFilter, buffer, ref len);
            Assert.AreEqual(Utils.MRS_E_INVALID_PARAMETER, res);
        }

        [Test]
        public void HalfChange()
        {
            ulong len = (ulong)kSdpFullString.Length + 1; // at least as big as input (+ null terminator)
            StringBuilder buffer = new StringBuilder((int)len);
            var audioFilter = new Utils.SdpFilter
            {
                CodecName = "opus"
            };
            var videoFilter = new Utils.SdpFilter
            {
                CodecName = "a non-existing codec name causing no change"
            };
            uint res = Utils.SdpForceCodecs(kSdpFullString, audioFilter, videoFilter, buffer, ref len);
            Assert.AreEqual(res, Utils.MRS_SUCCESS);
            string output = buffer.ToString();

            // Audio codec should be "opus" alone
            {
                var regex = new Regex("m=audio [^ ]+ [^ ]+ ([^\\r\\n]+)\\r?\\n");
                var audioMedia = regex.Match(output);
                Assert.NotNull(audioMedia);
                Assert.AreEqual(2, audioMedia.Groups.Count);        // [0] is always the entire match
                Assert.AreEqual("111", audioMedia.Groups[1].Value); // [1] is the first capture
            }

            // Video codec should be left unchanged
            {
                var regex = new Regex("m=video [^ ]+ [^ ]+ ([^\\r\\n]+)\\r?\\n");
                var videoMedia = regex.Match(output);
                Assert.NotNull(videoMedia);
                Assert.AreEqual(2, videoMedia.Groups.Count);        // [0] is always the entire match
                Assert.AreEqual("120", videoMedia.Groups[1].Value); // [1] is the first capture
            }
        }
    }
}
