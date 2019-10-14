// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Collections.Concurrent;
using System.Threading;
using UnityEngine;

namespace Microsoft.MixedReality.WebRTC.Unity
{
    /// <summary>
    /// This component represents a remote audio source added as an audio track to an
    /// existing WebRTC peer connection by a remote peer and received locally.
    /// The audio track can optionally be displayed locally with a <see cref="MediaPlayer"/>.
    /// </summary>
    [AddComponentMenu("MixedReality-WebRTC/Remote Audio Source")]
    public class RemoteAudioSource : AudioSource
    {
        /// <summary>
        /// Peer connection this remote audio source is extracted from.
        /// </summary>
        [Header("Audio track")]
        public PeerConnection PeerConnection;

        /// <summary>
        /// Automatically play the remote audio track when it is added.
        /// </summary>
        public bool AutoPlayOnAdded = true;

        /// <summary>
        /// Is the audio source currently playing?
        /// The concept of _playing_ is described in the <see cref="Play"/> function.
        /// </summary>
        /// <seealso cref="Play"/>
        /// <seealso cref="Stop()"/>
        public bool IsPlaying { get; private set; }

        /// <summary>
        /// Internal queue used to marshal work back to the main Unity thread.
        /// </summary>
        private ConcurrentQueue<Action> _mainThreadWorkQueue = new ConcurrentQueue<Action>();

        /// <summary>
        /// Clip corresponding to the remote audio track.
        /// </summary>
        private AudioClip _audioClip; // created when the first webrtc frame arrives in RemoteAudioFrameReady.

        /// <summary>
        /// Set when we've enqueued the job to create the audio clip on the main thread. (to avoid duplication)
        /// </summary>
        private volatile bool _audioClipEnqueuedCreate = false;

        /// <summary>
        /// Internal adaptor from webrtc frame data to unity audio clip.
        /// </summary>
        class AudioData
        {
            float[] _buffer = null;
            bool _isFull = false;
            int _readPos = 0; // AudioClip fetches start from here
            int _writePos = 0; // WebRTC data gets appended here
            int _channelCount = 0;
            int _sampleRate = 0;
            public Action<int, int> AudioFormatChanged = null;

            void UpdateInternalFormat(AudioFrame frame)
            {
                if (frame.sampleRate != _sampleRate
                    || frame.channelCount != _channelCount
                    || _buffer == null
                    || _buffer.Length != (frame.sampleRate * frame.channelCount))
                {
                    // If anything changes, let's drop the buffer for simplicity (i.e. we won't resample or do extra work)
                    _sampleRate = (int)frame.sampleRate;
                    _channelCount = (int)frame.channelCount;
                    _buffer = new float[frame.sampleRate * frame.channelCount];
                    _readPos = 0;
                    _writePos = 0;
                    AudioFormatChanged?.Invoke(_sampleRate, _channelCount);
                }
            }

            internal void AppendAudio16(AudioFrame frame)
            {
                Debug.Assert(Monitor.IsEntered(this));
                UpdateInternalFormat(frame);
                Debug.Assert((frame.frameCount * frame.channelCount) <= _buffer.Length);
                unsafe
                {
                    short* src = (short*)frame.audioData;
                    int spaceAtEnd = _buffer.Length - _writePos;
                    int srcSamples = (int)(frame.frameCount * frame.channelCount);
                    // write1 is from _writePos forwards. write2 is the wraparound part (very often 0)
                    int write1 = Math.Min(spaceAtEnd, srcSamples);
                    for (int i = 0; i < write1; ++i)
                    {
                        _buffer[i + _writePos] = src[i] / 32768.0f; // 16 bit data is signed. Remap to -1,1
                    }

                    // Adjust the _writePos. Note that this may also update *_readPos*, if the reader is slow.
                    // We will always enque ALL the data on the basis that newer is better.
                    // Sometimes, if the reader isn't consuming fast enough, we may advance 'through' the read position.
                    // This will give bad artifacts, so instead we'll drop some of the input instead.

                    if (write1 == srcSamples)
                    {
                        var newPos = _writePos + write1;
                        if (_isFull || (_writePos < _readPos && _readPos < newPos))
                        {
                            _readPos = newPos;
                            _isFull = true;
                        }
                        _writePos = newPos;
                    }
                    else
                    {
                        Debug.Assert(_writePos == _buffer.Length);
                        int write2 = srcSamples - write1;
                        for (int i = 0; i < write2; ++i)
                        {
                            _buffer[i] = src[write1 + i] / 32768.0f;
                        }
                        // Notation - *=sample, W=writePos, R=readPos, B=both, .=empty
                        // Case Full or [**W...R**] or [..R***W..] => [***B****]
                        if (_isFull || (_writePos < _readPos) || (_readPos < write2))
                        {
                            _readPos = write2;
                            _isFull = true;
                        }
                        _writePos = write2;
                    }
                }
            }

