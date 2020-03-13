// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using UnityEditor;
using UnityEngine;

namespace Microsoft.MixedReality.WebRTC.Unity.Editor
{
    /// <summary>
    /// Inspector editor for <see cref="WebcamSource"/>.
    /// Allows displaying some error message when Mixed Reality Capture is enabled but
    /// XR is not, the later corresponding to a non-exclusive app (2D slate) where MRC
    /// is not available.
    /// </summary>
    [CustomEditor(typeof(WebcamSource))]
    [CanEditMultipleObjects]
    public class WebcamSourceEditor : UnityEditor.Editor
    {
        SerializedProperty _peerConnection;
        SerializedProperty _trackName;
        SerializedProperty _autoStartOnEnabled;
        SerializedProperty _autoStopOnDisabled;
        SerializedProperty _preferredVideoCodec;
        SerializedProperty _enableMixedRealityCapture;
        SerializedProperty _enableMrcRecordingIndicator;
        SerializedProperty _formatMode;
        SerializedProperty _videoProfileId;
        SerializedProperty _videoProfileKind;
        SerializedProperty _constraints;
        SerializedProperty _width;
        SerializedProperty _height;
        SerializedProperty _framerate;
        SerializedProperty _videoStreamStarted;
        SerializedProperty _videoStreamStopped;

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
            _trackName = serializedObject.FindProperty("TrackName");
            _autoStartOnEnabled = serializedObject.FindProperty("AutoStartOnEnabled");
            _autoStopOnDisabled = serializedObject.FindProperty("AutoStopOnDisabled");
            _preferredVideoCodec = serializedObject.FindProperty("PreferredVideoCodec");
            _enableMixedRealityCapture = serializedObject.FindProperty("EnableMixedRealityCapture");
            _enableMrcRecordingIndicator = serializedObject.FindProperty("EnableMRCRecordingIndicator");
            _formatMode = serializedObject.FindProperty("FormatMode");
            _videoProfileId = serializedObject.FindProperty("VideoProfileId");
            _videoProfileKind = serializedObject.FindProperty("VideoProfileKind");
            _constraints = serializedObject.FindProperty("Constraints");
            _width = _constraints.FindPropertyRelative("width");
            _height = _constraints.FindPropertyRelative("height");
            _framerate = _constraints.FindPropertyRelative("framerate");
            _videoStreamStarted = serializedObject.FindProperty("VideoStreamStarted");
            _videoStreamStopped = serializedObject.FindProperty("VideoStreamStopped");
        }

        /// <summary>
        /// Override implementation of <a href="https://docs.unity3d.com/ScriptReference/Editor.OnInspectorGUI.html">Editor.OnInspectorGUI</a>
        /// to draw the inspector GUI for the currently selected <see cref="LocalVideoSource"/>.
        /// </summary>
        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            GUILayout.Space(10);

            EditorGUILayout.LabelField("Video capture", EditorStyles.boldLabel);
            _autoStartOnEnabled.boolValue = EditorGUILayout.ToggleLeft("Auto-start capture when enabled", _autoStartOnEnabled.boolValue);
            _autoStopOnDisabled.boolValue = EditorGUILayout.ToggleLeft("Auto-stop capture when disabled", _autoStopOnDisabled.boolValue);
            EditorGUILayout.PropertyField(_formatMode, new GUIContent("Capture format"));
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
            _enableMixedRealityCapture.boolValue = EditorGUILayout.ToggleLeft("Enable Mixed Reality Capture (MRC)", _enableMixedRealityCapture.boolValue);
            if (_enableMixedRealityCapture.boolValue)
            {
                using (var scope = new EditorGUI.IndentLevelScope())
                {
                    _enableMrcRecordingIndicator.boolValue = EditorGUILayout.ToggleLeft("Show recording indicator in device", _enableMrcRecordingIndicator.boolValue);
                    if (!PlayerSettings.virtualRealitySupported)
                    {
                        EditorGUILayout.HelpBox("Mixed Reality Capture can only work in exclusive-mode apps. XR support must be enabled in Project Settings > Player > XR Settings > Virtual Reality Supported, and the project then saved to disk.", MessageType.Error);
                        if (GUILayout.Button("Enable XR support"))
                        {
                            PlayerSettings.virtualRealitySupported = true;
                        }
                    }
                }
            }
            EditorGUILayout.PropertyField(_videoStreamStarted);
            EditorGUILayout.PropertyField(_videoStreamStopped);

            GUILayout.Space(10);

            EditorGUILayout.LabelField("WebRTC track", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(_trackName);

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
                var newCodecValue = (SdpVideoCodecs)EditorGUILayout.EnumPopup(_preferredVideoCodec.displayName, codecValue);

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

            serializedObject.ApplyModifiedProperties();
        }
    }
}
