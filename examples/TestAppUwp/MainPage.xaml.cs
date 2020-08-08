// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using Microsoft.MixedReality.WebRTC;
using Windows.ApplicationModel;
using Windows.Storage;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Markup;

namespace TestAppUwp
{
    public class CategoryBase { }

    public class Category : CategoryBase
    {
        public string Name { get; set; }
        public string Tooltip { get; set; }
        public Symbol Glyph { get; set; }
        public Type PageType { get; set; }
    }

    public class Separator : CategoryBase { }

    public class Header : CategoryBase
    {
        public string Name { get; set; }
    }

    [ContentProperty(Name = "ItemTemplate")]
    class MenuItemTemplateSelector : DataTemplateSelector
    {
        public DataTemplate ItemTemplate { get; set; }

        internal DataTemplate HeaderTemplate = (DataTemplate)XamlReader.Load(
            @"<DataTemplate xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation'>
                   <NavigationViewItemHeader Content='{Binding Name}' />
                  </DataTemplate>");

        internal DataTemplate SeparatorTemplate = (DataTemplate)XamlReader.Load(
            @"<DataTemplate xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation'>
                    <NavigationViewItemSeparator />
                  </DataTemplate>");

        protected override DataTemplate SelectTemplateCore(object item)
        {
            return item is Separator ? SeparatorTemplate : item is Header ? HeaderTemplate : ItemTemplate;
        }
    }

    /// <summary>
    /// Main application page with the navigation menu.
    /// </summary>
    public sealed partial class MainPage : Page
    {
        /// <summary>
        /// Collection of categories for the main navigation menu.
        /// </summary>
        public ObservableCollection<CategoryBase> Categories { get; } = new ObservableCollection<CategoryBase>()
        {
            new Category { Name = "Signaling", Glyph = Symbol.Map, PageType = typeof(SignalingPage) },
            new Category { Name = "Local Tracks", Glyph = Symbol.Library, PageType = typeof(TracksPage) },
            new Category { Name = "Session", Glyph = Symbol.VideoChat, PageType = typeof(SessionPage) },
            new Category { Name = "Media Player", Glyph = Symbol.Play, PageType = typeof(MediaPlayerPage) },
            new Category { Name = "Chat Channels", Glyph = Symbol.Message, PageType = typeof(ChatChannelsPage) },
            new Category { Name = "Debug Logs", Glyph = Symbol.Memo, PageType = typeof(DebugConsolePage) },
        };

        public static string GetDeviceName()
        {
            return Environment.MachineName;
        }

        public MainPage()
        {
            RestoreSettings();

            this.InitializeComponent();

            // Insert items manually into navigation menu. It doesn't seem that there is a way
            // to force selecting a menu item when using data binding, so selecting the first
            // page below would crash (empty MenuItems list if assigning MenuItemsSource).
            //foreach (var catBase in Categories)
            //{
            //    if (catBase is Category cat)
            //    {
            //        var item = new NavigationViewItem()
            //        {
            //            Content = cat.Name,
            //            Icon = new SymbolIcon() { Symbol = cat.Glyph },
            //            DataContext = catBase
            //        };
            //        AutomationProperties.SetName(item, cat.Name);
            //        navigationView.MenuItems.Add(item);
            //    }
            //    else if (catBase is Header headerCat)
            //    {
            //        var item = new NavigationViewItemHeader()
            //        {
            //            Name = headerCat.Name,
            //            DataContext = catBase
            //        };
            //        AutomationProperties.SetName(item, headerCat.Name);
            //        navigationView.MenuItems.Add(item);
            //    }
            //    else if (catBase is Separator sepCat)
            //    {
            //        navigationView.MenuItems.Add(new NavigationViewItemSeparator());
            //    }
            //}
            //navigationView.MenuItemsSource = Categories;

            // Open the first page by default
            rootFrame.Navigate((Categories[0] as Category).PageType);
            //((NavigationViewItem)(navigationView.MenuItems[0])).IsSelected = true; // doesn't work...

            this.Loaded += OnLoaded;
            Application.Current.Suspending += App_Suspending;
            Application.Current.Resuming += App_Resuming;
        }

        private void App_Suspending(object sender, SuspendingEventArgs e)
        {
            // Save local and remote peer IDs for next launch for convenience
            ApplicationDataContainer localSettings = ApplicationData.Current.LocalSettings;
            SessionModel sessionModel = SessionModel.Current;
            localSettings.Values["DssServerAddress"] = sessionModel.NodeDssSignaler.HttpServerAddress;
            localSettings.Values["LocalPeerID"] = sessionModel.NodeDssSignaler.LocalPeerId;
            localSettings.Values["RemotePeerID"] = sessionModel.NodeDssSignaler.RemotePeerId;
            localSettings.Values["PollTimeMs"] = sessionModel.NodeDssSignaler.PollTimeMs;
            localSettings.Values["PreferredAudioCodec"] = sessionModel.PreferredAudioCodec;
            localSettings.Values["PreferredAudioCodecExtraParamsLocal"] = sessionModel.PreferredAudioCodecExtraParamsLocal;
            localSettings.Values["PreferredAudioCodecExtraParamsRemote"] = sessionModel.PreferredAudioCodecExtraParamsRemote;
            localSettings.Values["PreferredVideoCodec"] = sessionModel.PreferredVideoCodec;
            localSettings.Values["PreferredVideoCodecExtraParamsLocal"] = sessionModel.PreferredVideoCodecExtraParamsLocal;
            localSettings.Values["PreferredVideoCodecExtraParamsRemote"] = sessionModel.PreferredVideoCodecExtraParamsRemote;
        }

        private void App_Resuming(object sender, object e)
        {
            RestoreSettings();
        }

        private void RestoreSettings()
        {
            ApplicationDataContainer localSettings = ApplicationData.Current.LocalSettings;
            SessionModel sessionModel = SessionModel.Current;

            // Uncomment these lines if you want to connect a HoloLens (or any non-x64 device) to a
            // x64 PC.
            //var arch = System.Environment.GetEnvironmentVariable("PROCESSOR_ARCHITECTURE");
            //if (arch == "AMD64")
            //{
            //    sessionModel.NodeDssSignaler.LocalPeerId = "Pc";
            //    sessionModel.NodeDssSignaler.RemotePeerId = "Device";
            //}
            //else
            //{
            //    sessionModel.NodeDssSignaler.LocalPeerId = "Device";
            //    sessionModel.NodeDssSignaler.RemotePeerId = "Pc";
            //}

            // Get server address and peer ID from local settings if available.
            if (localSettings.Values.TryGetValue("DssServerAddress", out object dssServerAddress))
            {
                if (dssServerAddress is string str)
                {
                    sessionModel.NodeDssSignaler.HttpServerAddress = str;
                }
            }
            if (string.IsNullOrWhiteSpace(sessionModel.NodeDssSignaler.HttpServerAddress))
            {
                sessionModel.NodeDssSignaler.HttpServerAddress = "http://localhost:3000/";
            }

            if (localSettings.Values.TryGetValue("LocalPeerID", out object localObj))
            {
                if (localObj is string str)
                {
                    sessionModel.NodeDssSignaler.LocalPeerId = str;
                }
            }
            if (localSettings.Values.TryGetValue("RemotePeerID", out object remoteObj))
            {
                if (remoteObj is string str)
                {
                    sessionModel.NodeDssSignaler.RemotePeerId = str;
                }
            }
            if (localSettings.Values.TryGetValue("PollTimeMs", out object pollTimeObject))
            {
                if (pollTimeObject is int pollTimeMs)
                {
                    sessionModel.NodeDssSignaler.PollTimeMs = pollTimeMs;
                }
            }

            if (!Utils.IsFirstInstance())
            {
                // Swap the peer IDs. This way two instances launched on the same machine connect
                // to each other by default
                var tmp = sessionModel.NodeDssSignaler.LocalPeerId;
                sessionModel.NodeDssSignaler.LocalPeerId = sessionModel.NodeDssSignaler.RemotePeerId;
                sessionModel.NodeDssSignaler.RemotePeerId = tmp;
            }

            // Ensure the local peer is not empty, otherwise the signaler will throw an exception
            // during OnLoaded(), which will crash the application.
            if (string.IsNullOrWhiteSpace(sessionModel.NodeDssSignaler.LocalPeerId))
            {
                sessionModel.NodeDssSignaler.LocalPeerId = GetDeviceName();
            }

            if (localSettings.Values.TryGetValue("PreferredAudioCodec", out object preferredAudioObj))
            {
                sessionModel.PreferredAudioCodec = (preferredAudioObj as string);
            }
            if (localSettings.Values.TryGetValue("PreferredAudioCodecExtraParamsLocal", out object preferredAudioParamsLocalObj))
            {
                sessionModel.PreferredAudioCodecExtraParamsLocal = (preferredAudioParamsLocalObj as string);
            }
            if (localSettings.Values.TryGetValue("PreferredAudioCodecExtraParamsRemote", out object preferredAudioParamsRemoteObj))
            {
                sessionModel.PreferredAudioCodecExtraParamsRemote = (preferredAudioParamsRemoteObj as string);
            }
            if (localSettings.Values.TryGetValue("PreferredVideoCodec", out object preferredVideoObj))
            {
                sessionModel.PreferredVideoCodec = (preferredVideoObj as string);
            }
            if (localSettings.Values.TryGetValue("PreferredVideoCodecExtraParamsLocal", out object preferredVideoParamsLocalObj))
            {
                sessionModel.PreferredVideoCodecExtraParamsLocal = (preferredVideoParamsLocalObj as string);
            }
            if (localSettings.Values.TryGetValue("PreferredVideoCodecExtraParamsRemote", out object preferredVideoParamsRemoteObj))
            {
                sessionModel.PreferredVideoCodecExtraParamsRemote = (preferredVideoParamsRemoteObj as string);
            }
        }

        //private void OnPeerRenegotiationNeeded()
        //{
        //    RunOnMainThread(() => negotiationStatusText.Text = "Renegotiation needed");
        //}

        private async void OnLoaded(object sender, RoutedEventArgs e)
        {
            // This should move to the App, no need to wait for the main page loaded...
            await SessionModel.Current.InitializePeerConnectionAsync();

            // Automatically start polling for convenience
            SessionModel.Current.NodeDssSignaler.StartPollingAsync();

            //audioTrackComboBox.IsEnabled = false;
            //videoTrackComboBox.IsEnabled = false;

            // Populate the combo box with the VideoProfileKind enum
            {
                var values = Enum.GetValues(typeof(VideoProfileKind));
                //KnownVideoProfileKindComboBox.ItemsSource = values.Cast<VideoProfileKind>();
                //KnownVideoProfileKindComboBox.SelectedIndex = Array.IndexOf(values, VideoProfileKind.Unspecified);
            }

            //VideoCaptureDeviceList.SelectionChanged += VideoCaptureDeviceList_SelectionChanged;
            //KnownVideoProfileKindComboBox.SelectionChanged += KnownVideoProfileKindComboBox_SelectionChanged;
            //VideoProfileComboBox.SelectionChanged += VideoProfileComboBox_SelectionChanged;

            //videoPlayerElement.TransportControls = localVideoControls;

            //chatInputBox.IsEnabled = true;
            //chatSendButton.IsEnabled = true;

            //_videoPlayer.CurrentStateChanged += OnMediaStateChanged;
            //_videoPlayer.MediaOpened += OnMediaOpened;
            //_videoPlayer.MediaFailed += OnMediaFailed;
            //_videoPlayer.MediaEnded += OnMediaEnded;
            //_videoPlayer.RealTimePlayback = true;
            //_videoPlayer.AutoPlay = false;

            // Bind the XAML UI control (videoPlayerElement) to the MediaFoundation rendering pipeline (_videoPlayer)
            // so that the former can render in the UI the video frames produced in the background by the latter.
            //videoPlayerElement.SetMediaPlayer(_videoPlayer);
        }

        private void OnNavigationViewItemInvoked(NavigationView sender, NavigationViewItemInvokedEventArgs args)
        {
            if (args.IsSettingsInvoked)
            {
                rootFrame.Navigate(typeof(SettingsPage));
            }
            else
            {
                // TODO: use e.g. args.InvokedItemContainer.Tag to avoid string matching and be more reliable?
                foreach (var catBase in Categories)
                {
                    if (catBase is Category cat)
                    {
                        if (cat.PageType == null)
                        {
                            continue;
                        }
                        if ((args.InvokedItem as string) == cat.Name)
                        {
                            rootFrame.Navigate(cat.PageType);
                        }
                    }
                }
            }
        }
    }
}
