# Getting started

The MixedReality-WebRTC project is comprised of several components:

- A **C++ library** to integrate into a native C++ application
- A **C# library** to integrate into a C# application
- A **Unity integration** to help integrate into an existing Unity application

Not all components are required for all use cases, but each component builds upon the previous one. This means that for use in a C++ application only the C++ library needs to be installed. But the Unity integration will require installing also the C# and C++ libraries.

> [!Note]
> A note on terminology: in this documentation the term _component_ refers to one of the libraries mentioned above. This has no relation with a _Unity component_, which is a C# class deriving from [`MonoBehaviour`](https://docs.unity3d.com/ScriptReference/MonoBehaviour.html).

In this chapter we discuss:
- [**Download**](download.md) : Downloading precompiled binary packages of MixedReality-WebRTC.
- [**Installation**](installation.md) : How to install the various libraries for use in your poject.
- [**Building from sources**](building.md) : Building the MixedReality-WebRTC from sources.
- [**Hello, C# world! (Desktop)**](cs/helloworld-cs-core3.md) : Your first C# .NET Core 3.0 application based on the C# library.
- **Hello, C# world! (UWP)** (TODO) : Your first C# UWP application based on the C# library.
- [**Hello, Unity world!**](helloworld-unity.md) : Your first Unity application based on the Unity integration.
