// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using Microsoft.MixedReality.WebRTC;
using TestAppUwp.Video;
using Windows.Media.Core;
using Windows.Media.MediaProperties;
using Windows.Media.Playback;
using Windows.UI.Xaml;

namespace TestAppUwp
{
    public delegate Task<LocalAudioTrack> AudioTrackFactoryDelegate();
    public delegate Task<LocalVideoTrack> VideoTrackFactoryDelegate();

    public class AudioTrackTypeViewModel
    {
        public string DisplayName;
        public AudioTrackFactoryDelegate Factory;
    }

    public class VideoTrackTypeViewModel
    {
        public string DisplayName;
        public VideoTrackFactoryDelegate Factory;
    }

    public class VideoCaptureFormatViewModel
    {
        public VideoCaptureFormat Format;
        public string FormatEncodingDisplayName;
    }

    public class MediaPlayerViewModel : NotifierBase
    {
        public ObservableCollection<AudioTrackTypeViewModel> AudioTrackTypeList { get; }
            = new ObservableCollection<AudioTrackTypeViewModel>();

        public ObservableCollection<VideoTrackTypeViewModel> VideoTrackTypeList { get; }
            = new ObservableCollection<VideoTrackTypeViewModel>();

        public MediaPlayer VideoPlayer => _videoPlayer;

        public MediaPlaybackState VideoPlaybackState
        {
            get { return _videoPlayer.PlaybackSession.PlaybackState; }
        }

        public AudioTrackViewModel AudioTrack
        {
            get { return _playbackAudioTrack; }
            set { SetAudioTrack(value); }
        }

        public VideoTrackViewModel VideoTrack
        {
            get { return _playbackVideoTrack; }
            set { SetVideoTrack(value); }
        }

        public uint FrameWidth
        {
            get
            {
                lock (_mediaPlaybackLock)
                {
                    return _videoWidth;
                }
            }
        }

        public uint FrameHeight
        {
            get
            {
                lock (_mediaPlaybackLock)
                {
                    return _videoHeight;
                }
            }
        }

        public float FrameLoaded
        {
            get { return _videoBridge.FrameLoad; }
        }

        public float FramePresented
        {
            get { return _videoBridge.FramePresent; }
        }

        public float FrameSkipped
        {
            get { return _videoBridge.FrameSkip; }
        }

        public float FrameLate
        {
            get { return _videoBridge.LateFrame; }
        }

        public MediaPlayerViewModel()
        {
            _videoPlayer.CurrentStateChanged += OnMediaStateChanged;
            _videoPlayer.MediaOpened += OnMediaOpened;
            _videoPlayer.MediaFailed += OnMediaFailed;
            _videoPlayer.MediaEnded += OnMediaEnded;
            _videoPlayer.RealTimePlayback = true;
            _videoPlayer.AutoPlay = false;

            AudioTrackTypeList.Add(new AudioTrackTypeViewModel
            {
                DisplayName = "Local microphone (default device)",
                Factory = async () =>
                {
                    // FIXME - this leaks 'source', never disposed (and is the track itself disposed??)
                    var source = await DeviceAudioTrackSource.CreateAsync();
                    var settings = new LocalAudioTrackInitConfig();
                    return LocalAudioTrack.CreateFromSource(source, settings);
                }
            });

            VideoTrackTypeList.Add(new VideoTrackTypeViewModel
            {
                DisplayName = "Local webcam (default device)",
                Factory = async () =>
                {
                    // FIXME - this leaks 'source', never disposed (and is the track itself disposed??)
                    var source = await DeviceVideoTrackSource.CreateAsync();
                    var settings = new LocalVideoTrackInitConfig();
                    return LocalVideoTrack.CreateFromSource(source, settings);
                }
            });

            _videoStatsTimer.Interval = TimeSpan.FromMilliseconds(300);
            _videoStatsTimer.Tick += (_1, _2) => UpdateVideoStats();
        }

        /// <summary>
        /// Set the audio track used as the audio source of the media player.
        /// </summary>
        /// <param name="audioTrack">The audio track to use.</param>
        public void SetAudioTrack(AudioTrackViewModel audioTrack)
        {
            if (_playbackAudioTrack == audioTrack)
            {
                return;
            }

            // Detach old source
            if (_playbackAudioTrack?.Track != null)
            {
                _playbackAudioTrack.Track.AudioFrameReady -= AudioTrack_FrameReady;
                _audioSampleRate = 0;
                _audioChannelCount = 0;
                //ClearAudioStats();
            }

            _playbackAudioTrack = audioTrack;

            // Attach new source
            if (_playbackAudioTrack?.Track != null)
            {
                _playbackAudioTrack.Track.AudioFrameReady += AudioTrack_FrameReady;
            }

            RaisePropertyChanged("AudioTrack");
        }

