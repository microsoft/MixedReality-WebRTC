// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

#pragma once

#include "export.h"
#include "peer_connection.h"

namespace Microsoft::MixedReality::WebRTC {

/// Enumeration of all object types that the global factory keeps track of for
/// the purpose of keeping itself alive. Each value correspond to a type of
/// wrapper object. Wrapper objects must call |GlobalFactory::AddObject()| and
/// |GlobalFactory::RemoveObject()| to register themselves with the global
/// factory while alive.
enum class ObjectType : int {
  kPeerConnection,
  kLocalVideoTrack,
  kExternalVideoTrackSource,
};

/// Global factory wrapper adding thread safety to all global objects, including
/// the peer connection factory, and on UWP the so-called "WebRTC factory".
///
/// The global factory is kept alive by the various wrapper objects registered
/// with |AddObject()|, and is automatically destroyed when the last of them is
/// removed with |RemoveObject()|. Note that those calls are automatically made,
/// generally by the constructor and destructor of the wrappers.
///
/// Because multiple threads can access that global factory in parallel, the
/// actual destruction of the singleton instance may need to be delayed past the
/// last |RemoveObject()| call until all threads finished using the instance.
/// For that, the following usage pattern must be followed:
///   - Get a RefPtr<> to the singleton instance using |InstancePtr()|. This may
///   initialize a new instance. This also locks the factory to keep it alive
///   until the RefPtr<> goes out of scope..
///   - Use the factory, for example to get the peer connection factory or to
///   add/remove some wrapper objects. Whether or not any operation fails, the
///   factory cannot be destroyed due to the temporary lock held by the RefPtr<>
///   acquired above, even if no alive object is present.
///   - When the RefPtr<> goes out of scope, it releases its lock to the
///   factory. At that point, |ShutdownImpl()| is called to check if there is
///   any object still alive, and to try shutting down the factory if not.
///
/// The delayed call to |ShutdownImpl()| from releasing the lock when
/// RefPtr<GlobalFactory> goes out of scope, instead of calling it from
/// |RemoveObject()| when the last object is removed, is here to ensure that
/// multiple threads accessing the global factory singleton instance do not
/// destroy it before the last of them finished using that instance, as there is
/// no way for the thread to get the singleton with |InstancePtr()| and perform
/// all operations needed in an atomic fashion.
///
/// Note that, after any wrapper is added with |AddObject()|, the factory will
/// be kept alive by the wrapper being registered, so it is not ever necessary
/// to store a RefPtr<> to the factory (in fact, this might cause circular
/// references). As a rule, users should never store a RefPtr<> to the global
/// factory, but must use a local variable instead to temporarily block
/// destruction during a single API call.
///
/// Example:
///   RefPtr<GlobalFactory> global_factory = GlobalFactory::InstancePtr();
///   RefPtr<Wrapper> dummy = new Wrapper(); // calls GlobalFactory::AddObject()
///
class GlobalFactory {
 public:
  /// Global factory of all global objects, including the peer connection
  /// factory itself, with added thread safety.
  /// This automatically create a new instance if none exists.
  static RefPtr<GlobalFactory> InstancePtr();

  /// Force-shutdown the library. This destroys the current singleton instance.
  /// See also |mrsShutdownOptions::kIgnoreLiveObjectsAndForceShutdown|.
  static void ForceShutdown() noexcept;

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

  /// Temporarily prevent destruction of this instance. Do not call directly,
  /// use RefPtr<> and |InstancePtr()| instead.
  void AddRef() const noexcept {
    temp_ref_count_.fetch_add(1, std::memory_order_relaxed);
  }

  /// Release the temporary reference acquired with |AddRef()|. Do not call
  /// directly, use RefPtr<> and |InstancePtr()| instead.
  void RemoveRef() const noexcept {
    if (temp_ref_count_.fetch_sub(1, std::memory_order_acq_rel) == 1) {
      const_cast<GlobalFactory*>(this)->ShutdownImpl(/* force = */ false);
    }
  }

 private:
  GlobalFactory(const GlobalFactory&) = delete;
  GlobalFactory& operator=(const GlobalFactory&) = delete;
  static std::unique_ptr<GlobalFactory>& MutableInstance(
      bool createIfNotExist = true);
  mrsResult InitializeImplNoLock();
  bool ShutdownImpl(bool force = false);
  void ReportLiveObjectsNoLock();

 private:
  /// WebRTC peer connection factory used to create most WebRTC objects.
  rtc::scoped_refptr<webrtc::PeerConnectionFactoryInterface> peer_factory_
      RTC_GUARDED_BY(mutex_);

#if defined(WINUWP)

  /// UWP-specific factory.
  WebRtcFactoryPtr impl_ RTC_GUARDED_BY(mutex_);

#else  // defined(WINUWP)

  /// WebRTC networking thread.
  std::unique_ptr<rtc::Thread> network_thread_ RTC_GUARDED_BY(mutex_);

  /// WebRTC background worker thread for intensive operations, generally audio
  /// or video processing.
  std::unique_ptr<rtc::Thread> worker_thread_ RTC_GUARDED_BY(mutex_);

  /// WebRTC signaling thread used to serialize all calls to the internal WebRTC
  /// library, and from which most callbacks are invoked.
  std::unique_ptr<rtc::Thread> signaling_thread_ RTC_GUARDED_BY(mutex_);

#endif  // defined(WINUWP)

  std::recursive_mutex mutex_;

  /// Collection of all objects alive. Any object present in this collection
  /// prevents the current singleton instance from being destroyed.
  std::unordered_map<TrackedObject*, ObjectType> alive_objects_
      RTC_GUARDED_BY(mutex_);

  mrsShutdownOptions shutdown_options_;

  /// Reference count for RAII-style shutdown. This is not used as a true
  /// reference count, but instead as a lock to temporarily prevent the
  /// destruction of this instance while trying to create some objects, and as a
  /// notification mechanism when said reference is released to check for
  /// shutdown. Therefore most of the time the reference count is zero, yet the
  /// instance stays alive if |alive_objects_| if not empty.
  mutable std::atomic_uint32_t temp_ref_count_{0};
};

}  // namespace Microsoft::MixedReality::WebRTC
