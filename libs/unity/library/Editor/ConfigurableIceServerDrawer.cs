// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using UnityEngine;
using UnityEditor;

namespace Microsoft.MixedReality.WebRTC.Unity.Editor
{
    /// <summary>
    /// Property drawer for <see cref="ConfigurableIceServer"/>, to display servers on a single line
    /// with the kind first (fixed width) and the server address next (stretching).
    /// </summary>
    [CustomPropertyDrawer(typeof(ConfigurableIceServer))]
    public class ConfigurableIceServerDrawer : PropertyDrawer
    {
        const float kTypeWidth = 60f;

        public override void OnGUI(Rect rect, SerializedProperty property, GUIContent label)
        {
            var type = property.FindPropertyRelative("Type");
            EditorGUI.PropertyField(new Rect(rect.x, rect.y, kTypeWidth, rect.height), type, GUIContent.none);

            rect.x += kTypeWidth - 10f;
            rect.width -= kTypeWidth - 10f;
            var uri = property.FindPropertyRelative("Uri");
            EditorGUI.PropertyField(rect, uri, GUIContent.none);
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            return EditorStyles.textField.lineHeight + 3f;
        }
    }
}
