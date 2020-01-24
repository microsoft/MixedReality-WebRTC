cmake_minimum_required(VERSION 3.10.2)

set(deps-dir ${CMAKE_SOURCE_DIR}/../../../../deps)
set(mr-webrtc-native-dir ../../../../../libs/Microsoft.MixedReality.WebRTC.Native)

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
        libwebrtc-full-a
        STATIC IMPORTED
)

# See BUILD.md for instructions to build libwebrtc_full.a
set_target_properties(
        libwebrtc-full-a
        PROPERTIES IMPORTED_LOCATION
        ${deps-dir}/webrtc/libwebrtc_full.a
)

target_include_directories(
        Microsoft.MixedReality.WebRTC.Native
        PRIVATE
        ${mr-webrtc-native-dir}/include
        ${mr-webrtc-native-dir}/src
        ${deps-dir}/webrtc/include
        ${deps-dir}/webrtc/include/third_party/abseil-cpp
        ${deps-dir}/webrtc/include/third_party/libyuv/include
)

target_link_libraries(
        Microsoft.MixedReality.WebRTC.Native
        log
        android
        dl
        OpenSLES
        libwebrtc-full-a
)
