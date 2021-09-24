# WebRTC tries to establish a direct network connection between two clients.
# However, because of docker network limitation, you won't be able to make a connection between your host and docker container.
# https://docs.docker.com/network/network-tutorial-host/
# This will work on Linux only
docker run --rm -it --network host --name mixed-reality-receiver-linux mixed-reality-receiver-linux
