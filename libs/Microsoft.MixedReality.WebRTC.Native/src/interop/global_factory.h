// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

#pragma once

#include "export.h"
#include "peer_connection.h"
#include "utils.h"

namespace Microsoft::MixedReality::WebRTC {

/// The global factory is a helper class used to initialize and shutdown the
/// internal WebRTC library, which adds extra functionalities over a classical
/// init/shutdown pair of functions:
/// - Automatically initialize the library when requesting a pointer to the
/// singleton instance with |InstancePtr()|. This is multithread-safe.
/// - Keep track of "tracked objects" (derived from |TrackedObject|) registered
/// with the global factory (mainly wrapper objects), and automatically shutdown
/// the library when no object is alive anymore. This is critical to ensure
/// WebRTC threads are terminated, to allow the library to unload e.g. in the
/// Unity editor or other processes dynamically (re)loading the library.
/// - Ensure that intertwined calls to |InstancePtr()| and |RemoveRef()| (from
/// un-registering a tracked object being destroyed) are multithread-safe.
class GlobalFactory {
 public:
  /// Report live objects to debug output, and return the number of live objects
  /// at the time of the call. If the library is not initialized, this function
  /// returns 0. This is multithread-safe.
  static uint32_t StaticReportLiveObjects() noexcept;

  /// Get the library shutdown options. This function does not initialize the
  /// library, but will store the options for a future initializing. Conversely,
  /// if the library is already initialized then the options are set
  /// immediately. This is multithread-safe.
  static mrsShutdownOptions GetShutdownOptions() noexcept;

  /// Set the library shutdown options. This function does not initialize the
  /// library, but will store the options for a future initializing. Conversely,
  /// if the library is already initialized then the options are set
  /// immediately. This is multithread-safe.
  static void SetShutdownOptions(mrsShutdownOptions options) noexcept;

  /// Force-shutdown the library if it is initialized, or does nothing
  /// otherwise. This call will terminate the WebRTC threads, therefore will
  /// prevent any dispatched call to a WebRTC object from completing. However,
  /// by shutting down the threads it will allow unloading the current module
  /// (DLL), so is recommended to call manually at the end of the process when
  /// WebRTC objects are not in use anymore but before static deinitializing.
  /// This is multithread-safe.
  static void ForceShutdown() noexcept;

  /// Attempt to shutdown the library if no tracked object is alive anymore.
  /// This is always conservative and safe, and will do nothing if any tracked
  /// object is still alive. The function returns |true| if the library is shut
  /// down after the call, either because it was already or because the call did
  /// shut it down. This is multithread-safe.
  static bool TryShutdown() noexcept;

  /// Try to get a pointer to the (initialized) global factory singleton
  /// instance. If the library is not initialized, this returns a NULL pointer.
  /// This is multithread-safe.
  static RefPtr<GlobalFactory> InstancePtrIfExist() {
    return GetInstancePtrImpl(/* ensureInitialized = */ false);
  }

  /// Get a pointer to the global factory singleton instance. If the library is
  /// not initialized, this call initializes it prior to returning a pointer to
  /// it. This is multithread-safe.
  static RefPtr<GlobalFactory> InstancePtr() {
    return GetInstancePtrImpl(/* ensureInitialized = */ true);
  }

  /// Add a reference to the library, preventing it from being shutdown. This is
  /// multithread-safe, and is generally called automatically by |RefPtr<>|.
  void AddRef() const noexcept {
    // Calling the member function |AddRef()| implies already holding a
    // reference, expect for |GetLockImpl()| which will acquire the init mutex
    // so cannot run concurrently with |RemoveRef()|.
    RTC_DCHECK(peer_factory_);
    ref_count_.fetch_add(1, std::memory_order_relaxed);
  }

  /// Remove a reference to the library acquired with |AddRef()|. If this was
  /// the last reference, attempt to shutdown the library. This is
  /// multithread-safe, and is generally called automatically by |RefPtr<>|.
  void RemoveRef() const noexcept {
    // Update the reference count under the init lock to ensure it cannot change
    // from another thread, since invoking |AddRef()| requires having already a
    // pointer to the GlobalFactory (so this reference would never be the last
    // one) or calling |GetLockImpl()| to get a new pointer, which will only
    // call |AddRef()| under the lock too so will block.
    std::scoped_lock lock(init_mutex_);
    RTC_DCHECK(peer_factory_);
    // Usually this is memory_order_acq_rel, but here the |init_mutex_| forces
    // the necessary memory barrier, so only the atomicity is relevant.
    if (ref_count_.fetch_sub(1, std::memory_order_relaxed) == 1) {
      const_cast<GlobalFactory*>(this)->ShutdownImplNoLock(
          ShutdownAction::kTryShutdownIfSafe);
    }
  }

  /// Get the existing peer connection factory, or NULL if the library is not
  /// initialized.
  rtc::scoped_refptr<webrtc::PeerConnectionFactoryInterface>
  GetPeerConnectionFactory() noexcept;

  /// Get the WebRTC background worker thread, or NULL if the library is not
  /// initialized.
  rtc::Thread* GetWorkerThread() const noexcept;

