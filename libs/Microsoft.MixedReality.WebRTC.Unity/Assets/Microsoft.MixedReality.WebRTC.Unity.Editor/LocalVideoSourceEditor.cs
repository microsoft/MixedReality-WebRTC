// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

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
        SerializedProperty _trackName;
        SerializedProperty _autoStartCapture;
        SerializedProperty _preferredVideoCodec;
        SerializedProperty _enableMixedRealityCapture;
        SerializedProperty _enableMrcRecordingIndicator;
        SerializedProperty _autoAddTrack;
        SerializedProperty _formatMode;
        SerializedProperty _videoProfileId;
        SerializedProperty _videoProfileKind;
        SerializedProperty _constraints;
        SerializedProperty _width;
        SerializedProperty _height;
        SerializedProperty _framerate;

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

            /// <summary>
            /// Try to use the given codec if available.
            /// </summary>
            Custom
        }

        /// <summary>
        /// Implementation of <a href="https://docs.unity3d.com/ScriptReference/ScriptableObject.Awake.html">MonoBehaviour.Awake</a>
        /// to cache the <a href="https://docs.unity3d.com/ScriptReference/SerializedProperty.html">SerializedProperty</a> pointing
        /// to the individual fields of the <see cref="LocalVideoSource"/>.
        /// </summary>
        void OnEnable()
        {
            _peerConnection = serializedObject.FindProperty("PeerConnection");
            _trackName = serializedObject.FindProperty("TrackName");
            _autoStartCapture = serializedObject.FindProperty("AutoStartCapture");
            _preferredVideoCodec = serializedObject.FindProperty("PreferredVideoCodec");
            _enableMixedRealityCapture = serializedObject.FindProperty("EnableMixedRealityCapture");
            _enableMrcRecordingIndicator = serializedObject.FindProperty("EnableMRCRecordingIndicator");
            _autoAddTrack = serializedObject.FindProperty("AutoAddTrack");
            _formatMode = serializedObject.FindProperty("FormatMode");
            _videoProfileId = serializedObject.FindProperty("VideoProfileId");
            _videoProfileKind = serializedObject.FindProperty("VideoProfileKind");
            _constraints = serializedObject.FindProperty("Constraints");
            _width = _constraints.FindPropertyRelative("width");
            _height = _constraints.FindPropertyRelative("height");
            _framerate = _constraints.FindPropertyRelative("framerate");
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
            EditorGUILayout.PropertyField(_trackName);
            EditorGUILayout.PropertyField(_autoAddTrack);
            EditorGUILayout.PropertyField(_autoStartCapture);

            EditorGUILayout.PropertyField(_enableMixedRealityCapture);
            if (_enableMixedRealityCapture.boolValue)
            {
                EditorGUILayout.PropertyField(_enableMrcRecordingIndicator);
                if (!PlayerSettings.virtualRealitySupported)
                {
                    EditorGUILayout.HelpBox("Mixed Reality Capture can only work in exclusive-mode apps. XR support must be enabled in Project Settings > Player > XR Settings > Virtual Reality Supported, and the project then saved to disk.", MessageType.Error);
                    if (GUILayout.Button("Enable XR support"))
                    {
                        PlayerSettings.virtualRealitySupported = true;
                    }
                }
            }

            try
            {
                // Convert the selected codec name to an enum value.
                // This may throw an exception if this is a custom name, which will be handled below.
                SdpVideoCodecs codecValue;
                string customCodecValue = string.Empty;
                if (_preferredVideoCodec.stringValue.Length == 0)
                {
                    codecValue = SdpVideoCodecs.None;
                }
                else
                {
                    try
                    {
                        codecValue = (SdpVideoCodecs)Enum.Parse(typeof(SdpVideoCodecs), _preferredVideoCodec.stringValue);
                    }
                    catch
                    {
                        codecValue = SdpVideoCodecs.Custom;
                        customCodecValue = _preferredVideoCodec.stringValue;
                        // Hide internal marker
                        if (customCodecValue == "__CUSTOM")
                        {
                            customCodecValue = string.Empty;
                        }
                    }
                }

                // Display the edit field for the enum
                EditorGUILayout.BeginHorizontal();
                GUILayout.Label(_preferredVideoCodec.displayName);
                var newCodecValue = (SdpVideoCodecs)EditorGUILayout.EnumPopup(codecValue);
                EditorGUILayout.EndHorizontal();

                // Update the value if changed or custom
                if ((newCodecValue != codecValue) || (newCodecValue == SdpVideoCodecs.Custom))
                {
                    if (newCodecValue == SdpVideoCodecs.None)
                    {
                        _preferredVideoCodec.stringValue = string.Empty;
                    }
                    else if (newCodecValue == SdpVideoCodecs.Custom)
                    {
                        ++EditorGUI.indentLevel;
                        string newValue = EditorGUILayout.TextField("SDP codec name", customCodecValue);
                        if (newValue == string.Empty)
                        {
                            EditorGUILayout.HelpBox("The SDP codec name must be non-empty. See https://en.wikipedia.org/wiki/RTP_audio_video_profile for valid names.", MessageType.Error);

                            // Force a non-empty value now, otherwise the field will reset to None
                            newValue = "__CUSTOM";
                        }
                        _preferredVideoCodec.stringValue = newValue;
                        --EditorGUI.indentLevel;
                    }
                    else
                    {
                        _preferredVideoCodec.stringValue = Enum.GetName(typeof(SdpVideoCodecs), newCodecValue);
                    }
                }
            }
            catch (Exception)
            {
                EditorGUILayout.PropertyField(_preferredVideoCodec);
            }

            EditorGUILayout.PropertyField(_formatMode);
            if ((LocalVideoSourceFormatMode)_formatMode.intValue == LocalVideoSourceFormatMode.Manual)
            {
                ++EditorGUI.indentLevel;

                EditorGUILayout.PropertyField(_videoProfileKind);
                EditorGUILayout.PropertyField(_videoProfileId);
                EditorGUILayout.IntSlider(_width, 0, 10000);
                EditorGUILayout.IntSlider(_height, 0, 10000);
                //EditorGUILayout.Slider(_framerate, 0.0, 60.0); //< TODO

                if ((_videoProfileKind.intValue != (int)VideoProfileKind.Unspecified) && (_videoProfileId.stringValue.Length > 0))
                {
                    EditorGUILayout.HelpBox("Video profile ID is already unique. Specifying also a video kind over-constrains the selection algorithm and can decrease the chances of finding a matching video profile. It is recommended to select either a video profile kind, or a video profile ID (and leave the kind to Unspecified).", MessageType.Warning);
                }

                --EditorGUI.indentLevel;
            }

            serializedObject.ApplyModifiedProperties();
        }
    }
}
