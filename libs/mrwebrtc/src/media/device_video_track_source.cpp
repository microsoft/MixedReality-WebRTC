// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

#include "pch.h"

#include "interop/global_factory.h"
#include "media/device_video_track_source.h"
#include "video_track_source_interop.h"

#if defined(WINUWP)
#include <winrt/Windows.Foundation.Collections.h>
#include <winrt/Windows.Media.Capture.h>
#endif

namespace {

using namespace Microsoft::MixedReality::WebRTC;

#if defined(WINUWP)
uint32_t FourCCFromMFSubType(winrt::hstring const& subtype) {
  using namespace winrt::Windows::Media::MediaProperties;
  uint32_t fourcc;
  if (_wcsicmp(subtype.c_str(), MediaEncodingSubtypes::Yv12().c_str()) == 0) {
    fourcc = libyuv::FOURCC_YV12;
  } else if (_wcsicmp(subtype.c_str(), MediaEncodingSubtypes::Yuy2().c_str()) ==
             0) {
    fourcc = libyuv::FOURCC_YUY2;
  } else if (_wcsicmp(subtype.c_str(), MediaEncodingSubtypes::Iyuv().c_str()) ==
             0) {
    fourcc = libyuv::FOURCC_IYUV;
  } else if (_wcsicmp(subtype.c_str(),
                      MediaEncodingSubtypes::Rgb24().c_str()) == 0) {
    fourcc = libyuv::FOURCC_24BG;
  } else if (_wcsicmp(subtype.c_str(),
                      MediaEncodingSubtypes::Rgb32().c_str()) == 0) {
    fourcc = libyuv::FOURCC_ARGB;
  } else if (_wcsicmp(subtype.c_str(), MediaEncodingSubtypes::Mjpg().c_str()) ==
             0) {
    fourcc = libyuv::FOURCC_MJPG;
  } else if (_wcsicmp(subtype.c_str(), MediaEncodingSubtypes::Nv12().c_str()) ==
             0) {
    fourcc = libyuv::FOURCC_NV12;
  } else {
    fourcc = libyuv::FOURCC_ANY;
  }
  return fourcc;
}
#endif

/// Convert a WebRTC VideoType format into its FOURCC counterpart.
uint32_t FourCCFromVideoType(webrtc::VideoType videoType) {
  switch (videoType) {
    default:
    case webrtc::VideoType::kUnknown:
      return (uint32_t)libyuv::FOURCC_ANY;
    case webrtc::VideoType::kI420:
      return (uint32_t)libyuv::FOURCC_I420;
    case webrtc::VideoType::kIYUV:
      return (uint32_t)libyuv::FOURCC_IYUV;
    case webrtc::VideoType::kRGB24:
      // this seems unintuitive, but is how defined in the core implementation
      return (uint32_t)libyuv::FOURCC_24BG;
    case webrtc::VideoType::kABGR:
      return (uint32_t)libyuv::FOURCC_ABGR;
    case webrtc::VideoType::kARGB:
      return (uint32_t)libyuv::FOURCC_ARGB;
    case webrtc::VideoType::kARGB4444:
      return (uint32_t)libyuv::FOURCC_R444;
    case webrtc::VideoType::kRGB565:
      return (uint32_t)libyuv::FOURCC_RGBP;
    case webrtc::VideoType::kARGB1555:
      return (uint32_t)libyuv::FOURCC_RGBO;
    case webrtc::VideoType::kYUY2:
      return (uint32_t)libyuv::FOURCC_YUY2;
    case webrtc::VideoType::kYV12:
      return (uint32_t)libyuv::FOURCC_YV12;
    case webrtc::VideoType::kUYVY:
      return (uint32_t)libyuv::FOURCC_UYVY;
    case webrtc::VideoType::kMJPEG:
      return (uint32_t)libyuv::FOURCC_MJPG;
    case webrtc::VideoType::kNV21:
      return (uint32_t)libyuv::FOURCC_NV21;
    case webrtc::VideoType::kNV12:
      return (uint32_t)libyuv::FOURCC_NV12;
    case webrtc::VideoType::kBGRA:
      return (uint32_t)libyuv::FOURCC_BGRA;
  };
}

class SimpleMediaConstraints : public webrtc::MediaConstraintsInterface {
 public:
  using webrtc::MediaConstraintsInterface::Constraint;
  using webrtc::MediaConstraintsInterface::Constraints;
  static Constraint MinWidth(uint32_t min_width) {
    return Constraint(webrtc::MediaConstraintsInterface::kMinWidth,
                      std::to_string(min_width));
  }
  static Constraint MaxWidth(uint32_t max_width) {
    return Constraint(webrtc::MediaConstraintsInterface::kMaxWidth,
                      std::to_string(max_width));
  }
  static Constraint MinHeight(uint32_t min_height) {
    return Constraint(webrtc::MediaConstraintsInterface::kMinHeight,
                      std::to_string(min_height));
  }
  static Constraint MaxHeight(uint32_t max_height) {
    return Constraint(webrtc::MediaConstraintsInterface::kMaxHeight,
                      std::to_string(max_height));
  }
  static Constraint MinFrameRate(double min_framerate) {
    // Note: kMinFrameRate is read back as an int
    const int min_int = (int)std::floor(min_framerate);
    return Constraint(webrtc::MediaConstraintsInterface::kMinFrameRate,
                      std::to_string(min_int));
  }
  static Constraint MaxFrameRate(double max_framerate) {
    // Note: kMinFrameRate is read back as an int
    const int max_int = (int)std::ceil(max_framerate);
    return Constraint(webrtc::MediaConstraintsInterface::kMaxFrameRate,
                      std::to_string(max_int));
  }
  const Constraints& GetMandatory() const override { return mandatory_; }
  const Constraints& GetOptional() const override { return optional_; }
  Constraints mandatory_;
  Constraints optional_;
};

#if defined(WINUWP)
using WebRtcFactoryPtr =
    std::shared_ptr<wrapper::impl::org::webRtc::WebRtcFactory>;
#endif  // defined(WINUWP)

/// Helper to open a video capture device.
mrsResult OpenVideoCaptureDevice(
    const mrsLocalVideoDeviceInitConfig& config,
    std::unique_ptr<cricket::VideoCapturer>& capturer_out) noexcept {
  capturer_out.reset();
#if defined(WINUWP)
  RefPtr<GlobalFactory> global_factory(GlobalFactory::InstancePtr());
  WebRtcFactoryPtr uwp_factory;
  {
    mrsResult res = global_factory->GetOrCreateWebRtcFactory(uwp_factory);
    if (res != Result::kSuccess) {
      RTC_LOG(LS_ERROR) << "Failed to initialize the UWP factory.";
      return res;
    }
  }

  // Check for calls from main UI thread; this is not supported (will deadlock)
  auto mw = winrt::Windows::ApplicationModel::Core::CoreApplication::MainView();
  auto cw = mw.CoreWindow();
  auto dispatcher = cw.Dispatcher();
  if (dispatcher.HasThreadAccess()) {
    return Result::kWrongThread;
  }

  // Get devices synchronously (wait for UI thread to retrieve them for us)
  rtc::Event blockOnDevicesEvent(true, false);
  auto vci = wrapper::impl::org::webRtc::VideoCapturer::getDevices();
  vci->thenClosure([&blockOnDevicesEvent] { blockOnDevicesEvent.Set(); });
  blockOnDevicesEvent.Wait(rtc::Event::kForever);
  auto deviceList = vci->value();

  std::wstring video_device_id_str;
  if (!IsStringNullOrEmpty(config.video_device_id)) {
    video_device_id_str =
        rtc::ToUtf16(config.video_device_id, strlen(config.video_device_id));
  }

  for (auto&& vdi : *deviceList) {
    auto devInfo =
        wrapper::impl::org::webRtc::VideoDeviceInfo::toNative_winrt(vdi);
    const winrt::hstring& id = devInfo.Id();
    if (!video_device_id_str.empty() && (video_device_id_str != id)) {
      RTC_LOG(LS_VERBOSE) << "Skipping device ID " << rtc::ToUtf8(id.c_str())
                          << " not matching requested device.";
      continue;
    }

    auto createParams =
        wrapper::org::webRtc::VideoCapturerCreationParameters::wrapper_create();
    createParams->factory = uwp_factory;
    createParams->name = devInfo.Name().c_str();
    createParams->id = id.c_str();
    if (config.video_profile_id) {
      createParams->videoProfileId = config.video_profile_id;
    }
    createParams->videoProfileKind =
        (wrapper::org::webRtc::VideoProfileKind)config.video_profile_kind;
    createParams->enableMrc = (config.enable_mrc != mrsBool::kFalse);
    createParams->enableMrcRecordingIndicator =
        (config.enable_mrc_recording_indicator != mrsBool::kFalse);
    createParams->width = config.width;
    createParams->height = config.height;
    createParams->framerate = config.framerate;

    auto vcd = wrapper::impl::org::webRtc::VideoCapturer::create(createParams);
    if (vcd != nullptr) {
      auto nativeVcd = wrapper::impl::org::webRtc::VideoCapturer::toNative(vcd);

      RTC_LOG(LS_INFO) << "Using video capture device '"
                       << createParams->name.c_str()
                       << "' (id=" << createParams->id.c_str() << ")";

      if (auto supportedFormats = nativeVcd->GetSupportedFormats()) {
        RTC_LOG(LS_INFO)
            << "Supported video formats (after any video profile filtering):";
        for (auto&& format : *supportedFormats) {
          auto str = format.ToString();
          RTC_LOG(LS_INFO) << "- " << str.c_str();
        }
      }

      capturer_out = std::move(nativeVcd);
      return Result::kSuccess;
    }
  }
  RTC_LOG(LS_ERROR) << "Failed to find a local video capture device matching "
                       "the capture format constraints. None of the "
                    << deviceList->size()
                    << " devices tested had a compatible capture format.";
  return Result::kNotFound;
#else
  // List all available video capture devices, or match by ID if specified.
  std::vector<std::string> device_names;
  {
    std::unique_ptr<webrtc::VideoCaptureModule::DeviceInfo> info(
        webrtc::VideoCaptureFactory::CreateDeviceInfo());
    if (!info) {
      return Result::kUnknownError;
    }

    const int num_devices = info->NumberOfDevices();
    constexpr uint32_t kSize = 256;
    if (!IsStringNullOrEmpty(config.video_device_id)) {
      // Look for the one specific device the user asked for
      std::string video_device_id_str = config.video_device_id;
      for (int i = 0; i < num_devices; ++i) {
        char name[kSize] = {};
        char id[kSize] = {};
        if (info->GetDeviceName(i, name, kSize, id, kSize) != -1) {
          if (video_device_id_str == id) {
            // Keep only the device the user selected
            device_names.push_back(name);
            break;
          }
        }
      }
      if (device_names.empty()) {
        RTC_LOG(LS_ERROR)
            << "Could not find video capture device by unique ID: "
            << config.video_device_id;
        return Result::kNotFound;
      }
    } else {
      // List all available devices
      for (int i = 0; i < num_devices; ++i) {
        char name[kSize] = {};
        char id[kSize] = {};
        if (info->GetDeviceName(i, name, kSize, id, kSize) != -1) {
          device_names.push_back(name);
        }
      }
      if (device_names.empty()) {
        RTC_LOG(LS_ERROR) << "Could not find any video catpure device.";
        return Result::kNotFound;
      }
    }
  }

  // Open the specified capture device, or the first one available if none
  // specified.
  cricket::WebRtcVideoDeviceCapturerFactory factory;
  for (const auto& name : device_names) {
    // cricket::Device identifies devices by (friendly) name, not unique ID
    capturer_out = factory.Create(cricket::Device(name, 0));
    if (capturer_out) {
      return Result::kSuccess;
    }
  }
  RTC_LOG(LS_ERROR) << "Failed to open any video capture device (tried "
                    << device_names.size() << " devices).";
  return Result::kUnknownError;
#endif
}

#ifdef WINUWP

winrt::Windows::Media::Capture::KnownVideoProfile KnownVideoProfileFromKind(
    mrsVideoProfileKind profile_kind) {
  RTC_DCHECK(profile_kind != mrsVideoProfileKind::kUnspecified);
  return (winrt::Windows::Media::Capture::KnownVideoProfile)(
      (int)(profile_kind)-1);
}

mrsVideoProfileKind KnownVideoProfileToKind(
    winrt::Windows::Media::Capture::KnownVideoProfile known_profile) {
  return (mrsVideoProfileKind)((int)(known_profile) + 1);
}

#endif

}  // namespace

