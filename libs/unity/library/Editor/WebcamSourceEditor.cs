// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using UnityEditor;
using UnityEngine;

namespace Microsoft.MixedReality.WebRTC.Unity.Editor
{
    /// <summary>
    /// Inspector editor for <see cref="WebcamSource"/>.
    /// </summary>
    [CustomEditor(typeof(WebcamSource))]
    [CanEditMultipleObjects]
    public class WebcamSourceEditor : UnityEditor.Editor
    {
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

        GUIContent _anyContent;
        float _anyWidth;
        float _unitWidth;

        int _prevWidth = 640;
        int _prevHeight = 480;
        double _prevFramerate = 30.0;
        VideoProfileKind _prevVideoProfileKind = VideoProfileKind.VideoConferencing;
        string _prevVideoProfileId = "<profile id>";

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

        void OnEnable()
        {
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

            _anyContent = new GUIContent("(any)");
            _anyWidth = -1f; // initialized later
            _unitWidth = -1f; // initialized later
        }

        /// <summary>
        /// Override implementation of <a href="https://docs.unity3d.com/ScriptReference/Editor.OnInspectorGUI.html">Editor.OnInspectorGUI</a>
        /// to draw the inspector GUI for the currently selected <see cref="WebcamSource"/>.
        /// </summary>
        public override void OnInspectorGUI()
        {
            // CalcSize() can only be called inside a GUI method
            if (_anyWidth < 0)
                _anyWidth = GUI.skin.label.CalcSize(_anyContent).x;
            if (_unitWidth < 0)
                _unitWidth = GUI.skin.label.CalcSize(new GUIContent("fps")).x;

            serializedObject.Update();

            if (!PlayerSettings.WSA.GetCapability(PlayerSettings.WSACapability.WebCam))
            {
                EditorGUILayout.HelpBox("The UWP player is missing the WebCam capability. The WebcamSource component will not function correctly."
                    + " Add the WebCam capability in Project Settings > Player > UWP > Publishing Settings > Capabilities.", MessageType.Error);
                if (GUILayout.Button("Open Player Settings"))
                {
                    SettingsService.OpenProjectSettings("Project/Player");
                }
                if (GUILayout.Button("Add WebCam Capability"))
                {
                    PlayerSettings.WSA.SetCapability(PlayerSettings.WSACapability.WebCam, true);
                }
            }

            GUILayout.Space(10);

            EditorGUILayout.LabelField("Video capture", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(_formatMode, new GUIContent("Capture format",
                "Decide how to obtain the constraints used to select the best capture format."));
            if ((LocalVideoSourceFormatMode)_formatMode.intValue == LocalVideoSourceFormatMode.Manual)
            {
                using (new EditorGUI.IndentLevelScope())
                {
                    EditorGUILayout.LabelField("General constraints (all platforms)");
                    using (new EditorGUI.IndentLevelScope())
                    {
                        OptionalIntField(_width, ref _prevWidth,
                            new GUIContent("Width", "Only consider capture formats with the specified width."),
                            new GUIContent("px", "Pixels"));
                        OptionalIntField(_height, ref _prevHeight,
                            new GUIContent("Height", "Only consider capture formats with the specified height."),
                            new GUIContent("px", "Pixels"));
                        OptionalDoubleField(_framerate, ref _prevFramerate,
                            new GUIContent("Framerate", "Only consider capture formats with the specified framerate."),
                            new GUIContent("fps", "Frames per second"));
                    }

                    EditorGUILayout.LabelField("UWP constraints");
                    using (new EditorGUI.IndentLevelScope())
                    {
                        OptionalEnumField(_videoProfileKind, VideoProfileKind.Unspecified, ref _prevVideoProfileKind,
                            new GUIContent("Video profile kind", "Only consider capture formats associated with the specified video profile kind."));
                        OptionalTextField(_videoProfileId, ref _prevVideoProfileId,
                            new GUIContent("Video profile ID", "Only consider capture formats associated with the specified video profile."));
                        if ((_videoProfileKind.intValue != (int)VideoProfileKind.Unspecified) && (_videoProfileId.stringValue.Length > 0))
                        {
                            EditorGUILayout.HelpBox("Video profile ID is already unique. Specifying also a video kind over-constrains the selection algorithm and can decrease the chances of finding a matching video profile. It is recommended to select either a video profile kind, or a video profile ID.", MessageType.Warning);
                        }
                    }
                }
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

            GUILayout.Space(10);

            EditorGUILayout.PropertyField(_videoStreamStarted);
            EditorGUILayout.PropertyField(_videoStreamStopped);

            serializedObject.ApplyModifiedProperties();
        }

        /// <summary>
        /// ToggleLeft control associated with a given SerializedProperty, to enable automatic GUI
        /// handlings like Prefab revert menu.
        /// </summary>
        /// <param name="property">The boolean property associated with the control.</param>
        /// <param name="label">The label to display next to the toggle control.</param>
        private void ToggleLeft(SerializedProperty property, GUIContent label)
        {
            var rect = EditorGUILayout.GetControlRect();
            using (new EditorGUI.PropertyScope(rect, label, property))
            {
                property.boolValue = EditorGUI.ToggleLeft(rect, label, property.boolValue);
            }
        }

        /// <summary>
        /// IntField with optional toggle associated with a given SerializedProperty, to enable
        /// automatic GUI handlings like Prefab revert menu.
        /// 
        /// Valid integer values are any non-zero positive integer. Any negative or zero value
        /// is considered invalid, and means that the value is considered as not set, which shows
        /// up as an unchecked left toggle widget.
        /// 
        /// To enforce a valid value when the toggle control is checked by the user, a default valid
        /// value is provided <paramref name="lastValidValue"/>. For UI consistency, the last selected
        /// valid value is returned in <paramref name="lastValidValue"/>, to allow toggling the field
        /// ON and OFF without losing the valid value it previously had.
        /// </summary>
        /// <param name="intProperty">The integer property associated with the control.</param>
        /// <param name="lastValidValue">
        /// Default value if the property value is invalid (negative or zero).
        /// Assigned the new value on return if valid.
        /// </param>
        /// <param name="label">The label to display next to the toggle control.</param>
        /// <param name="unitLabel">The label indicating the unit of the value.</param>
        private void OptionalIntField(SerializedProperty intProperty, ref int lastValidValue, GUIContent label, GUIContent unitLabel)
        {
            if (lastValidValue <= 0)
            {
                throw new ArgumentOutOfRangeException("Default value cannot be invalid.");
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                var rect = EditorGUILayout.GetControlRect();
                using (new EditorGUI.PropertyScope(rect, label, intProperty))
                {
                    bool hadValidValue = (intProperty.intValue > 0);
                    bool needsValidValue = EditorGUI.ToggleLeft(rect, label, hadValidValue);
                    int newValue = intProperty.intValue;
                    if (needsValidValue)
                    {
                        // Force a valid value, otherwise the edit field won't show up
                        if (newValue <= 0)
                        {
                            newValue = lastValidValue;
                        }

                        // Make updating the value of the serialized property delayed to allow overriding the
                        // value the user will input before it's assigned to the property, for validation.
                        newValue = EditorGUILayout.DelayedIntField(newValue);
                        if (newValue < 0)
                        {
                            newValue = 0;
                        }
                    }
                    else
                    {
                        // Force invalid value for consistency, otherwise this breaks Prefab revert
                        newValue = 0;
                    }
                    intProperty.intValue = newValue;
                    if (newValue > 0)
                    {
                        GUILayout.Label(unitLabel, GUILayout.Width(_unitWidth));

                        // Save valid value as new default. This allows toggling the toggle widget ON and OFF
                        // without losing the value previously input. This works only while the inspector is
                        // alive, that is while the object is select, but is better than nothing.
                        lastValidValue = newValue;
                    }
                    else
                    {
                        GUILayout.Label(_anyContent, GUILayout.Width(_anyWidth));
                    }
                }
            }
        }

        /// <summary>
        /// DoubleField with optional toggle associated with a given SerializedProperty, to enable
        /// automatic GUI handlings like Prefab revert menu.
        /// 
        /// Valid doubles values are any non-zero positive doubles. Any negative or zero value
        /// is considered invalid, and means that the value is considered as not set, which shows
        /// up as an unchecked left toggle widget.
        /// 
        /// To enforce a valid value when the toggle control is checked by the user, a default valid
        /// value is provided <paramref name="lastValidValue"/>. For UI consistency, the last selected
        /// valid value is returned in <paramref name="lastValidValue"/>, to allow toggling the field
        /// ON and OFF without losing the valid value it previously had.
        /// </summary>
        /// <param name="doubleProperty">The double property associated with the control.</param>
        /// <param name="lastValidValue">
        /// Default value if the property value is invalid (negative or zero).
        /// Assigned the new value on return if valid.
        /// </param>
        /// <param name="label">The label to display next to the toggle control.</param>
        /// <param name="unitLabel">The label indicating the unit of the value.</param>
        private void OptionalDoubleField(SerializedProperty doubleProperty, ref double lastValidValue, GUIContent label, GUIContent unitLabel)
        {
            if (lastValidValue <= 0.0)
            {
                throw new ArgumentOutOfRangeException("Default value cannot be invalid.");
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                var rect = EditorGUILayout.GetControlRect();
                using (new EditorGUI.PropertyScope(rect, label, doubleProperty))
                {
                    bool hadValidValue = (doubleProperty.doubleValue > 0.0);
                    bool needsValidValue = EditorGUI.ToggleLeft(rect, label, hadValidValue);
                    double newValue = doubleProperty.doubleValue;
                    if (needsValidValue)
                    {
                        // Force a valid value, otherwise the edit field won't show up
                        if (newValue <= 0.0)
                        {
                            newValue = lastValidValue;
                        }

                        // Make updating the value of the serialized property delayed to allow overriding the
                        // value the user will input before it's assigned to the property, for validation.
                        newValue = EditorGUILayout.DelayedDoubleField(newValue);
                        if (newValue < 0.0)
                        {
                            newValue = 0.0;
                        }
                    }
                    else
                    {
                        // Force invalid value for consistency, otherwise this breaks Prefab revert
                        newValue = 0.0;
                    }
                    doubleProperty.doubleValue = newValue;
                    if (newValue > 0.0)
                    {
                        GUILayout.Label(unitLabel, GUILayout.Width(_unitWidth));

                        // Save valid value as new default. This allows toggling the toggle widget ON and OFF
                        // without losing the value previously input. This works only while the inspector is
                        // alive, that is while the object is select, but is better than nothing.
                        lastValidValue = newValue;
                    }
                    else
                    {
                        GUILayout.Label(_anyContent, GUILayout.Width(_anyWidth));
                    }
                }
            }
        }

        /// <summary>
        /// Helper to convert an enum to its integer value.
        /// </summary>
        /// <typeparam name="TValue">The enum type.</typeparam>
        /// <param name="value">The enum value.</param>
        /// <returns>The integer value associated with <paramref name="value"/>.</returns>
        public static int EnumToInt<TValue>(TValue value) where TValue : Enum => (int)(object)value;

        /// <summary>
        /// Helper to convert an integer to its enum value.
        /// </summary>
        /// <typeparam name="TValue">The enum type.</typeparam>
        /// <param name="value">The integer value.</param>
        /// <returns>The enum value whose integer value is <paramref name="value"/>.</returns>
        public static TValue IntToEnum<TValue>(int value) where TValue : Enum => (TValue)(object)value;

        /// <summary>
        /// EnumPopup with optional toggle associated with a given SerializedProperty, to enable
        /// automatic GUI handlings like Prefab revert menu.
        /// 
        /// Valid enum values are any value different from <paramref name="nilValue"/>. A value of
        /// <paramref name="nilValue"/> is considered invalid, and means that the value is considered as
        /// not set, which shows up as an unchecked left toggle widget.
        /// 
        /// To enforce a valid value when the toggle control is checked by the user, a default valid value
        /// is provided <paramref name="lastValidValue"/> which must be different from <paramref name="nilValue"/>.
        /// For UI consistency, the last selected valid value is returned in <paramref name="lastValidValue"/>,
        /// to allow toggling the field ON and OFF without losing the valid value it previously had.
        /// </summary>
        /// <param name="enumProperty">The enum property associated with the control.</param>
        /// <param name="nilValue">Value considered to be "invalid", which deselects the toggle control.</param>
        /// <param name="lastValidValue">
        /// Default value if the property value is not <paramref name="nilValue"/>.
        /// Assigned the new value on return if not <paramref name="nilValue"/>.
        /// </param>
        /// <param name="label">The label to display next to the toggle control.</param>
        private void OptionalEnumField<T>(SerializedProperty enumProperty, T nilValue, ref T lastValidValue, GUIContent label) where T : Enum
        {
            if (nilValue.CompareTo(lastValidValue) == 0)
            {
                throw new ArgumentOutOfRangeException("Default value cannot be invalid.");
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                var rect = EditorGUILayout.GetControlRect();
                using (new EditorGUI.PropertyScope(rect, label, enumProperty))
                {
                    bool hadValidValue = (enumProperty.intValue != EnumToInt<T>(nilValue));
                    bool needsValidValue = EditorGUI.ToggleLeft(rect, label, hadValidValue);
                    T newValue = IntToEnum<T>(enumProperty.intValue);
                    if (needsValidValue)
                    {
                        // Force a valid value, otherwise the popup control won't show up
                        if (newValue.CompareTo(nilValue) == 0)
                        {
                            newValue = lastValidValue;
                        }

                        newValue = (T)EditorGUILayout.EnumPopup(newValue);
                    }
                    else
                    {
                        // Force invalid value for consistency, otherwise this breaks Prefab revert
                        newValue = nilValue;
                    }
                    enumProperty.intValue = EnumToInt<T>(newValue);
                    if (newValue.CompareTo(nilValue) != 0)
                    {
                        // Save valid value as new default. This allows toggling the toggle widget ON and OFF
                        // without losing the value previously input. This works only while the inspector is
                        // alive, that is while the object is select, but is better than nothing.
                        lastValidValue = newValue;
                    }
                    else
                    {
                        GUILayout.Label(_anyContent, GUILayout.Width(_anyWidth));
                    }
                }
            }
        }

        /// <summary>
        /// TextField with optional toggle associated with a given SerializedProperty, to enable
        /// automatic GUI handlings like Prefab revert menu.
        /// 
        /// Valid string values are any non-empty non-space-only string. Any empty string or string
        /// made up of only spaces is considered invalid, and means that the value is considered as
        /// not set, which shows up as an unchecked left toggle widget.
        /// 
        /// To enforce a valid value when the toggle control is checked by the user, a default valid
        /// value is provided <paramref name="lastValidValue"/>. For UI consistency, the last selected
        /// valid value is returned in <paramref name="lastValidValue"/>, to allow toggling the field
        /// ON and OFF without losing the valid value it previously had.
        /// </summary>
        /// <param name="stringProperty">The string property associated with the control.</param>
        /// <param name="lastValidValue">
        /// Default value if the property value null or whitespace.
        /// Assigned the new value on return if valid.
        /// </param>
        /// <param name="label">The label to display next to the toggle control.</param>
        private void OptionalTextField(SerializedProperty stringProperty, ref string lastValidValue, GUIContent label)
        {
            if (string.IsNullOrWhiteSpace(lastValidValue))
            {
                throw new ArgumentOutOfRangeException("Default value cannot be invalid.");
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                var rect = EditorGUILayout.GetControlRect();
                using (new EditorGUI.PropertyScope(rect, label, stringProperty))
                {
                    bool hadValidValue = !string.IsNullOrWhiteSpace(stringProperty.stringValue);
                    bool needsValidValue = EditorGUI.ToggleLeft(rect, label, hadValidValue);
                    string newValue = stringProperty.stringValue;
                    if (needsValidValue)
                    {
                        // Force a valid value, otherwise the edit field won't show up
                        if (string.IsNullOrWhiteSpace(newValue))
                        {
                            newValue = lastValidValue;
                        }

                        // Make updating the value of the serialized property delayed to allow overriding the
                        // value the user will input before it's assigned to the property, for validation.
                        newValue = EditorGUILayout.DelayedTextField(newValue);
                        if (string.IsNullOrWhiteSpace(newValue))
                        {
                            newValue = string.Empty;
                        }
                    }
                    else
                    {
                        // Force invalid value for consistency, otherwise this breaks Prefab revert
                        newValue = string.Empty;
                    }
                    stringProperty.stringValue = newValue;
                    if (!string.IsNullOrWhiteSpace(newValue))
                    {
                        // Save valid value as new default. This allows toggling the toggle widget ON and OFF
                        // without losing the value previously input. This works only while the inspector is
                        // alive, that is while the object is select, but is better than nothing.
                        lastValidValue = newValue;
                    }
                    else
                    {
                        GUILayout.Label(_anyContent, GUILayout.Width(_anyWidth));
                    }
                }
            }
        }
    }
}
