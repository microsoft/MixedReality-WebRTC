# Build MixedReality-WebRTC for Linux

## Prerequisites

1. Build the Chromium WebRTC base library. Instructions [here](../libwebrtc/README.md).

2. Install prerequisites:
   MixedReality-WebRTC uses WebRTC native m71. In higher version of WebRTC some of APIs were significally changed, so you wont be able to compiler mrwebrtc at all, just because include files are different, the same as some of the classes and etc.
   
   If you take a closer look on WebRTC native build, you will see, that it is checking out all the dependecies and even checking the [llvm](https://github.com/llvm/llvm-project) source code, build it with GNU C++ compiler, and the use llvm to build the WebRTC it self. 

   Moreover, you won't be able to compile Mixed Reality WebRTC for linux with GNC C++ compiler, only with the clang.

   The m71 WebRTC version has pinned version of llvm project - 8.0.0. So you have to install the same compiler on your linux machine or use the clang compiled by WebRTC itself (it will be in output folder after libwebrtc compilation)
    * `apt-get install clang-8`
    * `apt-get install clang++-8`
    * `apt-get install lld-8`
    * `apt-get install libc++-8-dev`
    * `apt-get install libc++abi-8-dev`

## Build MixedReality-WebRTC

### Command line

2. Run `cmake .`

2. Run `make`

### Artifacts produced by the build

* `/libmrwebrtc.so`

Note, that to be able to run any application based on this webrtc, you should install the PulseAudio first, as during the initialization of a PeerConnection, the factory method which is used for that, will also try to initialize ADM (Audio Device Module). For more informaton, take a look on a `audio_device_impl.cc` class from the WebRTC sources.