REM Copyright (c) Microsoft Corporation. All rights reserved.
REM Licensed under the MIT License. See LICENSE in the project root for license information.

@echo off

REM %1 Kind (CPP / CS)
REM %2 Platform (Win32 / WSA)
REM %3 Architecture (x86 / x64 / ARM / ARM64 / AnyCPU)
REM %4 Build config (Debug / Release)

if %1==CPP (
    if %3==x64 (
        if %2==WSA (
            REM UWP (MSVC) becomes WSA (Unity)
            REM x64 (MSVC) becomes x86_64 (Unity)
            xcopy /Y /Q ..\bin\UWP\x64\%4\Microsoft.MixedReality.WebRTC.Native.dll ..\libs\Microsoft.MixedReality.WebRTC.Unity\Assets\Plugins\%2\x86_64\
            xcopy /Y /Q ..\bin\UWP\x64\%4\Microsoft.MixedReality.WebRTC.Native.pdb ..\libs\Microsoft.MixedReality.WebRTC.Unity\Assets\Plugins\%2\x86_64\
        ) else (
            REM x64 (MSVC) becomes x86_64 (Unity)
            xcopy /Y /Q ..\bin\%2\x64\%4\Microsoft.MixedReality.WebRTC.Native.dll ..\libs\Microsoft.MixedReality.WebRTC.Unity\Assets\Plugins\%2\x86_64\
            xcopy /Y /Q ..\bin\%2\x64\%4\Microsoft.MixedReality.WebRTC.Native.pdb ..\libs\Microsoft.MixedReality.WebRTC.Unity\Assets\Plugins\%2\x86_64\
        )
    ) else (
        if %2==WSA (
            REM UWP (MSVC) becomes WSA (Unity)
            xcopy /Y /Q ..\bin\UWP\%3\%4\Microsoft.MixedReality.WebRTC.Native.dll ..\libs\Microsoft.MixedReality.WebRTC.Unity\Assets\Plugins\%2\%3\
            xcopy /Y /Q ..\bin\UWP\%3\%4\Microsoft.MixedReality.WebRTC.Native.pdb ..\libs\Microsoft.MixedReality.WebRTC.Unity\Assets\Plugins\%2\%3\
        ) else (
            xcopy /Y /Q ..\bin\%2\%3\%4\Microsoft.MixedReality.WebRTC.Native.dll ..\libs\Microsoft.MixedReality.WebRTC.Unity\Assets\Plugins\%2\%3\
            xcopy /Y /Q ..\bin\%2\%3\%4\Microsoft.MixedReality.WebRTC.Native.pdb ..\libs\Microsoft.MixedReality.WebRTC.Unity\Assets\Plugins\%2\%3\
        )
    )
) else (
    REM C# assemblies are AnyCPU (architecture-independent) - just need a single copy for all architectures
    REM But the Unity Editor doesn't like DLLs in multiple directories, so copy them in Win32\x86_64 where it's looking
    xcopy /Y /Q ..\bin\netstandard2.0\%4\Microsoft.MixedReality.WebRTC.dll ..\libs\Microsoft.MixedReality.WebRTC.Unity\Assets\Plugins\Win32\x86_64\
    xcopy /Y /Q ..\bin\netstandard2.0\%4\Microsoft.MixedReality.WebRTC.pdb ..\libs\Microsoft.MixedReality.WebRTC.Unity\Assets\Plugins\Win32\x86_64\
)
