// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using UnityEditor;
using UnityEngine;

namespace Microsoft.MixedReality.WebRTC.Unity.Editor
{
    /// <summary>
    /// Inspector editor for <see cref="MicrophoneSource"/>.
    /// </summary>
    [CustomEditor(typeof(MicrophoneSource))]
    [CanEditMultipleObjects]
    public class MicrophoneSourceEditor : UnityEditor.Editor
    {
        SerializedProperty _trackName;
        SerializedProperty _autoStartOnEnabled;
        SerializedProperty _preferredAudioCodec;
        SerializedProperty _audioStreamStarted;
        SerializedProperty _audioStreamStopped;

        void OnEnable()
        {
            _trackName = serializedObject.FindProperty("TrackName");
            _autoStartOnEnabled = serializedObject.FindProperty("AutoStartOnEnabled");
            _preferredAudioCodec = serializedObject.FindProperty("PreferredAudioCodec");
            _audioStreamStarted = serializedObject.FindProperty("AudioStreamStarted");
            _audioStreamStopped = serializedObject.FindProperty("AudioStreamStopped");
        }

        /// <summary>
        /// Override implementation of <a href="https://docs.unity3d.com/ScriptReference/Editor.OnInspectorGUI.html">Editor.OnInspectorGUI</a>
        /// to draw the inspector GUI for the currently selected <see cref="MicrophoneSource"/>.
        /// </summary>
        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            GUILayout.Space(10);

            EditorGUILayout.LabelField("Audio capture", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(_autoStartOnEnabled,
                new GUIContent("Start capture when enabled", "Automatically start audio capture when this component is enabled."));
            EditorGUILayout.PropertyField(_audioStreamStarted);
            EditorGUILayout.PropertyField(_audioStreamStopped);

            GUILayout.Space(10);

            EditorGUILayout.LabelField("WebRTC track", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(_trackName);
            EditorGUILayout.PropertyField(_preferredAudioCodec);

            serializedObject.ApplyModifiedProperties();
        }
    }
}