namespace Microsoft {
namespace MixedReality {
namespace WebRTC {

ErrorOr<RefPtr<DeviceVideoTrackSource>> DeviceVideoTrackSource::Create(
    const mrsLocalVideoDeviceInitConfig& init_config) noexcept {
  RefPtr<GlobalFactory> global_factory(GlobalFactory::InstancePtr());
  auto pc_factory = global_factory->GetPeerConnectionFactory();
  if (!pc_factory) {
    return Error(Result::kInvalidOperation);
  }

  rtc::scoped_refptr<webrtc::VideoTrackSourceInterface> video_source;

#if !defined(MR_SHARING_ANDROID)

  // Open the video capture device
  std::unique_ptr<cricket::VideoCapturer> video_capturer;
  auto res = OpenVideoCaptureDevice(init_config, video_capturer);
  if (res != Result::kSuccess) {
    RTC_LOG(LS_ERROR) << "Failed to open video capture device.";
    return Error(res);
  }
  RTC_CHECK(video_capturer.get());

  // Apply the same constraints used for opening the video capturer
  auto videoConstraints = std::make_unique<SimpleMediaConstraints>();
  if (init_config.width > 0) {
    videoConstraints->mandatory_.push_back(
        SimpleMediaConstraints::MinWidth(init_config.width));
    videoConstraints->mandatory_.push_back(
        SimpleMediaConstraints::MaxWidth(init_config.width));
  }
  if (init_config.height > 0) {
    videoConstraints->mandatory_.push_back(
        SimpleMediaConstraints::MinHeight(init_config.height));
    videoConstraints->mandatory_.push_back(
        SimpleMediaConstraints::MaxHeight(init_config.height));
  }
  if (init_config.framerate > 0) {
    videoConstraints->mandatory_.push_back(
        SimpleMediaConstraints::MinFrameRate(init_config.framerate));
    videoConstraints->mandatory_.push_back(
        SimpleMediaConstraints::MaxFrameRate(init_config.framerate));
  }

  // Create the video track source
  video_source = pc_factory->CreateVideoSource(std::move(video_capturer),
                                               videoConstraints.get());

#else  // !defined(MR_SHARING_ANDROID)

  // Make sure the current thread is attached to the JVM. Since this method
  // is often called asynchronously (as it takes some time to initialize the
  // video capture device) it is likely to run on a background worker thread.
  RTC_DCHECK(webrtc::jni::GetJVM()) << "JavaVM not initialized.";
  JNIEnv* env = webrtc::jni::AttachCurrentThreadIfNeeded();

  // Create the surface texture helper, which manages the surface texture the
  // camera renders to.
  webrtc::ScopedJavaLocalRef<jclass> android_camera_interop_class =
      webrtc::GetClass(
          env, "com/microsoft/mixedreality/webrtc/AndroidCameraInterop");
  RTC_DCHECK(android_camera_interop_class.obj() != nullptr)
      << "Failed to find AndroidCameraInterop Java class.";
  jmethodID create_texture_helper_method = webrtc::GetStaticMethodID(
      env, android_camera_interop_class.obj(), "CreateSurfaceTextureHelper",
      "()Lorg/webrtc/SurfaceTextureHelper;");
  jobject texture_helper = env->CallStaticObjectMethod(
      android_camera_interop_class.obj(), create_texture_helper_method);
  CHECK_EXCEPTION(env);
  RTC_DCHECK(texture_helper != nullptr)
      << "Cannot get the Surface Texture Helper.";

  // Create the video track source which wraps the Android camera capturer
  rtc::scoped_refptr<webrtc::jni::AndroidVideoTrackSource> impl_source(
      new rtc::RefCountedObject<webrtc::jni::AndroidVideoTrackSource>(
          global_factory->GetSignalingThread(), env, false));
  rtc::scoped_refptr<webrtc::VideoTrackSourceProxy> proxy_source =
      webrtc::VideoTrackSourceProxy::Create(
          global_factory->GetSignalingThread(),
          global_factory->GetWorkerThread(), impl_source);

  int width = 0;
  if (init_config.width > 0) {
    width = (int)init_config.width;
  }
  int height = 0;
  if (init_config.height > 0) {
    height = (int)init_config.height;
  }
  float framerate = 0.0f;
  if (init_config.framerate > 0.0f) {
    framerate = (float)init_config.framerate;
  }

  // Create the camera capturer and bind it to the surface texture and the video
  // source, then start capturing.
  jmethodID start_capture_method = webrtc::GetStaticMethodID(
      env, android_camera_interop_class.obj(), "StartCapture",
      "(JLorg/webrtc/SurfaceTextureHelper;Ljava/lang/String;IIF)Lorg/webrtc/"
      "VideoCapturer;");
  jstring java_device_name = env->NewStringUTF(init_config.video_device_id);
  CHECK_EXCEPTION(env);
  jobject camera_tmp = env->CallStaticObjectMethod(
      android_camera_interop_class.obj(), start_capture_method,
      (jlong)proxy_source.get(), texture_helper, java_device_name, (jint)width,
      (jint)height, (jfloat)framerate);
  CHECK_EXCEPTION(env);

  // Java objects created are always returned as local references; create a new
  // global reference to keep the camera capturer alive.
  jobject java_video_capturer = (jobject)env->NewGlobalRef(camera_tmp);

  video_source = proxy_source;

#endif  // !defined(MR_SHARING_ANDROID)

  if (!video_source) {
    RTC_LOG(LS_ERROR) << "Failed to create video track source.";
    return Error(Result::kUnknownError);
  }

  // Create the wrapper
  RefPtr<DeviceVideoTrackSource> wrapper =
      new DeviceVideoTrackSource(global_factory, std::move(video_source)
#if defined(MR_SHARING_ANDROID)
                                                     ,
                                 java_video_capturer
#endif  // defined(MR_SHARING_ANDROID)
      );
  if (!wrapper) {
    RTC_LOG(LS_ERROR) << "Failed to create device video track source.";
    return Error(Result::kUnknownError);
  }
  return wrapper;
}

Error DeviceVideoTrackSource::GetVideoCaptureDevices(
    Callback<const mrsVideoCaptureDeviceInfo*> enum_callback,
    Callback<mrsResult> end_callback) noexcept {
#if defined(MR_SHARING_ANDROID)
  // Make sure the current thread is attached to the JVM. Since this method
  // is often called asynchronously (as it takes some time to enumerate the
  // video capture devices) it is likely to run on a background worker thread.
  RTC_DCHECK(webrtc::jni::GetJVM()) << "JavaVM not initialized.";
  JNIEnv* const env = webrtc::jni::AttachCurrentThreadIfNeeded();

  // Find the Android camera helper Java class
  webrtc::ScopedJavaLocalRef<jclass> android_camera_interop_class =
      webrtc::GetClass(
          env, "com/microsoft/mixedreality/webrtc/AndroidCameraInterop");
  RTC_DCHECK(android_camera_interop_class.obj() != nullptr)
      << "Failed to find AndroidCameraInterop Java class.";

  // Find the video capture device info storage class and its fields
  webrtc::ScopedJavaLocalRef<jclass> device_info_class = webrtc::GetClass(
      env, "com/microsoft/mixedreality/webrtc/VideoCaptureDeviceInfo");
  RTC_DCHECK(device_info_class.obj() != nullptr)
      << "Failed to find VideoCaptureDeviceInfo Java class.";
  jfieldID id_field =
      env->GetFieldID(device_info_class.obj(), "id", "Ljava/lang/String;");
  CHECK_EXCEPTION(env);
  jfieldID name_field =
      env->GetFieldID(device_info_class.obj(), "name", "Ljava/lang/String;");
  CHECK_EXCEPTION(env);

  // Retrieve the device list
  jmethodID enum_devices_method = webrtc::GetStaticMethodID(
      env, android_camera_interop_class.obj(), "GetVideoCaptureDevices",
      "()[Lcom/microsoft/mixedreality/webrtc/VideoCaptureDeviceInfo;");
  jobjectArray device_list = (jobjectArray)env->CallStaticObjectMethod(
      android_camera_interop_class.obj(), enum_devices_method);
  CHECK_EXCEPTION(env);
  const jsize num_devices = env->GetArrayLength(device_list);
  CHECK_EXCEPTION(env);
  if (num_devices <= 0) {
    return {};
  }
  Enumerator<const mrsVideoCaptureDeviceInfo*, mrsResult> enumerator(
      enum_callback, end_callback, mrsResult::kSuccess);
  for (jsize i = 0; i < num_devices; ++i) {
    jobject java_device_info = env->GetObjectArrayElement(device_list, i);
    CHECK_EXCEPTION(env);
    jstring java_id = (jstring)env->GetObjectField(java_device_info, id_field);
    CHECK_EXCEPTION(env);
    jstring java_name =
        (jstring)env->GetObjectField(java_device_info, name_field);
    CHECK_EXCEPTION(env);
    const char* native_id = env->GetStringUTFChars(java_id, nullptr);
    const char* native_name = env->GetStringUTFChars(java_name, nullptr);
    mrsVideoCaptureDeviceInfo device_info{native_id, native_name};
    enumerator.yield(&device_info);
    env->ReleaseStringUTFChars(java_name, native_name);
    env->ReleaseStringUTFChars(java_id, native_id);
  }
  return Error::None();
#elif defined(WINUWP)
  RefPtr<GlobalFactory> global_factory(GlobalFactory::InstancePtr());
  // The UWP factory needs to be initialized for getDevices() to work.
  if (!global_factory->GetPeerConnectionFactory()) {
    RTC_LOG(LS_ERROR) << "Failed to initialize the UWP factory.";
    return Error(Result::kUnknownError);
  }

  auto vci = wrapper::impl::org::webRtc::VideoCapturer::getDevices();
  vci->thenClosure([vci, enum_callback, end_callback] {
    Enumerator<const mrsVideoCaptureDeviceInfo*, mrsResult> enumerator(
        enum_callback, end_callback, mrsResult::kSuccess);
    auto deviceList = vci->value();
    for (auto&& vdi : *deviceList) {
      auto devInfo =
          wrapper::impl::org::webRtc::VideoDeviceInfo::toNative_winrt(vdi);
      auto id = winrt::to_string(devInfo.Id());
      auto name = winrt::to_string(devInfo.Name());
      mrsVideoCaptureDeviceInfo device_info{id.c_str(), name.c_str()};
      enumerator.yield(&device_info);
    }
  });
  return Error::None();
#else
  std::unique_ptr<webrtc::VideoCaptureModule::DeviceInfo> info(
      webrtc::VideoCaptureFactory::CreateDeviceInfo());
  if (!info) {
    RTC_LOG(LS_ERROR) << "Failed to start video capture devices enumeration.";
    return Error(Result::kUnknownError);
  }
  Enumerator<const mrsVideoCaptureDeviceInfo*, mrsResult> enumerator(
      enum_callback, end_callback, mrsResult::kSuccess);
  int num_devices = info->NumberOfDevices();
  for (int i = 0; i < num_devices; ++i) {
    constexpr uint32_t kSize = 256;
    char name[kSize] = {0};
    char id[kSize] = {0};
    if (info->GetDeviceName(i, name, kSize, id, kSize) != -1) {
      mrsVideoCaptureDeviceInfo device_info{id, name};
      enumerator.yield(&device_info);
    }
  }
  return Error::None();
#endif
}

Error DeviceVideoTrackSource::GetVideoProfiles(
    absl::string_view device_id,
    mrsVideoProfileKind profile_kind,
    Callback<const mrsVideoProfileInfo*> enum_callback,
    Callback<mrsResult> end_callback) noexcept {
#if defined(WINUWP)
  std::string device_id_str(device_id.data(), device_id.size());

  // Create an RAII enumerator to ensure the end callback is always called even
  // on error during enumeration or early out if device does not support
  // profiles.
  Enumerator<const mrsVideoProfileInfo*, mrsResult> enumerator(
      enum_callback, end_callback, mrsResult::kSuccess);

  // Check if the device supports video profiles at all
  if (!winrt::Windows::Media::Capture::MediaCapture::IsVideoProfileSupported(
          winrt::to_hstring(device_id))) {
    RTC_LOG(LS_INFO) << "Video capture device '" << device_id_str.c_str()
                     << "' does not support video profiles.";
    return Error::None();
  }

  // Enumerate the video profiles
  winrt::Windows::Foundation::Collections::IVectorView<
      winrt::Windows::Media::Capture::MediaCaptureVideoProfile>
      profile_list;
  if (profile_kind == mrsVideoProfileKind::kUnspecified) {
    RTC_LOG(LS_INFO) << "Enumerating video profiles for device '"
                     << device_id_str.c_str() << "'";
    profile_list =
        winrt::Windows::Media::Capture::MediaCapture::FindAllVideoProfiles(
            winrt::to_hstring(device_id_str));
  } else {
    RTC_LOG(LS_INFO) << "Enumerating video profiles for device '"
                     << device_id_str.c_str() << "' and profile kind "
                     << (int)profile_kind;
    auto const known_profile = KnownVideoProfileFromKind(profile_kind);
    profile_list =
        winrt::Windows::Media::Capture::MediaCapture::FindKnownVideoProfiles(
            winrt::to_hstring(device_id_str), known_profile);
  }
  for (auto&& profile : profile_list) {
    auto id_str = winrt::to_string(profile.Id());
    mrsVideoProfileInfo info{};
    info.id = id_str.c_str();
    enumerator.yield(&info);
  }
  return Error::None();
#else
  // Non-UWP platforms do not support video profiles

  // Silence unused parameter warnings
  (void)device_id;
  (void)profile_kind;
  (void)enum_callback;

  // End successfully without anything enumerated
  end_callback(mrsResult::kSuccess);
  return Error::None();
#endif
}

Error DeviceVideoTrackSource::GetVideoCaptureFormats(
    absl::string_view device_id,
    absl::string_view profile_id,
    mrsVideoProfileKind profile_kind,
    Callback<const mrsVideoCaptureFormatInfo*> enum_callback,
    Callback<mrsResult> end_callback) noexcept {
#if defined(MR_SHARING_ANDROID)
  // Non-UWP platforms do not support video profiles

  // Silence unused parameter warnings
  (void)profile_id;
  (void)profile_kind;

  // Make sure the current thread is attached to the JVM. Since this method
  // is often called asynchronously (as it takes some time to enumerate the
  // video capture devices) it is likely to run on a background worker thread.
  RTC_DCHECK(webrtc::jni::GetJVM()) << "JavaVM not initialized.";
  JNIEnv* const env = webrtc::jni::AttachCurrentThreadIfNeeded();

  // Find the Android camera helper Java class
  webrtc::ScopedJavaLocalRef<jclass> android_camera_interop_class =
      webrtc::GetClass(
          env, "com/microsoft/mixedreality/webrtc/AndroidCameraInterop");
  RTC_DCHECK(android_camera_interop_class.obj() != nullptr)
      << "Failed to find AndroidCameraInterop Java class.";

  // Find the video capture format info storage class and its fields
  webrtc::ScopedJavaLocalRef<jclass> format_info_class = webrtc::GetClass(
      env, "com/microsoft/mixedreality/webrtc/VideoCaptureFormatInfo");
  RTC_DCHECK(format_info_class.obj() != nullptr)
      << "Failed to find VideoCaptureFormatInfo Java class.";
  jfieldID width_field = env->GetFieldID(format_info_class.obj(), "width", "I");
  CHECK_EXCEPTION(env);
  jfieldID height_field =
      env->GetFieldID(format_info_class.obj(), "height", "I");
  CHECK_EXCEPTION(env);
  jfieldID framerate_field =
      env->GetFieldID(format_info_class.obj(), "framerate", "F");
  CHECK_EXCEPTION(env);
  jfieldID fourcc_field =
      env->GetFieldID(format_info_class.obj(), "fourcc", "J");
  CHECK_EXCEPTION(env);

  // Retrieve the format list
  jmethodID enum_formats_method = webrtc::GetStaticMethodID(
      env, android_camera_interop_class.obj(), "GetVideoCaptureFormats",
      "(Ljava/lang/String;)[Lcom/microsoft/mixedreality/webrtc/"
      "VideoCaptureFormatInfo;");
  std::string device_id_null_terminated(device_id.data(), device_id.size());
  jstring java_device_id = env->NewStringUTF(device_id_null_terminated.c_str());
  CHECK_EXCEPTION(env);
  jobjectArray format_list = (jobjectArray)env->CallStaticObjectMethod(
      android_camera_interop_class.obj(), enum_formats_method, java_device_id);
  CHECK_EXCEPTION(env);
  const jsize num_formats = env->GetArrayLength(format_list);
  CHECK_EXCEPTION(env);
  if (num_formats <= 0) {
    return {};
  }
  Enumerator<const mrsVideoCaptureFormatInfo*, mrsResult> enumerator(
      enum_callback, end_callback, mrsResult::kSuccess);
  for (jsize i = 0; i < num_formats; ++i) {
    jobject java_format_info = env->GetObjectArrayElement(format_list, i);
    CHECK_EXCEPTION(env);
    jint java_width = env->GetIntField(java_format_info, width_field);
    CHECK_EXCEPTION(env);
    jint java_height = env->GetIntField(java_format_info, height_field);
    CHECK_EXCEPTION(env);
    jfloat java_framerate =
        env->GetFloatField(java_format_info, framerate_field);
    CHECK_EXCEPTION(env);
    uint32_t fourcc =
        (uint32_t)env->GetLongField(java_format_info, fourcc_field);
    CHECK_EXCEPTION(env);
    mrsVideoCaptureFormatInfo format_info{};
    format_info.width = java_width;
    format_info.height = java_height;
    format_info.framerate = java_framerate;
    format_info.fourcc = fourcc;
    enumerator.yield(&format_info);
  }
  return Error::None();
#elif defined(WINUWP)
  RefPtr<GlobalFactory> global_factory(GlobalFactory::InstancePtr());
  // The UWP factory needs to be initialized for getDevices() to work.
  WebRtcFactoryPtr uwp_factory;
  {
    mrsResult res = global_factory->GetOrCreateWebRtcFactory(uwp_factory);
    if (res != Result::kSuccess) {
      RTC_LOG(LS_ERROR) << "Failed to initialize the UWP factory.";
      return Error(res);
    }
  }

  // On UWP, MediaCapture is used to open the video capture device and list
  // the available capture formats. This requires the UI thread to be idle,
  // ready to process messages. Because the enumeration is async, and this
  // function can return before the enumeration completed, if called on the
  // main UI thread then defer all of it to a different thread.
  // auto mw =
  // winrt::Windows::ApplicationModel::Core::CoreApplication::MainView(); auto
  // cw = mw.CoreWindow(); auto dispatcher = cw.Dispatcher(); if
  // (dispatcher.HasThreadAccess()) {
  //  if (completedCallback) {
  //    (*completedCallback)(Result::kWrongThread,
  //    completedCallbackUserData);
  //  }
  //  return Result::kWrongThread;
  //}

  // Keep a copy of the various string parameters before the views go out of
  // scope. Store in unique pointer for RAII-style async clean-up.
  auto device_id_ptr =
      std::make_unique<std::string>(device_id.data(), device_id.size());
  auto profile_id_ptr =
      profile_id.empty()
          ? std::make_unique<std::string>()
          : std::make_unique<std::string>(profile_id.data(), profile_id.size());

  // Only profile ID or kind can be specified, not both
  if (!profile_id.empty() &&
      (profile_kind != mrsVideoProfileKind::kUnspecified)) {
    RTC_LOG(LS_ERROR) << "Cannot specify both video profile ID and kind when "
                         "enumerating capture formats for device '"
                      << device_id_ptr->c_str()
                      << "'. Use either one or the other.";
    return Error(mrsResult::kInvalidParameter);
  }

  // Enumerate the video capture devices to find the device by ID
  auto asyncResults =
      winrt::Windows::Devices::Enumeration::DeviceInformation::FindAllAsync(
          winrt::Windows::Devices::Enumeration::DeviceClass::VideoCapture);
  asyncResults.Completed([device_id = std::move(device_id_ptr),
                          profile_id = std::move(profile_id_ptr), profile_kind,
                          enum_callback, end_callback,
                          uwp_factory = std::move(uwp_factory)](
                             auto&& asyncResults,
                             winrt::Windows::Foundation::AsyncStatus status) {
    // If reaching this point, then GetVideoCaptureFormats() will always return
    // success, as the rest of the code either doesn't fail or is asynchronous.
    // So create an RAII enumerator to ensure the end callback is always called
    // even on error during enumeration. Keep it under a unique_ptr<> to be able
    // to move it to the various async callbacks.
    auto enumerator = std::make_unique<
        Enumerator<const mrsVideoCaptureFormatInfo*, mrsResult>>(
        enum_callback, end_callback, mrsResult::kSuccess);

    // If the OS enumeration failed, terminate our own enumeration
    if (status != winrt::Windows::Foundation::AsyncStatus::Completed) {
      enumerator->setFailure(Result::kUnknownError);
      return;
    }
    winrt::Windows::Devices::Enumeration::DeviceInformationCollection
        devInfoCollection = asyncResults.GetResults();

    // Find the video capture device by unique identifier
    winrt::Windows::Devices::Enumeration::DeviceInformation devInfo(nullptr);
    for (auto curDevInfo : devInfoCollection) {
      auto id = winrt::to_string(curDevInfo.Id());
      if (*device_id != id) {
        continue;
      }
      devInfo = curDevInfo;
      break;
    }
    if (!devInfo) {
      RTC_LOG(LS_ERROR)
          << "Cannot enumerate video capture formats for unknown device ID '"
          << device_id->c_str() << "'";
      enumerator->setFailure(Result::kInvalidParameter);
      return;
    }

    // The normal codepath would be to create and open the video capture device,
    // and call getSupportedFormats(). But this is broken on UWP and somewhat
    // non-trivial to fix. Instead, since we know webrtc-uwp-sdk uses
    // MediaCapture internally, simply get the capture formats from it directly.
#if 0
    // Device found, create an instance to enumerate. Most devices require
    // actually opening the device to enumerate its capture formats.
    auto createParams =
        wrapper::org::webRtc::VideoCapturerCreationParameters::wrapper_create();
    createParams->factory = uwp_factory;
    createParams->name = devInfo.Name().c_str();
    createParams->id = devInfo.Id().c_str();
    createParams->videoProfileId =
        (profile_id->empty() ? "" : profile_id->c_str());
    createParams->videoProfileKind =
        (wrapper::org::webRtc::VideoProfileKind)profile_kind;
    auto vcd = wrapper::impl::org::webRtc::VideoCapturer::create(createParams);
    if (vcd == nullptr) {
      RTC_LOG(LS_ERROR) << "Failed to open video capture device '"
                        << device_id->c_str()
                        << "' for enumerating capture formats.";
      enumerator->setFailure(Result::kUnknownError);
      return;
    }

    // Get its supported capture formats
    auto captureFormatList = vcd->getSupportedFormats();
    for (auto&& captureFormat : *captureFormatList) {
      mrsVideoCaptureFormatInfo format_info{};
      format_info.width = captureFormat->get_width();
      format_info.height = captureFormat->get_height();
      format_info.framerate = captureFormat->get_framerateFloat();
      format_info.fourcc = captureFormat->get_fourcc();
      // When VideoEncodingProperties.Subtype() contains a GUID, the
      // conversion to FOURCC fails and returns FOURCC_ANY. So ignore
      // those formats, as we don't know their encoding.
      if (format_info.fourcc != libyuv::FOURCC_ANY) {
        enumerator->yield(&format_info);
      }
    }
#else  // #if 0
    // Workaround for broken getSupportedFormats() on webrtc-uwp-sdk.

    using namespace winrt::Windows::Media::Capture;
    using namespace winrt::Windows::Media::MediaProperties;
    using namespace winrt::Windows::Foundation::Collections;
    using namespace winrt::Windows::Foundation;

    winrt::hstring device_id_hstr{winrt::to_hstring(*device_id)};

    if (MediaCapture::IsVideoProfileSupported(device_id_hstr)) {
      // For devices supporting video profiles, enumerate the formats of all
      // profiles (or the one selected).
      winrt::hstring profile_id_hstr(winrt::to_hstring(*profile_id));
      IVectorView<MediaCaptureVideoProfile> profile_list;
      if (profile_kind != mrsVideoProfileKind::kUnspecified) {
        auto known_profile = (KnownVideoProfile)((int)profile_kind - 1);
        profile_list =
            MediaCapture::FindKnownVideoProfiles(device_id_hstr, known_profile);
      } else {
        profile_list = MediaCapture::FindAllVideoProfiles(device_id_hstr);
      }
      for (auto&& profile : profile_list) {
        // Skip if a profile was specified and it's not this one
        if (!profile_id_hstr.empty() && (profile.Id() != profile_id_hstr)) {
          continue;
        }
        // Enumerate all supported formats
        auto rmc_list = profile.SupportedRecordMediaDescription();
        for (auto&& rmc : rmc_list) {
          mrsVideoCaptureFormatInfo format_info{};
          format_info.fourcc = FourCCFromMFSubType(rmc.Subtype());
          // When VideoEncodingProperties.Subtype() contains a GUID, the
          // conversion to FOURCC fails and returns FOURCC_ANY. So ignore
          // those formats, as we don't know their encoding.
          if (format_info.fourcc != libyuv::FOURCC_ANY) {
            format_info.width = rmc.Width();
            format_info.height = rmc.Height();
            format_info.framerate = rmc.FrameRate();
            enumerator->yield(&format_info);
          }
        }
      }
    } else {
      // For devices that do not support video profiles, it is necessary to
      // initialize a new MediaCapture instance to enumerate the formats from
      // the device directly.
      auto init_settings = MediaCaptureInitializationSettings::
          MediaCaptureInitializationSettings();
      init_settings.StreamingCaptureMode(StreamingCaptureMode::Video);
      init_settings.VideoDeviceId(device_id_hstr);
      auto media_capture = MediaCapture::MediaCapture();
      auto async_res = media_capture.InitializeAsync(init_settings);
      async_res.Completed([enumerator = std::move(enumerator),
                           media_capture = std::move(media_capture)](
                              auto&& asyncResults, AsyncStatus status) {
        if (status != AsyncStatus::Completed) {
          RTC_LOG(LS_ERROR) << "Failed to initialize MediaCapture to "
                               "enumerate video capture formats.";
          enumerator->setFailure(Result::kUnknownError);
          return;
        }

        // Enumerate all formats from the video device controller
        std::vector<cricket::VideoFormat> formats;
        auto device_controller = media_capture.VideoDeviceController();
        auto stream_props = device_controller.GetAvailableMediaStreamProperties(
            MediaStreamType::VideoRecord);
        for (unsigned int i = 0; i < stream_props.Size(); i++) {
          IVideoEncodingProperties prop;
          stream_props.GetAt(i).as(prop);
          const int width = prop.Width();
          const int height = prop.Height();
          if ((width <= 0) || (height <= 0)) {
            continue;
          }
          const double framerate =
              static_cast<double>(prop.FrameRate().Numerator()) /
              static_cast<double>(prop.FrameRate().Denominator());
          if (framerate <= 0.0) {
            continue;
          }
          mrsVideoCaptureFormatInfo format;
          format.fourcc = FourCCFromMFSubType(prop.Subtype());
          if (format.fourcc != libyuv::FOURCC_ANY) {
            format.width = width;
            format.height = height;
            format.framerate = (float)framerate;
            enumerator->yield(&format);
          }
        }
      });
    }

#endif  // #if 0
  });
#else
  // Non-UWP platforms do not support video profiles

  // Silence unused parameter warnings
  (void)profile_id;
  (void)profile_kind;

  std::unique_ptr<webrtc::VideoCaptureModule::DeviceInfo> info(
      webrtc::VideoCaptureFactory::CreateDeviceInfo());
  if (!info) {
    return Error(Result::kUnknownError);
  }
  Enumerator<const mrsVideoCaptureFormatInfo*, mrsResult> enumerator(
      enum_callback, end_callback, mrsResult::kSuccess);
  int num_devices = info->NumberOfDevices();
  for (int device_idx = 0; device_idx < num_devices; ++device_idx) {
    // Filter devices by name
    constexpr uint32_t kSize = 256;
    char name[kSize] = {0};
    char id[kSize] = {0};
    if (info->GetDeviceName(device_idx, name, kSize, id, kSize) == -1) {
      continue;
    }
    if (device_id != id) {
      continue;
    }

    // Enum video capture formats
    int32_t num_capabilities = info->NumberOfCapabilities(id);
    for (int32_t cap_idx = 0; cap_idx < num_capabilities; ++cap_idx) {
      webrtc::VideoCaptureCapability capability{};
      if (info->GetCapability(id, cap_idx, capability) != -1) {
        mrsVideoCaptureFormatInfo format_info{};
        format_info.width = capability.width;
        format_info.height = capability.height;
        format_info.framerate = (float)capability.maxFPS;
        format_info.fourcc = FourCCFromVideoType(capability.videoType);
        // Ignore unknown capture formats
        if (format_info.fourcc != libyuv::FOURCC_ANY) {
          enumerator.yield(&format_info);
        }
      }
    }

    break;
  }
#endif
  return Error::None();
}

#if defined(MR_SHARING_ANDROID)

DeviceVideoTrackSource::DeviceVideoTrackSource(
    RefPtr<GlobalFactory> global_factory,
    rtc::scoped_refptr<webrtc::VideoTrackSourceInterface> source,
    jobject java_video_capturer) noexcept
    : VideoTrackSource(std::move(global_factory),
                       ObjectType::kDeviceVideoTrackSource,
                       std::move(source)),
      java_video_capturer_(java_video_capturer) {}

DeviceVideoTrackSource::~DeviceVideoTrackSource() {
  // Stop video capture and release Java capturer
  if (java_video_capturer_) {
    JNIEnv* env = webrtc::jni::GetEnv();
    RTC_DCHECK(env);
    webrtc::ScopedJavaLocalRef<jclass> pc_factory_class =
        webrtc::GetClass(env, "com/microsoft/mixedreality/webrtc/AndroidCameraInterop");
    jmethodID stop_camera_method =
        webrtc::GetStaticMethodID(env, pc_factory_class.obj(), "StopCamera",
                                  "(Lorg/webrtc/VideoCapturer;)V");
    env->CallStaticVoidMethod(pc_factory_class.obj(), stop_camera_method,
                              java_video_capturer_);
    CHECK_EXCEPTION(env);
    java_video_capturer_ = nullptr;
  }
}

#else  // defined(MR_SHARING_ANDROID)

DeviceVideoTrackSource::DeviceVideoTrackSource(
    RefPtr<GlobalFactory> global_factory,
    rtc::scoped_refptr<webrtc::VideoTrackSourceInterface> source) noexcept
    : VideoTrackSource(std::move(global_factory),
                       ObjectType::kDeviceVideoTrackSource,
                       std::move(source)) {}

#endif  // defined(MR_SHARING_ANDROID)

}  // namespace WebRTC
}  // namespace MixedReality
}  // namespace Microsoft
