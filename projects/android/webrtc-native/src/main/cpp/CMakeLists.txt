cmake_minimum_required(VERSION 3.10.2)

# See ../libwebrtc/README.md for instructions on how to generate `libwebrtc.cmake`
include(../../../../../libwebrtc/.libwebrtc.cmake)
set(mr-webrtc-native-dir ../../../../../../libs/Microsoft.MixedReality.WebRTC.Native)

set(CMAKE_SHARED_LINKER_FLAGS "${CMAKE_SHARED_LINKER_FLAGS} -fsanitize=address")

add_library(
        Microsoft.MixedReality.WebRTC.Native
        SHARED
        ${mr-webrtc-native-dir}/src/interop/external_video_track_source_interop.cpp
        ${mr-webrtc-native-dir}/src/interop/global_factory.cpp
        ${mr-webrtc-native-dir}/src/interop/local_video_track_interop.cpp
        ${mr-webrtc-native-dir}/src/interop/peer_connection_interop.cpp
        ${mr-webrtc-native-dir}/src/interop/interop_api.cpp
        ${mr-webrtc-native-dir}/src/media/external_video_track_source.cpp
        ${mr-webrtc-native-dir}/src/media/local_video_track.cpp
        ${mr-webrtc-native-dir}/src/audio_frame_observer.cpp
        ${mr-webrtc-native-dir}/src/data_channel.cpp
        ${mr-webrtc-native-dir}/src/mrs_errors.cpp
        ${mr-webrtc-native-dir}/src/pch.cpp
        ${mr-webrtc-native-dir}/src/peer_connection.cpp
        ${mr-webrtc-native-dir}/src/sdp_utils.cpp
        ${mr-webrtc-native-dir}/src/str.cpp
        ${mr-webrtc-native-dir}/src/video_frame_observer.cpp
        ./jni_onload.cpp
)

add_library(
        libwebrtc
        STATIC IMPORTED
)

# See ../libwebrtc/README.md for instructions on building `libwebrtc.a`
set_target_properties(
        libwebrtc
        PROPERTIES IMPORTED_LOCATION
        ${libwebrtc-out-dir}/libwebrtc.a
)

target_include_directories(
        Microsoft.MixedReality.WebRTC.Native
        PRIVATE
        ${mr-webrtc-native-dir}/include
        ${mr-webrtc-native-dir}/src
        ${libwebrtc-src-dir}/webrtc/src/
        ${libwebrtc-src-dir}/webrtc/src/third_party/abseil-cpp
        ${libwebrtc-src-dir}/webrtc/src/third_party/libyuv/include
)

target_link_libraries(
        Microsoft.MixedReality.WebRTC.Native
        log
        android
        dl
        OpenSLES
        libwebrtc
)
