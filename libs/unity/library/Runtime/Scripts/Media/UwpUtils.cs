// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Threading.Tasks;
using UnityEngine;

#if UNITY_WSA && !UNITY_EDITOR
using global::Windows.UI.Core;
using global::Windows.Foundation;
using global::Windows.Media.Core;
using global::Windows.Media.Capture;
using global::Windows.ApplicationModel.Core;

namespace Microsoft.MixedReality.WebRTC.Unity
{
    internal static class UwpUtils
    {
        internal static async Task<bool> RequestAccessAsync(StreamingCaptureMode mode)
        {
            // Note that the UWP UI thread and the main Unity app thread are always different.
            // https://docs.unity3d.com/Manual/windowsstore-appcallbacks.html
            Debug.Assert(!UnityEngine.WSA.Application.RunningOnUIThread());
            var permissionTcs = new TaskCompletionSource<bool>();
            UnityEngine.WSA.Application.InvokeOnUIThread(() =>
            {
                // Request UWP access to audio capture. The OS may show some popup dialog to the
                // user to request permission. This will succeed only if the user grants permission.
                try
                {
                    // On UWP the app must have the "microphone" capability, and the user must allow microphone
                    // access. So check that access before trying to initialize the WebRTC library, as this
                    // may result in a popup window being displayed the first time, which needs to be accepted
                    // before the microphone can be accessed by WebRTC.
                    var mediaAccessRequester = new MediaCapture();
                    var mediaSettings = new MediaCaptureInitializationSettings();
                    mediaSettings.AudioDeviceId = "";
                    mediaSettings.VideoDeviceId = "";
                    mediaSettings.StreamingCaptureMode = mode;
                    mediaSettings.PhotoCaptureSource = PhotoCaptureSource.VideoPreview;
                    mediaSettings.SharingMode = MediaCaptureSharingMode.SharedReadOnly; // for MRC and lower res camera
                    mediaAccessRequester.InitializeAsync(mediaSettings).AsTask().ContinueWith(task =>
                    {
                        if (task.Exception != null)
                        {
                            Debug.LogError($"Audio access failure: {task.Exception.InnerException.Message}.");
                            permissionTcs.SetResult(false);
                        }
                        else
                        {
                            permissionTcs.SetResult(true);
                        }
                    });
                }
                catch (Exception ex)
                {
                    // Log an error and prevent activation
                    Debug.LogError($"Audio access failure: {ex.Message}.");
                    permissionTcs.SetResult(false);
                }
            },
            waitUntilDone: false);
            return await permissionTcs.Task.ConfigureAwait(false);
        }
    }
}
#endif
