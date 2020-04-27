// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using UnityEngine;
using UnityEditor;

namespace Microsoft.MixedReality.WebRTC.Unity.Editor
{
    /// <summary>
    /// Property drawer for <see cref="ToggleLeftAttribute"/>.
    /// </summary>
    [CustomPropertyDrawer(typeof(ToggleLeftAttribute))]
    public class ToggleLeftDrawer : PropertyDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            using (new EditorGUI.PropertyScope(position, label, property))
            {
                property.boolValue = EditorGUI.ToggleLeft(position, label, property.boolValue);
            }
        }
    }
}
