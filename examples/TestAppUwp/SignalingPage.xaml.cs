// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace TestAppUwp
{
    /// <summary>
    /// Page displaying the content of the signaling navigation tab, with controls and options
    /// to manage the <see cref="NodeDssSignaler"/> instance used for the WebRTC signaling of the
    /// current session.
    /// </summary>
    public sealed partial class SignalingPage : Page
    {
        private SignalerViewModel _signalerViewModel;

        public SignalingPage()
        {
            this.InitializeComponent();
            _signalerViewModel = new SignalerViewModel(SessionModel.Current.NodeDssSignaler);
        }

        private void PollDssButtonClicked(object sender, RoutedEventArgs e)
        {
            // If already polling, stop
            if (_signalerViewModel.Signaler.IsPolling)
            {
                Logger.Log($"Stop polling node-dss signaling server");
                _signalerViewModel.StopPolling();
                return;
            }

            // If not polling, try to start if the poll parameters are valid
            if (!int.TryParse(dssPollTimeMs.Text, out int pollTimeMs))
            {
                _signalerViewModel.ErrorMessage = "Failed to parse poll time";
                return;
            }
            Logger.Log($"Start polling node-dss signaling server");
            _signalerViewModel.StartPolling(pollTimeMs);
        }
    }
}
