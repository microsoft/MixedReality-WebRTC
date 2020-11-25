// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

#include <jni.h>

// Export as global (public) symbol
#define MRS_JNIEXPORT __attribute__((visibility("default")))

#include "modules/utility/include/jvm_android.h"
#include "rtc_base/logging.h"
#include "rtc_base/ssladapter.h"
#include "sdk/android/native_api/base/init.h"
#include "sdk/android/src/jni/classreferenceholder.h"
#include "sdk/android/src/jni/jni_helpers.h"
#include "sdk/android/src/jni/jvm.h"

static bool isInialized = false;
static jint jni_version;

/// Auto-magic function called by the Java VM when the library is loaded.
/// This is called on a thread which is already attached to the JVM, so has a
/// valid JNIEnv already.
extern "C" jint MRS_JNIEXPORT JNICALL JNI_OnLoad(JavaVM* jvm, void* reserved) {
  // mrwebrtc-unityplugin also seems to call this method, and it wants back whatever
  // jni_version initially return.
  if (isInialized) return jni_version;

  RTC_LOG(INFO) << "JNI_OnLoad() for MR-WebRTC";

  // This is supposed to be a handy helper which calls InitGlobalJniVariables()
  // + InitClassLoader(), but it doesn't return the value that needs to be
  // returned from JNI_OnLoad(). So call manually below instead.
  // webrtc::InitAndroid(jvm);

  // Manually initalize the global C++ variables with the current JVM and its
  // environment for the current thread (from sdk/android/src/jni/jvm.cc).
  jni_version = webrtc::jni::InitGlobalJniVariables(jvm);
  RTC_DCHECK_GE(jni_version, 0);
  if (jni_version < 0) {
    RTC_LOG(LS_ERROR) << "Failed to initialize JVM during JNI_OnLoad().";
    return -1;
  }

  // Initialize SSL to ensure cryptographic functions can be used for the peer
  // connection.
  RTC_CHECK(rtc::InitializeSSL()) << "Failed to InitializeSSL()";

  // Initialize the class loader (of sdk/android/native_api/jni/class_loader.cc)
  // which is used to load Java classes from C++ and keep them alive.
  // webrtc::jni::LoadGlobalClassReferenceHolder(); //deprecated
  webrtc::InitClassLoader(webrtc::jni::GetEnv());

  // This is called by the official JNI_OnLoad() of
  // examples/androidnativeapi/jni/onload.cc. This seems to initialize a
  // completely different JVM/JNI set of C++ references
  // (modules/utility/source/jvm_android.cc), only used by the audio module.
  // Apparently it seems this was to become the "new way" but got cancelled and
  // the change partially reverted.
  // https://bugs.chromium.org/p/webrtc/issues/detail?id=8067
  // Now, in addition, when initializing WebRTC from Java/Unity, this is actually
  // automatically called from
  // JNI_PeerConnectionFactory_InitializeAndroidGlobals() which is called from
  // Java via the Unity wrapper code in Android.Initialize(), so cannot be
  // called manually here otherwise it asserts.
  // webrtc::JVM::Initialize(jvm);

  // As per JNI's specification, return the JNI version expected by the app.
  RTC_LOG(LS_INFO) << "Initialized Java with JNI version #" << jni_version;

  // Initialization seems to be called twice on quest. This fixes it.
  isInialized = true;

  return jni_version;
}

/// Auto-magic function called by the Java VM when the library is unloaded.
extern "C" void MRS_JNIEXPORT JNICALL JNI_OnUnLoad(JavaVM* jvm,
                                                   void* reserved) {
  RTC_LOG(INFO) << "JNI_OnUnLoad() for MR-WebRTC";

  // Clean-up the second JVM/JNI set. Unclear if that should be done since the
  // Java path to initialize from Android.Initialize() doesn't have a shutdown
  // path.
  // Comment says it should be called from the same thread as Initialize(), but
  // since Initialize() also needs to be called from the first JVM-attached
  // thread (otherwise nothing can be done) then it's not like we have a lot of
  // choice but to hope the JVM calls JNI_OnUnLoad() on the same thread it calls
  // JNI_OnLoad(). The Oracle spec doesn't say anything about that though.
  // https://docs.oracle.com/javase/9/docs/specs/jni/invocation.html#jni_onunload
  webrtc::JVM::Uninitialize();

  RTC_CHECK(rtc::CleanupSSL()) << "Failed to CleanupSSL()";

  // This (sdk/android/src/jni/classreferenceholder.h) is deprecated and
  // currently no-op. But the Unity sample plugin has a different version
  // (examples/unityplugin/classreferenceholder.h) which does a bunch of work.
  // It seems like this should actually release the references, but probably in
  // most cases the library is not expected to be unloaded, so keeping
  // references alive is easier and safer.
  // webrtc::jni::FreeGlobalClassReferenceHolder();

  // There is no shutdown equivalent to webrtc::jni::InitGlobalJniVariables().
}
