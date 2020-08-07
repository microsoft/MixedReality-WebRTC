// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using Microsoft.MixedReality.WebRTC.Unity.Editor;
using UnityEngine;

#if !UNITY_EDITOR && UNITY_ANDROID
using UnityEngine.Android;
#endif

#if UNITY_WSA && !UNITY_EDITOR
using System.Threading.Tasks;
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

#if !UNITY_EDITOR && UNITY_ANDROID
        protected bool _androidRecordAudioRequestPending = false;
        protected float _androidRecordAudioRequestRetryUntilTime = 0f;
#endif

        protected async void OnEnable()
        {
            if (Source != null)
            {
                return;
            }

#if !UNITY_EDITOR && UNITY_ANDROID
            // Ensure Android binding is initialized before accessing the native implementation
            Android.Initialize();

            // Check for permission to access the camera
            if (!Permission.HasUserAuthorizedPermission(Permission.Microphone))
            {
                if (!_androidRecordAudioRequestPending)
                {
                    // Monitor the OnApplicationFocus(true) event during the next 5 minutes,
                    // and check for permission again each time (see below why).
                    _androidRecordAudioRequestPending = true;
                    _androidRecordAudioRequestRetryUntilTime = Time.time + 300;

                    // Display dialog requesting user permission. This will return immediately,
                    // and unfortunately there's no good way to tell when this completes. As a rule
                    // of thumb, application should lose focus, so check when focus resumes should
                    // be sufficient without having to poll every frame.
                    Permission.RequestUserPermission(Permission.Microphone);
                }
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
            try
            {
                AttachSource(await DeviceAudioTrackSource.CreateAsync(initConfig));
            }
            catch (Exception ex)
            {
                Debug.LogError($"Failed to create device track source for {nameof(MicrophoneSource)} component '{name}'.");
                Debug.LogException(ex, this);
                return;
            }
        }

#if !UNITY_EDITOR && UNITY_ANDROID
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
                    Debug.Log("User granted authorization to access microphone, starting MicrophoneSource now...");
                    OnEnable();
                }
                else if (Time.time <= _androidRecordAudioRequestRetryUntilTime)
                {
                    // OnApplicationFocus(true) may be called for unrelated reason(s) so do not disable on first call,
                    // but instead retry during a given period after the request was made, until we're reasonably
                    // confident that the user dialog was actually answered (that is, that OnApplicationFocus(true) was
                    // called because of that dialog, and not because of another reason).
                    // This may lead to false positives (checking permission after the user denied it), but the user
                    // dialog will not popup again, so this is all in the background and essentially harmless.
                    _androidRecordAudioRequestPending = true;
                }
                else
                {
                    // Some reasonable time passed since we made the permission request, and we still get a denied
                    // answer, so assume the user actually denied it and stop retrying.
                    _androidRecordAudioRequestRetryUntilTime = 0f;
                    Debug.LogError("User denied RecordAudio (microphone) permission; cannot use MicrophoneSource. Forcing enabled=false.");
                    enabled = false;
                }
            }
        }
#endif

        protected void OnDisable()
        {
            DisposeSource();
        }

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
