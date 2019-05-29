@echo off

REM %1 Kind (CPP / CS)
REM %2 Platform (Win32 / WSA)
REM %3 Architecture (x86 / x64 / ARM / ARM64 / AnyCPU)
REM %4 Build config (Debug / Release)

if %1==CPP (
    if %3==x64 (
        REM x64 becomes x86_64
        xcopy /Y /Q ..\..\bin\%2\x64\%4\Microsoft.MixedReality.WebRTC.Native.dll ..\..\libs\Microsoft.MixedReality.WebRTC.Unity\Assets\Plugins\%2\x86_64\
        xcopy /Y /Q ..\..\bin\%2\x64\%4\Microsoft.MixedReality.WebRTC.Native.pdb ..\..\libs\Microsoft.MixedReality.WebRTC.Unity\Assets\Plugins\%2\x86_64\
    ) else (
        xcopy /Y /Q ..\..\bin\%2\%3\%4\Microsoft.MixedReality.WebRTC.Native.dll ..\..\libs\Microsoft.MixedReality.WebRTC.Unity\Assets\Plugins\%2\%3\
        xcopy /Y /Q ..\..\bin\%2\%3\%4\Microsoft.MixedReality.WebRTC.Native.pdb ..\..\libs\Microsoft.MixedReality.WebRTC.Unity\Assets\Plugins\%2\%3\
    )
) else (
    REM C# assemblies are AnyCPU (architecture-independent)
    xcopy /Y /Q ..\..\bin\%3\%4\Microsoft.MixedReality.WebRTC.dll ..\..\libs\Microsoft.MixedReality.WebRTC.Unity\Assets\Plugins\
    xcopy /Y /Q ..\..\bin\%3\%4\Microsoft.MixedReality.WebRTC.pdb ..\..\libs\Microsoft.MixedReality.WebRTC.Unity\Assets\Plugins\
)