        /// <summary>
        /// Set the video track used as the video source of the media player.
        /// </summary>
        /// <param name="videoTrack">The video track to use.</param>
        public void SetVideoTrack(VideoTrackViewModel videoTrack)
        {
            if (_playbackVideoTrack == videoTrack)
            {
                return;
            }

            // Notify media player that source changed
            if (_isVideoPlaying)
            {
                _videoStreamSource.NotifyError(MediaStreamSourceErrorStatus.ConnectionToServerLost);
                _videoPlayer.Pause();
                _isVideoPlaying = false;
            }

            // Detach old source
            if (_playbackVideoTrack?.Track != null)
            {
                try
                {
                    _playbackVideoTrack.Track.I420AVideoFrameReady -= VideoTrack_I420AFrameReady;
                }
                catch (ObjectDisposedException)
                {
                    // This may happen if the track is remote and has already been disposed. Ignore.
                }
                _videoWidth = 0;
                _videoHeight = 0;
                _videoStatsTimer.Stop();
                _videoBridge.Clear();
                UpdateVideoStats();
                ThreadHelper.RunOnMainThread(() =>
                {
                    RaisePropertyChanged("FrameWidth");
                    RaisePropertyChanged("FrameHeight");
                });
            }

            _playbackVideoTrack = videoTrack;

            // Attach new source
            if (_playbackVideoTrack?.Track != null)
            {
                _playbackVideoTrack.Track.I420AVideoFrameReady += VideoTrack_I420AFrameReady;
                _videoStatsTimer.Start();
            }

            RaisePropertyChanged("VideoTrack");
        }

        private VideoBridge _videoBridge = new VideoBridge(3);
        private readonly object _mediaPlaybackLock = new object();
        private MediaStreamSource _videoStreamSource = null;
        private MediaSource _videoSource = null;
        private readonly MediaPlayer _videoPlayer = new MediaPlayer();
        private bool _isVideoPlaying = false;
        private bool _isAudioPlaying = false;
        private AudioTrackViewModel _playbackAudioTrack = null;
        private VideoTrackViewModel _playbackVideoTrack = null;
        private uint _videoWidth = 0;
        private uint _videoHeight = 0;
        private uint _audioChannelCount = 0;
        private uint _audioSampleRate = 0;
        private DispatcherTimer _videoStatsTimer = new DispatcherTimer();

        /// <summary>
        /// Create a new 30-fps NV12-encoded video source for the specified video size.
        /// </summary>
        /// <param name="width">The width of the video in pixels.</param>
        /// <param name="height">The height of the video in pixels.</param>
        /// <returns>The newly created video source.</returns>
        private MediaStreamSource CreateVideoStreamSource(uint width, uint height, uint framerate)
        {
            if (width == 0)
            {
                throw new ArgumentException("Invalid zero width for video stream source.", "width");
            }
            if (height == 0)
            {
                throw new ArgumentException("Invalid zero height for video stream source.", "height");
            }

            // Note: IYUV and I420 have same memory layout (though different FOURCC)
            // https://docs.microsoft.com/en-us/windows/desktop/medfound/video-subtype-guids
            var videoProperties = VideoEncodingProperties.CreateUncompressed(MediaEncodingSubtypes.Iyuv, width, height);
            var videoStreamDesc = new VideoStreamDescriptor(videoProperties);
            videoStreamDesc.EncodingProperties.FrameRate.Numerator = framerate;
            videoStreamDesc.EncodingProperties.FrameRate.Denominator = 1;
            videoStreamDesc.EncodingProperties.Bitrate = (framerate * width * height * 12); // NV12=12bpp
            var videoStreamSource = new MediaStreamSource(videoStreamDesc);
            videoStreamSource.BufferTime = TimeSpan.Zero; // TODO : playback breaks if buffering, need to investigate
            videoStreamSource.Starting += OnMediaStreamSourceStarting;
            videoStreamSource.Closed += OnMediaStreamSourceClosed;
            videoStreamSource.Paused += OnMediaStreamSourcePaused;
            videoStreamSource.SampleRequested += OnMediaStreamSourceRequested;
            videoStreamSource.IsLive = true; // Enables optimizations for live sources
            videoStreamSource.CanSeek = false; // Cannot seek live WebRTC video stream
            return videoStreamSource;
        }

