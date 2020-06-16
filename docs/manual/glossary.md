---
uid: glossary
---

# Glossary

## D

### Data channel

A data channel is a "pipe" to send and receive random blobs of bytes.

## L

### Local media track

A local media track is a [_media track_](#media-track) whose source is local to the peer connection, that is which has a local frame-producing [_media track source_](#media-track-source). In other words the track is a sender track, which produce frames to be sent to the remote peer.

> See also
>
> - [_media track_](#media-track)
> - [_remote media track_](#remote-media-track)

## M

### Media

The term _media_ refers to either or both of _audio_ and _video_. It does **not** include [_data channels_](#data-channel).

### Media track

A _media track_ is a slim entity which bridges a [_media track source_](#media-track-source) with a [_transceiver_](#transceiver).

> See also
>
> - [_media_](#media)
> - [_media track source_](#media-track-source)
> - [_local media track_](#local-media-track)
> - [_remote media track_](#remote-media-track)

### Media track source

A _media track source_ is an entity producing some [_media_](#media) frames. Those frames are made available to one or more [_media tracks_](#media-track). The source itself is a standalone object not associated with any particular peer connection, and therefore can be used with multiple tracks from multiple peer connections at the same time.

> See also
>
> - [_media_](#media)
> - [_media track source_](#media-track-source)

## P

### Peer connection

The peer connection is the main entity of WebRTC. It manages a connection to a single remote peer. The peer connection contains a collection of [_transceivers_](#transceiver), which describe which [_media_](#media) is sent to and received from the remote peer, as well as a collection of [_data channels_](#data-channel), both of which can be empty.

> See also
>
> - [_transceiver_](#transceiver)
> - [_data channel_](#data-channel)

## R

### Remote media track

A remote media track is a [_media track_](#media-track) whose source is remote to the peer connection, that is which receives its frame from the remote peer; this is a receiver track.

> See also
>
> - [_media track_](#media-track)
> - [_local media track_](#local-media-track)

## T

### Transceiver

A transceiver is a "pipe" for transporting some [_media_](#media) between two peers. Each transceiver is owned by a specific [_peer connection_](#peer-connection), and describes how this media is encoded (audio or video codec type and options) and transported (transport options, like bandwidth).

A transceiver always has exactly one [_local media track_](#local-media-track) (sender) and one [_remote media track_](#remote-media-track) (receiver), both of which can be `null`. If `null`, the transceiver acts as if a dummy track existed which draws its media frames from a null source (black frames for video, silence for audio).
