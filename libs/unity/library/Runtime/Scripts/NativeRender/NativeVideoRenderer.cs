// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace Microsoft.MixedReality.WebRTC.Unity
{
    /// <summary>
    /// This will render the video stream through native calls with DX11 or OpenGL, completely bypassing C# marshalling.
    /// This provides a considerable performance improvement compared to <see cref="VideoRenderer"/>.
    /// </summary>
    [AddComponentMenu("MixedReality-WebRTC/Native Video Renderer")]
    public class NativeVideoRenderer : MonoBehaviour
    {
        [SerializeField]
        private RawImage _rawImage;

        private RemoteVideoTrack _source;
        private Material _videoMaterial;
        private NativeVideo _nativeVideo;

        private Texture2D _textureY;
        private Texture2D _textureU;
        private Texture2D _textureV;

        private VideoFrameQueue<I420AVideoFrameStorage> _i420AFrameQueue = new VideoFrameQueue<I420AVideoFrameStorage>(2);

        private int dirtyWidth;
        private int dirtyHeight;
        
        private void Awake()
        {
            NativeRenderingPluginUpdate.AddRef(this);
        }

        private void OnDestroy()
        {
            NativeRenderingPluginUpdate.DecRef(this);

            // Teardown will also get called when the PeerConnection is destroyed.
            // So this is probably going to be called twice in many cases.
            TearDown();
        }

        private void Update()
        {
            if (_nativeVideo != null && (_textureY.width != dirtyWidth || _textureY.height != dirtyHeight))
            {
                CreateEmptyVideoTextures(dirtyWidth, dirtyHeight, 128);
                _nativeVideo.UpdateTextures(GetTextureDescArray());
            }
            
            if (_nativeVideo == null && _i420AFrameQueue.TryDequeue(out I420AVideoFrameStorage frame))
            {
                StartNativeRendering((int)frame.Width, (int)frame.Height);
            }
        }

        /// <summary>
        /// Start rendering the passed source.
        /// </summary>
        /// <remarks>
        /// Can be used to handle <see cref="VideoTrackSource.VideoStreamStarted"/> or <see cref="VideoReceiver.VideoStreamStarted"/>.
        /// </remarks>
        public void StartRendering(IVideoSource source)
        {
            _source = source as RemoteVideoTrack;
            Debug.Assert(_source != null, "NativeVideoRender currently only supports RemoteVideoTack");

            switch (source.FrameEncoding)
            {
                case VideoEncoding.I420A:
                    _i420AFrameQueue.Clear();
                    _source.I420AVideoFrameReady += I420AVideoFrameReady;
                    break;
                case VideoEncoding.Argb32:
                    break;
            }
        }

        private void I420AVideoFrameReady(I420AVideoFrame frame)
        {
            _i420AFrameQueue?.Enqueue(frame);
        }

        private void StartNativeRendering(int width, int height)
        {
            // Subscription is only used to ge the frame dimensions to generate the textures. So Unsubscribe once that is done.
            _source.I420AVideoFrameReady -= I420AVideoFrameReady;
            _i420AFrameQueue.Clear();

            CreateEmptyVideoTextures(width, height, 128);
            _nativeVideo = new NativeVideo(_source.NativeHandle);
            _nativeVideo.TextureSizeChanged += TextureSizeChangeCallback;
            RegisterRemoteTextures();
        }
        
        private void TextureSizeChangeCallback(int width, int height, IntPtr videoHandle)
        {
            // This may get called many times from different threads.
            Debug.Log("TextureSizeChangeCallback " + width + " " + height);
            // nativeTextureDirty = true;
            dirtyWidth = width;
            dirtyHeight = height;
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
            CreateEmptyVideoTextures(4,4,128);
            TearDown();
        }

        private void TearDown()
        {
            try
            {
                _nativeVideo?.DisableRemoteVideo();
                _nativeVideo?.Dispose();
            }
            finally
            {
                _nativeVideo = null;
            }
            _source = null;
        }
        
        private void RegisterRemoteTextures()
        {
            if (_nativeVideo != null && _textureY != null)
            {
                TextureDesc[] textures = GetTextureDescArray();
#if WEBRTC_DEBUGGING
                Debug.Log(string.Format("RegisteringRemoteTextures: {0:X16}, {1:X16}, {2:X16}", textures[0].texture.ToInt64(), textures[1].texture.ToInt64(), textures[2].texture.ToInt64()));
#endif
                _nativeVideo.EnableRemoteVideo(VideoKind.I420, textures);
            }
        }

        private TextureDesc[] GetTextureDescArray()
        {
            return new TextureDesc[3]
            {
                new TextureDesc
                {
                    texture = _textureY.GetNativeTexturePtr(),
                    width = _textureY.width,
                    height = _textureY.height,
                },
                new TextureDesc
                {
                    texture = _textureU.GetNativeTexturePtr(),
                    width = _textureU.width,
                    height = _textureU.height,
                },
                new TextureDesc
                {
                    texture = _textureV.GetNativeTexturePtr(),
                    width = _textureV.width,
                    height = _textureV.height,
                },
            };
        }

        private void CreateEmptyVideoTextures(int width, int height, byte defaultY)
        {
            Debug.Log($"Creating empty textures {width} {height}");
            
            dirtyWidth = width;
            dirtyHeight = height;
            
            _videoMaterial = _rawImage.material;

            int lumaWidth = width;
            int lumaHeight = height;
            int chromaWidth = lumaWidth / 2;
            int chromaHeight = lumaHeight / 2;

            if (_textureY == null || (_textureY.width != lumaWidth || _textureY.height != lumaHeight))
            {
                _textureY = new Texture2D(lumaWidth, lumaHeight, TextureFormat.R8, false, true);
            }
            if (_textureU == null || (_textureU.width != chromaWidth || _textureU.height != chromaHeight))
            {
                _textureU = new Texture2D(chromaWidth, chromaHeight, TextureFormat.R8, false, true);
            }
            if (_textureV == null || (_textureV.width != chromaWidth || _textureV.height != chromaHeight))
            {
                _textureV = new Texture2D(chromaWidth, chromaHeight, TextureFormat.R8, false, true);
            }

            {
                byte[] pixels = _textureY.GetRawTextureData();
                for (int i = 0; i < _textureY.width * _textureY.height; ++i)
                {
                    pixels[i] = defaultY;
                }
                _textureY.LoadRawTextureData(pixels);
                _textureY.Apply();
            }
            {
                byte[] pixels = _textureU.GetRawTextureData();
                for (int h = 0, index = 0; h < _textureU.height; ++h)
                {
                    float value = 255f * h / _textureU.height;
                    for (int w = 0; w < _textureU.width; ++w, ++index)
                    {
                        pixels[index] = (byte)value;
                    }
                }
                _textureU.LoadRawTextureData(pixels);
                _textureU.Apply();
            }
            {
                byte[] pixels = _textureV.GetRawTextureData();
                for (int h = 0, index = 0; h < _textureV.height; ++h)
                {
                    for (int w = 0; w < _textureV.width; ++w, ++index)
                    {
                        float value = 255f * w / _textureV.width;
                        pixels[index] = (byte)value;
                    }
                }
                _textureV.LoadRawTextureData(pixels);
                _textureV.Apply();
            }
            
            _videoMaterial.SetTexture("_UPlane", _textureU);
            _videoMaterial.SetTexture("_YPlane", _textureY);
            _videoMaterial.SetTexture("_VPlane", _textureV);
        }
    }
}
