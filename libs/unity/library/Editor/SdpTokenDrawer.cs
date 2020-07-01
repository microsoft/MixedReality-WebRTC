// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using UnityEngine;
using UnityEditor;

namespace Microsoft.MixedReality.WebRTC.Unity.Editor
{
    /// <summary>
    /// Property drawer for <see cref="SdpTokenAttribute"/>, to validate the associated string
    /// property content and display an error message box if invalid characters are found.
    /// </summary>
    [CustomPropertyDrawer(typeof(SdpTokenAttribute))]
    public class SdpTokenDrawer : PropertyDrawer
    {
        private const int c_errorMessageHeight = 35;

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            try
            {
                var sdpTokenAttr = attribute as SdpTokenAttribute;
                SdpTokenAttribute.Validate(property.stringValue, sdpTokenAttr.AllowEmpty);
            }
            catch (ArgumentException)
            {
                // Display error message below the property
                var totalHeight = position.height;
                position.yMin = position.yMax - c_errorMessageHeight;
                EditorGUI.HelpBox(position, "Invalid characters in property. SDP tokens cannot contain some characters like space or quote. See SdpTokenAttribute.Validate() for details.", MessageType.Error);

                // Adjust rect for the property itself
                position.yMin = position.yMax - totalHeight;
                position.yMax -= c_errorMessageHeight;
            }

            EditorGUI.PropertyField(position, property, label);
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            float height = base.GetPropertyHeight(property, label);
            try
            {
                var sdpTokenAttr = attribute as SdpTokenAttribute;
                SdpTokenAttribute.Validate(property.stringValue, sdpTokenAttr.AllowEmpty);
            }
            catch (ArgumentException)
            {
                // Add extra space for the error message
                height += c_errorMessageHeight;
            }
            return height;
        }
    }
}
