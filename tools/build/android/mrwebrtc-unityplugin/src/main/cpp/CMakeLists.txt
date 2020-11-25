# Copyright (c) Microsoft Corporation.
# Licensed under the MIT License.

cmake_minimum_required(VERSION 3.10.2)

string(TOLOWER ${CMAKE_BUILD_TYPE} lower_cmake_build_type)

#---------------------------------------------------------------------
# Import libmrwebrtc.so shared library.
#

# Declare and configure the library.
add_library(
        mrwebrtc
        SHARED IMPORTED
)
set_target_properties(
        mrwebrtc
        PROPERTIES
        IMPORTED_LOCATION ../../../../../webrtc-native/build/intermediates/cmake/${lower_cmake_build_type}/obj/${ANDROID_ABI}/libmrwebrtc.so
)

#---------------------------------------------------------------------
# Configure libmrwebrtc-unityplugin.so build.
#

# Base path to Microsoft.MixedReality.WebRTC.Native project.
set(mrwebrtc-dir ../../../../../../../libs/mrwebrtc)
# Base path to Microsoft.MixedReality.WebRTC.UnityPlugin.Native project.
set(mrwebrtc-unityplugin-dir ../../../../../../../libs/mrwebrtc-unityplugin)

# List of source files to compile.
add_library(
        mrwebrtc-unityplugin
        SHARED
        ${mrwebrtc-unityplugin-dir}/src/api.cpp
        ${mrwebrtc-unityplugin-dir}/src/log_helpers.cpp
        ${mrwebrtc-unityplugin-dir}/src/native_renderer.cpp
        ${mrwebrtc-unityplugin-dir}/src/pch.cpp
        ${mrwebrtc-unityplugin-dir}/src/render_api.cpp
        ${mrwebrtc-unityplugin-dir}/src/render_api_OpenGLCoreES.cpp
)

# List of include directories.
target_include_directories(
        mrwebrtc-unityplugin
        PRIVATE
        ${mrwebrtc-dir}/src
        ${mrwebrtc-dir}/include
        ${mrwebrtc-unityplugin-dir}/src
        ${mrwebrtc-unityplugin-dir}/include
)

# Build and link Microsoft.MixedReality.WebRTC.Native.so.
target_link_libraries(
        mrwebrtc-unityplugin
        log
        android
        dl
        OpenSLES
        mrwebrtc
        GLESv3
)