  /// Add to the global factory collection a tracked object whose lifetime is
  /// monitored (via the library reference count) to know when it is safe to
  /// shutdown the library and terminate the WebRTC threads. This is generally
  /// called form a wrapper object's constructor for safety.
  void AddObject(TrackedObject* obj) noexcept;

  /// Remove an object added with |AddObject|. This is generally called from a
  /// wrapper object's destructor for safety.
  void RemoveObject(TrackedObject* obj) noexcept;

  /// Report live objects to WebRTC logging system for debugging.
  /// This is automatically called if the |mrsShutdownOptions::kLogLiveObjects|
  /// shutdown option is set, but can also be called manually at any time.
  /// Return the number of live objects at the time of the call, which can be
  /// outdated as soon as the call returns if other threads add/remove objects.
  uint32_t ReportLiveObjects();

#if defined(WINUWP)
  using WebRtcFactoryPtr =
      std::shared_ptr<wrapper::impl::org::webRtc::WebRtcFactory>;
  WebRtcFactoryPtr get();
  mrsResult GetOrCreateWebRtcFactory(WebRtcFactoryPtr& factory);
#endif  // defined(WINUWP)

 private:
  friend struct std::default_delete<GlobalFactory>;

  /// Get the raw singleton instance, initialized or not.
  static GlobalFactory* GetInstance();

  /// Get the singleton instance, and optionally initializes it, or return NULL
  /// if not initialized.
  static RefPtr<GlobalFactory> GetInstancePtrImpl(bool ensureInitialized);

  GlobalFactory() = default;
  ~GlobalFactory();

  GlobalFactory(const GlobalFactory&) = delete;
  GlobalFactory& operator=(const GlobalFactory&) = delete;

  mrsResult InitializeImplNoLock();

  enum class ShutdownAction {
    /// Try to safely shutdown, only if no tracked object is alive.
    kTryShutdownIfSafe,
    /// Force shutdown even if some tracked objects are still alive.
    kForceShutdown,
    /// Shutting down from ~GlobalFactory(), same as kForceShutdown but display
    /// additional error message if the library was still initialized when the
    /// destructor was called, which generally indicates some serious error.
    kFromObjectDestructor
  };

  /// Shutdown the library. Return |true| if the library is shut down after the
  /// call, either because it was already shut down or because this call shut it
  /// down.
  bool ShutdownImplNoLock(ShutdownAction shutdown_action);

  void ReportLiveObjectsNoLock();

 private:
  /// Mutex for multithread-safe factory initializing and shutdown.
  mutable std::mutex init_mutex_;

  /// Global peer connection factory. This is initialized only while the library
  /// is initialized, and is immutable between init and shutdown, so do not
  /// require |mutex_| for access, but |init_mutex_| instead. This acts as a
  /// marker of whether the library is initialized or not.
  rtc::scoped_refptr<webrtc::PeerConnectionFactoryInterface> peer_factory_
      RTC_GUARDED_BY(init_mutex_);

#if defined(WINUWP)

  /// UWP factory for WinRT wrapper layer. This is initialized only while the
  /// library is initialized, and is immutable between init and shutdown, so do
  /// not require |mutex_| for access.
  WebRtcFactoryPtr impl_ RTC_GUARDED_BY(init_mutex_);

#else  // defined(WINUWP)

  /// WebRTC networking thread. This is initialized only while the library
  /// is initialized, and is immutable between init and shutdown, so do not
  /// require |mutex_| for access, but |init_mutex_| instead.
  std::unique_ptr<rtc::Thread> network_thread_ RTC_GUARDED_BY(init_mutex_);

  /// WebRTC background worker thread. This is initialized only while the
  /// library is initialized, and is immutable between init and shutdown, so do
  /// not require |mutex_| for access, but |init_mutex_| instead.
  std::unique_ptr<rtc::Thread> worker_thread_ RTC_GUARDED_BY(init_mutex_);

  /// WebRTC signaling thread. This is initialized only while the library
  /// is initialized, and is immutable between init and shutdown, so do not
  /// require |mutex_| for access, but |init_mutex_| instead.
  std::unique_ptr<rtc::Thread> signaling_thread_ RTC_GUARDED_BY(init_mutex_);

#endif  // defined(WINUWP)

  /// Reference count to the library, for automated shutdown.
  mutable std::atomic_uint32_t ref_count_{0};

  /// Recursive mutex for thread-safety of calls to this instance.
  /// This is used to protect members not related with init/shutdown, while the
  /// caller holds a reference to the library (|ref_count_| > 0).
  mutable std::recursive_mutex mutex_;

  /// Shutdown options.
  mrsShutdownOptions shutdown_options_ RTC_GUARDED_BY(mutex_) =
      mrsShutdownOptions::kDefault;

  /// Collection of all tracked objects alive. This is solely used to display a
  /// debugging report with |ReportLiveObjects()|.
  std::vector<TrackedObject*> alive_objects_ RTC_GUARDED_BY(mutex_);
};

}  // namespace Microsoft::MixedReality::WebRTC
