// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using UnityEngine;
using UnityEngine.XR;

namespace Microsoft.MixedReality.WebRTC.Unity
{
    /// <summary>
    /// Attribute for a <see xref="UnityEngine.Camera"/> property used by <see cref="SceneVideoSource"/>
    /// to capture the content of a framebuffer, and for which some constraints on stereoscopic rendering
    /// options need to be enforced (and errors can be reported in the Editor if they are not followed).
    /// </summary>
    /// <seealso cref="SceneVideoSource"/>
    public class CaptureCameraAttribute : PropertyAttribute
    {
        /// <summary>
        /// Validate that a given <see xref="UnityEngine.Camera"/> instance can be used for framebuffer
        /// capture by <see cref="SceneVideoSource"/> based on the XR settings currently in effect.
        /// </summary>
        /// <param name="camera">The camera instance to test the settings of.</param>
        /// <exception xref="System.NotSupportedException">
        /// The camera has settings not compatible with its use with <see cref="SceneVideoSource"/>.
        /// </exception>
        /// <seealso xref="CaptureCameraDrawer.Validate(Camera)"/>
        public static void Validate(Camera camera)
        {
            if (camera != null)
            {
                if (XRSettings.stereoRenderingMode == XRSettings.StereoRenderingMode.MultiPass)
                {
                    // Ensure camera is not rendering to both eyes in multi-pass stereo, otherwise the command buffer
                    // is executed twice (once per eye) and will produce twice as many frames, which leads to stuttering
                    // when playing back the video stream resulting from combining those frames.
                    if (camera.stereoTargetEye == StereoTargetEyeMask.Both)
                    {
                        throw new NotSupportedException("Capture camera renders both eyes in multi-pass stereoscopic rendering. This is not" +
                            " supported by the capture mechanism which cannot discriminate them. Set Camera.stereoTargetEye to either Left or" +
                            " Right, or use a different XRSettings.stereoRenderingMode.");
                    }
                }
#if !UNITY_2019_1_OR_NEWER
                else if ((XRSettings.stereoRenderingMode == XRSettings.StereoRenderingMode.SinglePassInstanced)
                    || (XRSettings.stereoRenderingMode == XRSettings.StereoRenderingMode.SinglePassMultiview)) // same as instanced (OpenGL)
                {
                    throw new NotSupportedException("Capture camera does not support single-pass instanced stereoscopic rendering before Unity 2019.1.");
                }
#endif
            }
        }
    }
}
