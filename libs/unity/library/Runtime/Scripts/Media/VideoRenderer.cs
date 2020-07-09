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
    [AddComponentMenu("MixedReality-WebRTC/Video Renderer")]
    public class VideoRenderer : MonoBehaviour
    {
        [Tooltip("Max playback framerate, in frames per second")]
        [Range(0.001f, 120f)]
        public float MaxFramerate = 30f;

        [Header("Statistics")]
        [ToggleLeft]
        public bool EnableStatistics = true;

        /// <summary>
        /// A textmesh onto which frame load stat data will be written
        /// </summary>
        /// <remarks>
        /// This is how fast the frames are given from the underlying implementation
        /// </remarks>
        [Tooltip("A textmesh onto which frame load stat data will be written")]
        public TextMesh FrameLoadStatHolder;

        /// <summary>
        /// A textmesh onto which frame present stat data will be written
        /// </summary>
        /// <remarks>
        /// This is how fast we render frames to the display
        /// </remarks>
        [Tooltip("A textmesh onto which frame present stat data will be written")]
        public TextMesh FramePresentStatHolder;

        /// <summary>
        /// A textmesh into which frame skip stat dta will be written
        /// </summary>
        /// <remarks>
        /// This is how often we skip presenting an underlying frame
        /// </remarks>
        [Tooltip("A textmesh onto which frame skip stat data will be written")]
        public TextMesh FrameSkipStatHolder;

        // Source that this renderer is currently subscribed to.
        private IVideoSource _source;

        /// <summary>
        /// Internal reference to the attached texture
        /// </summary>
        private Texture2D _textureY = null; // also used for ARGB32
        private Texture2D _textureU = null;
        private Texture2D _textureV = null;

        /// <summary>
        /// Internal timing counter
        /// </summary>
        private float lastUpdateTime = 0.0f;

        private Material videoMaterial;
        private float _minUpdateDelay;

        private VideoFrameQueue<I420AVideoFrameStorage> _i420aFrameQueue = null;
        private VideoFrameQueue<Argb32VideoFrameStorage> _argb32FrameQueue = null;

        private ProfilerMarker displayStatsMarker = new ProfilerMarker("DisplayStats");
        private ProfilerMarker loadTextureDataMarker = new ProfilerMarker("LoadTextureData");
        private ProfilerMarker uploadTextureToGpuMarker = new ProfilerMarker("UploadTextureToGPU");

        private void Start()
        {
            CreateEmptyVideoTextures();

            // Leave 3ms of margin, otherwise it misses 1 frame and drops to ~20 FPS
            // when Unity is running at 60 FPS.
            _minUpdateDelay = Mathf.Max(0f, 1f / Mathf.Max(0.001f, MaxFramerate) - 0.003f);
        }

        /// <summary>
        /// Start rendering the passed source.
        /// </summary>
        /// <remarks>
        /// Can be used to handle <see cref="VideoTrackSource.VideoStreamStarted"/> or <see cref="VideoReceiver.VideoStreamStarted"/>.
        /// </remarks>
        public void StartRendering(IVideoSource source)
        {
            bool isRemote = (source is RemoteVideoTrack);
            int frameQueueSize = (isRemote ? 5 : 3);

            switch (source.FrameEncoding)
            {
                case VideoEncoding.I420A:
                    _i420aFrameQueue = new VideoFrameQueue<I420AVideoFrameStorage>(frameQueueSize);
                    source.I420AVideoFrameReady += I420AVideoFrameReady;
                    break;

                case VideoEncoding.Argb32:
                    _argb32FrameQueue = new VideoFrameQueue<Argb32VideoFrameStorage>(frameQueueSize);
                    source.Argb32VideoFrameReady += Argb32VideoFrameReady;
                    break;
            }
        }

        /// <summary>
        /// Stop rendering the passed source. Must be called with the same source passed to <see cref="StartRendering(IVideoSource)"/>
        /// </summary>
        /// <remarks>
        /// Can be used to handle <see cref="VideoTrackSource.VideoStreamStopped"/> or <see cref="VideoReceiver.VideoStreamStopped"/>.
        /// </remarks>
        public void StopRendering(IVideoSource _)
        {
            // Clear the video display to not confuse the user who could otherwise
            // think that the video is still playing but is lagging/frozen.
            CreateEmptyVideoTextures();
        }

        protected void OnDisable()
        {
            // Clear the video display to not confuse the user who could otherwise
            // think that the video is still playing but is lagging/frozen.
            CreateEmptyVideoTextures();
        }

        protected void I420AVideoFrameReady(I420AVideoFrame frame)
        {
            // This callback is generally from a non-UI thread, but Unity object access is only allowed
            // on the main UI thread, so defer to that point.
            _i420aFrameQueue.Enqueue(frame);
        }

        protected void Argb32VideoFrameReady(Argb32VideoFrame frame)
        {
            // This callback is generally from a non-UI thread, but Unity object access is only allowed
            // on the main UI thread, so defer to that point.
            _argb32FrameQueue.Enqueue(frame);
        }

        private void CreateEmptyVideoTextures()
        {
            // Create a default checkboard texture which visually indicates
            // that no data is available. This is useful for debugging and
            // for the user to know about the state of the video.
            _textureY = new Texture2D(2, 2);
            _textureY.SetPixel(0, 0, Color.blue);
            _textureY.SetPixel(1, 1, Color.blue);
            _textureY.Apply();
            _textureU = new Texture2D(2, 2);
            _textureU.SetPixel(0, 0, Color.blue);
            _textureU.SetPixel(1, 1, Color.blue);
            _textureU.Apply();
            _textureV = new Texture2D(2, 2);
            _textureV.SetPixel(0, 0, Color.blue);
            _textureV.SetPixel(1, 1, Color.blue);
            _textureV.Apply();

            // Assign that texture to the video player's Renderer component
            videoMaterial = GetComponent<Renderer>().material;
            if (_i420aFrameQueue != null)
            {
                videoMaterial.SetTexture("_YPlane", _textureY);
                videoMaterial.SetTexture("_UPlane", _textureU);
                videoMaterial.SetTexture("_VPlane", _textureV);
            }
            else if (_argb32FrameQueue != null)
            {
                videoMaterial.SetTexture("_MainTex", _textureY);
            }
        }

        //// <summary>
        /// Unity Engine Start() hook
        /// </summary>
        /// <remarks>
        /// https://docs.unity3d.com/ScriptReference/MonoBehaviour.Start.html
        /// </remarks>
        private void Update()
        {
            if ((_i420aFrameQueue != null) || (_argb32FrameQueue != null))
            {
#if UNITY_EDITOR
                // Inside the Editor, constantly update _minUpdateDelay to
                // react to user changes to MaxFramerate.

                // Leave 3ms of margin, otherwise it misses 1 frame and drops to ~20 FPS
                // when Unity is running at 60 FPS.
                _minUpdateDelay = Mathf.Max(0f, 1f / Mathf.Max(0.001f, MaxFramerate) - 0.003f);
#endif
                // FIXME - This will overflow/underflow the queue if not set at the same rate
                // as the one at which frames are enqueued!
                var curTime = Time.time;
                if (curTime - lastUpdateTime >= _minUpdateDelay)
                {
                    if (_i420aFrameQueue != null)
                    {
                        TryProcessI420AFrame();
                    }
                    else if (_argb32FrameQueue != null)
                    {
                        TryProcessArgb32Frame();
                    }
                    lastUpdateTime = curTime;
                }

                if (EnableStatistics)
                {
                    // Share our stats values, if possible.
                    using (var profileScope = displayStatsMarker.Auto())
                    {
                        IVideoFrameQueue stats = (_i420aFrameQueue != null ? (IVideoFrameQueue)_i420aFrameQueue : _argb32FrameQueue);
                        if (FrameLoadStatHolder != null)
                        {
                            FrameLoadStatHolder.text = stats.QueuedFramesPerSecond.ToString("F2");
                        }
                        if (FramePresentStatHolder != null)
                        {
                            FramePresentStatHolder.text = stats.DequeuedFramesPerSecond.ToString("F2");
                        }
                        if (FrameSkipStatHolder != null)
                        {
                            FrameSkipStatHolder.text = stats.DroppedFramesPerSecond.ToString("F2");
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Internal helper that attempts to process frame data in the frame queue
        /// </summary>
        private void TryProcessI420AFrame()
        {
            if (_i420aFrameQueue.TryDequeue(out I420AVideoFrameStorage frame))
            {
                int lumaWidth = (int)frame.Width;
                int lumaHeight = (int)frame.Height;
                if (_textureY == null || (_textureY.width != lumaWidth || _textureY.height != lumaHeight))
                {
                    _textureY = new Texture2D(lumaWidth, lumaHeight, TextureFormat.R8, mipChain: false);
                    videoMaterial.SetTexture("_YPlane", _textureY);
                }
                int chromaWidth = lumaWidth / 2;
                int chromaHeight = lumaHeight / 2;
                if (_textureU == null || (_textureU.width != chromaWidth || _textureU.height != chromaHeight))
                {
                    _textureU = new Texture2D(chromaWidth, chromaHeight, TextureFormat.R8, mipChain: false);
                    videoMaterial.SetTexture("_UPlane", _textureU);
                }
                if (_textureV == null || (_textureV.width != chromaWidth || _textureV.height != chromaHeight))
                {
                    _textureV = new Texture2D(chromaWidth, chromaHeight, TextureFormat.R8, mipChain: false);
                    videoMaterial.SetTexture("_VPlane", _textureV);
                }

                // Copy data from C# buffer into system memory managed by Unity.
                // Note: This only "looks right" in Unity because we apply the
                // "YUVFeedShader(Unlit)" to the texture (converting YUV planar to RGB).
                // Note: Texture2D.LoadRawTextureData() expects some bottom-up texture data but
                // the WebRTC video frame is top-down, so the image is uploaded vertically flipped,
                // and needs to be flipped by in the shader used to sample it. See #388.
                using (var profileScope = loadTextureDataMarker.Auto())
                {
                    unsafe
                    {
                        fixed (void* buffer = frame.Buffer)
                        {
                            var src = new IntPtr(buffer);
                            int lumaSize = lumaWidth * lumaHeight;
                            _textureY.LoadRawTextureData(src, lumaSize);
                            src += lumaSize;
                            int chromaSize = chromaWidth * chromaHeight;
                            _textureU.LoadRawTextureData(src, chromaSize);
                            src += chromaSize;
                            _textureV.LoadRawTextureData(src, chromaSize);
                        }
                    }
                }

                // Upload from system memory to GPU
                using (var profileScope = uploadTextureToGpuMarker.Auto())
                {
                    _textureY.Apply();
                    _textureU.Apply();
                    _textureV.Apply();
                }

                // Recycle the video frame packet for a later frame
                _i420aFrameQueue.RecycleStorage(frame);
            }
        }

        /// <summary>
        /// Internal helper that attempts to process frame data in the frame queue
        /// </summary>
        private void TryProcessArgb32Frame()
        {
            if (_argb32FrameQueue.TryDequeue(out Argb32VideoFrameStorage frame))
            {
                int width = (int)frame.Width;
                int height = (int)frame.Height;
                if (_textureY == null || (_textureY.width != width || _textureY.height != height))
                {
                    _textureY = new Texture2D(width, height, TextureFormat.BGRA32, mipChain: false);
                    videoMaterial.SetTexture("_MainTex", _textureY);
                }

                // Copy data from C# buffer into system memory managed by Unity.
                // Note: Texture2D.LoadRawTextureData() expects some bottom-up texture data but
                // the WebRTC video frame is top-down, so the image is uploaded vertically flipped,
                // and needs to be flipped by in the shader used to sample it. See #388.
                using (var profileScope = loadTextureDataMarker.Auto())
                {
                    unsafe
                    {
                        fixed (void* buffer = frame.Buffer)
                        {
                            var src = new IntPtr(buffer);
                            int size = width * height * 4;
                            _textureY.LoadRawTextureData(src, size);
                        }
                    }
                }

                // Upload from system memory to GPU
                using (var profileScope = uploadTextureToGpuMarker.Auto())
                {
                    _textureY.Apply();
                }

                // Recycle the video frame packet for a later frame
                _argb32FrameQueue.RecycleStorage(frame);
            }
        }
    }
}
