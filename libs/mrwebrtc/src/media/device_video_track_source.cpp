// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

#include "pch.h"

#include "interop/global_factory.h"
#include "media/device_video_track_source.h"
#include "video_track_source_interop.h"

namespace {

using namespace Microsoft::MixedReality::WebRTC;

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
        RTC_LOG(LS_INFO) << "Supported video formats:";
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

  // Create the surface texture helper, which manages the surface texture the camera renders to.
  webrtc::ScopedJavaLocalRef<jclass> android_camera_interop_class =
      webrtc::GetClass(env, "com/microsoft/mixedreality/webrtc/AndroidCameraInterop");
  RTC_DCHECK(android_camera_interop_class.obj() != nullptr) << "Failed to find AndroidCameraInterop Java class.";
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

  // Create the camera capturer and bind it to the surface texture and the video source, then start capturing.
  jmethodID start_capture_method = webrtc::GetStaticMethodID(
      env, android_camera_interop_class.obj(), "StartCapture",
      "(JLorg/webrtc/SurfaceTextureHelper;)Lorg/webrtc/VideoCapturer;");
  jobject camera_tmp =
      env->CallStaticObjectMethod(android_camera_interop_class.obj(), start_capture_method,
                                  (jlong)proxy_source.get(), texture_helper);
  CHECK_EXCEPTION(env);

  // Java objects created are always returned as local references; create a new global reference to keep the camera capturer alive.
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
      , java_video_capturer
#endif  // defined(MR_SHARING_ANDROID)
      );
  if (!wrapper) {
    RTC_LOG(LS_ERROR) << "Failed to create device video track source.";
    return Error(Result::kUnknownError);
  }
  return wrapper;
}

DeviceVideoTrackSource::DeviceVideoTrackSource(
    RefPtr<GlobalFactory> global_factory,
    rtc::scoped_refptr<webrtc::VideoTrackSourceInterface> source
#if defined(MR_SHARING_ANDROID)
    , jobject java_video_capturer
#endif  // defined(MR_SHARING_ANDROID)
    ) noexcept
    : VideoTrackSource(std::move(global_factory),
                       ObjectType::kDeviceVideoTrackSource,
                       std::move(source))
#if defined(MR_SHARING_ANDROID)
    , java_video_capturer_(java_video_capturer)
#endif  // defined(MR_SHARING_ANDROID)
  {}

DeviceVideoTrackSource::~DeviceVideoTrackSource() {
#if defined(MR_SHARING_ANDROID)
  // Stop video capture and release Java capturer
  if (java_video_capturer_) {
    JNIEnv* env = webrtc::jni::GetEnv();
    RTC_DCHECK(env);
    webrtc::ScopedJavaLocalRef<jclass> pc_factory_class =
        webrtc::GetClass(env, "org/webrtc/UnityUtility");
    jmethodID stop_camera_method =
        webrtc::GetStaticMethodID(env, pc_factory_class.obj(), "StopCamera",
                                  "(Lorg/webrtc/VideoCapturer;)V");
    env->CallStaticVoidMethod(pc_factory_class.obj(), stop_camera_method,
                              java_video_capturer_);
    CHECK_EXCEPTION(env);
    java_video_capturer_ = nullptr;
  }
#endif  // defined(MR_SHARING_ANDROID)
}

}  // namespace WebRTC
}  // namespace MixedReality
}  // namespace Microsoft
