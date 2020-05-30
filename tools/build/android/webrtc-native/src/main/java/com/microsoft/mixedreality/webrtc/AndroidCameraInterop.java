// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

// This file is originaly based on the UnityUtility.java file from the
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
import java.util.List;
import javax.annotation.Nullable;
import org.webrtc.CameraEnumerator;
import org.webrtc.Camera2Enumerator;
import org.webrtc.ContextUtils;
import org.webrtc.SurfaceTextureHelper;
import org.webrtc.VideoCapturer;
import org.webrtc.VideoSource;

public class AndroidCameraInterop {
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

  public static VideoCapturer StartCapture(
      long nativeTrackSource, SurfaceTextureHelper surfaceTextureHelper) {
    VideoCapturer capturer =
        createCameraCapturer(new Camera2Enumerator(ContextUtils.getApplicationContext()));

    VideoSource videoSource = new VideoSource(nativeTrackSource);

    capturer.initialize(surfaceTextureHelper, ContextUtils.getApplicationContext(),
        videoSource.getCapturerObserver());

    capturer.startCapture(720, 480, 30);
    return capturer;
  }

  public static void StopCamera(VideoCapturer camera) throws InterruptedException {
    camera.stopCapture();
    camera.dispose();
  }
}
