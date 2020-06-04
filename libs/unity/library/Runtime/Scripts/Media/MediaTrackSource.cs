// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using UnityEngine;

namespace Microsoft.MixedReality.WebRTC.Unity
{
    /// <summary>
    /// Base class for media track source components producing some media frames locally.
    /// </summary>
    /// <seealso cref="AudioTrackSource"/>
    /// <seealso cref="VideoTrackSource"/>
    public abstract class MediaTrackSource : MediaProducer
    {
        /// <summary>
        /// Create a new media source of the given <see cref="MediaKind"/>.
        /// </summary>
        /// <param name="mediaKind">The media kind of the source.</param>
        public MediaTrackSource(MediaKind mediaKind) : base(mediaKind)
        {
        }

        /// <summary>
        /// Internal callback invoked by the base class to let derived classes create the
        /// actual implementation of the source.
        /// </summary>
        protected abstract Task CreateSourceAsync();

        /// <summary>
        /// Internal callback invoked by the base class to let derived classes destroy the
        /// implementation of the source they previously created in <see cref="CreateSourceAsync"/>.
        /// </summary>
        protected abstract void DestroySource();

        protected virtual async void OnEnable()
        {
            await CreateSourceAsync();
        }

        protected virtual void OnDisable()
        {
            DestroySource();
        }
    }
}
