# Install the ClangCL toolchain for ARM64 (Desktop and UWP)

## Preparation

With the Visual Studio Installer, install the clang-cl/LLVM toolchain for Desktop x86/x64. This provides support for those platforms as well as a starting point for creating the ARM64 variants.

## Desktop

- Go to the `C:\Program Files (x86)\Microsoft Visual Studio\2019\Enterprise\MSBuild\Microsoft\VC\v160\Platforms\x64\PlatformToolsets` folder, or the similar path for your local Visual Studio 2019 install.
- Copy the entire `ClangCL` folder.
- Paste it into `C:\Program Files (x86)\Microsoft Visual Studio\2019\Enterprise\MSBuild\Microsoft\VC\v160\Platforms\ARM64\PlatformToolsets`.
- Inside the newly created `ClangCL` folder, edit `Toolset.props` and:
  - Replace `Microsoft.Cpp.MSVC.Toolset.x64.props` with `Microsoft.Cpp.MSVC.Toolset.arm64.props`.
  - Inside `<LlvmArchitectureSwitch>`, delete the `-m64` flag and replace it with the `--target=aarch64-linux-gnu` flag.
- Restart Visual Studio 2019 for changes to take effect.

## UWP

- Go to the `C:\Program Files (x86)\Microsoft Visual Studio\2019\Enterprise\MSBuild\Microsoft\VC\v160\Application Type\Windows Store\10.0\Platforms\ARM64\PlatformToolsets` folder, or the similar path for your local Visual Studio 2019 install.
- Copy the `v142` folder, which contains the toolchain for MSVC v142, and paste a copy of it renamed to `ClangCL`. This will serve as the base for the UWP ClangCL toolchain.
- Inside the newly created `ClangCL` folder:
  - Edit `Toolset.props`:
    - After the import of `Microsoft.Cpp.MSVC.Toolset.ARM64.props`, add a new line importing the ClangCL default properties: `<Import Project="$(VCTargetsPath)\Microsoft.Cpp.ClangCl.Common.props"/>`. This is the same line found in the Desktop variant.
    - Copy 2 other lines from the Desktop variant which are missing here, and paste them into the `<PropertyGroup>` block:
      ```xml
      <DebuggerFlavor Condition="'$(DebuggerFlavor)'==''">WindowsLocalDebugger</DebuggerFlavor>
      <LlvmArchitectureSwitch>--target=aarch64-linux-gnu</LlvmArchitectureSwitch>
      ```
      It is unclear if the first one is needed, but doesn't seem to cause any issue.
  - Edit `Toolset.targets`:
    - Again, similar to the Desktop variant, replace `Microsoft.CppCommon.targets` with `Microsoft.Cpp.ClangCl.Common.targets`.
- Restart Visual Studio 2019 for changes to take effect.
