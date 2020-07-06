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
set(mr-webrtc-native-dir ../../../../../../../libs/mrwebrtc)

# List of source files to compile.
add_library(
        mrwebrtc
        SHARED
        ${mr-webrtc-native-dir}/src/interop/audio_track_source_interop.cpp
        ${mr-webrtc-native-dir}/src/interop/data_channel_interop.cpp
        ${mr-webrtc-native-dir}/src/interop/device_audio_track_source_interop.cpp
        ${mr-webrtc-native-dir}/src/interop/device_video_track_source_interop.cpp
        ${mr-webrtc-native-dir}/src/interop/external_video_track_source_interop.cpp
        ${mr-webrtc-native-dir}/src/interop/global_factory.cpp
        ${mr-webrtc-native-dir}/src/interop/interop_api.cpp
        ${mr-webrtc-native-dir}/src/interop/local_audio_track_interop.cpp
        ${mr-webrtc-native-dir}/src/interop/local_video_track_interop.cpp
        ${mr-webrtc-native-dir}/src/interop/object_interop.cpp
        ${mr-webrtc-native-dir}/src/interop/peer_connection_interop.cpp
        ${mr-webrtc-native-dir}/src/interop/ref_counted_object_interop.cpp
        ${mr-webrtc-native-dir}/src/interop/remote_audio_track_interop.cpp
        ${mr-webrtc-native-dir}/src/interop/remote_video_track_interop.cpp
        ${mr-webrtc-native-dir}/src/interop/transceiver_interop.cpp
        ${mr-webrtc-native-dir}/src/interop/video_track_source_interop.cpp
        ${mr-webrtc-native-dir}/src/media/audio_track_read_buffer.cpp
        ${mr-webrtc-native-dir}/src/media/audio_track_source.cpp
        ${mr-webrtc-native-dir}/src/media/device_audio_track_source.cpp
        ${mr-webrtc-native-dir}/src/media/device_video_track_source.cpp
        ${mr-webrtc-native-dir}/src/media/external_video_track_source.cpp
        ${mr-webrtc-native-dir}/src/media/local_audio_track.cpp
        ${mr-webrtc-native-dir}/src/media/local_video_track.cpp
        ${mr-webrtc-native-dir}/src/media/media_track.cpp
        ${mr-webrtc-native-dir}/src/media/remote_audio_track.cpp
        ${mr-webrtc-native-dir}/src/media/remote_video_track.cpp
        ${mr-webrtc-native-dir}/src/media/transceiver.cpp
        ${mr-webrtc-native-dir}/src/media/video_track_source.cpp
        ${mr-webrtc-native-dir}/src/audio_frame_observer.cpp
        ${mr-webrtc-native-dir}/src/data_channel.cpp
        ${mr-webrtc-native-dir}/src/mrs_errors.cpp
        ${mr-webrtc-native-dir}/src/pch.cpp
        ${mr-webrtc-native-dir}/src/peer_connection.cpp
        ${mr-webrtc-native-dir}/src/sdp_utils.cpp
        ${mr-webrtc-native-dir}/src/toggle_audio_mixer.cpp
        ${mr-webrtc-native-dir}/src/tracked_object.cpp
        ${mr-webrtc-native-dir}/src/utils.cpp
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
        # Link dependencies
        log
        android
        dl
        OpenSLES
        # Tell the linker which symbols to actually make global and which to keep local
        # in the final libmrwebrtc.so, instead of keeping all the global (public) ones.
        # Not strictly necessary, but much cleaner as we control the API and prevent
        # inadvertent leaking of internal symbols.
        -Wl,--version-script=${CMAKE_CURRENT_LIST_DIR}/mrwebrtc_exports.lst
        # Import all public symbols from libwebrtc as if they were public in mrwebrtc too.
        # This is necessary to get the Java_* functions for JNI.
        -Wl,--whole-archive
        # Link libwebrtc as a whole archive, importing all its global symbols as global
        # symbols of libmrwebrtc.so (before version script changes that)
        libwebrtc
        # Disable the --whole-archive option in case (and that happens) some library is
        # appended to the linker command line. Currently "-lm -latomic" follows this, and
        # will make the link fail without this.
        -Wl,--no-whole-archive
)
