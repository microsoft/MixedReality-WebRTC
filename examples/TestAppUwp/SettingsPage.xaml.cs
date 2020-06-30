// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Collections.Generic;
using Microsoft.MixedReality.WebRTC;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace TestAppUwp
{
    /// <summary>
    /// Page for general application settings.
    /// </summary>
    public sealed partial class SettingsPage : Page
    {
        public SessionModel SessionModel
        {
            get { return SessionModel.Current; }
        }

        /// <summary>
        /// Get the string representing the preferred audio codec the user selected.
        /// </summary>
        // TODO - Use per-transceiver properties instead of per-connection ones
        public string PreferredAudioCodec
        {
            get
            {
                if (PreferredAudioCodec_Custom.IsChecked.GetValueOrDefault(false))
                {
                    return CustomPreferredAudioCodec.Text;
                }
                else if (PreferredAudioCodec_OPUS.IsChecked.GetValueOrDefault(false))
                {
                    return "opus";
                }
                return string.Empty;
            }
            set
            {
                if (string.IsNullOrWhiteSpace(value))
                {
                    PreferredAudioCodec_Default.IsChecked = true;
                }
                else if (value == "opus")
                {
                    PreferredAudioCodec_OPUS.IsChecked = true;
                }
                else
                {
                    PreferredAudioCodec_Custom.IsChecked = true;
                    CustomPreferredAudioCodec.Text = value;
                }
            }
        }

        /// <summary>
        /// Get the string representing the preferred video codec the user selected.
        /// </summary>
        // TODO - Use per-transceiver properties instead of per-connection ones
        public string PreferredVideoCodec
        {
            get
            {
                if (PreferredVideoCodec_Custom.IsChecked.GetValueOrDefault(false))
                {
                    return CustomPreferredVideoCodec.Text;
                }
                else if (PreferredVideoCodec_H264.IsChecked.GetValueOrDefault(false))
                {
                    return "H264";
                }
                else if (PreferredVideoCodec_VP8.IsChecked.GetValueOrDefault(false))
                {
                    return "VP8";
                }
                return string.Empty;
            }
            set
            {
                if (string.IsNullOrWhiteSpace(value))
                {
                    PreferredVideoCodec_Default.IsChecked = true;
                }
                else if (value == "H264")
                {
                    PreferredVideoCodec_H264.IsChecked = true;
                }
                else if (value == "VP8")
                {
                    PreferredVideoCodec_VP8.IsChecked = true;
                }
                else
                {
                    PreferredVideoCodec_Custom.IsChecked = true;
                    CustomPreferredVideoCodec.Text = value;
                }
            }
        }

        public SettingsPage()
        {
            this.InitializeComponent();

            // Restore
            var sessionModel = SessionModel.Current;
            PreferredAudioCodec = sessionModel.PreferredAudioCodec;
            PreferredVideoCodec = sessionModel.PreferredVideoCodec;
        }

        private void SdpSemanticChanged(object sender, RoutedEventArgs e)
        {
            if (sender == sdpSemanticUnifiedPlan)
            {
                SessionModel.Current.SdpSemantic = SdpSemantic.UnifiedPlan;
            }
            else if (sender == sdpSemanticPlanB)
            {
                SessionModel.Current.SdpSemantic = SdpSemantic.PlanB;
            }
        }

        private void StunServerTextChanged(object sender, TextChangedEventArgs e)
        {
            SessionModel.Current.IceServer = new IceServer
            {
                Urls = new List<string> { "stun:" + stunServer.Text }
            };
        }

        // TODO - Use MVVM
        // TODO - Use per-transceiver properties instead of per-connection ones
        private void PreferredAudioCodecChecked(object sender, RoutedEventArgs args)
        {
            // Ignore calls during startup, before components are initialized
            if (PreferredAudioCodec_Custom == null)
            {
                return;
            }

            if (PreferredAudioCodec_Custom.IsChecked.GetValueOrDefault(false))
            {
                CustomPreferredAudioCodecHelpText.Visibility = Visibility.Visible;
                CustomPreferredAudioCodec.Visibility = Visibility.Visible;
            }
            else
            {
                CustomPreferredAudioCodecHelpText.Visibility = Visibility.Collapsed;
                CustomPreferredAudioCodec.Visibility = Visibility.Collapsed;
            }

            SessionModel.Current.PreferredAudioCodec = PreferredAudioCodec;
        }

        // TODO - Use MVVM
        // TODO - Use per-transceiver properties instead of per-connection ones
        private void PreferredVideoCodecChecked(object sender, RoutedEventArgs args)
        {
            // Ignore calls during startup, before components are initialized
            if (PreferredVideoCodec_Custom == null)
            {
                return;
            }

            if (PreferredVideoCodec_Custom.IsChecked.GetValueOrDefault(false))
            {
                CustomPreferredVideoCodecHelpText.Visibility = Visibility.Visible;
                CustomPreferredVideoCodec.Visibility = Visibility.Visible;
            }
            else
            {
                CustomPreferredVideoCodecHelpText.Visibility = Visibility.Collapsed;
                CustomPreferredVideoCodec.Visibility = Visibility.Collapsed;
            }

            SessionModel.Current.PreferredVideoCodec = PreferredVideoCodec;
        }
    }
}
