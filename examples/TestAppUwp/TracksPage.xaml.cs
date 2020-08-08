// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media.Animation;
using Windows.UI.Xaml.Navigation;

namespace TestAppUwp
{
    public class TrackViewModelBase : NotifierBase
    {
    }

    public class AddNewTrackViewModel : TrackViewModelBase
    {
        public string DisplayName;
        public Type PageType;
    }

    public class LocalTrackViewModel : TrackViewModelBase
    {
        public readonly Symbol Symbol;

        public string DisplayName;

        public string Status
        {
            get { return "status..."; }
        }

        public LocalTrackViewModel(Symbol symbol)
        {
            Symbol = symbol;
        }
    }

    public class TrackDataTemplateSelector : DataTemplateSelector
    {
        public DataTemplate Normal { get; set; }
        public DataTemplate AddItem { get; set; }

        protected override DataTemplate SelectTemplateCore(object item, DependencyObject container)
        {
            if ((container is FrameworkElement) && (item != null) && (item is TrackViewModelBase))
            {
                if (item is LocalTrackViewModel)
                {
                    return Normal;
                }
                else
                {
                    return AddItem;
                }
            }
            return null;
        }
    }

    /// <summary>
    /// Top-level page for all tracks-related settings.
    ///
    /// Starts by displaying the list of existing tracks.
    /// </summary>
    public sealed partial class TracksPage : Page
    {
        public TracksPage()
        {
            this.InitializeComponent();
            contentFrame.Navigate(typeof(TrackListPage), null, new SuppressNavigationTransitionInfo());
        }

        protected override void OnNavigatedFrom(NavigationEventArgs e)
        {
            // When navigating away from the Tracks page, reset its content back to the list
            // of pages (TrackListPage), cancelling any track creation process.
            while (contentFrame.CanGoBack)
            {
                contentFrame.GoBack();
            }

            base.OnNavigatedFrom(e);
        }
    }
}
