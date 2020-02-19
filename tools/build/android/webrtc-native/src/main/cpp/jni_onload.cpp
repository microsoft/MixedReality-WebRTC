// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

#include <jni.h>
#undef JNIEXPORT
#define JNIEXPORT __attribute__((visibility("default")))

#include "rtc_base/logging.h"
#include "rtc_base/ssladapter.h"
#include "modules/utility/include/jvm_android.h"
#include "sdk/android/native_api/base/init.h"
#include "sdk/android/src/jni/classreferenceholder.h"
#include "sdk/android/src/jni/jni_helpers.h"

extern "C" jint JNIEXPORT JNICALL JNI_OnLoad(JavaVM* jvm, void* reserved) {
  RTC_LOG(INFO) << "JNI_OnLoad() for MR-WebRTC";
  webrtc::InitAndroid(jvm);
  webrtc::JVM::Initialize(jvm);
  RTC_CHECK(rtc::InitializeSSL()) << "Failed to InitializeSSL()";
  return JNI_VERSION_1_6;
}

extern "C" void JNIEXPORT JNICALL JNI_OnUnLoad(JavaVM* jvm, void* reserved) {
  RTC_LOG(INFO) << "JNI_OnUnLoad() for MR-WebRTC";
  RTC_CHECK(rtc::CleanupSSL()) << "Failed to CleanupSSL()";
}
