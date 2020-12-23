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

  public static @Nullable VideoCapturer StartCapture(
      long nativeTrackSource, SurfaceTextureHelper surfaceTextureHelper, String deviceName, int width, int height,
      float framerate) {
    CameraEnumerator enumerator = new Camera2Enumerator(ContextUtils.getApplicationContext());

    // Capture format uses some fixed-point framerate with 3 decimals
    final int framerateInt = (framerate < 1.0f) ? 0 : Math.round(framerate * 1000.0f);

    // Find the capture device by name
    final String[] deviceNames = enumerator.getDeviceNames();
    final boolean hasDeviceName = (deviceName != null) && !deviceName.isEmpty();
    CaptureFormat captureFormat = null;
    for (String name : deviceNames) {
      // Ignore devices with mismatching name only if a name was specified
      if (hasDeviceName && (!deviceName.equals(name))) {
        continue;
      }

      // Match constraints with existing capture format
      captureFormat = null;
      final List<CaptureFormat> formats = enumerator.getSupportedFormats(name);
      if (formats == null) {
        continue;
      }
      for (CaptureFormat format : formats) {
        if ((width > 0) && (format.width != width)) {
          continue;
        }
        if ((height > 0) && (format.height != height)) {
          continue;
        }
        if ((framerateInt > 0) && ((framerateInt < format.framerate.min) || (framerateInt > format.framerate.max))) {
          continue;
        }
        // Found compatible format; also save device name in case it was not specified
        Logging.d(TAG, String.format("Found video capture device '%s' with format (%d x %d @ [%f:%f] fps)",
                name, format.width, format.height, format.framerate.min / 1000.0f,
                format.framerate.max / 1000.0f));
        captureFormat = format;
        deviceName = name;
        break;
      }
      if (hasDeviceName && (captureFormat == null)) {
        // Device was matched by name but no format was matched
        Logging.e(TAG, String.format(
                "Failed to find matching capture format for device '%s' with constraints w=%d h=%d fps=%f(%d).",
                deviceName, width, height, framerate, framerateInt));
        return null;
      }
    }
    if (captureFormat == null) {
      if (hasDeviceName) {
        Logging.e(TAG, "Failed to find matching video capture device for name '" + deviceName + "'");
      } else {
        Logging.e(TAG, "Failed to find any compatible video capture device");
      }
      return null;
    }

    VideoCapturer videoCapturer = enumerator.createCapturer(deviceName, null);
    if (videoCapturer == null) {
      Logging.e(TAG, "Failed to create a video capturer for device '" + deviceName + '"');
      return null;
    }

    VideoSource videoSource = new VideoSource(nativeTrackSource);
    videoCapturer.initialize(surfaceTextureHelper, ContextUtils.getApplicationContext(),
        videoSource.getCapturerObserver());
    videoCapturer.startCapture(captureFormat.width, captureFormat.height, captureFormat.framerate.max);
    return videoCapturer;
  }

  public static void StopCamera(VideoCapturer camera) throws InterruptedException {
    camera.stopCapture();
    camera.dispose();
  }
}