        private void OnMediaStreamSourceStarting(MediaStreamSource sender, MediaStreamSourceStartingEventArgs args)
        {
            //Debug.Assert(Dispatcher.HasThreadAccess == false);
            Logger.Log("Video playback stream source starting...");
            args.Request.SetActualStartPosition(TimeSpan.Zero);
        }

        private void OnMediaStreamSourceClosed(MediaStreamSource sender, MediaStreamSourceClosedEventArgs args)
        {
            //Debug.Assert(Dispatcher.HasThreadAccess == false);
            Logger.Log("Video playback stream source closed.");
        }

        private void OnMediaStreamSourcePaused(MediaStreamSource sender, object args)
        {
            //Debug.Assert(Dispatcher.HasThreadAccess == false);
            Logger.Log("Video playback stream source paused.");
        }

        /// <summary>
        /// Callback from the Media Foundation pipeline when a new video frame is needed.
        /// </summary>
        /// <param name="sender">The stream source requesting a new sample.</param>
        /// <param name="args">The sample request to fullfil.</param>
        private void OnMediaStreamSourceRequested(MediaStreamSource sender, MediaStreamSourceSampleRequestedEventArgs args)
        {
            _videoBridge.TryServeVideoFrame(args);
        }

        private void OnMediaStateChanged(MediaPlayer sender, object args)
        {
            if (sender == _videoPlayer)
            {
                Logger.Log($"Media player state changed to {sender.PlaybackSession.PlaybackState}.");
                ThreadHelper.RunOnMainThread(() => RaisePropertyChanged("VideoPlaybackState"));
            }
        }

        /// <summary>
        /// Callback on Media Foundation pipeline media successfully opened and
        /// ready to be played in a local media player.
        /// </summary>
        /// <param name="sender">The <see xref="MediaPlayer"/> source object owning the media.</param>
        /// <param name="args">(unused)</param>
        private void OnMediaOpened(MediaPlayer sender, object args)
        {
            // Now it is safe to call Play() on the MediaElement
            //ThreadHelper.RunOnMainThread(() => sender.Play());
            sender.Play();
        }

        /// <summary>
        /// Callback on Media Foundation pipeline media failed to open or to continue playback.
        /// </summary>
        /// <param name="sender">The <see xref="MediaPlayer"/> source object owning the media.</param>
        /// <param name="args">(unused)</param>
        private void OnMediaFailed(MediaPlayer sender, MediaPlayerFailedEventArgs args)
        {
            Logger.Log($"MediaElement reported an error: \"{args.ErrorMessage}\" (\"{args.ExtendedErrorCode.Message}\")");
        }

        /// <summary>
        /// Callback on Media Foundation pipeline media ended playback.
        /// </summary>
        /// <param name="sender">The <see xref="MediaPlayer"/> source object owning the media.</param>
        /// <param name="args">(unused)</param>
        /// <remarks>This appears to never be called for live sources.</remarks>
        private void OnMediaEnded(MediaPlayer sender, object args)
        {
            //ThreadHelper.RunOnMainThread(() =>
            {
                Logger.Log("Local MediaElement video playback ended.");
                //StopLocalMedia();
                sender.Pause();
                sender.Source = null;
                if (sender == _videoPlayer)
                {
                    //< TODO - This should never happen. But what to do with
                    //         local channels if it happens?
                    lock (_mediaPlaybackLock)
                    {
                        _videoStreamSource.NotifyError(MediaStreamSourceErrorStatus.Other);
                        _videoStreamSource = null;
                        _videoSource.Dispose();
                        _videoSource = null;
                        _isVideoPlaying = false;
                    }
                }
            }//);
        }


