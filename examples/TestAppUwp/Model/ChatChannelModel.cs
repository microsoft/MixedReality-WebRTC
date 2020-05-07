// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.MixedReality.WebRTC;

namespace TestAppUwp
{
    /// <summary>
    /// Model for a chat channel backed by a WebRTC data channel.
    /// </summary>
    public class ChatChannelModel : NotifierBase
    {
        /// <summary>
        /// Backing WebRTC data channel used to transmit the chat messages.
        /// </summary>
        public DataChannel DataChannel { get; }

        /// <summary>
        /// Full chat text.
        /// </summary>
        public string FullText
        {
            get { return _fullText; }
            set { SetProperty(ref _fullText, value); }
        }

        /// <summary>
        /// Channel label.
        /// </summary>
        /// <remarks>
        /// This is the data channel's SDP name, and therefore is immutable after the channel is created.
        /// </remarks>
        public readonly string Label;

        /// <summary>
        /// Status text displayed under the channel label.
        /// </summary>
        public string StatusText
        {
            get { return _statusText; }
            private set { SetProperty(ref _statusText, value); }
        }

        /// <summary>
        /// Can the chat channel send message?
        /// </summary>
        public bool CanSend
        {
            get { return _canSend; }
            private set { SetProperty(ref _canSend, value); }
        }

        private string _fullText = "";
        private string _statusText;
        private bool _canSend = false;

        public ChatChannelModel(DataChannel dataChannel)
        {
            DataChannel = dataChannel;
            Label = DataChannel.Label;
            UpdateState();
            dataChannel.StateChanged += () => UpdateState();
            dataChannel.MessageReceived += (byte[] message) =>
            {
                string text = System.Text.Encoding.UTF8.GetString(message);
                AppendText($"[remote] {text}\n");
            };
        }

        /// <summary>
        /// Append some text to <see cref="FullText"/>.
        /// </summary>
        /// <param name="text">The text to append.</param>
        public void AppendText(string text)
        {
            _fullText += text;
            RaisePropertyChanged("FullText");
        }

        private void UpdateState()
        {
            StatusText = $"State: {DataChannel.State}";
            CanSend = (DataChannel.State == DataChannel.ChannelState.Open);
        }
    }
}
