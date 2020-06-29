// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using UnityEditor;
using UnityEngine;

namespace Microsoft.MixedReality.WebRTC.Unity.Editor
{
    /// <summary>
    /// Inspector editor for <see cref="VideoRenderer"/>.
    /// </summary>
    [CustomEditor(typeof(RemoteVideoRenderer))]
    [CanEditMultipleObjects]
    public class VideoRendererEditor : UnityEditor.Editor
    {
        //SerializedProperty _source;
        SerializedProperty _maxFramerate;
        SerializedProperty _enableStatistics;
        SerializedProperty _frameLoadStatHolder;
        SerializedProperty _framePresentStatHolder;
        SerializedProperty _frameSkipStatHolder;

        void OnEnable()
        {
            //_source = serializedObject.FindProperty("Source");
            _maxFramerate = serializedObject.FindProperty("_widget.MaxFramerate");
            _enableStatistics = serializedObject.FindProperty("_widget.EnableStatistics");
            _frameLoadStatHolder = serializedObject.FindProperty("_widget.FrameLoadStatHolder");
            _framePresentStatHolder = serializedObject.FindProperty("_widget.FramePresentStatHolder");
            _frameSkipStatHolder = serializedObject.FindProperty("_widget.FrameSkipStatHolder");
        }

        /// <summary>
        /// Override implementation of <a href="https://docs.unity3d.com/ScriptReference/Editor.OnInspectorGUI.html">Editor.OnInspectorGUI</a>
        /// to draw the inspector GUI for the currently selected <see cref="MicrophoneSource"/>.
        /// </summary>
        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            GUILayout.Space(10);

            EditorGUILayout.LabelField("Video", EditorStyles.boldLabel);
            //EditorGUILayout.PropertyField(_source);
            EditorGUILayout.PropertyField(_maxFramerate);

            GUILayout.Space(10);

            EditorGUILayout.PropertyField(_enableStatistics);
            if (_enableStatistics.boolValue)
            {
                using (new EditorGUI.IndentLevelScope())
                {
                    EditorGUILayout.PropertyField(_frameLoadStatHolder);
                    EditorGUILayout.PropertyField(_framePresentStatHolder);
                    EditorGUILayout.PropertyField(_frameSkipStatHolder);
                }
            }

            serializedObject.ApplyModifiedProperties();
        }
    }
}
