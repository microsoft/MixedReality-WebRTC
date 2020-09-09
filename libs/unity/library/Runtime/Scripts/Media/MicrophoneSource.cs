// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Threading;
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
        private TaskCompletionSource<bool> _androidPermissionRequestTcs;
        private object _androidPermissionRequestLock = new object();
#endif

        private AsyncInitHelper<WebRTC.AudioTrackSource> _initHelper = new AsyncInitHelper<WebRTC.AudioTrackSource>();

        protected void OnEnable()
        {
            Debug.Assert(Source == null);
            var cts = new CancellationTokenSource();
            _initHelper.TrackInitTask(InitAsync(cts.Token), cts);
        }

        private async Task<WebRTC.AudioTrackSource> InitAsync(CancellationToken token)
        {
            // Cache public properties on the Unity app thread.
            var deviceConfig = new LocalAudioDeviceInitConfig
            {
                AutoGainControl = _autoGainControl,
            };

            // Continue the task outside the Unity app context, in order to avoid deadlock
            // if OnDisable waits on this task.
            bool accessGranted = await RequestAccessAsync(token).ConfigureAwait(continueOnCapturedContext: false);
            if (!accessGranted)
            {
                return null;
            }
            return await CreateSourceAsync(deviceConfig).ConfigureAwait(continueOnCapturedContext: false);
        }

        // This method might be run outside the app thread and should not access the Unity API.
        private static async Task<WebRTC.AudioTrackSource> CreateSourceAsync(LocalAudioDeviceInitConfig deviceConfig)
        {
            var createTask = DeviceAudioTrackSource.CreateAsync(deviceConfig);
            // Continue the task outside the Unity app context, in order to avoid deadlock
            // if OnDisable waits on this task.
            return await createTask.ConfigureAwait(continueOnCapturedContext: false);
        }

        protected void Update()
        {
            WebRTC.AudioTrackSource source = null;
            try
            {
                source = _initHelper.Result;
            }
            catch (Exception ex)
            {
                Debug.LogError($"Failed to create device track source for {nameof(MicrophoneSource)} component '{name}'.");
                Debug.LogException(ex, this);
            }
            if (source != null)
            {
                AttachSource(source);
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
            lock(_androidPermissionRequestLock)
            {
                if (_androidPermissionRequestTcs != null)
                {
                    if (Permission.HasUserAuthorizedPermission(Permission.Microphone))
                    {
                        // If now authorized, unblock the initialization task.
                        Debug.Log("User granted authorization to access microphone, starting MicrophoneSource now...");
                        _androidPermissionRequestTcs.SetResult(true);
                        _androidPermissionRequestTcs = null;
                    }
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

        private Task<bool> RequestAccessAsync(CancellationToken token)
        {
#if !UNITY_EDITOR && UNITY_ANDROID
            // Ensure Android binding is initialized before accessing the native implementation
            Android.Initialize();

            // Check for permission to access the camera
            if (!Permission.HasUserAuthorizedPermission(Permission.Microphone))
            {
                // Display dialog requesting user permission. This will return immediately,
                // and unfortunately there's no good way to tell when this completes.
                Permission.RequestUserPermission(Permission.Microphone);

                // As a rule of thumb, application should lose focus, so check when focus resumes should
                // be sufficient without having to poll every frame.
                // Monitor the OnApplicationFocus(true) event during the next 5 minutes,
                // and check for permission again each time
                var tcs = new TaskCompletionSource<bool>();
                lock(_androidPermissionRequestLock)
                {
                    Debug.Assert(_androidPermissionRequestTcs == null);
                    _androidPermissionRequestTcs = tcs;
                }
                Task.Delay(TimeSpan.FromMinutes(5)).ContinueWith(_ =>
                {
                    lock (_androidPermissionRequestLock)
                    {
                        // Check if the component is still waiting on the same permission request.
                        // If it has been disabled and then re-enabled, _androidPermissionRequestTcs will be different.
                        if (_androidPermissionRequestTcs == tcs)
                        {
                            Debug.LogError("User denied RecordAudio (microphone) permission; cannot use MicrophoneSource.");
                            _androidPermissionRequestTcs.SetResult(false);
                            _androidPermissionRequestTcs = null;
                        }
                    }
                });

                // If the initialization is canceled, end the task and reset the TCS.
                token.Register(() =>
                {
                    lock (_androidPermissionRequestLock)
                    {
                        // Check if the component is still waiting on the same permission request.
                        // If the request has completed or timed out, _androidPermissionRequestTcs will be null.
                        if (_androidPermissionRequestTcs != null)
                        {
                            Debug.Assert(_androidPermissionRequestTcs == tcs);
                            _androidPermissionRequestTcs.SetCanceled();
                            _androidPermissionRequestTcs = null;
                        }
                    }
                });
                return tcs.Task;
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
