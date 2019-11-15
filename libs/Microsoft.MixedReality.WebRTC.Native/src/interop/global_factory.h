// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

#pragma once

#include "export.h"
#include "peer_connection.h"

namespace Microsoft::MixedReality::WebRTC {

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

  /// Add a peer connection to the global map of the factory.
  PeerConnectionHandle AddPeerConnection(
      rtc::scoped_refptr<PeerConnection> peer);

  /// Remove a peer connection from the global map of the factory, releasing the
  /// factory's reference to it. This may or may not destroy the peer
  /// connection. If the peer connection was the last one, the factory shuts
  /// itself down.
  void RemovePeerConnection(PeerConnectionHandle* handle);

  /// Notify the factory that a peer connection was destroyed after being
  /// removed from the factory map, so that the factory can check again if this
  /// is the last one and if it needs to shut itself down.
  void NotifyPeerConnectionDestroyed();

  /// Add to the global factory collection an object that is standalone, that is
  /// can live outside of a peer connection's lifetime, and therefore whose
  /// lifetime must be tracked to know when it is safe to terminate the WebRTC
  /// threads. This is generally called form the object's constructor for
  /// safety.
  void AddObject(void* ptr);

  /// Remove an object added with |AddObject|. This is generally called from the
  /// object's destructor for safety.
  void RemoveObject(void* ptr);

#if defined(WINUWP)
  using WebRtcFactoryPtr =
      std::shared_ptr<wrapper::impl::org::webRtc::WebRtcFactory>;
  WebRtcFactoryPtr get();
  mrsResult GetOrCreateWebRtcFactory(WebRtcFactoryPtr& factory);
#endif  // defined(WINUWP)

 private:
  mrsResult Initialize();
  void CheckForShutdown();
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

  /// Collection of all peer connection objects alive.
  std::unordered_map<
      PeerConnectionHandle,
      rtc::scoped_refptr<Microsoft::MixedReality::WebRTC::PeerConnection>>
      peer_connection_map_ RTC_GUARDED_BY(mutex_);

  /// Collection of all objects alive.
  std::unordered_set<void*> alive_objects_ RTC_GUARDED_BY(mutex_);
};

}  // namespace Microsoft::MixedReality::WebRTC
