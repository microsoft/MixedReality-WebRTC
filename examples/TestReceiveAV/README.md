#Usage

 - Generate an X509 certificate and key for the web socket server using the instructions below,
 - Build and run the TestReceiveAV project
 - Open the `webrtc.html` page in a WebRTC enabled browser.
 - Click the `Start` button in the Web Browser.
 - If successful a new Window should pop-up displaying the Video stream received from the browser and the audio should be played on the default system speaker.

 #Certificate Instructions

 A web socket connection is used to exchange the SDP offer and answer as well as ICE candidates. The web socket connection will only work if listening port on the test application can be accessed by the browser. It should work correctly if both are on the same development machine or local network.

 A PKCS12 archive file which contains an X509 certificate with a common name of "localhost" and a private key is required. The archive file is needed for the web socket server and is suitable for use when the web socket connection is between a browser and the test application running on the same machine. The instructions use to generate the certificate using openssl are below:

 ````
openssl req -config req.conf -x509 -newkey rsa:4096 -keyout private/localhost.pem -out localhost.pem -nodes -days 3650
openssl pkcs12 -export -in localhost.pem -inkey private/localhost.pem -out localhost.pfx -nodes

openssl req -config req.conf -x509 -newkey rsa:2048 -keyout winsvr19-test-key.pem -out winsvr19-test.pem -nodes -days 3650
openssl pkcs12 -export -in winsvr19-test.pem -inkey winsvr19-test-key.pem -out winsvr19-test.pfx -nodes
````
An example req.conf file contents:

````
[req]
default_bits = 2048
default_md = sha256
prompt = no
encrypt_key = no
distinguished_name = dn
x509_extensions = x509_ext
string_mask = utf8only
[dn]
CN = localhost
[x509_ext]
subjectAltName = localhost, IP:127.0.0.1, IP:::1 
keyUsage = Digital Signature, Key Encipherment, Data Encipherment
extendedKeyUsage = TLS Web Server Authentication
````

To get the thumbprint for certificate used for DTLS use:

````
openssl x509 -fingerprint -sha256 -in localhost.pem
````
