// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.MixedReality.WebRTC;
using Microsoft.MixedReality.WebRTC.Unity;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Microsoft.MixedReality.WebRTC.Unity
{
    /// <summary>
    /// A video source producing some colored frames generated programmatically.
    /// </summary>
    public class UniformColorVideoSource : CustomVideoSource<Argb32VideoFrameStorage>
    {
        /// <summary>
        /// List of colors to cycle through.
        /// </summary>
        [Tooltip("List of colors to cycle through")]
        public List<Color32> Colors = new List<Color32>();

        /// <summary>
        /// Color cycling speed, in change per second.
        /// </summary>
        [Tooltip("Color cycling speed, in change per second")]
        public float Speed = 1f;

        /// <summary>
        /// Frame width, in pixels.
        /// </summary>
        private const int FrameWidth = 16;

        /// <summary>
        /// Frame height, in pixels.
        /// </summary>
        private const int FrameHeight = 16;

        /// <summary>
        /// Row stride, in bytes.
        /// </summary>
        private const int FrameStride = FrameWidth * 4;

        /// <summary>
        /// Frame buffer size, in pixels.
        /// </summary>
        private const int FrameSize = FrameWidth * FrameHeight;

        private uint[] _data = new uint[FrameSize];
        private int _index = -2;

        protected void Start()
        {
            // Update buffer on start in case OnFrameRequested() is called before Update()
            UpdateBuffer();
        }

        protected void Update()
        {
            UpdateBuffer();
        }

        protected void UpdateBuffer()
        {
            if (Colors.Count > 0)
            {
                int index = Mathf.FloorToInt(Time.time * Speed) % Colors.Count;
                if (index != _index)
                {
                    _index = index;
                    var col32 = Colors[index];
                    uint color = col32.b | (uint)col32.g << 8 | (uint)col32.r << 16 | (uint)col32.a << 24;
                    for (int k = 0; k < FrameSize; ++k)
                    {
                        _data[k] = color;
                    }
                }
            }
            else if (_index != -1)
            {
                // Fallback to bright purple
                _index = -1;
                uint color = 0xFFFF00FFu;
                for (int k = 0; k < FrameSize; ++k)
                {
                    _data[k] = color;
                }
            }
        }

        protected override void OnFrameRequested(in FrameRequest request)
        {
            var frame = new Argb32VideoFrame
            {
                width = FrameWidth,
                height = FrameHeight,
                stride = FrameStride
            };
            unsafe
            {
                fixed (void* ptr = _data)
                {
                    frame.data = (IntPtr)ptr;
                    request.CompleteRequest(frame);
                }
            }
        }
    }
}
