// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Diagnostics;
using Windows.UI.Xaml.Controls;

namespace TestAppUwp
{
    public class Logger : NotifierBase
    {
        public static void Log(string message)
        {
            Instance.LogMessage(message);
        }

        private string _fullText;

        public static Logger Instance { get; } = new Logger();

        public string FullText
        {
            get { return _fullText; }
            set { SetProperty(ref _fullText, value); }
        }

        public void LogMessage(string message)
        {
            Debugger.Log(4, "TestAppUWP", message);
            message += "\n";
            _fullText += message;
            RaisePropertyChanged("FullText");
        }
    }

    /// <summary>
    /// Page displaying the so-called "debug console", a raw text dump of all messages
    /// output through the <see cref="Logger"/> singleton instance by the various part
    /// of the application, and mainly used for debugging.
    /// </summary>
    public sealed partial class DebugConsolePage : Page
    {
        public DebugConsolePage()
        {
            this.InitializeComponent();
        }
    }
}
