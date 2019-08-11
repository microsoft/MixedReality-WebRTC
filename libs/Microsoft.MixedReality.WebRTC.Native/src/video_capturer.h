// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license
// information.

#pragma once

#include <mutex>

namespace Microsoft::MixedReality::WebRTC {

/// Helper to open a video capture device by unique identifier and return it
/// wrapped as a VideoCaptureModule object.
rtc::scoped_refptr<webrtc::VideoCaptureModule> OpenVideoCaptureDevice(
    const char* video_device_id,
    bool enable_mrc = false) noexcept;

/// Bridge between a VideoCaptureModule producing some frames from a video
/// capture device, and a VideoTrackSource providing the frame to WebRTC.
class VideoCapturer : public rtc::VideoSourceInterface<webrtc::VideoFrame>,
                      public rtc::VideoSinkInterface<webrtc::VideoFrame> {
 public:
  VideoCapturer(rtc::scoped_refptr<webrtc::VideoCaptureModule> vcm);
  virtual ~VideoCapturer();

  void AddOrUpdateSink(rtc::VideoSinkInterface<webrtc::VideoFrame>* sink,
                       const rtc::VideoSinkWants& wants) override;

  void RemoveSink(rtc::VideoSinkInterface<webrtc::VideoFrame>* sink) override;

  void OnFrame(const webrtc::VideoFrame& frame) override;

 private:
  void Destroy();

  rtc::scoped_refptr<webrtc::VideoCaptureModule> vcm_;
  webrtc::VideoCaptureCapability capability_;
  std::vector<rtc::VideoSinkInterface<webrtc::VideoFrame>*> sinks_;
  std::mutex sinks_mutex_;
};

/// Video track source encapsulating a VideoCapturer video source.
class CapturerTrackSource : public webrtc::VideoTrackSource {
 public:
  static rtc::scoped_refptr<CapturerTrackSource> Create(
      std::unique_ptr<VideoCapturer> capturer) {
    return new rtc::RefCountedObject<CapturerTrackSource>(std::move(capturer));
  }

 protected:
  explicit CapturerTrackSource(std::unique_ptr<VideoCapturer> capturer)
      : VideoTrackSource(/*remote=*/false), capturer_(std::move(capturer)) {}

 private:
  rtc::VideoSourceInterface<webrtc::VideoFrame>* source() override {
    return capturer_.get();
  }
  std::unique_ptr<VideoCapturer> capturer_;
};

}  // namespace Microsoft::MixedReality::WebRTC