            internal void OnClipRead(float[] data)
            {
                lock (this)
                {
                    Debug.Assert(data.Length > 0);
                    Debug.Assert(!_isFull || (_readPos == _writePos));
                    if (_buffer == null || (_readPos==_writePos && !_isFull))
                    {
                        return; // null or empty buffer
                    }
                    _isFull = false; // We are going to consume some

                    int totalRead;
                    if (_readPos < _writePos) // simple case - only a single copy
                    {
                        int avail1 = _writePos - _readPos;
                        int read1 = Math.Min(avail1, data.Length);
                        for (int i = 0; i < read1; ++i)
                        {
                            data[i] = _buffer[_readPos + i];
                        }
                        totalRead = read1;
                        _readPos += read1;
                    }
                    else // ( _readPos >= _writePos ) // [***W....R***] or [***B****], potential split read
                    {
                        int read1 = _buffer.Length - _readPos;
                        if ( data.Length <= read1) // single read
                        {
                            for (int i = 0; i < data.Length; ++i)
                            {
                                data[i] = _buffer[_readPos + i];
                            }
                            totalRead = data.Length;
                            _readPos += data.Length;
                        }
                        else // split read
                        {
                            for (int i = 0; i < read1; ++i)
                            {
                                data[i] = _buffer[_readPos + i];
                            }
                            int read2 = Math.Min(data.Length - read1, _writePos);
                            for (int i = 0; i < read2; ++i)
                            {
                                data[i + read1] = _buffer[i];
                            }
                            totalRead = read1 + read2;
                            _readPos = read2;
                        }
                    }
                    // zero fill if we underflowed
                    for (int i = totalRead; i < data.Length; ++i)
                    {
                        data[i] = 0;
                    }
                }
            }
        }

        /// <summary>
        /// Local storage of audio data to be fed to _audioClip;
        /// </summary>
        private AudioData _audioData = new AudioData();

        /// <summary>
        /// Manually start playback of the remote audio feed by registering some listeners
        /// to the peer connection and starting to enqueue audio frames as they become ready.
        /// 
        /// If <see cref="AutoPlayOnAdded"/> is <c>true</c> then this is called automatically
        /// as soon as the peer connection is initialized.
        /// </summary>
        /// <remarks>
        /// This is only valid while the peer connection is initialized, that is after the
        /// <see cref="PeerConnection.OnInitialized"/> event was fired.
        /// </remarks>
        /// <seealso cref="Stop()"/>
        /// <seealso cref="IsPlaying"/>
        public void Play()
        {
            if (!IsPlaying)
            {
                _audioData.AudioFormatChanged = OnAudioFormatChanged;
                IsPlaying = true;
                PeerConnection.Peer.RemoteAudioFrameReady += RemoteAudioFrameReady;
            }
        }

        /// <summary>
        /// Stop playback of the remote audio feed and unregister the handler listening to remote
        /// video frames.
        /// 
        /// Note that this is independent of whether or not a remote track is actually present.
        /// In particular this does not fire the <see cref="AudioSource.AudioStreamStopped"/>, which corresponds
        /// to a track being made available to the local peer by the remote peer.
        /// </summary>
        /// <seealso cref="Play()"/>
        /// <seealso cref="IsPlaying"/>
        public void Stop()
        {
            if (IsPlaying)
            {
                IsPlaying = false;
                PeerConnection.Peer.RemoteAudioFrameReady -= RemoteAudioFrameReady;
                _audioClip = null;
            }
        }

        /// <summary>
        /// Implementation of <a href="https://docs.unity3d.com/ScriptReference/MonoBehaviour.Awake.html">MonoBehaviour.Awake</a>
        /// which registers some handlers with the peer connection to listen to its <see cref="PeerConnection.OnInitialized"/>
        /// and <see cref="PeerConnection.OnShutdown"/> events.
        /// </summary>
        protected void Awake()
        {
            //FrameQueue = new AudioFrameQueue<AudioFrameStorage>(5);
            PeerConnection.OnInitialized.AddListener(OnPeerInitialized);
            PeerConnection.OnShutdown.AddListener(OnPeerShutdown);
        }

        /// <summary>
        /// Implementation of <a href="https://docs.unity3d.com/ScriptReference/MonoBehaviour.OnDestroy.html">MonoBehaviour.OnDestroy</a>
        /// which unregisters all listeners from the peer connection.
        /// </summary>
        protected void OnDestroy()
        {
            PeerConnection.OnInitialized.RemoveListener(OnPeerInitialized);
            PeerConnection.OnShutdown.RemoveListener(OnPeerShutdown);
        }

        /// <summary>
        /// Implementation of <a href="https://docs.unity3d.com/ScriptReference/MonoBehaviour.Update.html">MonoBehaviour.Update</a>
        /// to execute from the current Unity main thread any background work enqueued from free-threaded callbacks.
        /// </summary>
        protected void Update()
        {
            // Execute any pending work enqueued by background tasks
            while (_mainThreadWorkQueue.TryDequeue(out Action workload))
            {
                workload();
            }
        }

