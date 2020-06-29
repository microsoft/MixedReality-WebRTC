// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

// This file is originally based on the UnityUtility.java file from the
// WebRTC.org project, modified for the needs of the MixedReality-WebRTC
// project and expanded with additional functionalities.

// UnityUtility.java:
/*
 *  Copyright 2017 The WebRTC project authors. All Rights Reserved.
 *
 *  Use of this source code is governed by a BSD-style license
 *  that can be found in the LICENSE file in the root of the source
 *  tree. An additional intellectual property rights grant can be found
 *  in the file PATENTS.  All contributing project authors may
 *  be found in the AUTHORS file in the root of the source tree.
 */

package com.microsoft.mixedreality.webrtc;

import android.content.Context;
import android.graphics.ImageFormat;

import java.util.List;
import javax.annotation.Nullable;
import org.webrtc.CameraEnumerationAndroid.CaptureFormat;
import org.webrtc.CameraEnumerator;
import org.webrtc.Camera2Enumerator;
import org.webrtc.ContextUtils;
import org.webrtc.Logging;
import org.webrtc.SurfaceTextureHelper;
import org.webrtc.VideoCapturer;
import org.webrtc.VideoSource;

public class AndroidCameraInterop {
  private static final String TAG = "AndroidCameraInterop";
  private static final String VIDEO_CAPTURER_THREAD_NAME = "VideoCapturerThread";

  public static SurfaceTextureHelper CreateSurfaceTextureHelper() {
    final SurfaceTextureHelper surfaceTextureHelper =
        SurfaceTextureHelper.create(VIDEO_CAPTURER_THREAD_NAME, null);
    return surfaceTextureHelper;
  }

  private static boolean useCamera2() {
    return Camera2Enumerator.isSupported(ContextUtils.getApplicationContext());
  }

  private static @Nullable VideoCapturer createCameraCapturer(CameraEnumerator enumerator) {
    final String[] deviceNames = enumerator.getDeviceNames();

    for (String deviceName : deviceNames) {
      if (enumerator.isFrontFacing(deviceName)) {
        VideoCapturer videoCapturer = enumerator.createCapturer(deviceName, null);
        if (videoCapturer != null) {
          return videoCapturer;
        }
      }
    }

    return null;
  }

  public static VideoCaptureDeviceInfo[] GetVideoCaptureDevices() {
    CameraEnumerator enumerator = new Camera2Enumerator(ContextUtils.getApplicationContext());
    final String[] deviceNames = enumerator.getDeviceNames();
    VideoCaptureDeviceInfo[] deviceInfos = new VideoCaptureDeviceInfo[deviceNames.length];
    int index = 0;
    for (String name : deviceNames) {
      // For lack of a better solution, return the device name in both fields
      VideoCaptureDeviceInfo deviceInfo = new VideoCaptureDeviceInfo();
      deviceInfo.id = name;
      deviceInfo.name = name;
      deviceInfos[index++] = deviceInfo;
    }
    return deviceInfos;
  }

  public static VideoCaptureFormatInfo[] GetVideoCaptureFormats(String deviceId) {
    CameraEnumerator enumerator = new Camera2Enumerator(ContextUtils.getApplicationContext());
    final List<CaptureFormat> formats = enumerator.getSupportedFormats(deviceId);
    if (formats == null) {
      return null;
    }
    VideoCaptureFormatInfo[] formatInfos = new VideoCaptureFormatInfo[formats.size()];
    int index = 0;
    for (CaptureFormat format : formats) {
      VideoCaptureFormatInfo formatInfo = new VideoCaptureFormatInfo();
      formatInfo.width = format.width;
      formatInfo.height = format.height;
      // Framerate range is stored in thousand-frame-per-second (as integer)
      formatInfo.framerate = (float)format.framerate.max / 1000.0F;
      if (format.imageFormat == ImageFormat.NV21) {
        // This is currently hard-coded in the implementation so should be the only value returned.
        formatInfo.fourcc = 0x3132564E; // NV21
      } else {
        Logging.d(TAG, "Unknown FOURCC for Android image format #" + format.imageFormat);
        formatInfo.fourcc = 0;
      }
      formatInfos[index++] = formatInfo;
    }
    return formatInfos;
  }

  public static VideoCapturer StartCapture(
      long nativeTrackSource, SurfaceTextureHelper surfaceTextureHelper, int width, int height,
      int framerate) {
    VideoCapturer capturer =
        createCameraCapturer(new Camera2Enumerator(ContextUtils.getApplicationContext()));

    VideoSource videoSource = new VideoSource(nativeTrackSource);

    capturer.initialize(surfaceTextureHelper, ContextUtils.getApplicationContext(),
        videoSource.getCapturerObserver());

    // Set default values if not specified (<= 0).
    // TODO: Resolve partial resolution constraint to a supported resolution
    if (width <= 0) {
      width = 720;
    }
    if (height <= 0) {
      height = 480;
    }
    if (framerate <= 0) {
      framerate = 30;
    }

    capturer.startCapture(width, height, framerate);
    return capturer;
  }

  public static void StopCamera(VideoCapturer camera) throws InterruptedException {
    camera.stopCapture();
    camera.dispose();
  }
}
