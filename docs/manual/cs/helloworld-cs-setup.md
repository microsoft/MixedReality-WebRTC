# Creating a project

In this tutorial we use [.NET Core 3.0](https://dotnet.microsoft.com/download/dotnet-core/3.0) to create a C# Desktop application that is mostly platform independent, except for the UI part which requires the Desktop Pack for Windows to use the [Windows Presentation Foundation (WPF)](https://github.com/dotnet/wpf) UI framework.

_Note_: Currently .NET Core 3.0 is still in preview, so some extra Visual Studio 2019 setup is required.

_Note_: This tutorial assumes that the host device where the app will be running during the tutorial has access to:
- a webcam, or any other video capture device recognized by WebRTC
- a microphone, or any other audio capture device recognized by WebRTC

## Install .NET Core 3.0

Download the latest .NET Core 3.0 **SDK** (and not Runtime) from its [download page](https://dotnet.microsoft.com/download/dotnet-core/3.0) and install it.

## Generate the project

Open a terminal and use the `dotnet` command to create a new project from the WPF template. We will name this tutorial project `TestNetCoreWpf`.

```
dotnet new wpf --name TestNetCoreWpf
```

This generates a folder named `TestNetCoreWpf` which contains the following notable files:
- **`TestNetCoreWpf.csproj`** : The C# project
- **`MainWindow.xaml`** : The XAML file describing the user interface of the main application window
- **`MainWindow.xaml.cs`** : The C# code associated with `MainWindow.xaml`, where the UI actions are created

## Configure Visual Studio 2019

_This step is only needed if using a preview version of .NET Core._

In order to be able to open C# projects created with a preview version of .NET Core 3.0, Visual Studio 2019 must be configured to use preview SDKs. Otherwise it will display a generic error dialog box when trying to open the `.csproj` project file.

Open Visual Studio 2019, go to **Tools** > **Options** > **Environment** > **Preview Features**, and check the **Use previews of the .NET Core SDK** option. After that, restart Visual Studio 2019 for changes to take effect.

![Allow preview versions of .NET Core 3.0 SDK](cs2.png)

## Open the .NET Core project in Visual Studio 2019

Open the C# project generated earlier (`TestNetCoreWpf.csproj`), then build and run it, either by pressing **F5** or selecting in the menu **Debug** > **Start Debugging**. After the project built successfully, an empty main window should appear, with the WPF debug bar at its top. Note that this bar only appears while debugging, not when running the app outside Visual Studio 2019. It can be ignored.

![Empty main window of the newly generated C# project](cs3.png)

_Note_ : The project can alternatively be built on the command line with `dotnet build`, and launched with `dotnet run` (which implies building). However because we will be modifying the XAML user interface, Visual Studio 2019 provides a better experience with its integrated visual XAML editor than editing `.xaml` files from code. 

## Add a dependency to MixedReality-WebRTC

In order to use the MixedReality-WebRTC project in this new `TestNetCoreWpf` app, we will add a dependency to its C# NuGet package hosted on [nuget.org](https://www.nuget.org/). This is by far the easiest way, although a locally-built copy of the `Microsoft.MixedReality.WebRTC.dll` assembly could also be alternatively used (but this is out of the scope of this tutorial).

There are again multiple ways to add a reference to this NuGet package, in particular via the Visual Studio NuGet package manager for the project, or via the `dotnet` command line. For simplicity, we show here how to do so the `dotnet` way, which simply involves typing a single command.

```
dotnet add TestNetCoreWpf.csproj package Microsoft.MixedReality.WebRTC
```

This will download from [nuget.org](https://www.nuget.org/) and install the `Microsoft.MixedReality.WebRTC.nupkg` NuGet package, which contains the same-named assembly, as well as its native dependencies for both Desktop and UWP platforms.

After that, `TestNetCoreWpf.csproj` should contain a reference to the package, with a version corresponding to the latest version found on [nuget.org](https://www.nuget.org/).

```xml
<ItemGroup>
  <PackageReference Include="Microsoft.MixedReality.WebRTC" Version="..." />
</ItemGroup>
```

## Test the reference

In order to ensure everything works fine and the `Microsoft.MixedReality.WebRTC` assembly can be used, we will use one of its functions to list the video capture devices, as a test. This makes uses of the static method [`PeerConnection.GetVideoCaptureDevicesAsync()`](xref:PeerConnection.GetVideoCaptureDevicesAsync). This is more simple than creating objects, as there is no clean-up needed after use.

In `MainWindows.xaml.cs`:

1. At the top of the file, add some `using` statement to import the `Microsoft.MixedReality.WebRTC` assembly.
   ```cs
   using Microsoft.MixedReality.WebRTC;
   ```

2. In the `MainWindow` constructor, register a handler for the `OnLoaded` event, which will be fired once the XAML user interface finished loading. For now it is not required to wait on the UI to call `Microsoft.MixedReality.WebRTC` methods. But later when accessing the UI to interact with its controls, either to get user inputs or display results, this will be required. So as a best practice we start doing so right away instead of invoking some code directly in the `MainWindow` constructor.
   ```cs
   public MainWindow()
   {
       InitializeComponent();
       this.Loaded += OnLoaded;
   }
   ```

3. Create the event handler `OnLoaded()` and use it to enumerate the video capture devices. `GetVideoCaptureDevicesAsync()` returns a `Task` object which, once the task is completed successfully, will hold a list of video capture devices found on the host device where the app is running.
   ```cs
   public void OnLoaded(object sender, RoutedEventArgs e)
   {
        // Asynchronously retrieve a list of available video capture devices (webcams).
        PeerConnection.GetVideoCaptureDevicesAsync().ContinueWith((enumTask) =>
            {
                // Abort if the device enumeration failed
                if (enumTask.Exception != null)
                {
                    throw enumTask.Exception;
                }

                // Get the device list and, for example, print them to the debugger console
                var devices = enumTask.Result;
                foreach (var device in devices)
                {
                    // This message will show up in the Output window of Visual Studio
                    Debugger.Log(0, "", $"Found video capture device {device.name} (id: {device.id})");
                }
            }
        );
   }
   ```

Launch the app again. The main window is still empty, but the **Output window** of Visual Studio 2019 should show a list of devices. This list depends on the actual host device running the app, but looks something like:
```
Found video capture device <some webcam name> (id: <some long ID>)
```

Note that there might be multiple lines if multiple capture devices are available. In general the first one listed will be the default used by WebRTC, although it is possible to explicitly select a device (see [`PeerConnection.AddLocalVideoTrackAsync`](xref:PeerConnection.AddLocalVideoTrackAsync)).

----

Next : [Creating a peer connection](helloworld-cs-peerconnection)
