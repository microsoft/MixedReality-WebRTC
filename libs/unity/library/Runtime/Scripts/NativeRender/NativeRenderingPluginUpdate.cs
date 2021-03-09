//
// Copyright (C) Microsoft. All rights reserved.
//
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Microsoft.MixedReality.WebRTC.Unity
{
    internal class NativeRenderingPluginUpdate : MonoBehaviour
    {
        private static GameObject _owner;
        private static bool _pluginInitialized;

        private static HashSet<MonoBehaviour> _nativeVideoRenderersRefs = new HashSet<MonoBehaviour>();

        public static void AddRef(MonoBehaviour nativeVideoRenderer)
        {
            Debug.Log("NativeRenderingPluginUpdate AddRef");
            if (_nativeVideoRenderersRefs.Count == 0)
            {
                if (!_pluginInitialized)
                {
                    _pluginInitialized = true;
                    NativeVideo.SetLoggingFunctions(
                        LogDebugCallback,
                        LogErrorCallback,
                        LogWarningCallback);
                    NativeVideo.SetTextureChangeCallback();
                }
                _owner = new GameObject("mrwebrtc-unityplugin");
                _owner.AddComponent<NativeRenderingPluginUpdate>();
            }

            _nativeVideoRenderersRefs.Add(nativeVideoRenderer);

#if WEBRTC_DEBUGGING
            Debug.Log($"NativeRenderingPluginUpdate.AddRef: {_nativeVideoRenderersRefs.Count}");
#endif
        }

        public static void DecRef(MonoBehaviour nativeVideoRenderer)
        {
            _nativeVideoRenderersRefs.Remove(nativeVideoRenderer);

            if (_nativeVideoRenderersRefs.Count == 0)
            {
                Destroy(_owner);
                _owner = null;
            }

#if WEBRTC_DEBUGGING
            Debug.Log($"NativeRenderingPluginUpdate.DecRef: {_nativeVideoRenderersRefs}");
#endif
        }

        private void Start()
        {
            StartCoroutine(nameof(CallPluginAtEndOfFrames));
        }

        private void OnDestroy()
        {
            StopAllCoroutines();
        }

        private IEnumerator CallPluginAtEndOfFrames()
        {
            IntPtr videoUpdateMethod = NativeVideo.GetVideoUpdateMethod();

            while (true)
            {
                // Wait until all frame rendering is done
                yield return new WaitForEndOfFrame();

                // No specific event ID needed, since we only handle one thing.
                if (_nativeVideoRenderersRefs.Count > 0) GL.IssuePluginEvent(videoUpdateMethod, 0);
            }
        }

        [AOT.MonoPInvokeCallback(typeof(LogCallback))]
        public static void LogDebugCallback(string str)
        {
#if WEBRTC_DEBUGGING
            Debug.Log(str);
#endif
        }

        [AOT.MonoPInvokeCallback(typeof(LogCallback))]
        public static void LogWarningCallback(string str)
        {
            Debug.LogWarning(str);
        }

        [AOT.MonoPInvokeCallback(typeof(LogCallback))]
        public static void LogErrorCallback(string str)
        {
            Debug.LogError(str);
        }
    }
}
