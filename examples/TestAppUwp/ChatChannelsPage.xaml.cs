// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;

namespace TestAppUwp
{
    /// <summary>
    /// Page displaying the list of chat channels and some controls to send messages
    /// to the remote peer through the currently selected channel.
    /// </summary>
    public sealed partial class ChatChannelsPage : Page
    {
        public SessionModel SessionModel => SessionModel.Current;

        public ChatChannelsPage()
        {
            this.InitializeComponent();
        }

        private void ChatList_ItemClick(object sender, ItemClickEventArgs e)
        {
            if (e.ClickedItem is ChatChannelModel chat)
            {
                chatTextBox.Text = chat.FullText;
            }
        }

        /// <summary>
        /// Callback on Send button from text chat clicker.
        /// If connected, this sends the text message to the remote peer using
        /// the previously opened data channel.
        /// </summary>
        /// <param name="sender">The object which invoked the event.</param>
        /// <param name="e">Event arguments.</param>
        private void ChatSendButton_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(chatInputBox.Text))
            {
                return;
            }

            ChatChannelModel chat = SessionModel.ChatChannels.SelectedItem;
            if (chat == null)
            {
                return;
            }

            // Send the message through the data channel
            byte[] chatMessage = System.Text.Encoding.UTF8.GetBytes(chatInputBox.Text);
            chat.DataChannel.SendMessage(chatMessage);

            // Save and display in the UI
            var newLine = $"[local] {chatInputBox.Text}\n";
            chat.AppendText(newLine);
            chatScrollViewer.ChangeView(chatScrollViewer.HorizontalOffset,
                chatScrollViewer.ScrollableHeight,
                chatScrollViewer.ZoomFactor); // scroll to end
            chatInputBox.Text = string.Empty;
        }

        /// <summary>
        /// Callback on key down event invoked in the chat window, to handle
        /// the "press Enter to send" text chat functionality.
        /// </summary>
        /// <param name="sender">The object which invoked the event.</param>
        /// <param name="e">Event arguments.</param>
        private void OnChatKeyDown(object sender, KeyRoutedEventArgs e)
        {
            if (e.Key == Windows.System.VirtualKey.Enter)
            {
                ChatSendButton_Click(this, null);
            }
        }

    }
}
