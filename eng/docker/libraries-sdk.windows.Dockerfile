# escape=`
# Simple Dockerfile which copies clr and library build artifacts into target dotnet sdk image
ARG SDK_BASE_IMAGE=mcr.microsoft.com/dotnet/nightly/sdk:8.0-nanoserver-ltsc2022
FROM $SDK_BASE_IMAGE as target

SHELL ["pwsh", "-Command", "$ErrorActionPreference = 'Stop'; $ProgressPreference = 'SilentlyContinue';"]

ARG VERSION=9.0
ENV _DOTNET_INSTALL_CHANNEL=$VERSION
ARG CONFIGURATION=Release

USER ContainerAdministrator

# remove the existing ASP.NET SDK, we want to keep only the latest one we download later
RUN Remove-Item -Force -Recurse 'C:/Program Files/dotnet/shared/Microsoft.AspNetCore.App/*'

RUN Invoke-WebRequest -Uri https://builds.dotnet.microsoft.com/dotnet/scripts/v1/dotnet-install.ps1 -OutFile .\dotnet-install.ps1
RUN & .\dotnet-install.ps1 -Channel $env:_DOTNET_INSTALL_CHANNEL -Quality daily -InstallDir 'C:/Program Files/dotnet'

USER ContainerUser

COPY . /live-runtime-artifacts

# Add AspNetCore bits to testhost:
ENV _ASPNETCORE_SOURCE="C:/Program Files/dotnet/shared/Microsoft.AspNetCore.App/*"
ENV _ASPNETCORE_DEST="C:/live-runtime-artifacts/testhost/net$VERSION-windows-$CONFIGURATION-x64/shared/Microsoft.AspNetCore.App"
RUN & New-Item -ItemType Directory -Path $env:_ASPNETCORE_DEST
RUN Copy-Item -Recurse -Path $env:_ASPNETCORE_SOURCE -Destination $env:_ASPNETCORE_DEST
