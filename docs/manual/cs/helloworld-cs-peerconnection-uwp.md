# Creating a peer connection

Next, we create a [`PeerConnection`](xref:Microsoft.MixedReality.WebRTC.PeerConnection) object which encapsulates the connection to the remote peer.

The [`PeerConnection`](xref:Microsoft.MixedReality.WebRTC.PeerConnection) class is marked as disposable, so must be disposed to clean-up the native resources. Failing to do so generally lead to crashes or hangs, as the internal WebRTC threads are not stopped and therefore the native DLL cannot be unloaded. Unlike in the [.NET Core tutorial](helloworld-cs-peerconnection-core3.md) where we build a console application however, here with the XAML framework we cannot easily use the `using var` construct to rely on the C# compiler to call the [`Dispose()`](xref:System.IDisposable.Dispose) method for us, because currently the only available method is the `OnLoaded()` method, and it will terminate and release all its local variable once loading is finished, and before the end of the application. Instead, we need to keep a reference to the [`PeerConnection`](xref:Microsoft.MixedReality.WebRTC.PeerConnection) instance and call [`Dispose()`](xref:System.IDisposable.Dispose) explicitly when done with it.

Continue editing the `MainPage.xaml.cs` file and append the following:

1. At the top of the `MainPage` class, declare a private variable of type `PeerConnection`.
   ```cs
   private PeerConnection _peerConnection;
   ```

2. Continue to append to the `OnLoaded()` method. First, instantiate the peer connection.
   ```cs
   _peerConnection = new PeerConnection();
   ```

3. The [`PeerConnection`](xref:Microsoft.MixedReality.WebRTC.PeerConnection) object is initally created in an idle state where it cannot be used until initialized with a call to [`InitializeAsync()`](xref:Microsoft.MixedReality.WebRTC.PeerConnection.InitializeAsync(Microsoft.MixedReality.WebRTC.PeerConnectionConfiguration,CancellationToken)). This method takes a [`PeerConnectionConfiguration`](xref:Microsoft.MixedReality.WebRTC.PeerConnectionConfiguration) object which allows specifying some options to configure the connection. In this tutorial, most default options are suitable, but we want to specify a STUN server to make sure that the peer connection can connect to the remote peer even if behind a [NAT](https://en.wikipedia.org/wiki/Network_address_translation).
   ```cs
   var config = new PeerConnectionConfiguration
   {
       IceServers = new List<IceServer> {
               new IceServer{ Urls = { "stun:stun.l.google.com:19302" } }
           }
   };
   await _peerConnection.InitializeAsync(config);
   ```
   In this example we use a free STUN server courtesy of Google. Note that this is fine for testing, but **must not be used for production**. Also, the ICE server list uses the [`List<>`](xref:System.Collections.Generic.List`1) generic class, so we need to import the `System.Collections.Generic` module with a `using` directive at the top of the file.
   ```cs
   using System.Collections.Generic;
   ```

4. Print a simple message to the debugger to confirm that the peer connection wass initialized. In a real-world application, properly notifying the user of failures is critical, but here for the sake of this tutorial we simply rely on a any exception interrupting the application before the message is printed if an error occur.
    ```cs
   Debugger.Log(0, "", "Peer connection initialized successfully.\n");
   ```

Run the application again; the printed message should appear after some time in the Visual Studio **Output** window under the **Debug** section. It can take up to a few seconds to initialize the peer connection, depending on the device.

![Peer connection initialized](cs-uwp11.png)

----

Next : [Adding local media tracks](helloworld-cs-mediatracks-uwp.md)
