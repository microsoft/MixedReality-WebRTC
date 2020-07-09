// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

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
        SerializedProperty _autoGainControl;
        SerializedProperty _audioSourceStopped;

        void OnEnable()
        {
            _autoGainControl = serializedObject.FindProperty("_autoGainControl");
        }

        /// <summary>
        /// Override implementation of <a href="https://docs.unity3d.com/ScriptReference/Editor.OnInspectorGUI.html">Editor.OnInspectorGUI</a>
        /// to draw the inspector GUI for the currently selected <see cref="MicrophoneSource"/>.
        /// </summary>
        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            if (!PlayerSettings.WSA.GetCapability(PlayerSettings.WSACapability.Microphone))
            {
                EditorGUILayout.HelpBox("The UWP player is missing the Microphone capability. The MicrophoneSource component will not function correctly."
                    + " Add the Microphone capability in Project Settings > Player > UWP > Publishing Settings > Capabilities.", MessageType.Error);
                if (GUILayout.Button("Open Player Settings"))
                {
                    SettingsService.OpenProjectSettings("Project/Player");
                }
                if (GUILayout.Button("Add Microphone Capability"))
                {
                    PlayerSettings.WSA.SetCapability(PlayerSettings.WSACapability.Microphone, true);
                }
            }

            GUILayout.Space(10);

            EditorGUILayout.LabelField("Audio processing", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(_autoGainControl);

            serializedObject.ApplyModifiedProperties();
        }
    }
}