        /// <summary>
        /// Stop playback of the local video from the local webcam, and remove the local
        /// audio and video tracks from the peer connection. This is called on the UI thread.
        /// </summary>
        private void StopLocalMedia()
        {
            lock (_mediaPlaybackLock)
            {
                if (_isVideoPlaying)
                {
                    _videoPlayer.Pause();
                    _videoPlayer.Source = null;
                    //videoPlayerElement.SetMediaPlayer(null);
                    _videoStreamSource.NotifyError(MediaStreamSourceErrorStatus.Other);
                    _videoStreamSource = null;
                    _videoSource.Dispose();
                    _videoSource = null;
                    _isVideoPlaying = false;

                    //_playbackAudioTrack.AudioFrameReady -= AudioTrack_FrameReady;
                    //_playbackVideoTrack.I420AVideoFrameReady -= VideoTrack_I420AFrameReady;
                }
            }

            //LocalVideoTracks.Remove(LocalVideoTracks.Single(t => t.Track == _playbackVideoTrack));
            //videoTrackComboBox.IsEnabled = (LocalVideoTracks.Count > 0);

            //// Avoid deadlock in audio processing stack, as this call is delegated to the WebRTC
            //// signaling thread (and will block the caller thread), and audio processing will
            //// delegate to the UI thread for UWP operations (and will block the signaling thread).
            //await RunOnWorkerThread(() =>
            //{
            //    lock (_mediaPlaybackLock)
            //    {
            //        SelectedAudioTransceiver.LocalAudioTrack = null;
            //        SelectedVideoTransceiver.LocalVideoTrack = null;
            //        _sdpSessionViewModel.AutomatedNegotiation = true;
            //        if (_peerConnection.IsConnected)
            //        {
            //            _sdpSessionViewModel.StartNegotiation();
            //        }
            //        _playbackAudioTrack.Dispose();
            //        _playbackAudioTrack = null;
            //        _playbackVideoTrack.Dispose();
            //        _playbackVideoTrack = null;
            //    }
            //});

        }


        /// <summary>
        /// Callback on video frame received from the local video capture device,
        /// for local rendering before (or in parallel of) being sent to the remote peer.
        /// </summary>
        /// <param name="frame">The newly captured video frame.</param>
        private void VideoTrack_I420AFrameReady(I420AVideoFrame frame)
        {
            // Lazily start the video media player when receiving the first video frame from
            // the video track. Currently there is no exposed API to tell once connected that
            // the remote peer will be sending some video track, so handle local and remote
            // video tracks the same for simplicity.
            bool needNewSource = false;
            uint width = frame.width;
            uint height = frame.height;
            lock (_mediaPlaybackLock)
            {
                if (!_isVideoPlaying)
                {
                    _isVideoPlaying = true;
                    _videoWidth = width;
                    _videoHeight = height;
                    needNewSource = true;
                }
                else if ((width != _videoWidth) || (height != _videoHeight))
                {
                    _videoWidth = width;
                    _videoHeight = height;
                    needNewSource = true;
                }
            }
            if (needNewSource)
            {
                // We don't know the remote video framerate yet, so use a default.
                uint framerate = 30;
                //RunOnMainThread(() =>
                {
                    Logger.Log($"Creating new video source: {width}x{height}@{framerate}");
                    _videoPlayer.Pause();
                    //_videoPlayer.Source = null;
                    _videoStreamSource?.NotifyError(MediaStreamSourceErrorStatus.Other);
                    _videoSource?.Dispose();
                    _videoStreamSource = CreateVideoStreamSource(width, height, framerate);
                    _videoSource = MediaSource.CreateFromMediaStreamSource(_videoStreamSource);
                    _videoPlayer.Source = _videoSource;
                }//);

                ThreadHelper.RunOnMainThread(() =>
                {
                    RaisePropertyChanged("FrameWidth");
                    RaisePropertyChanged("FrameHeight");
                });
            }

            _videoBridge.HandleIncomingVideoFrame(frame);
        }

        /// <summary>
        /// Callback on audio frame produced by the local peer.
        /// </summary>
        /// <param name="frame">The newly produced audio frame.</param>
        private void AudioTrack_FrameReady(AudioFrame frame)
        {
            //uint channelCount = frame.channelCount;
            //uint sampleRate = frame.sampleRate;
            //bool sourceChanged = false;
            //lock (_mediaPlaybackLock)
            //{
            //    if (!_isAudioPlaying || (_audioChannelCount != channelCount) || (_audioSampleRate != sampleRate))
            //    {
            //        _isAudioPlaying = true;
            //        _audioChannelCount = channelCount;
            //        _audioSampleRate = sampleRate;
            //        sourceChanged = true;
            //    }
            //}

            //if (sourceChanged)
            //{
            //    // As an example of handling, update the UI to display the number of audio
            //    // channels and the sample rate of the audio track.
            //    //RunOnMainThread(() => UpdateRemoteAudioStats(channelCount, sampleRate));
            //}
        }

        private void UpdateVideoStats()
        {
            RaisePropertyChanged("FrameLoaded");
            RaisePropertyChanged("FramePresented");
            RaisePropertyChanged("FrameSkipped");
            RaisePropertyChanged("FrameLate");
        }
    }
}
