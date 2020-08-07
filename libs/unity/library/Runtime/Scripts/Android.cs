// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using UnityEngine;

namespace Microsoft.MixedReality.WebRTC.Unity
{
    public static class Android
    {
        /// <summary>
        /// Check if the Android interop layer for Android is already initialized.
        /// </summary>
        public static bool IsInitialized { get; private set; } = false;

        /// <summary>
        /// Initialize the MixedReality-WebRTC library interop layer for Android.
        ///
        /// This is automatically called by the various library API functions, and
        /// can be safely called multiple times (no-op after first call).
        /// </summary>
        public static void Initialize()
        {
#if !UNITY_EDITOR && UNITY_ANDROID
            if (IsInitialized)
            {
                return;
            }

            // See webrtc/examples/unityplugin/ANDROID_INSTRUCTION
            // Below is equivalent of this java code:
            //   PeerConnectionFactory.InitializationOptions.Builder builder = PeerConnectionFactory.InitializationOptions.Builder(UnityPlayer.currentActivity);
            //   builder.setNativeLibraryName("mrwebrtc");
            //   PeerConnectionFactory.InitializationOptions options = builder.createInitializationOptions();
            //   PeerConnectionFactory.initialize(options);
            AndroidJavaClass playerClass = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
            Debug.Assert(playerClass != null);
            AndroidJavaObject activity = playerClass.GetStatic<AndroidJavaObject>("currentActivity");
            Debug.Assert(activity != null);
            Debug.Log("Found Unity Java activity.");
            AndroidJavaClass webrtcClass = new AndroidJavaClass("org.webrtc.PeerConnectionFactory");
            Debug.Assert(webrtcClass != null);
            AndroidJavaClass initOptionsClass = new AndroidJavaClass("org.webrtc.PeerConnectionFactory$InitializationOptions");
            Debug.Assert(initOptionsClass != null);
            AndroidJavaObject builder = initOptionsClass.CallStatic<AndroidJavaObject>("builder", new object[1] { activity });
            Debug.Assert(builder != null);
            builder.Call<AndroidJavaObject>("setNativeLibraryName", new object[1] { "mrwebrtc" });
            AndroidJavaObject options = builder.Call<AndroidJavaObject>("createInitializationOptions");
            webrtcClass.CallStatic("initialize", new object[1] { options });
            IsInitialized = true;
            Debug.Log("Initialized MixedReality-WebRTC Java binding for Android.");
#endif
        }
    }
}
