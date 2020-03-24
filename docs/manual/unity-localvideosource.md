# Unity `WebcamSource` component

The [`WebcamSource`](xref:Microsoft.MixedReality.WebRTC.Unity.WebcamSource) Unity component represents a single video track obtaining its frames from a local video capture device (webcam). The component controls both the capture device and the track it feeds.

![The WebcamSource Unity component](unity-localvideosource.png)

## Properties

### Local video capture

#### [`WebcamDevice`](xref:Microsoft.MixedReality.WebRTC.Unity.WebcamSource.WebcamDevice)

Description of the video capture device to use, colloquially referred to as _webcam_ for short, even if other non-webcam capture devices are also supported, like the HoloLens 1 and HoloLens 2 cameras. Valid device unique identifiers can be enumerated with [`GetVideoCaptureDevicesAsync()`](xref:Microsoft.MixedReality.WebRTC.PeerConnection.GetVideoCaptureDevicesAsync) and correspond to the [`VideoCaptureDevice.id`](xref:Microsoft.MixedReality.WebRTC.VideoCaptureDevice.id) field. Note that this property is not exposed to the Unity editor, as devices should be enumerate at runtime to support the various video capture device configurations of the host device.

#### [`FormatMode`](xref:Microsoft.MixedReality.WebRTC.Unity.WebcamSource.FormatMode)

Select between automated and manual video capture format selection mode. In automated mode, the implementation selects the best video capture format. In manual mode, some constraints can be specified to restrict the video capture formats the implementation might consider using, and even force one particular format.

#### [`Constraints`](xref:Microsoft.MixedReality.WebRTC.Unity.WebcamSource.Constraints)

Optional resolution and framerate constraints to apply when selecting a video capture format. This allows restricting the set of capture formats the implementation considers when selecting a capture format to use, possibly even forcing a single one. Constraints reducing the number of matching capture formats to zero will make opening the device fail, therefore it is recommended to enumerate the supported capture formats with [`GetVideoCaptureFormatsAsync`](xref:Microsoft.MixedReality.WebRTC.PeerConnection.GetVideoCaptureFormatsAsync(System.String)).

#### [`VideoProfileId`](xref:Microsoft.MixedReality.WebRTC.Unity.WebcamSource.VideoProfileId)

[UWP only] Optional unique identifier of the video profile to use to enumerate the video capture formats. This allows selecting a video profile other than the default one, which sometimes enables access to other resolutions and framerates, and is required on HoloLens 2 to use the low-power capture formats. It is recommended to specify either `VideoProfileId` or `VideoProfileKind`, but not both.

#### [`VideoProfileKind`](xref:Microsoft.MixedReality.WebRTC.Unity.WebcamSource.VideoProfileKind)

[UWP only] Optional video profile kind to use to enumerate the video capture formats. This allows selecting a video profile other than the default one, which sometimes enables access to other resolutions and framerates, and is required on HoloLens 2 to use the low-power capture formats. It is recommended to specify either `VideoProfileId` or `VideoProfileKind`, but not both.

#### [`EnableMixedRealityCapture`](xref:Microsoft.MixedReality.WebRTC.Unity.WebcamSource.EnableMixedRealityCapture)

[UWP only] On platforms supporting Mixed Reality Capture (MRC) like HoloLens 1st generation and 2nd generation, instruct the video capture module to enable this feature and produce a video stream containing the holograms rendered over the raw webcam feed. This has no effect if the local device does not support MRC.

#### [`EnableMRCRecordingIndicator`](xref:Microsoft.MixedReality.WebRTC.Unity.WebcamSource.EnableMRCRecordingIndicator)

[UWP only] On platforms supporting Mixed Reality Capture (MRC) like HoloLens 1st generation and 2nd generation, and when `EnableMixedRealityCapture` is `true`, enable the on-screen recording indicator (red circle) on the device while the camera is capturing.

### Video track

#### [`Transceiver`](xref:Microsoft.MixedReality.WebRTC.Unity.VideoSender.Transceiver)

    Reference to the [`Transceiver`](xref:Microsoft.MixedReality.WebRTC.Transceiver) instance the video track is attached to, if any.

#### [`Track`](xref:Microsoft.MixedReality.WebRTC.Unity.VideoSender.Track)

Reference to the [`LocalVideoTrack`](xref:Microsoft.MixedReality.WebRTC.LocalVideoTrack) instance the component is encapsulating.

#### [`PreferredVideoCodec`](xref:Microsoft.MixedReality.WebRTC.Unity.VideoSender.PreferredVideoCodec)

Optional choice of a preferred video codec to use for local video capture and SDP offering. This is implemented by filtering out all other video codecs when sending an SDP offer message if the original offer was already containing that codec. See [this Wikipedia article](https://en.wikipedia.org/wiki/RTP_audio_video_profile) for the SDP names to use. Currently the implementation only supports some of them, including `"H264"` (UWP only), `"VP8"`, or `"VP9"`. H.265 is not supported.

> [!WARNING]
> Currently the H.264 video codec is only supported on UWP platforms. Windows Desktop platforms, including the Unity editor, cannot encode nor decode H.264 video streams, and will fail any attempt to establish a peer-to-peer connection if this video codec is selected via `PreferredVideoCodec`. To diagnose and/or work around this limitation, use the VP8 software codec (`PreferredVideoCodec = "VP8"`), which is supported on all platforms.
