// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

#pragma once

#include "export.h"
#include "peer_connection.h"

namespace Microsoft::MixedReality::WebRTC {

enum class ObjectType : int {
  kPeerConnection,
  kLocalVideoTrack,
  kExternalVideoTrackSource,
};

/// Global factory wrapper adding thread safety to all global objects, including
/// the peer connection factory, and on UWP the so-called "WebRTC factory".
class GlobalFactory {
 public:
  ~GlobalFactory();

  /// Global factory of all global objects, including the peer connection
  /// factory itself, with added thread safety.
  static const std::unique_ptr<GlobalFactory>& Instance();

  /// Get or create the peer connection factory.
  rtc::scoped_refptr<webrtc::PeerConnectionFactoryInterface> GetOrCreate();

  /// Get or create the peer connection factory.
  mrsResult GetOrCreate(
      rtc::scoped_refptr<webrtc::PeerConnectionFactoryInterface>& factory);

  /// Get the existing peer connection factory, or NULL if not created.
  rtc::scoped_refptr<webrtc::PeerConnectionFactoryInterface>
  GetExisting() noexcept;

  /// Get the worker thread. This is only valid if initialized.
  rtc::Thread* GetWorkerThread() noexcept;

  /// Add to the global factory collection an object whose lifetime must be
  /// tracked to know when it is safe to terminate the WebRTC threads. This is
  /// generally called form the object's constructor for safety.
  void AddObject(ObjectType type, TrackedObject* obj) noexcept;

  /// Remove an object added with |AddObject|. This is generally called from the
  /// object's destructor for safety.
  void RemoveObject(ObjectType type, TrackedObject* obj) noexcept;

#if defined(WINUWP)
  using WebRtcFactoryPtr =
      std::shared_ptr<wrapper::impl::org::webRtc::WebRtcFactory>;
  WebRtcFactoryPtr get();
  mrsResult GetOrCreateWebRtcFactory(WebRtcFactoryPtr& factory);
#endif  // defined(WINUWP)

 private:
  mrsResult Initialize();
  void ShutdownNoLock();

 private:
  rtc::scoped_refptr<webrtc::PeerConnectionFactoryInterface> factory_
      RTC_GUARDED_BY(mutex_);
#if defined(WINUWP)
  WebRtcFactoryPtr impl_ RTC_GUARDED_BY(mutex_);
#else   // defined(WINUWP)
  std::unique_ptr<rtc::Thread> network_thread_ RTC_GUARDED_BY(mutex_);
  std::unique_ptr<rtc::Thread> worker_thread_ RTC_GUARDED_BY(mutex_);
  std::unique_ptr<rtc::Thread> signaling_thread_ RTC_GUARDED_BY(mutex_);
#endif  // defined(WINUWP)
  std::recursive_mutex mutex_;

  /// Collection of all objects alive.
  std::unordered_map<TrackedObject*, ObjectType> alive_objects_
      RTC_GUARDED_BY(mutex_);
};

}  // namespace Microsoft::MixedReality::WebRTC
