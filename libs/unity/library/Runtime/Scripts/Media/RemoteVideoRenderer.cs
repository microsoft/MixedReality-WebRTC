// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using UnityEngine;
using Unity.Profiling;
using System;
using Microsoft.MixedReality.WebRTC.Unity.Editor;

namespace Microsoft.MixedReality.WebRTC.Unity
{
    /// <summary>
    /// Utility component used to play video frames obtained from a remote WebRTC video track.
    /// </summary>
    /// <remarks>
    /// This component writes to the attached <a href="https://docs.unity3d.com/ScriptReference/Material.html">Material</a>,
    /// via the attached <a href="https://docs.unity3d.com/ScriptReference/Renderer.html">Renderer</a>.
    /// </remarks>
    [RequireComponent(typeof(Renderer))]
    [AddComponentMenu("MixedReality-WebRTC/Remote Video Renderer")]
    public class RemoteVideoRenderer : VideoReceiver
    {
        [SerializeField]
        private VideoRendererWidget _widget = new VideoRendererWidget();

        protected override void Awake()
        {
            _widget.Initialize(this, GetComponent<Renderer>(), 5);
        }

        private void OnEnable()
        {
            _widget.StartPlaying();
        }

        private void OnDisable()
        {
            _widget.StopPlaying();
        }

        //// <summary>
        /// Unity Engine Start() hook
        /// </summary>
        /// <remarks>
        /// https://docs.unity3d.com/ScriptReference/MonoBehaviour.Start.html
        /// </remarks>
        protected override void Update()
        {
            base.Update();
            _widget.Update();
        }
    }
}
