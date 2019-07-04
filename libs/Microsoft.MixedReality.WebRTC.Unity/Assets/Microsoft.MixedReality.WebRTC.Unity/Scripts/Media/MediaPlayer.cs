// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using UnityEngine;
using Unity.Profiling;

namespace Microsoft.MixedReality.WebRTC.Unity
{
    /// <summary>
    /// Play video frames received from a WebRTC video track.
    /// </summary>
    /// <remarks>
    /// This component writes to the attached <a href="https://docs.unity3d.com/ScriptReference/Material.html">Material</a>,
    /// via the attached <a href="https://docs.unity3d.com/ScriptReference/Renderer.html">Renderer</a>.
    /// </remarks>
    [RequireComponent(typeof(Renderer))]
    [AddComponentMenu("MixedReality-WebRTC/Media Player")]
    public class MediaPlayer : MonoBehaviour
    {
        [Header("Source")]
        [Tooltip("Audio source providing the audio data used by this player")]
        public AudioSource AudioSource;

        [Tooltip("Video source providing the video frames rendered by this player")]
        public VideoSource VideoSource;

        [Tooltip("Max video playback framerate, in frames per second")]
        [Range(0.001f, 120f)]
        public float MaxVideoFramerate = 30f;

        [Header("Statistics")]
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

        /// <summary>
        /// The frame queue from which frames will be rendered.
        /// </summary>
        public VideoFrameQueue<I420VideoFrameStorage> FrameQueue = null;

        /// <summary>
        /// Internal reference to the attached texture
        /// </summary>
        private Texture2D _textureY = null;
        private Texture2D _textureU = null;
        private Texture2D _textureV = null;

        /// <summary>
        /// Internal timing counter
        /// </summary>
        private float lastUpdateTime = 0.0f;

        private Material videoMaterial;
        private float _minUpdateDelay;

        private ProfilerMarker displayStatsMarker = new ProfilerMarker("DisplayStats");
        private ProfilerMarker loadTextureDataMarker = new ProfilerMarker("LoadTextureData");
        private ProfilerMarker uploadTextureToGpuMarker = new ProfilerMarker("UploadTextureToGPU");

        private void Start()
        {
            CreateEmptyVideoTextures();

            // Leave 3ms of margin, otherwise it misses 1 frame and drops to ~20 FPS
            // when Unity is running at 60 FPS.
            _minUpdateDelay = Mathf.Max(0f, 1f / Mathf.Max(0.001f, MaxVideoFramerate) - 0.003f);

            AudioSource?.AudioStreamStarted.AddListener(AudioStreamStarted);
            AudioSource?.AudioStreamStopped.AddListener(AudioStreamStopped);
            VideoSource?.VideoStreamStarted.AddListener(VideoStreamStarted);
            VideoSource?.VideoStreamStopped.AddListener(VideoStreamStopped);
        }

        private void OnDestroy()
        {
            AudioSource?.AudioStreamStarted.RemoveListener(AudioStreamStarted);
            AudioSource?.AudioStreamStopped.RemoveListener(AudioStreamStopped);
            VideoSource?.VideoStreamStarted.RemoveListener(VideoStreamStarted);
            VideoSource?.VideoStreamStopped.RemoveListener(VideoStreamStopped);
        }

        private void AudioStreamStarted()
        {
        }

        private void AudioStreamStopped()
        {
        }

        private void VideoStreamStarted()
        {
            FrameQueue = VideoSource.FrameQueue;
        }

        private void VideoStreamStopped()
        {
            FrameQueue = null;

            // Clear the video display to not confuse the user who could otherwise
            // think that the video is still playing but is lagging.
            CreateEmptyVideoTextures();
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
            videoMaterial.SetTexture("_YPlane", _textureY);
            videoMaterial.SetTexture("_UPlane", _textureU);
            videoMaterial.SetTexture("_VPlane", _textureV);
        }

        /// <summary>
        /// Unity Engine Start() hook
        /// </summary>
        /// <remarks>
        /// https://docs.unity3d.com/ScriptReference/MonoBehaviour.Start.html
        /// </remarks>
        private void Update()
        {
            if (FrameQueue != null)
            {
#if UNITY_EDITOR
                // Inside the Editor, constantly update _minUpdateDelay to
                // react to user changes to MaxFramerate.

                // Leave 3ms of margin, otherwise it misses 1 frame and drops to ~20 FPS
                // when Unity is running at 60 FPS.
                _minUpdateDelay = Mathf.Max(0f, 1f / Mathf.Max(0.001f, MaxVideoFramerate) - 0.003f);
#endif
                var curTime = Time.time;
                if (curTime - lastUpdateTime >= _minUpdateDelay)
                {
                    TryProcessFrame();
                    lastUpdateTime = curTime;
                }

                if (EnableStatistics)
                {
                    // Share our stats values, if possible.
                    using (var profileScope = displayStatsMarker.Auto())
                    {
                        if (FrameLoadStatHolder != null)
                        {
                            FrameLoadStatHolder.text = FrameQueue.QueuedFramesPerSecond.ToString("F2");
                        }
                        if (FramePresentStatHolder != null)
                        {
                            FramePresentStatHolder.text = FrameQueue.DequeuedFramesPerSecond.ToString("F2");
                        }
                        if (FrameSkipStatHolder != null)
                        {
                            FrameSkipStatHolder.text = FrameQueue.DroppedFramesPerSecond.ToString("F2");
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Internal helper that attempts to process frame data in the frame queue
        /// </summary>
        private void TryProcessFrame()
        {
            if (FrameQueue.TryDequeue(out I420VideoFrameStorage frame))
            {
                int lumaWidth = (int)frame.Width;
                int lumaHeight = (int)frame.Height;
                if (_textureY == null || (_textureY.width != lumaWidth || _textureY.height != lumaHeight))
                {
                    _textureY = new Texture2D(lumaWidth, lumaHeight, TextureFormat.R8, false);
                    videoMaterial.SetTexture("_YPlane", _textureY);
                }
                int chromaWidth = lumaWidth / 2;
                int chromaHeight = lumaHeight / 2;
                if (_textureU == null || (_textureU.width != chromaWidth || _textureU.height != chromaHeight))
                {
                    _textureU = new Texture2D(chromaWidth, chromaHeight, TextureFormat.R8, false);
                    videoMaterial.SetTexture("_UPlane", _textureU);
                }
                if (_textureV == null || (_textureV.width != chromaWidth || _textureV.height != chromaHeight))
                {
                    _textureV = new Texture2D(chromaWidth, chromaHeight, TextureFormat.R8, false);
                    videoMaterial.SetTexture("_VPlane", _textureV);
                }

                // Copy data from C# buffer into system memory managed by Unity.
                // Note: This only "looks right" in Unity because we apply the 
                // "YUVFeedShader" to the texture (converting YUV planar to RGB).
                using (var profileScope = loadTextureDataMarker.Auto())
                {
                    unsafe
                    {
                        fixed (void* buffer = frame.Buffer)
                        {
                            var src = new System.IntPtr(buffer);
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
                FrameQueue.RecycleStorage(frame);
            }
        }
    }
}
