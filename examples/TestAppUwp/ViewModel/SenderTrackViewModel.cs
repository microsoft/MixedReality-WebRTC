// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using Microsoft.MixedReality.WebRTC;
using Windows.Graphics.Display;

namespace TestAppUwp
{
    public class SenderTrackViewModel
    {
        public static readonly SenderTrackViewModel Null = new SenderTrackViewModel(null, "<none>");

        public SenderTrackViewModel(MediaTrack track, string displayName = null)
        {
            Track = track;
            DisplayName = string.IsNullOrWhiteSpace(displayName) ? Track?.Name : displayName;
        }

        public MediaTrack Track { get; }

        public string DisplayName { get; }
    }
}
