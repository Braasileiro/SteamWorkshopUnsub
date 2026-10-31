# Steamworks.NET binaries

This directory contains the prebuilt Standalone binaries for Steamworks.NET 2025.164.1. The
Standalone distribution is intended for non-Unity applications.

Source release: https://github.com/rlabrecque/Steamworks.NET/releases/tag/2025.164.1

The platform directories use .NET Runtime Identifier (RID) names:

- `linux-x64`: Linux managed wrapper and native Steam API library.
- `win-x64`: Windows managed wrapper and native Steam API library.

The managed wrapper is platform-specific and must not be shared between these directories.

To verify the vendored files from the repository root, run:

```sh
cd deps/Steamworks.NET
sha256sum --check checksums.sha256
```

`LICENSE.txt` applies to the Steamworks.NET wrapper. The native Steam API redistributables remain
subject to Valve's Steamworks SDK terms.
