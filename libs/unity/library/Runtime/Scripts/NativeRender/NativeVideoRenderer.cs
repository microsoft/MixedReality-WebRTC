// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using UnityEngine;
using Microsoft.MixedReality.WebRTC.UnityPlugin;
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
        private NativeRenderer _nativeRenderer;

        private Texture2D _textureY;
        private Texture2D _textureU;
        private Texture2D _textureV;

        private VideoFrameQueue<I420AVideoFrameStorage> _i420aFrameQueue;

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
            if (_i420aFrameQueue != null)
            {
	            if (_i420aFrameQueue.TryDequeue(out I420AVideoFrameStorage frame) && _nativeRenderer == null)
	            {
		            StartNativeRendering((int)frame.Width, (int)frame.Height);
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
	                _i420aFrameQueue = new VideoFrameQueue<I420AVideoFrameStorage>(2);
	                _source.I420AVideoFrameReady += I420AVideoFrameReady;
                    break;
                case VideoEncoding.Argb32:
	                break;
            }
        }

        private void I420AVideoFrameReady(I420AVideoFrame frame)
        {
	        _i420aFrameQueue?.Enqueue(frame);
        }

        private void StartNativeRendering(int width, int height)
        {
	        // Subscription is only used to ge the frame dimensions to generate the textures. So Unsubscribe once that is done.
	        _source.I420AVideoFrameReady -= I420AVideoFrameReady;
	        _i420aFrameQueue = null;

	        CreateEmptyVideoTextures(width, height, 128);
	        _nativeRenderer = new NativeRenderer(_source.NativeHandle); //the need for this peer connectionc an be gotten rid of, it should just go off video handles
	        RegisterRemoteTextures();
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
		        _nativeRenderer?.DisableRemoteVideo();
		        _nativeRenderer?.Dispose();
	        }
	        finally
	        {
		        _nativeRenderer = null;
	        }
	        _source = null;
        }

        private void RegisterRemoteTextures()
        {
	        if (_nativeRenderer != null && _textureY != null)
            {
                TextureDesc[] textures = new TextureDesc[3]
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
#if WEBRTC_DEBUGGING
                Debug.Log(string.Format("RegisteringRemoteTextures: {0:X16}, {1:X16}, {2:X16}", textures[0].texture.ToInt64(), textures[1].texture.ToInt64(), textures[2].texture.ToInt64()));
#endif
                _nativeRenderer.EnableRemoteVideo(VideoKind.I420, textures);
            }
        }

        private void CreateEmptyVideoTextures(int width, int height, byte defaultY)
		{
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
