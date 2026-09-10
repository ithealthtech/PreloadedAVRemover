# Building

## Prerequisites

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- Windows x64 (the project targets `net8.0-windows` and uses WinForms)
- WiX Toolset 5.0.2, for MSI packaging only

There is no solution file. Build each sub-project individually.

## Sub-projects

| Path | Output |
| --- | --- |
| `PreloadedAVRemover.csproj` | Main WinForms application |
| `installer/Installer.Core` | Installer operations library |
| `installer/SetupLauncher` | Branded setup launcher |
| `installer/Msi` | WiX MSI package |
| `tests/PreloadedAVRemover.Tests` | Test suite |

## Application

```powershell
dotnet build .\PreloadedAVRemover.csproj
```

Self-contained single-file publish, as shipped:

```powershell
dotnet publish .\PreloadedAVRemover.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o .\publish
```

## Installer package

Build all three distributable artifacts:

```powershell
.\installer\Build-Installer.ps1 -Configuration Release
```

Outputs land in `publish-installer\artifacts` with a `SHA256SUMS.txt` manifest. The
launcher validates the adjacent MSI and portable ZIP against SHA-256 values embedded at
build time before installing or extracting them.

The launcher offers three modes:

- **Everyone on this computer** - per-machine MSI under Program Files, elevated through
  Windows Installer.
- **Just me** - the same dual-purpose MSI per-user under Local AppData, without
  registering it for other Windows users.
- **Portable** - verifies and extracts the portable ZIP to a chosen folder with no
  Windows Installer registration or shortcuts.

## WiX version pin

The MSI is built with WiX Toolset **5.0.2**. WiX v6 introduced a separate Open Source
Maintenance Fee; 5.0.2 is intentionally pinned for this GPL project pending an
organizational licensing decision for newer releases. Do not bump it casually.

## Code signing

The published executable is **not** code-signed in this repository and may trigger
SmartScreen. Production distribution should add an organization-controlled signing
certificate and release pipeline. See [Security model](security-model.md).

## Continuous integration

`.github/workflows/ci.yml` runs formatting verification, a Release build, the full test
suite, the non-elevated layout-only UI self-test, a self-contained single-file publish
verification, and a complete MSI/launcher/portable packaging build. It does not create a
release or deploy artifacts.
