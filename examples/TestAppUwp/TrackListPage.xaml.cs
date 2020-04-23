// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media.Animation;

namespace TestAppUwp
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class TrackListPage : Page
    {
        public SessionModel SessionModel
        {
            get { return SessionModel.Current; }
        }

        public TrackListPage()
        {
            this.InitializeComponent();
        }

        private void ListView_ItemClick(object sender, ItemClickEventArgs e)
        {
            if (e.ClickedItem is AddNewTrackViewModel addNewTrackViewModel)
            {
                tracksListView.SelectedItem = null;
                Frame.Navigate(addNewTrackViewModel.PageType, null,
                    new SlideNavigationTransitionInfo() { Effect = SlideNavigationTransitionEffect.FromRight });
            }
        }
    }
}
