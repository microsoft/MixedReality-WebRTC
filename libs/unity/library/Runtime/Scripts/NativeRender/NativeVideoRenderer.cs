// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using UnityEngine;
using Microsoft.MixedReality.WebRTC.UnityPlugin;
using UnityEngine.UI;

namespace Microsoft.MixedReality.WebRTC
{
	/// <summary>
	/// This will render the video stream through native calls with DX11 or OpenGL, completely bypassing C# marshalling.
	/// This provides a considerable performance improvement.
	/// </summary>
    [AddComponentMenu("MixedReality-WebRTC/Native Video Renderer")]
    public class NativeVideoRenderer : MonoBehaviour
    {
	    [SerializeField]
	    private RawImage _rawImage;

        private RemoteVideoTrack _source;
        private Material videoMaterial;
        private NativeRenderer nativeRenderer;

        private Texture2D textureY;
        private Texture2D textureU;
        private Texture2D textureV;

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
	            if (_i420aFrameQueue.TryDequeue(out I420AVideoFrameStorage frame))
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
	        // Clear the video display to not confuse the user who could otherwise
	        // think that the video is still playing but is lagging/frozen.
	        _i420aFrameQueue.Enqueue(frame);
        }

        private void StartNativeRendering(int width, int height)
        {
	        // Subscription is only used to ge the frame dimensions to generate the textures. So Unsubscribe once that is done.
	        _source.I420AVideoFrameReady -= I420AVideoFrameReady;
	        _i420aFrameQueue = null;

	        CreateEmptyVideoTextures(width, height, 128);
	        nativeRenderer = new NativeRenderer(_source.PeerConnection); //the need for this peer connection can be gotten rid of, it should just go off video handles
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
		        nativeRenderer?.DisableRemoteVideo();
		        nativeRenderer?.Dispose();
	        }
	        finally
	        {
		        nativeRenderer = null;
	        }
	        _source = null;
        }

        private void RegisterRemoteTextures()
        {
	        if (nativeRenderer != null && textureY != null)
            {
                TextureDesc[] textures = new TextureDesc[3]
                {
                    new TextureDesc
                    {
                        texture = textureY.GetNativeTexturePtr(),
                        width = textureY.width,
                        height = textureY.height,
                    },
                    new TextureDesc
                    {
                        texture = textureU.GetNativeTexturePtr(),
                        width = textureU.width,
                        height = textureU.height,
                    },
                    new TextureDesc
                    {
                        texture = textureV.GetNativeTexturePtr(),
                        width = textureV.width,
                        height = textureV.height,
                    },
                };
#if WEBRTC_DEBUGGING
                Debug.Log(string.Format("RegisteringRemoteTextures: {0:X16}, {1:X16}, {2:X16}", textures[0].texture.ToInt64(), textures[1].texture.ToInt64(), textures[2].texture.ToInt64()));
#endif
                nativeRenderer.EnableRemoteVideo(VideoKind.I420, textures);
            }
        }

        private void CreateEmptyVideoTextures(int width, int height, byte defaultY)
		{
			videoMaterial = _rawImage.material;

			int lumaWidth = width;
			int lumaHeight = height;
			int chromaWidth = lumaWidth / 2;
			int chromaHeight = lumaHeight / 2;

			if (textureY == null || (textureY.width != lumaWidth || textureY.height != lumaHeight))
			{
				textureY = new Texture2D(lumaWidth, lumaHeight, TextureFormat.R8, false, true);
			}
			if (textureU == null || (textureU.width != chromaWidth || textureU.height != chromaHeight))
			{
				textureU = new Texture2D(chromaWidth, chromaHeight, TextureFormat.R8, false, true);
			}
			if (textureV == null || (textureV.width != chromaWidth || textureV.height != chromaHeight))
			{
				textureV = new Texture2D(chromaWidth, chromaHeight, TextureFormat.R8, false, true);
			}

			{
				byte[] pixels = textureY.GetRawTextureData();
				for (int i = 0; i < textureY.width * textureY.height; ++i)
				{
					pixels[i] = defaultY;
				}
				textureY.LoadRawTextureData(pixels);
				textureY.Apply();
			}
			{
				byte[] pixels = textureU.GetRawTextureData();
				for (int h = 0, index = 0; h < textureU.height; ++h)
				{
					float value = 255f * h / textureU.height;
					for (int w = 0; w < textureU.width; ++w, ++index)
					{
						pixels[index] = (byte)value;
					}
				}
				textureU.LoadRawTextureData(pixels);
				textureU.Apply();
			}
			{
				byte[] pixels = textureV.GetRawTextureData();
				for (int h = 0, index = 0; h < textureV.height; ++h)
				{
					for (int w = 0; w < textureV.width; ++w, ++index)
					{
						float value = 255f * w / textureV.width;
						pixels[index] = (byte)value;
					}
				}
				textureV.LoadRawTextureData(pixels);
				textureV.Apply();
			}

			videoMaterial.SetTexture("_UPlane", textureU);
			videoMaterial.SetTexture("_YPlane", textureY);
			videoMaterial.SetTexture("_VPlane", textureV);
		}
    }
}
