FROM ubuntu:bionic AS main

RUN apt update && apt install -y curl gnupg2 \
    && apt-get install -y wget \
    && curl https://packages.microsoft.com/keys/microsoft.asc | apt-key add - \
    && curl https://packages.microsoft.com/config/ubuntu/18.04/prod.list | tee /etc/apt/sources.list.d/msprod.list \
    && apt update \
    && wget https://packages.microsoft.com/config/ubuntu/18.04/packages-microsoft-prod.deb -O packages-microsoft-prod.deb \
    && dpkg -i packages-microsoft-prod.deb \
    && rm packages-microsoft-prod.deb

RUN apt-get update \
  && apt-get install -y apt-transport-https \
  && apt-get update \
  && apt-get install -y dotnet-sdk-5.0

RUN apt-get install -y libc++-8-dev
RUN apt-get install -y libc++abi-8-dev

RUN apt-get install -y pulseaudio
RUN apt-get install -y pulseaudio-utils

RUN adduser root pulse-access

FROM mcr.microsoft.com/dotnet/sdk:5.0-focal AS build
COPY . /src
WORKDIR /src
RUN dotnet restore "TestReceiveAV.Linux.csproj"
RUN dotnet build "TestReceiveAV.Linux.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "TestReceiveAV.Linux.csproj" -c Release -o /app/publish

FROM main
WORKDIR /app
COPY --from=publish /app/publish .
RUN ldd runtimes/linux-x64/native/libmrwebrtc.so
ENTRYPOINT ["dotnet", "TestReceiveAV.Linux.dll"]
