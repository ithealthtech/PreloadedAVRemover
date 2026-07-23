[CmdletBinding()]
param(
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Release',
    [string]$OutputDirectory = 'publish-installer'
)

$ErrorActionPreference = 'Stop'
$repoRoot = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '..')).Path
$projectXml = [xml](Get-Content -Raw -LiteralPath (Join-Path $repoRoot 'PreloadedAVRemover.csproj'))
$displayVersion = [string]$projectXml.Project.PropertyGroup.Version
if ($displayVersion -notmatch '^(?<numeric>\d+\.\d+\.\d+)(?<suffix>-[0-9A-Za-z.-]+)?$') { throw "Unsupported product version: $displayVersion" }
$productVersion = $Matches.numeric
$fileVersion = "$productVersion.0"
$versionSlug = $displayVersion
$outputRoot = [IO.Path]::GetFullPath((Join-Path $repoRoot $OutputDirectory))
if (-not $outputRoot.StartsWith($repoRoot + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) { throw "Output must be inside the repository: $outputRoot" }
if (Test-Path -LiteralPath $outputRoot) { Remove-Item -LiteralPath $outputRoot -Recurse -Force }
$work = Join-Path $outputRoot 'work'
$appOutput = Join-Path $work 'app'
$brandingOutput = Join-Path $work 'branding'
$msiOutput = Join-Path $work 'msi'
$portableStage = Join-Path $work 'portable'
$launcherOutput = Join-Path $work 'launcher'
$artifacts = Join-Path $outputRoot 'artifacts'
New-Item -ItemType Directory -Force -Path $appOutput,$brandingOutput,$msiOutput,$portableStage,$launcherOutput,$artifacts | Out-Null

function Invoke-DotNet {
    param([string[]]$Arguments)
    & dotnet @Arguments
    if ($LASTEXITCODE -ne 0) { throw "dotnet $($Arguments -join ' ') failed with exit code $LASTEXITCODE" }
}

Invoke-DotNet -Arguments @('publish', (Join-Path $repoRoot 'PreloadedAVRemover.csproj'), '-c', $Configuration, '-r', 'win-x64', '--self-contained', 'true', '-p:PublishSingleFile=true', '-p:IncludeNativeLibrariesForSelfExtract=true', '-o', $appOutput)

Add-Type -AssemblyName System.Drawing
$wordmarkPath = Join-Path $repoRoot 'installer\Branding\it-health-tech-logo.png'
$markPath = Join-Path $repoRoot 'installer\Branding\it-health-tech-mark.png'
function New-BrandedImage([string]$path, [int]$width, [int]$height, [bool]$dialog) {
    $canvas = [Drawing.Bitmap]::new($width, $height)
    $graphics = [Drawing.Graphics]::FromImage($canvas)
    try {
        $graphics.Clear([Drawing.Color]::White)
        $graphics.InterpolationMode = [Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
        $graphics.PixelOffsetMode = [Drawing.Drawing2D.PixelOffsetMode]::HighQuality
        $graphics.SmoothingMode = [Drawing.Drawing2D.SmoothingMode]::HighQuality
        if ($dialog) {
            $railBrush = [Drawing.SolidBrush]::new([Drawing.Color]::FromArgb(8,42,76))
            try {
                $graphics.FillRectangle($railBrush, 0, 0, 155, $height)
            } finally { $railBrush.Dispose() }
            $source = [Drawing.Image]::FromFile($markPath)
            try { $graphics.DrawImage($source, 41, 52, 72, 72) } finally { $source.Dispose() }
        } else {
            # WiX reserves the left side of banner images for page titles and descriptions.
            # Keep the wordmark entirely inside the right-side branding region.
            $source = [Drawing.Image]::FromFile($wordmarkPath)
            try {
                $targetWidth = 125
                $targetHeight = [int][Math]::Round($source.Height * ($targetWidth / $source.Width))
                $graphics.DrawImage($source, $width - $targetWidth - 12, [int](($height - $targetHeight) / 2), $targetWidth, $targetHeight)
            } finally { $source.Dispose() }
        }
        $canvas.Save($path, [Drawing.Imaging.ImageFormat]::Png)
    } finally { $graphics.Dispose(); $canvas.Dispose() }
}
New-BrandedImage (Join-Path $brandingOutput 'installer-banner.png') 493 58 $false
New-BrandedImage (Join-Path $brandingOutput 'installer-dialog.png') 493 312 $true

function Assert-WhiteArtworkRegion([string]$path, [int]$left, [int]$top, [int]$right, [int]$bottom) {
    $image = [Drawing.Bitmap]::new($path)
    try {
        $white = [Drawing.Color]::White.ToArgb()
        for ($y = $top; $y -lt $bottom; $y++) {
            for ($x = $left; $x -lt $right; $x++) {
                if ($image.GetPixel($x, $y).ToArgb() -ne $white) {
                    throw "Installer artwork intrudes into the reserved text region at ($x,$y): $path"
                }
            }
        }
    } finally { $image.Dispose() }
}

# Fail packaging if branding overlaps regions where standard WiX dialogs render text.
Assert-WhiteArtworkRegion (Join-Path $brandingOutput 'installer-dialog.png') 155 0 493 312
Assert-WhiteArtworkRegion (Join-Path $brandingOutput 'installer-banner.png') 0 0 348 58

Copy-Item -LiteralPath (Join-Path $repoRoot 'installer\Branding\it-health-tech.ico') -Destination (Join-Path $brandingOutput 'it-health-tech.ico') -Force

$licenseText = Get-Content -Raw -LiteralPath (Join-Path $repoRoot 'LICENSE')
$escapedLicense = $licenseText.Replace('\','\\').Replace('{','\{').Replace('}','\}').Replace("`r`n", '\par ').Replace("`n", '\par ')
[IO.File]::WriteAllText((Join-Path $brandingOutput 'LICENSE.rtf'), "{\rtf1\ansi\deff0{\fonttbl{\f0 Segoe UI;}}\fs18 $escapedLicense}", [Text.Encoding]::ASCII)

$msiBase = "OEM-Endpoint-Cleanup-$versionSlug-win-x64"
$wixProject = Join-Path $repoRoot 'installer\Msi\OemEndpointCleanup.Installer.wixproj'
Invoke-DotNet -Arguments @('build', $wixProject, '-c', $Configuration, '--no-incremental', "-p:ApplicationSource=$appOutput", "-p:RepoRoot=$repoRoot", "-p:ProductVersion=$productVersion", "-p:ProductDisplayVersion=$displayVersion", "-p:BrandingDir=$brandingOutput", "-p:InstallerFileBase=$msiBase", "-p:InstallerOutput=$msiOutput")
$msiPath = Get-ChildItem -LiteralPath $msiOutput -Recurse -Filter "$msiBase.msi" | Select-Object -First 1 -ExpandProperty FullName
if (-not $msiPath) { throw 'The MSI build completed without producing the expected file.' }

Copy-Item -LiteralPath (Join-Path $appOutput 'PreloadedAVRemover.exe') -Destination $portableStage
foreach ($name in 'README.md','LICENSE','NOTICE','policy.example.json','TEST_REPORT.md') { Copy-Item -LiteralPath (Join-Path $repoRoot $name) -Destination $portableStage }
$portableName = "OEM-Endpoint-Cleanup-$versionSlug-portable-win-x64.zip"
$portablePath = Join-Path $artifacts $portableName
Compress-Archive -Path (Join-Path $portableStage '*') -DestinationPath $portablePath -CompressionLevel Optimal
$msiName = "$msiBase.msi"
$finalMsiPath = Join-Path $artifacts $msiName
Copy-Item -LiteralPath $msiPath -Destination $finalMsiPath
$msiHash = (Get-FileHash -LiteralPath $finalMsiPath -Algorithm SHA256).Hash
$portableHash = (Get-FileHash -LiteralPath $portablePath -Algorithm SHA256).Hash

$launcherProject = Join-Path $repoRoot 'installer\SetupLauncher\SetupLauncher.csproj'
Invoke-DotNet -Arguments @('publish', $launcherProject, '-c', $Configuration, '-r', 'win-x64', '--self-contained', 'true', '-p:PublishSingleFile=true', '-p:IncludeNativeLibrariesForSelfExtract=true', "-p:SetupVersion=$displayVersion", "-p:SetupFileVersion=$fileVersion", "-p:InstallerMsiFileName=$msiName", "-p:InstallerMsiSha256=$msiHash", "-p:PortableZipFileName=$portableName", "-p:PortableZipSha256=$portableHash", '-o', $launcherOutput)
$setupName = "OEM-Endpoint-Cleanup-Setup-$versionSlug-win-x64.exe"
$setupPath = Join-Path $artifacts $setupName
Copy-Item -LiteralPath (Join-Path $launcherOutput 'OEMEndpointCleanupSetup.exe') -Destination $setupPath
& $setupPath --self-test
if ($LASTEXITCODE -ne 0) { throw "Setup UI self-test failed with exit code $LASTEXITCODE" }
& $setupPath --verify-payloads
if ($LASTEXITCODE -ne 0) { throw "Setup payload verification failed with exit code $LASTEXITCODE" }

$checksumLines = foreach ($file in Get-ChildItem -LiteralPath $artifacts -File | Sort-Object Name) {
    "{0}  {1}" -f (Get-FileHash -LiteralPath $file.FullName -Algorithm SHA256).Hash.ToLowerInvariant(), $file.Name
}
[IO.File]::WriteAllText((Join-Path $artifacts 'SHA256SUMS.txt'), ($checksumLines -join "`n") + "`n", [Text.UTF8Encoding]::new($false))

[pscustomobject]@{
    Version = $displayVersion
    Setup = $setupPath
    Msi = $finalMsiPath
    Portable = $portablePath
    Checksums = (Join-Path $artifacts 'SHA256SUMS.txt')
} | Format-List
