// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;

namespace TestAppUwp
{
    public class SignalerViewModel : NotifierBase
    {
        private static readonly string ButtonStartText = "Start polling";
        private static readonly string ButtonStopText = "Stop polling";

        private string _errorMessage;
        private bool _isButtonEnabled = true;
        private string _buttonText = ButtonStartText;

        public NodeDssSignaler Signaler { get; }

        public string ErrorMessage
        {
            get { return _errorMessage; }
            set { SetProperty(ref _errorMessage, value); }
        }

        public bool IsButtonEnabled
        {
            get { return _isButtonEnabled; }
            private set { SetProperty(ref _isButtonEnabled, value); }
        }

        public string ButtonText
        {
            get { return _buttonText; }
            private set { SetProperty(ref _buttonText, value); }
        }

        public SignalerViewModel(NodeDssSignaler signaler)
        {
            Signaler = signaler;
            Signaler.OnConnect += OnConnect;
            Signaler.OnDisconnect += OnDisconnect;
            Signaler.OnFailure += OnFailure;
        }

        public void StartPolling(int pollTimeMs)
        {
            ErrorMessage = string.Empty;
            IsButtonEnabled = false;

            if (string.IsNullOrWhiteSpace(Signaler.HttpServerAddress))
            {
                ErrorMessage = "Empty node-dss HTTP server address";
                IsButtonEnabled = true;
                return;
            }

            if (string.IsNullOrWhiteSpace(Signaler.LocalPeerId) || string.IsNullOrWhiteSpace(Signaler.RemotePeerId))
            {
                ErrorMessage = "Empty local or remote peer ID";
                IsButtonEnabled = true;
                return;
            }

            Signaler.PollTimeMs = pollTimeMs;
            try
            {
                Signaler.StartPollingAsync();
                ButtonText = ButtonStopText;
            }
            catch
            {
                ButtonText = ButtonStartText;
                IsButtonEnabled = true;
            }
        }

        public void StopPolling()
        {
            ErrorMessage = string.Empty;
            IsButtonEnabled = false;

            try
            {
                Signaler.StopPollingAsync();
            }
            catch
            {
                ButtonText = ButtonStopText;
                IsButtonEnabled = true;
            }
        }

        private void OnConnect()
        {
            ButtonText = ButtonStopText;
            IsButtonEnabled = true;
        }

        private void OnDisconnect()
        {
            ButtonText = ButtonStartText;
            IsButtonEnabled = true;
        }

        private void OnFailure(Exception ex)
        {
            ErrorMessage = ex.Message;
            ButtonText = ButtonStartText;
            IsButtonEnabled = true;
        }
    }
}
