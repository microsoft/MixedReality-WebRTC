// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using UnityEditor;
using UnityEngine;

namespace Microsoft.MixedReality.WebRTC.Unity.Editor
{
    /// <summary>
    /// Inspector editor for <see cref="LocalVideoSource"/>.
    /// Allows displaying some error message when Mixed Reality Capture is enabled but
    /// XR is not, the later corresponding to a non-exclusive app (2D slate) where MRC
    /// is not available.
    /// </summary>
    [CustomEditor(typeof(LocalVideoSource))]
    [CanEditMultipleObjects]
    public class LocalVideoSourceEditor : UnityEditor.Editor
    {
        SerializedProperty _peerConnection;
        SerializedProperty _autoStartCapture;
        SerializedProperty _preferredVideoCodec;
        SerializedProperty _enableMixedRealityCapture;
        SerializedProperty _autoAddTrack;

        /// <summary>
        /// Helper enumeration for commonly used video codecs.
        /// The enum names must match exactly the standard SDP naming.
        /// See https://en.wikipedia.org/wiki/RTP_audio_video_profile for reference.
        /// </summary>
        enum SdpVideoCodecs
        {
            /// <summary>
            /// Do not force any codec, let WebRTC decide.
            /// </summary>
            None,

            /// <summary>
            /// Try to use H.264 if available.
            /// </summary>
            H264,

            /// <summary>
            /// Try to use H.265 if available.
            /// </summary>
            H265,

            /// <summary>
            /// Try to use VP8 if available.
            /// </summary>
            VP8,

            /// <summary>
            /// Try to use VP9 if available.
            /// </summary>
            VP9,

            //Custom //< TODO
        }

        /// <summary>
        /// Implementation of <a href="https://docs.unity3d.com/ScriptReference/ScriptableObject.Awake.html">MonoBehaviour.Awake</a>
        /// to cache the <a href="https://docs.unity3d.com/ScriptReference/SerializedProperty.html">SerializedProperty</a> pointing
        /// to the individual fields of the <see cref="LocalVideoSource"/>.
        /// </summary>
        void OnEnable()
        {
            _peerConnection = serializedObject.FindProperty("PeerConnection");
            _autoStartCapture = serializedObject.FindProperty("AutoStartCapture");
            _preferredVideoCodec = serializedObject.FindProperty("PreferredVideoCodec");
            _enableMixedRealityCapture = serializedObject.FindProperty("EnableMixedRealityCapture");
            _autoAddTrack = serializedObject.FindProperty("AutoAddTrack");
        }

        /// <summary>
        /// Override implementation of <a href="https://docs.unity3d.com/ScriptReference/Editor.OnInspectorGUI.html">Editor.OnInspectorGUI</a>
        /// to draw the inspector GUI for the currently selected <see cref="LocalVideoSource"/>.
        /// </summary>
        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            GUILayout.Space(10);
            EditorGUILayout.PropertyField(_peerConnection);
            EditorGUILayout.PropertyField(_autoAddTrack);
            EditorGUILayout.PropertyField(_autoStartCapture);

            try
            {
                // Convert the selected codec name to an enum value.
                // This may throw an exception if this is a custom name, which will be handled below.
                SdpVideoCodecs codecValue;
                if (_preferredVideoCodec.stringValue.Length == 0)
                {
                    codecValue = SdpVideoCodecs.None;
                }
                else
                {
                    codecValue = (SdpVideoCodecs)System.Enum.Parse(typeof(SdpVideoCodecs), _preferredVideoCodec.stringValue);
                }

                // Display the edit field for the enum
                EditorGUILayout.BeginHorizontal();
                GUILayout.Label(_preferredVideoCodec.displayName);
                var newCodecValue = (SdpVideoCodecs)EditorGUILayout.EnumPopup(codecValue);
                EditorGUILayout.EndHorizontal();

                // Update the value if changed
                if (newCodecValue != codecValue)
                {
                    if (newCodecValue == SdpVideoCodecs.None)
                    {
                        _preferredVideoCodec.stringValue = string.Empty;
                    }
                    //else if (newCodecValue == SdpVideoCodecs.Custom) //< TODO
                    //{
                    //    _preferredVideoCodec.stringValue = EditorGUILayout.TextField("SDP codec name", _preferredVideoCodec.stringValue);
                    //}
                    else
                    {
                        _preferredVideoCodec.stringValue = System.Enum.GetName(typeof(SdpVideoCodecs), newCodecValue);
                    }
                }
            }
            catch (Exception)
            {
                EditorGUILayout.PropertyField(_preferredVideoCodec);
            }

            EditorGUILayout.PropertyField(_enableMixedRealityCapture);
            if (_enableMixedRealityCapture.boolValue && !PlayerSettings.virtualRealitySupported)
            {
                EditorGUILayout.HelpBox("Mixed Reality Capture can only work in exclusive-mode apps. XR support must be enabled in Project Settings > Player > XR Settings > Virtual Reality Supported, and the project then saved to disk.", MessageType.Error);
                if (GUILayout.Button("Enable XR support"))
                {
                    PlayerSettings.virtualRealitySupported = true;
                }
            }

            serializedObject.ApplyModifiedProperties();
        }
    }
}
