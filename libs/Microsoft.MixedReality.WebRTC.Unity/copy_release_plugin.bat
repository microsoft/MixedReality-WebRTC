
REM UWP ARM build for device deploy
xcopy /F /Y ..\..\bin\AnyCPU\Release\Microsoft.MixedReality.WebRTC.dll Assets\Plugins\WSA\ARM\
xcopy /F /Y ..\..\bin\AnyCPU\Release\Microsoft.MixedReality.WebRTC.pdb Assets\Plugins\WSA\ARM\
xcopy /F /Y ..\..\bin\UWP\ARM\Release\Microsoft.MixedReality.WebRTC.Native.dll Assets\Plugins\WSA\ARM\
xcopy /F /Y ..\..\bin\UWP\ARM\Release\Microsoft.MixedReality.WebRTC.Native.pdb Assets\Plugins\WSA\ARM\

REM Win32 (Desktop) x86_64 build for editor
xcopy /F /Y ..\..\bin\AnyCPU\Release\Microsoft.MixedReality.WebRTC.dll Assets\Plugins\Win32\x86_64\
xcopy /F /Y ..\..\bin\AnyCPU\Release\Microsoft.MixedReality.WebRTC.pdb Assets\Plugins\Win32\x86_64\
xcopy /F /Y ..\..\bin\Win32\x64\Release\Microsoft.MixedReality.WebRTC.Native.dll Assets\Plugins\Win32\x86_64\
xcopy /F /Y ..\..\bin\Win32\x64\Release\Microsoft.MixedReality.WebRTC.Native.pdb Assets\Plugins\Win32\x86_64\
