// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

#pragma once

#include "audio_frame_observer.h"
#include "export.h"
#include "interop_api.h"

extern "C" {

/// Register a custom callback to be called when the local audio track received
/// a frame.
///
/// WebRTC audio tracks produce an audio frame every 10 ms.
/// If you want the audio frames to be buffered (and optionally resampled)
/// automatically, and you want the application to control when new audio data
/// is read, create an AudioTrackReadBuffer using this function. If you want to
/// process the audio frames as soon as they are received, without conversions,
/// use |mrsRemoteAudioTrackRegisterFrameCallback| instead.
MRS_API void MRS_CALL
mrsRemoteAudioTrackRegisterFrameCallback(mrsRemoteAudioTrackHandle trackHandle,
                                         mrsAudioFrameCallback callback,
                                         void* user_data) noexcept;

/// Enable or disable a remote audio track. Enabled tracks output their media
/// content as usual. Disabled tracks output some void media content (silent
/// audio frames). Enabling/disabling a track is a lightweight concept similar
/// to "mute", which does not require an SDP renegotiation.
MRS_API mrsResult MRS_CALL
mrsRemoteAudioTrackSetEnabled(mrsRemoteAudioTrackHandle track_handle,
                              mrsBool enabled) noexcept;

/// Query a local audio track for its enabled status.
MRS_API mrsBool MRS_CALL
mrsRemoteAudioTrackIsEnabled(mrsRemoteAudioTrackHandle track_handle) noexcept;

/// Output the audio track to the WebRTC audio device.
///
/// The default behavior is for every remote audio frame to be passed to
/// remote audio frame callbacks, as well as output automatically to the
/// audio device used by WebRTC. If |false| is passed to this function, remote
/// audio frames will still be received and passed to callbacks, but won't be
/// output to the audio device.
///
/// NOTE: Changing the default behavior is not supported on UWP.
MRS_API void MRS_CALL
mrsRemoteAudioTrackOutputToDevice(mrsRemoteAudioTrackHandle track_handle,
                                  bool output) noexcept;

/// Returns whether the track is output directly to the system audio device.
MRS_API mrsBool MRS_CALL mrsRemoteAudioTrackIsOutputToDevice(
    mrsRemoteAudioTrackHandle track_handle) noexcept;

/// High level interface for consuming WebRTC audio tracks.
///
/// Enqueues audio frames for a remote audio track in an internal buffer as they
/// arrive. Users should call |mrsAudioTrackReadBufferRead| to read samples from the buffer when needed.
/// See also |mrsRemoteAudioTrackCreateReadBuffer|.
using mrsAudioTrackReadBufferHandle = void*;

/// Controls the padding behavior of |mrsAudioTrackReadBufferRead| on underrun.
enum class mrsAudioTrackReadBufferPadBehavior : int32_t {

  /// Do not pad the samples array.
  kDoNotPad = 0,

  /// Pad with zeros (silence).
  kPadWithZero = 1,

  /// Pad with a sine function.
  /// Generates audible artifacts on underrun. Use for debugging.
  kPadWithSine = 2,

  kCount
};


/// Starts buffering the audio data from the remote track in an
/// AudioTrackReadBuffer.
///
/// WebRTC audio tracks produce an audio frame every 10 ms.
/// If you want the audio frames to be buffered (and optionally resampled)
/// automatically, and you want the application to control when new audio data
/// is read, create an AudioTrackReadBuffer using this function. If you want to
/// process the audio frames as soon as they are received, without conversions,
/// use |mrsRemoteAudioTrackRegisterFrameCallback instead|.
MRS_API mrsResult MRS_CALL
mrsRemoteAudioTrackCreateReadBuffer(mrsRemoteAudioTrackHandle track_handle,
                                    mrsAudioTrackReadBufferHandle* bufferOut);


/// Fill |data| with samples from the internal buffer.
///
/// This method reads the internal buffer starting from the oldest data.
/// If the internal buffer is exhausted (underrun), |data|
/// is padded according to the value of |padBehavior|.
///
/// This method should be called regularly to consume the audio data as it is
/// received. Note that the internal buffer can overrun (and some frames can be
/// dropped) if this is not called frequently enough.
///
/// |sample_rate|: Desired sample rate. Data in the buffer is resampled if this
/// is different from the native track rate.
///
/// |num_channels|: Desired number of channels. Should be 1 or 2. Data in the
/// buffer is split/averaged if this is different from the native track channels
/// number.
///
/// |pad_behavior|: Controls how |data| is padded in case of underrun.
///
/// |samples_out|: Must point to an array of at least |num_samples_max|
/// elements. Will be filled with the samples read from the internal buffer.
/// The function will try to fill the entire length of the array.
///
/// |num_samples_max|: Capacity of |samples_out|.
///
/// |num_samples_read_out|: Must not be null. Set to the effective number
/// of samples read. This will be generally equal to |num_samples_max|,
/// but can be less in case of underrun.
///
/// |has_overrun_out|: Must not be null. Set to true if frames have been dropped
/// from the internal buffer between the previous call to
/// |mrsAudioTrackReadBufferRead| and this.
MRS_API mrsResult MRS_CALL
mrsAudioTrackReadBufferRead(mrsAudioTrackReadBufferHandle buffer,
                            int sample_rate,
                            int num_channels,
                            mrsAudioTrackReadBufferPadBehavior pad_behavior,
                            float* samples_out,
                            int num_samples_max,
                            int* num_samples_read_out,
                            mrsBool* has_overrun_out);

/// Release the buffer.
MRS_API void MRS_CALL
mrsAudioTrackReadBufferDestroy(mrsAudioTrackReadBufferHandle buffer);

}  // extern "C"
