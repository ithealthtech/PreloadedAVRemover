# Preloaded AV Remover

A portable Windows utility for detecting and removing common antivirus trials and security extras bundled with OEM computers.

![Platform](https://img.shields.io/badge/platform-Windows%20x64-2563eb)
![Framework](https://img.shields.io/badge/.NET-8.0-512bd4)
![Version](https://img.shields.io/badge/version-1.2.0-0f766e)

## Features

- Detects common consumer AV products from McAfee, Norton/Symantec, Trend Micro, Avast, AVG, Kaspersky, ESET, Bitdefender, Panda, Webroot, F-Secure, and Malwarebytes.
- Uses registered silent uninstall commands when available.
- Falls back to the vendor's interactive uninstaller when silent removal is unavailable.
- Rescans after removal and reports anything that remains installed.
- Optionally detects related browser and security extras.
- Never targets Microsoft Defender.
- Runs as a self-contained Windows x64 executable.

## Usage

1. Download `PreloadedAVRemover.exe` from the Releases page.
2. Run the application and approve the Windows UAC prompt.
3. Select one or more detected products.
4. Choose **Remove selected** and follow any vendor prompts.
5. Review the final verification result.

Some security products protect their uninstallers, require a password, or do not provide a silent-removal interface. The utility reports those products rather than deleting files or registry data directly.

## Build from source

Install the [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0), then run:

```powershell
dotnet publish .\PreloadedAVRemover.csproj `
  -c Release `
  -r win-x64 `
  --self-contained true `
  -p:PublishSingleFile=true `
  -p:IncludeNativeLibrariesForSelfExtract=true `
  -o .\publish
```

The standalone executable is written to `publish\PreloadedAVRemover.exe`.

## Safety model

The application reads the standard Windows uninstall registry locations. It executes only the uninstall command registered by the installed product, converting MSI packages to the standard quiet uninstall form where possible. It does not use `Win32_Product`, manually remove security services, or modify Microsoft Defender.

