// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using UnityEngine;
using UnityEditor;

namespace Microsoft.MixedReality.WebRTC.Unity.Editor
{
    /// <summary>
    /// Property drawer for <see cref="CaptureCameraAttribute"/>, to report an error to the user if
    /// the associated <see xref="UnityEngine.Camera"/> property instance cannot be used for framebuffer
    /// capture by <see cref="SceneVideoSource"/>.
    /// </summary>
    [CustomPropertyDrawer(typeof(CaptureCameraAttribute))]
    public class CaptureCameraDrawer : PropertyDrawer
    {
        private const int c_errorMessageHeight = 42;

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            try
            {
                Validate(property.objectReferenceValue as Camera);
            }
            catch (Exception ex)
            {
                // Display error message below the property
                var totalHeight = position.height;
                position.yMin = position.yMax - c_errorMessageHeight;
                EditorGUI.HelpBox(position, ex.Message, MessageType.Warning);

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
                Validate(property.objectReferenceValue as Camera);
            }
            catch (Exception)
            {
                // Add extra space for the error message
                height += c_errorMessageHeight;
            }
            return height;
        }

        /// <summary>
        /// Validate that a given <see xref="UnityEngine.Camera"/> instance can be used for framebuffer
        /// capture by <see cref="SceneVideoSource"/> based on the current settings of the Unity Player
        /// for the current build platform.
        /// </summary>
        /// <param name="camera">The camera instance to test the settings of.</param>
        /// <exception xref="System.NotSupportedException">
        /// The camera has settings not compatible with its use with <see cref="SceneVideoSource"/>.
        /// </exception>
        /// <seealso cref="CaptureCameraAttribute.Validate(Camera)"/>
        public static void Validate(Camera camera)
        {
            if (PlayerSettings.virtualRealitySupported && (camera != null))
            {
                if (PlayerSettings.stereoRenderingPath == StereoRenderingPath.MultiPass)
                {
                    // Ensure camera is not rendering to both eyes in multi-pass stereo, otherwise the command buffer
                    // is executed twice (once per eye) and will produce twice as many frames, which leads to stuttering
                    // when playing back the video stream resulting from combining those frames.
                    if (camera.stereoTargetEye == StereoTargetEyeMask.Both)
                    {
                        throw new NotSupportedException("Capture camera renders both eyes in multi-pass stereoscopic rendering. This is not" +
                            " supported by the capture mechanism which cannot discriminate them. Set Camera.stereoTargetEye to either Left or" +
                            " Right, or use a different rendering mode (Player Settings > XR Settings > Stereo Rendering Mode).");
                    }
                }
#if !UNITY_2019_1_OR_NEWER
                else if (PlayerSettings.stereoRenderingPath == StereoRenderingPath.Instancing)
                {
                    throw new NotSupportedException("Capture camera does not support single-pass instanced stereoscopic rendering before Unity 2019.1." +
                        " Use a different stereoscopic rendering mode (Player Settings > XR Settings > Stereo Rendering Mode) or upgrade to Unity 2019.1+.");
                }
#endif
            }
        }
    }
}
