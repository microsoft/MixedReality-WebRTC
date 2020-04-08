# Copyright (c) Microsoft Corporation.
# Licensed under the MIT License.

cmake_minimum_required(VERSION 3.10.2)

#---------------------------------------------------------------------
# Import libwebrtc.a static library
# See ../libwebrtc/README.md for instructions on building libwebrtc.a.
#

# Import CMake settings for libwebrtc.a. This file is generated during
# the libwebrtc build.
include(../../../../../libwebrtc/.libwebrtc.cmake)

# Declare and configure the library.
add_library(
        libwebrtc
        STATIC IMPORTED
)
set_target_properties(
        libwebrtc
        PROPERTIES
        IMPORTED_LOCATION ${libwebrtc-out-dir}/obj/libwebrtc.a
)

#---------------------------------------------------------------------
# Configure libmrwebrtc.so build.
#

# Base path to mrwebrtc project.
set(mr-webrtc-native-dir ../../../../../../../libs/Microsoft.MixedReality.WebRTC.Native)

# List of source files to compile.
add_library(
        mrwebrtc
        SHARED
        ${mr-webrtc-native-dir}/src/interop/external_video_track_source_interop.cpp
        ${mr-webrtc-native-dir}/src/interop/global_factory.cpp
        ${mr-webrtc-native-dir}/src/interop/interop_api.cpp
        ${mr-webrtc-native-dir}/src/interop/local_video_track_interop.cpp
        ${mr-webrtc-native-dir}/src/interop/peer_connection_interop.cpp
        ${mr-webrtc-native-dir}/src/media/external_video_track_source.cpp
        ${mr-webrtc-native-dir}/src/media/local_video_track.cpp
        ${mr-webrtc-native-dir}/src/audio_frame_observer.cpp
        ${mr-webrtc-native-dir}/src/data_channel.cpp
        ${mr-webrtc-native-dir}/src/mrs_errors.cpp
        ${mr-webrtc-native-dir}/src/pch.cpp
        ${mr-webrtc-native-dir}/src/peer_connection.cpp
        ${mr-webrtc-native-dir}/src/sdp_utils.cpp
        ${mr-webrtc-native-dir}/src/str.cpp
        ${mr-webrtc-native-dir}/src/tracked_object.cpp
        ${mr-webrtc-native-dir}/src/video_frame_observer.cpp
        ./jni_onload.cpp
)

# List of include directories.
target_include_directories(
        mrwebrtc
        PRIVATE
        ${mr-webrtc-native-dir}/include
        ${mr-webrtc-native-dir}/src
        ${libwebrtc-src-dir}/webrtc/src/
        ${libwebrtc-src-dir}/webrtc/src/third_party/abseil-cpp
        ${libwebrtc-src-dir}/webrtc/src/third_party/libyuv/include
)

# Build and link libmrwebrtc.so.
target_link_libraries(
        mrwebrtc
        log
        android
        dl
        OpenSLES
        libwebrtc
)
