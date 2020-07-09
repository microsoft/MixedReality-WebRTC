// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Diagnostics;
using Microsoft.MixedReality.WebRTC;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace TestAppUwp
{
    /// <summary>
    /// Model for tracks, whether local or remote.
    /// </summary>
    public abstract class TrackViewModel
    {
        public readonly MediaTrack TrackImpl;
        public bool IsRemote => TrackImpl is RemoteAudioTrack || TrackImpl is RemoteVideoTrack;
        public readonly string DeviceName;

        protected TrackViewModel(MediaTrack track, string deviceName)
        {
            TrackImpl = track;
            DeviceName = deviceName;
        }

        public string DisplayName
        {
            get
            {
                string label;
                string deviceName = (string.IsNullOrWhiteSpace(DeviceName) ?
                    (IsRemote ? "Remote track" : "Custom track") : DeviceName);
                string videoTrackName = TrackImpl?.Name;
                if (!string.IsNullOrWhiteSpace(videoTrackName))
                {
                    label = $"{videoTrackName} ({deviceName})";
                }
                else
                {
                    // The empty track placeholder
                    Debug.Assert(videoTrackName == null);
                    Debug.Assert(DeviceName == null);
                    label = "<none>";
                }
                return label;
            }
        }
    }

    /// <summary>
    /// Model for audio tracks, whether local or remote, for output in audio player.
    /// </summary>
    public class AudioTrackViewModel : TrackViewModel
    {
        public AudioTrackSource Source => (TrackImpl as LocalAudioTrack)?.Source;
        public IAudioSource Track => (IAudioSource)TrackImpl;

        public AudioTrackViewModel(LocalAudioTrack track, string deviceName)
            : base(track, deviceName)
        {
        }
        public AudioTrackViewModel(RemoteAudioTrack track)
            : base(track, null)
        {
        }
    }

    /// <summary>
    /// Model for video tracks, whether local or remote, for rendering in video player.
    /// </summary>
    public class VideoTrackViewModel : TrackViewModel
    {
        public VideoTrackSource Source => (TrackImpl as LocalVideoTrack)?.Source;
        public IVideoSource Track => (IVideoSource)TrackImpl;
        public VideoTrackViewModel(LocalVideoTrack track, string deviceName)
            : base(track, deviceName)
        {
        }

        public VideoTrackViewModel(RemoteVideoTrack track)
            : base(track, null)
        {
        }
    }

    /// <summary>
    /// Page displaying the media player to preview the content of audio and video tracks,
    /// both local and remote.
    /// </summary>
    public sealed partial class MediaPlayerPage : Page
    {
        private readonly MediaPlayerViewModel _viewModel = new MediaPlayerViewModel();

        public MediaPlayerPage()
        {
            this.InitializeComponent();

            // Bind the XAML UI control (videoPlayerElement) to the MediaFoundation rendering pipeline (_videoPlayer)
            // so that the former can render in the UI the video frames produced in the background by the latter.
            videoPlayerElement.SetMediaPlayer(_viewModel.VideoPlayer);
        }

        public SessionModel SessionModel
        {
            get { return SessionModel.Current; }
        }

        //private async void AddAudioTrack_Click(object sender, RoutedEventArgs e)
        //{
        //    var viewModel = addAudioTrackTypeList.SelectedItem as AudioTrackTypeViewModel;
        //    if (viewModel == null)
        //    {
        //        return;
        //    }
        //    LocalAudioTrack audioTrack = await viewModel.Factory();
        //    if (audioTrack == null)
        //    {
        //        return;
        //    }
        //    var item = new AudioTrackViewModel
        //    {
        //        DeviceName = viewModel.DisplayName,
        //        IsRemote = false,
        //        Track = audioTrack,
        //        TrackImpl = audioTrack
        //    };
        //    SessionModel.Current.AudioTracks.Add(item);
        //    // Select the newly-created track for convenience
        //    SessionModel.Current.AudioTracks.SelectedItem = item;
        //}

        //private async void AddVideoTrack_Click(object sender, RoutedEventArgs e)
        //{
        //    var viewModel = addVideoTrackTypeList.SelectedItem as VideoTrackTypeViewModel;
        //    if (viewModel == null)
        //    {
        //        return;
        //    }
        //    LocalVideoTrack videoTrack = await viewModel.Factory();
        //    if (videoTrack == null)
        //    {
        //        return;
        //    }
        //    var item = new VideoTrackViewModel
        //    {
        //        DeviceName = viewModel.DisplayName,
        //        IsRemote = false,
        //        Track = videoTrack,
        //        TrackImpl = videoTrack
        //    };
        //    SessionModel.Current.VideoTracks.Add(item);
        //    // Select the newly-created track for convenience
        //    SessionModel.Current.VideoTracks.SelectedItem = item;
        //}

        private void MuteLocalVideoClicked(object sender, RoutedEventArgs e)
        {
            //if (_playbackVideoTrack is LocalVideoTrack localVideoTrack)
            //{
            //    if (localVideoTrack.Enabled)
            //    {
            //        localVideoTrack.Enabled = false;
            //        muteLocalVideoStroke.Visibility = Visibility.Visible;
            //    }
            //    else
            //    {
            //        localVideoTrack.Enabled = true;
            //        muteLocalVideoStroke.Visibility = Visibility.Collapsed;
            //    }
            //}
            //else
            //{
            //    throw new ArgumentException("Cannot mute remote video track.");
            //}
        }

        private void MuteLocalAudioClicked(object sender, RoutedEventArgs e)
        {
            //if (_playbackAudioTrack is LocalAudioTrack localAudioTrack)
            //{
            //    if (localAudioTrack.Enabled)
            //    {
            //        localAudioTrack.Enabled = false;
            //        muteLocalAudioStroke.Visibility = Visibility.Visible;
            //    }
            //    else
            //    {
            //        localAudioTrack.Enabled = true;
            //        muteLocalAudioStroke.Visibility = Visibility.Collapsed;
            //    }
            //}
            //else
            //{
            //    throw new ArgumentException("Cannot mute remote audio track.");
            //}
        }

        private void AudioTrackComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var audioTrackViewModel = (AudioTrackViewModel)audioTrackComboBox.SelectedItem;
            _viewModel.AudioTrack = audioTrackViewModel;
        }

        private void VideoTrackComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var videoTrackViewModel = (VideoTrackViewModel)videoTrackComboBox.SelectedItem;
            _viewModel.VideoTrack = videoTrackViewModel;
        }

        private void UpdateVideoStats(float loaded, float presented, float skipped)
        {
            localLoadText.Text = $"Receive: {loaded:F2}";
            localPresentText.Text = $"Render: {presented:F2}";
            localSkipText.Text = $"Skip: {skipped:F2}";
            //localLateText.Text = $"Late: {late:F2}";
        }

        private void ClearVideoStats()
        {
            localLoadText.Text = $"Receive: -";
            localPresentText.Text = $"Render: -";
            localSkipText.Text = $"Skip: -";
            //localLateText.Text = $"Late: -";
        }

        private void ClearRemoteAudioStats()
        {
            remoteAudioChannelCount.Text = "-";
            remoteAudioSampleRate.Text = "-";
        }

        private void UpdateRemoteAudioStats(uint channelCount, uint sampleRate)
        {
            remoteAudioChannelCount.Text = channelCount.ToString();
            remoteAudioSampleRate.Text = $"{sampleRate} Hz";
        }

        private void SwitchMediaPlayerSource(IAudioSource audioTrack, IVideoSource videoTrack)
        {
            //lock (_mediaPlaybackLock)
            //{
            //    if (videoTrack != _playbackVideoTrack)
            //    {
            //        // Notify media player that source changed
            //        if (_isVideoPlaying)
            //        {
            //            _videoStreamSource.NotifyError(MediaStreamSourceErrorStatus.ConnectionToServerLost);
            //            _videoPlayer.Pause();
            //            _isVideoPlaying = false;
            //        }

            //        // Detach old source
            //        if (_playbackVideoTrack != null)
            //        {
            //            _playbackVideoTrack.I420AVideoFrameReady -= VideoTrack_I420AFrameReady;
            //            _videoWidth = 0;
            //            _videoHeight = 0;
            //            ClearVideoStats();
            //        }

            //        _playbackVideoTrack = videoTrack;

            //        // Attach new source
            //        if (_playbackVideoTrack != null)
            //        {
            //            _playbackVideoTrack.I420AVideoFrameReady += VideoTrack_I420AFrameReady;
            //        }

            //        LogMessage($"Changed video playback track.");
            //    }

            //    if (audioTrack != _playbackAudioTrack)
            //    {
            //        // Detach old source
            //        if (_playbackAudioTrack != null)
            //        {
            //            _playbackAudioTrack.AudioFrameReady -= AudioTrack_FrameReady;
            //            _audioSampleRate = 0;
            //            _audioChannelCount = 0;
            //            ClearRemoteAudioStats();
            //        }

            //        _playbackAudioTrack = audioTrack;

            //        // Attach new source
            //        if (_playbackAudioTrack != null)
            //        {
            //            _playbackAudioTrack.AudioFrameReady += AudioTrack_FrameReady;
            //        }

            //        LogMessage($"Changed video playback track.");
            //    }
            //}

            //// Update local media overlay panel
            //bool hasLocalMedia = ((_playbackVideoTrack != null) || (_playbackAudioTrack != null));
            //localMediaPanel.Visibility = (hasLocalMedia ? Visibility.Visible : Visibility.Collapsed);
            //muteLocalAudio.IsEnabled = (_playbackAudioTrack != null);
            //muteLocalVideo.IsEnabled = (_playbackVideoTrack != null);
        }



    }
}