        /// <summary>
        /// Internal helper callback fired when the peer is initialized, which starts listening for events
        /// on remote tracks added and removed, and optionally starts audio playback if the
        /// <see cref="AutoPlayOnAdded"/> property is <c>true</c>.
        /// </summary>
        private void OnPeerInitialized()
        {
            PeerConnection.Peer.TrackAdded += TrackAdded;
            PeerConnection.Peer.TrackRemoved += TrackRemoved;

            if (AutoPlayOnAdded)
            {
                Play();
            }
        }

        /// <summary>
        /// Internal helper callback fired when the peer is shut down, which stops audio playback and
        /// unregister all the event listeners from the peer connection about to be destroyed.
        /// </summary>
        private void OnPeerShutdown()
        {
            Stop();
            PeerConnection.Peer.TrackAdded -= TrackAdded;
            PeerConnection.Peer.TrackRemoved -= TrackRemoved;
        }

        /// <summary>
        /// Internal free-threaded helper callback on track added, which enqueues the
        /// <see cref="VideoSource.VideoStreamStarted"/> event to be fired from the main
        /// Unity thread.
        /// </summary>
        private void TrackAdded(WebRTC.PeerConnection.TrackKind trackKind)
        {
            if (trackKind == WebRTC.PeerConnection.TrackKind.Audio)
            {
                // Enqueue invoking the unity event from the main Unity thread, so that listeners
                // can directly access Unity objects from their handler function.
                _mainThreadWorkQueue.Enqueue(() => AudioStreamStarted.Invoke());
            }
        }

        /// <summary>
        /// Internal free-threaded helper callback on track added, which enqueues the
        /// <see cref="VideoSource.VideoStreamStopped"/> event to be fired from the main
        /// Unity thread.
        /// </summary>
        private void TrackRemoved(WebRTC.PeerConnection.TrackKind trackKind)
        {
            if (trackKind == WebRTC.PeerConnection.TrackKind.Audio)
            {
                // Enqueue invoking the unity event from the main Unity thread, so that listeners
                // can directly access Unity objects from their handler function.
                _mainThreadWorkQueue.Enqueue(() => AudioStreamStopped.Invoke());
            }
        }

        private void EnqueueCreateAudioClipOnMainThread(int sampleRate, int channelCount)
        {
            Debug.Assert(_audioClipEnqueuedCreate == false);
            _audioClipEnqueuedCreate = true;
            _mainThreadWorkQueue.Enqueue(() =>
            {
                int lengthSamples = sampleRate / 10; // Buffer up to 100ms of data.
                _audioClip = AudioClip.Create("RemoteClip", sampleRate, channelCount, sampleRate, true, _audioData.OnClipRead);
                _audioClipEnqueuedCreate = false;
                var aud = GetComponent<UnityEngine.AudioSource>();
                if (aud)
                {
                    aud.clip = _audioClip;
                    aud.volume = 1.0f;
                    aud.loop = true;
                    aud.bypassEffects = true;
                    aud.Play();
                }
            });
        }

        private void OnAudioFormatChanged(int sampleRate, int channelCount)
        {
            lock (this)
            {
                _audioClip = null;
                // TODO Race condition here. AudioSource.clip still refers to the old clip. We can't modify it (not main thread).
                // Thus we may get some calls to _audioData.OnClipRead (which is expecting old samplerate/channelcount)
                // before the new clip is created.
                EnqueueCreateAudioClipOnMainThread(sampleRate, channelCount);
            }
        }

        private void RemoteAudioFrameReady(AudioFrame frame)
        {
            // The two parts of getting WebRTC audio:
            //
            // _audioData buffers the audioframes which arrive via WebRTC callbacks.
            // WebRTC will sometimes change the format midstream, and if so, we update to match the new
            // format immediately and will need to create a new AudioClip instance with the new parameters.
            // Note we can't change AudioClip immediately since this is not the unity main thread.
            //
            // _audioClip is the unity object which can be attached to various audio emitters.
            // The audioclip in this class is created with callbacks which read the data from _audioBuffer.
            if (_audioClipEnqueuedCreate)
            {
                return; // waiting for creation.
            }
            if (_audioClip == null)
            {
                // Don't enqueue any audio data until we have a clip
                EnqueueCreateAudioClipOnMainThread((int)frame.sampleRate, (int)frame.channelCount);
                return;
            }
            lock (_audioData)
            {
                switch (frame.bitsPerSample)
                {
                    case 16:
                        _audioData.AppendAudio16(frame);
                        break;
                    case 8:
                    default:
                        Debug.LogError($"Unsupported audio format {frame.bitsPerSample} bits per sample");
                        break;
                }
            }
        }
    }
}
