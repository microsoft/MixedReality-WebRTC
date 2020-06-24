// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Microsoft.MixedReality.WebRTC;

namespace TestAppUwp
{
    public static class ThreadHelper
    {
        public static Task RunOnMainThread(Windows.UI.Core.DispatchedHandler handler)
        {
            var dispatcher = Windows.ApplicationModel.Core.CoreApplication.MainView.CoreWindow.Dispatcher;
            if (dispatcher.HasThreadAccess)
            {
                handler.Invoke();
                return Task.CompletedTask;
            }
            else
            {
                return dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, handler).AsTask();
            }
        }

        public static Task RunOnWorkerThread(Action handler)
        {
            var dispatcher = Windows.ApplicationModel.Core.CoreApplication.MainView.CoreWindow.Dispatcher;
            if (dispatcher.HasThreadAccess)
            {
                return Task.Run(handler);
            }
            else
            {
                handler.Invoke();
                return Task.CompletedTask;
            }
        }

        public static void EnsureIsMainThread()
        {
            var dispatcher = Windows.ApplicationModel.Core.CoreApplication.MainView.CoreWindow.Dispatcher;
            if (!dispatcher.HasThreadAccess)
            {
                throw new InvalidOperationException("Invalid operation called out of main thread");
            }
        }
    }

    /// <summary>
    /// View model for the SDP session.
    /// </summary>
    public class SessionViewModel : NotifierBase
    {
        /// <summary>
        /// Peer connection this view model is providing the session binding of.
        /// </summary>
        //private PeerConnection _peerConnection;

        public SessionViewModel()
        {
            //_peerConnection = peerConnection;
            //_peerConnection.RenegotiationNeeded += OnRenegotiationNeeded;
        }


    }
}
