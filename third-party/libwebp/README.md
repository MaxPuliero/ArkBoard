# Bundled libwebp 1.6.0 (Windows x64)

Built from the unmodified official source archive:

https://storage.googleapis.com/downloads.webmproject.org/releases/webp/libwebp-1.6.0.tar.gz

Source SHA-256: `e4ab7009bf0629fd11982d4c2aa83964cf244cffba7347ecd39019a9e38c4564`

Upstream source: https://github.com/webmproject/libwebp/tree/v1.6.0

Built with Visual Studio 2022 MSVC 14.44.35207, x64 Release,
`CMAKE_MSVC_RUNTIME_LIBRARY=MultiThreaded` (static CRT).
The binaries require only Windows KERNEL32 and the other bundled libwebp DLLs;
no Visual C++ Redistributable or WIC codec installation is required.

SHA-256 checksums:

```
libwebp.dll       fca6294ba7ad58319b5cfccabec89a10782f23691eedf13a1903ccf289a28b20
libwebpdemux.dll  47d45ac56af7b9fedccf1ac368bfdf178255e005e75337ce210045a62e3115d9
libsharpyuv.dll   2d2cb561b9e2f26b11194d4a0c035a88e98b03db0be493ea06cf32bbe9c5d229
```

`COPYING`, `PATENTS`, and `AUTHORS` are the upstream libwebp notices from v1.6.0.
libwebp is BSD-licensed, with an additional patent grant.

To rebuild the native dependency, extract the source outside the repository and
run `tools/build-webp.ps1 -SourceDirectory <libwebp-1.6.0> -BuildDirectory <temporary-build-folder>`.
This requires CMake and Visual Studio 2022 C++ build tools. Different toolchain
versions or paths may produce different binary hashes.

The build copies the three DLLs next to ArkBoard.exe and the notices to
`third-party/libwebp`. No installation, codec registration, network access, or
NuGet restore is needed to build or run ArkBoard. Normal builds use these
checked-in binaries and do not require CMake or Visual Studio.
