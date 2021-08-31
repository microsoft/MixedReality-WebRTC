// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.MixedReality.WebRTC;

namespace TestNetCoreConsole
{
    class Program
    {
        static async Task Main(string[] args)
        {
            Transceiver audioTransceiver = null;
            Transceiver videoTransceiver = null;
            AudioTrackSource audioTrackSource = null;
            VideoTrackSource videoTrackSource = null;
            LocalAudioTrack localAudioTrack = null;
            LocalVideoTrack localVideoTrack = null;

            try
            {
                bool needVideo = Array.Exists(args, arg => (arg == "-v") || (arg == "--video"));
                bool needAudio = Array.Exists(args, arg => (arg == "-a") || (arg == "--audio"));

                // Asynchronously retrieve a list of available video capture devices (webcams).
                var deviceList = await DeviceVideoTrackSource.GetCaptureDevicesAsync();

                // For example, print them to the standard output
                foreach (var device in deviceList)
                {
                    Console.WriteLine($"Found webcam {device.name} (id: {device.id})");
                }

                // Create a new peer connection automatically disposed at the end of the program
                using var pc = new PeerConnection();
                using var signaler = new NamedPipeSignaler.NamedPipeSignaler(pc, "testpipe");

                // Initialize the connection with a STUN server to allow remote access
                var config = new PeerConnectionConfiguration
                {
                    IceServers = new List<IceServer> { 
                            new IceServer{ Urls = { "stun:stun.l.google.com:19302" } }
                        }
                };

                await pc.InitializeAsync(config);
                Console.WriteLine("Peer connection initialized.");
                 
                var dataChannelLabel = $"data_channel_{Guid.NewGuid()}";
                Console.WriteLine($"Adding data channel with label '{dataChannelLabel}'");
                await pc.AddDataChannelAsync(dataChannelLabel, true, true, CancellationToken.None);
                
                // Record video from local webcam, and send to remote peer
                if (needVideo)
                {
                    Console.WriteLine("Opening local webcam...");
                    videoTrackSource = await DeviceVideoTrackSource.CreateAsync();

                    Console.WriteLine("Create local video track...");
                    var trackSettings = new LocalVideoTrackInitConfig { trackName = "webcam_track" };
                    localVideoTrack = LocalVideoTrack.CreateFromSource(videoTrackSource, trackSettings);

                    Console.WriteLine("Create video transceiver and add webcam track...");
                    videoTransceiver = pc.AddTransceiver(MediaKind.Video);
                    videoTransceiver.DesiredDirection = Transceiver.Direction.SendReceive;
                    videoTransceiver.LocalVideoTrack = localVideoTrack;
                }

                // Record audio from local microphone, and send to remote peer
                if (needAudio)
                {
                    Console.WriteLine("Opening local microphone...");
                    audioTrackSource = await DeviceAudioTrackSource.CreateAsync();

                    Console.WriteLine("Create local audio track...");
                    var trackSettings = new LocalAudioTrackInitConfig { trackName = "mic_track" };
                    localAudioTrack = LocalAudioTrack.CreateFromSource(audioTrackSource, trackSettings);

                    Console.WriteLine("Create audio transceiver and add mic track...");
                    audioTransceiver = pc.AddTransceiver(MediaKind.Audio);
                    audioTransceiver.DesiredDirection = Transceiver.Direction.SendReceive;
                    audioTransceiver.LocalAudioTrack = localAudioTrack;
                }

                // Setup signaling
                Console.WriteLine("Starting signaling...");
                signaler.SdpMessageReceived += async (SdpMessage message) =>
                {
                    await pc.SetRemoteDescriptionAsync(message);
                    if (message.Type == SdpMessageType.Offer)
                    {
                        pc.CreateAnswer();
                    }
                };
                signaler.IceCandidateReceived += (IceCandidate candidate) =>
                {
                    pc.AddIceCandidate(candidate);
                };
                await signaler.StartAsync();
                // Start peer connection
                pc.Connected += () => { Console.WriteLine("PeerConnection: connected."); };
                pc.IceStateChanged += (IceConnectionState newState) => { Console.WriteLine($"ICE state: {newState}"); };
                int numFrames = 0;
                pc.VideoTrackAdded += (RemoteVideoTrack track) =>
                {
                    track.I420AVideoFrameReady += (I420AVideoFrame frame) =>
                    {
                        ++numFrames;
                        if (numFrames % 60 == 0)
                        {
                            Console.WriteLine($"Received video frames: {numFrames}");
                        }
                    };
                };
                if (signaler.IsClient)
                {
                    Console.WriteLine("Connecting to remote peer...");
                    pc.CreateOffer();
                }
                else
                {
                    Console.WriteLine("Waiting for offer from remote peer...");
                }

                Console.WriteLine("Press a key to stop recording...");
                Console.ReadKey(true);

                Console.WriteLine("Removing data channels...");
                foreach (var dataChannel in pc.DataChannels.ToImmutableArray())
                {
                    Console.WriteLine($"Removing DataChannel {dataChannel.Label}: State '{dataChannel.State}'");
                    pc.RemoveDataChannel(dataChannel);
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
            }

            localAudioTrack?.Dispose();
            localVideoTrack?.Dispose();

            Console.WriteLine("Program termined.");

            localAudioTrack?.Dispose();
            localVideoTrack?.Dispose();
            audioTrackSource?.Dispose();
            videoTrackSource?.Dispose();
        }
    }
}
