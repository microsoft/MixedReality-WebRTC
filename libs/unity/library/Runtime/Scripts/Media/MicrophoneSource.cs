// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Threading.Tasks;
using Microsoft.MixedReality.WebRTC.Unity.Editor;
using UnityEngine;
using UnityEngine.Android;

#if UNITY_WSA && !UNITY_EDITOR
using global::Windows.UI.Core;
using global::Windows.Foundation;
using global::Windows.Media.Core;
using global::Windows.Media.Capture;
using global::Windows.ApplicationModel.Core;
#endif

namespace Microsoft.MixedReality.WebRTC.Unity
{
    /// <summary>
    /// This component represents a local audio source generating audio frames from a local
    /// audio capture device (microphone). The audio source can be used to create one or more
    /// audio tracks sharing the same audio content.
    /// </summary>
    [AddComponentMenu("MixedReality-WebRTC/Microphone Source")]
    public class MicrophoneSource : AudioTrackSource
    {
        public bool AutoGainControl => _autoGainControl;

        [SerializeField]
        [Tooltip("Enable automated gain control")]
        [ToggleLeft]
        protected bool _autoGainControl = true;

#if PLATFORM_ANDROID
        protected bool _androidRecordAudioRequestPending = false;
        protected bool _androidEnabledRequestPending = false;
#endif

        protected async void OnEnable()
        {
            if (Source != null)
            {
                return;
            }

#if PLATFORM_ANDROID
            // Check for permission to access the camera
            _androidEnabledRequestPending = false;
            if (!Permission.HasUserAuthorizedPermission(Permission.Microphone))
            {
                if (!_androidRecordAudioRequestPending)
                {
                    _androidRecordAudioRequestPending = true;

                    // Display dialog requesting user permission. This will return immediately,
                    // and unfortunately there's no good way to tell when this completes. As a rule
                    // of thumb, application should lose focus, so check when focus resumes should
                    // be sufficient without having to poll every frame.
                    Permission.RequestUserPermission(Permission.Microphone);
                }
                _androidEnabledRequestPending = true;
                return;
            }
#endif

#if UNITY_WSA && !UNITY_EDITOR
            // Request access to audio capture. The OS may show some popup dialog to the
            // user to request permission. This will succeed only if the user approves it.
            try
            {
                if (UnityEngine.WSA.Application.RunningOnUIThread())
                {
                    await RequestAccessAsync();
                }
                else
                {
                    UnityEngine.WSA.Application.InvokeOnUIThread(() => RequestAccessAsync(), waitUntilDone: true);
                }
            }
            catch (Exception ex)
            {
                // Log an error and prevent activation
                Debug.LogError($"Audio access failure: {ex.Message}.");
                this.enabled = false;
                return;
            }
#endif

            var initConfig = new LocalAudioDeviceInitConfig
            {
                AutoGainControl = _autoGainControl,
            };
            Source = await WebRTC.AudioTrackSource.CreateFromDeviceAsync(initConfig);
            if (Source == null)
            {
                throw new Exception("Failed ot create microphone audio source.");
            }

            IsStreaming = true;
            AudioSourceStarted.Invoke(this);
        }

#if PLATFORM_ANDROID
        protected void OnApplicationFocus(bool hasFocus)
        {
            if (!hasFocus)
            {
                return;
            }

            // If focus is restored after a pending request, check the permission again
            if (_androidRecordAudioRequestPending)
            {
                _androidRecordAudioRequestPending = false;

                if (Permission.HasUserAuthorizedPermission(Permission.Microphone))
                {
                    // If now authorized, start capture as if just enabled
                    if (_androidEnabledRequestPending)
                    {
                        OnEnable();
                    }
                }
                else
                {
                    // If still denied, disable this component
                    Debug.LogError("User denied RecordAudio (microphone) permission; cannot use MicrophoneSource.");
                    enabled = false;
                }
            }
        }
#endif

#if UNITY_WSA && !UNITY_EDITOR
        /// <summary>
        /// Internal UWP helper to ensure device access.
        /// </summary>
        /// <remarks>
        /// This must be called from the main UWP UI thread (not the main Unity app thread).
        /// </remarks>
        private Task RequestAccessAsync()
        {
            // On UWP the app must have the "microphone" capability, and the user must allow microphone
            // access. So check that access before trying to initialize the WebRTC library, as this
            // may result in a popup window being displayed the first time, which needs to be accepted
            // before the microphone can be accessed by WebRTC.
            var mediaAccessRequester = new MediaCapture();
            var mediaSettings = new MediaCaptureInitializationSettings();
            mediaSettings.AudioDeviceId = "";
            mediaSettings.VideoDeviceId = "";
            mediaSettings.StreamingCaptureMode = StreamingCaptureMode.Audio;
            mediaSettings.PhotoCaptureSource = PhotoCaptureSource.VideoPreview;
            mediaSettings.SharingMode = MediaCaptureSharingMode.SharedReadOnly; // for MRC and lower res camera
            return mediaAccessRequester.InitializeAsync(mediaSettings).AsTask();
        }
#endif
    }
}
