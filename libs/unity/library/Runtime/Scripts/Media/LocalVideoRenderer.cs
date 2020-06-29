// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using UnityEngine;
using Unity.Profiling;
using System;
using Microsoft.MixedReality.WebRTC.Unity.Editor;

namespace Microsoft.MixedReality.WebRTC.Unity
{
    /// <summary>
    /// Utility component used to play video frames obtained from a local WebRTC video track.
    /// </summary>
    /// <remarks>
    /// This component writes to the attached <a href="https://docs.unity3d.com/ScriptReference/Material.html">Material</a>,
    /// via the attached <a href="https://docs.unity3d.com/ScriptReference/Renderer.html">Renderer</a>.
    /// </remarks>
    [RequireComponent(typeof(Renderer))]
    [AddComponentMenu("MixedReality-WebRTC/Local Video Renderer")]
    public class LocalVideoRenderer : MonoBehaviour
    {
        public VideoTrackSource Source;

        [SerializeField]
        private VideoRendererWidget _widget = new VideoRendererWidget();

        private bool _isPlaying = false;

        private void OnDisable()
        {
            _widget.StopPlaying();
        }

        private void Update()
        {
            if (!_isPlaying && Source != null)
            {
                _widget.Initialize(Source, GetComponent<Renderer>(), 3);
                _widget.StartPlaying();
                _isPlaying = true;
            }
            else if (_isPlaying && Source == null)
            {
                _widget.StopPlaying();
                _isPlaying = false;
            }

            if (_isPlaying)
            {
                _widget.Update();
            }
        }
    }
}
