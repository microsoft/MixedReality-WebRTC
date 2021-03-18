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

        private int _dirtyWidth;
        private int _dirtyHeight;
        
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
            if (_nativeVideo != null && 
                (_textureY == null || _textureY.width != _dirtyWidth || _textureY.height != _dirtyHeight) &&
                _dirtyWidth != 0 && _dirtyHeight != 0)
            {
                switch (_source.FrameEncoding)
                {
                    case VideoEncoding.I420A:
                        CreateEmptyVideoTextures(_dirtyWidth, _dirtyHeight, 128);
                        _nativeVideo.UpdateRemoteTextures(VideoKind.I420, GetTextureDescArray());
                        break;
                    case VideoEncoding.Argb32:
                        break;
                }
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
                    _nativeVideo = new NativeVideo(_source.NativeHandle);
                    _nativeVideo.TextureSizeChanged += TextureSizeChangeCallback;
                    _nativeVideo.EnableRemoteVideo(VideoKind.I420, null);
                    break;
                case VideoEncoding.Argb32:
                    break;
            }
        }
        
        private void TextureSizeChangeCallback(int width, int height, IntPtr videoHandle)
        {
            // This may get called many times from different threads.
            Debug.Log("TextureSizeChangeCallback " + width + " " + height);
            _dirtyWidth = width;
            _dirtyHeight = height;
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

        private void CreateEmptyVideoTextures(int width, int height, byte defaultValue)
        {
            Debug.Log($"Creating empty textures {width} {height}");
            
            _dirtyWidth = width;
            _dirtyHeight = height;
            
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
                    pixels[i] = defaultValue;
                }
                _textureY.LoadRawTextureData(pixels);
                _textureY.Apply();
            }
            {
                byte[] pixels = _textureU.GetRawTextureData();
                for (int h = 0, index = 0; h < _textureU.height; ++h)
                {
                    for (int w = 0; w < _textureU.width; ++w, ++index)
                    {
                        pixels[index] = defaultValue;
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
                        pixels[index] = defaultValue;
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
