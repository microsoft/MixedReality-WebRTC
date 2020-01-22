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
/// Usage pattern:
///   - Get a RefPtr<> to the singleton instance using |InstancePtr()|. This may
///   initialize a new instance. This also adds a temporary reference to the
///   factory to keep it alive until the RefPtr<> goes out of scope.
///   - Use the factory, for example to get the peer connection factory or to
///   add/remove some wrapper objects. Whether or not any operation fails, the
///   factory cannot be destroyed due to the temporary reference, even if no
///   alive object is present anymore.
///   - When the RefPtr<> goes out of scope, it releases its temporary reference
///   to the factory. If that reference is the last one, then the factory
///   attempts to shutdown, that is goes to check the alive objects, which
///   constitutes long-term references and will also prevent its destruction.
///
/// The delayed call to |TryShutdown()| from releasing the temporary reference
/// when RefPtr<GlobalFactory> goes out of scope instead of when no object is
/// alive anymore allows the caller to create a wrapper object and fail
/// initializing it, and remove it from the global factory but without the
/// factory destroying itself yet. This works around a problem where the object
/// removing itself triggers the destruction of the global factory singleton
/// instance while another thread already acquired a reference to the global
/// factory but did not yet add any wrapper object so cannot prevent its
/// destruction, and is left with a dangling reference to a destroyed singleton.
/// The temporary reference count allows delaying that destruction for as long
/// as any thread has a reference to the singleton instance. This is not a real
/// reference count because the global factory is a singleton, and to avoid
/// circular references. Users must never store a RefPtr<> to the global
/// factory, but must use a local variable instead to temporarily block
/// destruction during a single API call.
///
/// Usage:
///   RefPtr<GlobalFactory> global_factory = GlobalFactory::InstancePtr();
///   RefPtr<Wrapper> dummy = new Wrapper(); // calls GlobalFactory::AddObject()
///
class GlobalFactory {
 public:
  /// Force-shutdown the library.
  static void ForceShutdown() noexcept;

  /// Global factory of all global objects, including the peer connection
  /// factory itself, with added thread safety.
  static RefPtr<GlobalFactory> InstancePtr();

  /// Attempt to shutdown the global factory if no live object is present
  /// anymore. This is always conservative and safe, and will do nothing if any
  /// object is still live.
  static bool TryShutdown() noexcept;

  GlobalFactory() = default;
  ~GlobalFactory();

  /// Get the existing peer connection factory, or NULL if not created.
  rtc::scoped_refptr<webrtc::PeerConnectionFactoryInterface>
  GetPeerConnectionFactory() noexcept;

  /// Get the worker thread. This is only valid if initialized.
  rtc::Thread* GetWorkerThread() noexcept;

  /// Add to the global factory collection an object whose lifetime must be
  /// tracked to know when it is safe to terminate the WebRTC threads. This is
  /// generally called form the object's constructor for safety.
  void AddObject(ObjectType type, TrackedObject* obj) noexcept;

  /// Remove an object added with |AddObject|. This is generally called from the
  /// object's destructor for safety.
  void RemoveObject(ObjectType type, TrackedObject* obj) noexcept;

  /// Report live objects to WebRTC logging system for debugging.
  /// This is automatically called if the |mrsShutdownOptions::kLogLiveObjects|
  /// shutdown option is set, but can also be called manually at any time.
  /// Return the number of live objects at the time of the call.
  uint32_t ReportLiveObjects();

#if defined(WINUWP)
  using WebRtcFactoryPtr =
      std::shared_ptr<wrapper::impl::org::webRtc::WebRtcFactory>;
  WebRtcFactoryPtr get();
  mrsResult GetOrCreateWebRtcFactory(WebRtcFactoryPtr& factory);
#endif  // defined(WINUWP)

  void AddRef() const noexcept {
    temp_ref_count_.fetch_add(1, std::memory_order_relaxed);
  }

  void RemoveRef() const noexcept {
    if (temp_ref_count_.fetch_sub(1, std::memory_order_acq_rel) == 1) {
      GlobalFactory::TryShutdown();
    }
  }

 private:
  GlobalFactory(const GlobalFactory&) = delete;
  GlobalFactory& operator=(const GlobalFactory&) = delete;
  static std::unique_ptr<GlobalFactory>& MutableInstance(
      bool createIfNotExist = true);
  mrsResult InitializeImplNoLock();
  void ForceShutdownImpl();
  bool TryShutdownImpl();
  void ReportLiveObjectsNoLock();

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

  /// Shutdown options.
  mrsShutdownOptions shutdown_options_;

  /// Reference count for RAII-style shutdown. This is not used as a true
  /// reference count, but instead as a marker of temporary acquiring a
  /// reference to the GlobalFactory while trying to create some objects, and as
  /// a notification mechanism when said reference is released to check for
  /// shutdown. Therefore most of the time the reference count is zero, yet the
  /// instance stays alive.
  mutable std::atomic_uint32_t temp_ref_count_{0};
};

}  // namespace Microsoft::MixedReality::WebRTC
