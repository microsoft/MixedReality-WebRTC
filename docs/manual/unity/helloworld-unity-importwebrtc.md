# Importing MixedReality-WebRTC

In order to use the Unity library, the following pieces are required:

- Native implementation : `mrwebrtc.dll` (one variant per platform and architecture)
- C# library : `Microsoft.MixedReality.WebRTC.dll` (single universal module for all platforms and architectures)
- Unity library itself (scripts and assets)

The Unity library is distributed as a [UPM package](https://docs.unity3d.com/Manual/Packages.html) containing all those components, including the prebuilt binaries for all supported Unity platforms. The library package itself, as well as the optional samples package, can be imported into an existing Unity project in two ways:

- Manually by downloading the UPM package(s) from [the GitHub Releases page](https://github.com/microsoft/MixedReality-WebRTC/releases). Packages imported that way are referred to by Unity as [_on-disk packages_ or _local packages_](https://docs.unity3d.com/Manual/upm-ui-local.html), and the installation process is described in [the official Unity instructions](https://docs.unity3d.com/Manual/upm-ui-local.html).
- (Semi-)automatically via the UPM window inside the Unity Editor, after configuring UPM to use the official [Mixed Reality UPM package registry](https://dev.azure.com/aipmr/MixedReality-Unity-Packages/_packaging?_a=feed&feed=Unity-packages).

In the following, we describe the latter method.

As of Unity versions 2018.4 LTS and 2019.4 LTS, the Unity Package Manager (UPM) supports custom UPM package registries, but does not have any UI to configure those. Instead, users need to modify the `<UnityProject>/Packages/manifest.json` file inside the project where they want to import the packages.

Open the `<UnityProject>/Packages/manifest.json` file inside a text editor. The file should start with (or at least contain) a "dependencies" section listing all packages that the current project depends on. Insert a new section called "scopedRegistries" to enable the official Microsoft Mixed Reality UPM registry, and add the `com.microsoft.mixedreality.webrtc` package (library), and optionally the `com.microsoft.mixedreality.webrtc.samples` package (samples), as dependencies.

```json
{
   "scopedRegistries": [
      {
        "name": "Microsoft Mixed Reality",
        "url": "https://pkgs.dev.azure.com/aipmr/MixedReality-Unity-Packages/_packaging/Unity-packages/npm/registry/",
        "scopes": ["com.microsoft.mixedreality"]
      }
   ],
   "dependencies": {
     "com.microsoft.mixedreality.webrtc": "2.0.0-preview.1",
     "com.microsoft.mixedreality.webrtc.samples": "2.0.0-preview.1",
     ...existing dependencies...
```

> [!NOTE]
>
> Merely configuring the Mixed Reality UPM registry is not enough, as current LTS versions of Unity do not support listing packages from it automatically. Therefore the packages have to be added manually too in the "dependencies" section.

Once done, save the file and return to Unity. UPM will resolve the new packages via the configured registry, download the packages, and import them inside the Unity project.

> [!WARNING]
>
> The library package `com.microsoft.mixedreality.webrtc` currently contains the debug symbols (PDBs) for Windows platforms, and is therefore fairly large (100+MB). Unity _will_ take some time to download it, and may stop for several minutes at the "resolving" step (modal dialog). Be patient and let the process complete.

After that the packages will be visible in the UPM window. It is recommended to ensure you have the latest version installed.

![Packages in the Project window](helloworld-unity-2.png)

The packages content can be inspected from **Project window** > **Packages**:

![Packages in the Project window](helloworld-unity-1b.png)

----

Next : [Creating a peer connection](helloworld-unity-peerconnection.md)
