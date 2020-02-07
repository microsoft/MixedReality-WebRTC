#Usage

 - Build and run the solution file,
 - Open the `webrtc.html` page in a WebRTC enabled browser.
 - Click the `Start` button in the Web Browser.
 - If successful a new Window should pop-up displaying the Video stream received from the browser.

 #Notes

 The browser script deliberately waits until the first ICE candidate has been gathered before establishing the web socket connection to send the SDP offer. This makes the signaling easier, a single SDP offer-answer exchange, no need to transmit additional ICE candidates.

 A limitation of the above approach is that it will not take full advantage of ICE and in particular is likely to cause the peer connection to fail unless the browser and test application are on the same machine or local network.
