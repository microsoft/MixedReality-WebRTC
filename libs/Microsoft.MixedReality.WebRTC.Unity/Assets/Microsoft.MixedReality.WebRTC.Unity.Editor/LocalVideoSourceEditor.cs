// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

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
        SerializedProperty _enableMixedRealityCapture;
        SerializedProperty _autoAddTrack;

        /// <summary>
        /// Implementation of <a href="https://docs.unity3d.com/ScriptReference/ScriptableObject.Awake.html">MonoBehaviour.Awake</a>
        /// to cache the <a href="https://docs.unity3d.com/ScriptReference/SerializedProperty.html">SerializedProperty</a> pointing
        /// to the individual fields of the <see cref="LocalVideoSource"/>.
        /// </summary>
        void OnEnable()
        {
            _peerConnection = serializedObject.FindProperty("PeerConnection");
            _autoStartCapture = serializedObject.FindProperty("AutoStartCapture");
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
