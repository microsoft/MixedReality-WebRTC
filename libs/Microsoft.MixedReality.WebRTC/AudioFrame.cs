// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;

namespace Microsoft.MixedReality.WebRTC
{
    /// <summary>
    /// Single audio frame.
    /// </summary>
    public ref struct AudioFrame
    {
    }

    /// <summary>
    /// Delegate used for events when an audio frame has been produced
    /// and is ready for consumption.
    /// </summary>
    /// <param name="frame">The newly available audio frame.</param>
    public delegate void AudioFrameDelegate(AudioFrame frame);
}
