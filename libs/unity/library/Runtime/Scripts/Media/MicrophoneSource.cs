// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Threading.Tasks;
using Microsoft.MixedReality.WebRTC.Unity.Editor;
using UnityEngine;

#if !UNITY_EDITOR && UNITY_ANDROID
using UnityEngine.Android;
#endif

#if UNITY_WSA && !UNITY_EDITOR
using global::Windows.Media.Capture;
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
        protected TaskCompletionSource<bool> _androidPermissionRequestTcs;
        protected float _androidRecordAudioRequestRetryUntilTime = 0f;
#endif

        private AsyncInitHelper<WebRTC.AudioTrackSource> _initHelper = new AsyncInitHelper<WebRTC.AudioTrackSource>();

        protected void OnEnable()
        {
            Debug.Assert(Source == null);
            _initHelper.TrackInitTask(InitAsync());
        }

        protected async Task<WebRTC.AudioTrackSource> InitAsync()
        {
            // Continue the task outside the Unity app context, in order to avoid deadlock
            // if OnDisable waits on this task.
            bool accessGranted = await RequestAccessAsync().ConfigureAwait(continueOnCapturedContext: false);
            if (!accessGranted)
            {
                return null;
            }

            var initConfig = new LocalAudioDeviceInitConfig
            {
                AutoGainControl = _autoGainControl,
            };
            try
            {
                var createTask = DeviceAudioTrackSource.CreateAsync(initConfig);
                // Continue the task outside the Unity app context, in order to avoid deadlock
                // if OnDisable waits on this task.
                return await createTask.ConfigureAwait(continueOnCapturedContext: false);
            }
            catch (Exception ex)
            {
                Debug.LogError($"Failed to create device track source for {nameof(MicrophoneSource)} component '{name}'.");
                Debug.LogException(ex, this);
                throw ex;
            }
        }

        protected void Update()
        {
            var result = _initHelper.Result;
            if (result != null)
            {
                AttachSource(result);
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
            if (_androidPermissionRequestTcs != null)
            {
                if (Permission.HasUserAuthorizedPermission(Permission.Microphone))
                {
                    // If now authorized, start capture as if just enabled
                    Debug.Log("User granted authorization to access microphone, starting MicrophoneSource now...");
                    _androidPermissionRequestTcs.SetResult(true);
                    _androidPermissionRequestTcs = null;
                }
                else if (Time.time > _androidRecordAudioRequestRetryUntilTime)
                {
                    // OnApplicationFocus(true) may be called for unrelated reason(s) so do not disable on first call,
                    // but instead retry during a given period after the request was made, until we're reasonably
                    // confident that the user dialog was actually answered (that is, that OnApplicationFocus(true) was
                    // called because of that dialog, and not because of another reason).
                    // This may lead to false positives (checking permission after the user denied it), but the user
                    // dialog will not popup again, so this is all in the background and essentially harmless.
                    // Here some reasonable time passed since we made the permission request, and we still get a denied
                    // answer, so assume the user actually denied it and stop retrying.
                    _androidPermissionRequestTcs.SetResult(false);
                    _androidPermissionRequestTcs = null;
                    _androidRecordAudioRequestRetryUntilTime = 0f;
                    Debug.LogError("User denied RecordAudio (microphone) permission; cannot use MicrophoneSource.");
                }
            }
        }
#endif

        protected void OnDisable()
        {
            /// Wait synchronously for the end of the initialization task, so that after the component
            /// has been disabled the device is released and free to be used again.
            /// Note that the initialization task needs not to be continued on the app thread, or it
            /// will deadlock.
            _initHelper.AbortInitTask().Wait();
            DisposeSource();
        }

        private Task<bool> RequestAccessAsync()
        {
#if !UNITY_EDITOR && UNITY_ANDROID
            // Ensure Android binding is initialized before accessing the native implementation
            Android.Initialize();

            // Check for permission to access the camera
            if (!Permission.HasUserAuthorizedPermission(Permission.Microphone))
            {
                Debug.Assert(_androidPermissionRequestTcs == null);

                // Monitor the OnApplicationFocus(true) event during the next 5 minutes,
                // and check for permission again each time (see below why).
                _androidPermissionRequestTcs = new TaskCompletionSource<bool>();
                _androidRecordAudioRequestRetryUntilTime = Time.time + 300;

                // Display dialog requesting user permission. This will return immediately,
                // and unfortunately there's no good way to tell when this completes. As a rule
                // of thumb, application should lose focus, so check when focus resumes should
                // be sufficient without having to poll every frame.
                Permission.RequestUserPermission(Permission.Microphone);
                return _androidPermissionRequestTcs.Task;
            }

            // Already has permission.
            return Task.FromResult(true);
#elif UNITY_WSA && !UNITY_EDITOR
            return UwpUtils.RequestAccessAsync(StreamingCaptureMode.Audio);
#else
            return Task.FromResult(true);
#endif
        }
    }
}
