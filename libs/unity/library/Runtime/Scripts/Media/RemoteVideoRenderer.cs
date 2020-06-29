// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using UnityEngine;
using Unity.Profiling;
using System;
using Microsoft.MixedReality.WebRTC.Unity.Editor;

namespace Microsoft.MixedReality.WebRTC.Unity
{
    /// <summary>
    /// Utility component used to play video frames obtained from a WebRTC video track. This can indiscriminately
    /// play video frames from a video track source on the local peer as well as video frames from a remote video
    /// receiver obtaining its frame from a remote WebRTC peer.
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

        private void Start()
        {
            _widget.Initialize(this, GetComponent<Renderer>());
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
