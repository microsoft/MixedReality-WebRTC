# Creating a peer connection

Next, we create a [`PeerConnection`](xref:Microsoft.MixedReality.WebRTC.PeerConnection) object which encapsulates the connection to the remote peer.

Continue editing the `Program.cs` file and append the following:

1. Create the peer connection object. Note that the [`PeerConnection`](xref:Microsoft.MixedReality.WebRTC.PeerConnection) class is marked as disposable, so must be disposed to clean-up the native resources. Failing to do so generally lead to crashes or hangs, as the internal WebRTC threads are not stopped and therefore the native DLL cannot be unloaded.
   ```cs
   using var pc = new PeerConnection();
   ```
   This construct with the `using` keyword is a shorthand that will automatically call [`Dispose()`](xref:System.IDisposable.Dispose) when that variable `pc` gets out of scope, in our case at the end of the `Main` function.

2. The [`PeerConnection`](xref:Microsoft.MixedReality.WebRTC.PeerConnection) object is initally created in an idle state where it cannot be used until initialized with a call to [`InitializeAsync()`](xref:Microsoft.MixedReality.WebRTC.PeerConnection.InitializeAsync(Microsoft.MixedReality.WebRTC.PeerConnectionConfiguration,CancellationToken)). This method takes a [`PeerConnectionConfiguration`](xref:Microsoft.MixedReality.WebRTC.PeerConnectionConfiguration) object which allows specifying some options to configure the connection. In this tutorial, most default options are suitable, but we want to specify a STUN server to make sure that the peer connection can connect to the remote peer even if behind a [NAT](https://en.wikipedia.org/wiki/Network_address_translation).
   ```cs
   var config = new PeerConnectionConfiguration
   {
       IceServers = new List<IceServer> {
               new IceServer{ Urls = { "stun:stun.l.google.com:19302" } }
           }
   };
   await pc.InitializeAsync(config);
   ```
   In this example we use a free STUN server courtesy of Google. Note that this is fine for testing, but **must not be used for production**. Also, the ICE server list uses the [`List<>`](xref:System.Collections.Generic.List`1) generic class, so we need to import the `System.Collections.Generic` module with a `using` directive at the top of the file.
   ```cs
   using System.Collections.Generic;
   ```

3. Print a simple message to console to notify the user that the peer connection is initialized. This is optional, but is always good practice to inform the user after important steps completed, whether successfully or not.
    ```cs
   Console.WriteLine("Peer connection initialized.");
   ```

Run the application again; the printed message should appear after some time. It generally takes up to a few seconds to initialize the peer connection, depending on the device.

![Peer connection initialized](cs4.png)

----

Next : [Adding local media tracks](helloworld-cs-mediatracks-core3.md)
