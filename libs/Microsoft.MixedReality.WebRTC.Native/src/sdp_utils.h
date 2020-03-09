// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license
// information.

#pragma once

#include <mutex>

#include "callback.h"

namespace Microsoft::MixedReality::WebRTC {

/// Check if the given SDP token is valid according to the RFC 4566 standard.
/// See https://tools.ietf.org/html/rfc4566#page-43 for details.
/// This is used to validate e.g. track, transceiver, or stream IDs.
bool SdpIsValidToken(std::string_view token) noexcept;

/// Parse a list of semicolon-separated pairs of "key=value" arguments into a
/// map of (key, value) pairs.
void SdpParseCodecParameters(const std::string& param_string,
                             std::map<std::string, std::string>& params);

/// Force audio and video codecs when advertizing capabilities in an SDP offer.
/// This is a workaround for the lack of access to codec selection. Instead of
/// selecting codecs in code, this can be used to intercept a generated SDP
/// offer before it is sent to the remote peer, and modify it by removing the
/// codecs the user does not want.
/// Codec names are compared to the list of supported codecs in the input
/// message string, and if found then other codecs are pruned out. If the codec
/// name is not found, the codec is assumed to be unsupported, so codecs for
/// that type are not modified.
/// |message| SDP message string to deserialize.
/// |audio_codec_name| SDP name of the audio codec to force, if supported.
/// |video_codec_name| SDP name of the video codec to force, if supported.
/// Returns the new SDP offer message string to be sent via the signaler.
std::string SdpForceCodecs(
    const std::string& message,
    const std::string& audio_codec_name,
    const std::map<std::string, std::string>& extra_audio_codec_params,
    const std::string& video_codec_name,
    const std::map<std::string, std::string>& extra_video_codec_params);

/// Decode a marshalled ICE server string.
/// Syntax is:
///   string = blocks
///   blocks = block [ "\n\n" blocks ]
///   block = lines
///   lines = line [ "\n" lines ]
///   line = key ":" value
webrtc::PeerConnectionInterface::IceServers DecodeIceServers(
    const std::string& str);

/// Encode a single URL of a single ICE server into a marshalled ICE server
/// string.
std::string EncodeIceServers(const std::string& url);

/// Encode a single URL of a single ICE server into a marshalled ICE server
/// string, with optional username and password for a TURN server.
std::string EncodeIceServers(const std::string& url,
                             const std::string& username,
                             const std::string& password);

}  // namespace Microsoft::MixedReality::WebRTC
