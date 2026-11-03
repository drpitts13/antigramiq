#Requires -Version 5.1
<#
.SYNOPSIS
    Builds HemoIQ and creates a desktop shortcut with the blood bag icon.
.DESCRIPTION
    1. Builds the project in Release mode via dotnet build.
    2. Converts blood_bag_icon.png into a proper multi-size .ico file.
    3. Places the .ico next to the executable.
    4. Creates (or updates) a shortcut on the current user's desktop.

    The shortcut always points at the checkout this script lives in. Run the
    copy in the checkout whose code you want to launch.

    If HemoIQ is already running from this build folder, the build
    cannot replace the executable. The script asks before closing it; pass
    -Force to close it without asking.
.PARAMETER Force
    Close a running HemoIQ from this build folder without prompting.
#>
param([switch]$Force)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$ProjectRoot = $PSScriptRoot
$CsprojPath  = Join-Path $ProjectRoot 'HemoIQ\HemoIQ.csproj'
$PngPath     = Join-Path $ProjectRoot 'HemoIQ\blood_bag_icon.png'
$ExePath     = Join-Path $ProjectRoot 'HemoIQ\bin\Release\net8.0-windows\HemoIQ.exe'
$IcoPath     = Join-Path $ProjectRoot 'HemoIQ\bin\Release\net8.0-windows\blood_bag.ico'
$ShortcutPath = Join-Path ([Environment]::GetFolderPath('Desktop')) 'HemoIQ.lnk'

# ---------------------------------------------------------------------------
# 0. Make sure the running app is not holding the executable open
# ---------------------------------------------------------------------------
$running = @(Get-Process -Name 'HemoIQ' -ErrorAction SilentlyContinue |
    Where-Object { $_.Path -and ([IO.Path]::GetFullPath($_.Path) -ieq [IO.Path]::GetFullPath($ExePath)) })
if ($running.Count -gt 0) {
    Write-Host "`nHemoIQ is running from this build folder and must close so the new build can replace it." -ForegroundColor Yellow
    if (-not $Force) {
        $answer = Read-Host "Close it now? Unsaved work in the app will be lost. (y/N)"
        if ($answer -notmatch '^(y|yes)$') { throw "Close HemoIQ and run this script again." }
    }
    $running | ForEach-Object { $_.CloseMainWindow() | Out-Null }
    $running | ForEach-Object { if (-not $_.WaitForExit(10000)) { Stop-Process -Id $_.Id -Force } }
    Write-Host "    Closed." -ForegroundColor Green
}

# ---------------------------------------------------------------------------
# 1. Build
# ---------------------------------------------------------------------------
$branch = (git -C $ProjectRoot rev-parse --abbrev-ref HEAD 2>$null)
$commit = (git -C $ProjectRoot log -1 --format='%h %s' 2>$null)
Write-Host "`n==> Building HemoIQ (Release)..." -ForegroundColor Cyan
Write-Host "    Source:  $ProjectRoot"
if ($branch) { Write-Host "    Branch:  $branch" }
if ($commit) { Write-Host "    Commit:  $commit" }

$buildStarted = Get-Date
dotnet build $CsprojPath --configuration Release --nologo -v quiet
if ($LASTEXITCODE -ne 0) { throw "dotnet build failed (exit $LASTEXITCODE)." }
if (-not (Test-Path $ExePath)) { throw "Build finished but $ExePath was not produced." }

# The executable is only rewritten when inputs changed, so check the main
# assembly against the newest source file instead of the build start time.
$dllPath = [IO.Path]::ChangeExtension($ExePath, '.dll')
$newestSource = Get-ChildItem (Join-Path $ProjectRoot 'HemoIQ') -Recurse -File `
        -Include *.cs, *.xaml, *.md |
    Where-Object { $_.FullName -notmatch '\\(bin|obj)\\' } |
    Sort-Object LastWriteTime -Descending | Select-Object -First 1
$built = Get-Item $dllPath
if ($newestSource -and $built.LastWriteTime -lt $newestSource.LastWriteTime) {
    throw "Build output ($($built.LastWriteTime)) is older than $($newestSource.Name) ($($newestSource.LastWriteTime)). The build did not pick up the latest code."
}
$state = if ($built.LastWriteTime -ge $buildStarted) { 'rebuilt' } else { 'already up to date' }
Write-Host "    Build succeeded ($state, $($built.LastWriteTime))." -ForegroundColor Green

# ---------------------------------------------------------------------------
# 2. Convert PNG -> ICO (multi-size: 16, 32, 48, 256)
# ---------------------------------------------------------------------------
Write-Host "`n==> Converting blood_bag_icon.png to .ico ..." -ForegroundColor Cyan

Add-Type -AssemblyName System.Drawing

function ConvertPngToIco {
    param(
        [string]$Source,
        [string]$Destination,
        [int[]]$Sizes = @(16, 32, 48, 256)
    )

    $original = [System.Drawing.Bitmap]::new($Source)

    # Build PNG-compressed image blobs for each size
    $blobs = @()
    foreach ($sz in $Sizes) {
        $bmp = [System.Drawing.Bitmap]::new($sz, $sz)
        $g   = [System.Drawing.Graphics]::FromImage($bmp)
        $g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
        $g.SmoothingMode     = [System.Drawing.Drawing2D.SmoothingMode]::HighQuality
        $g.PixelOffsetMode   = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
        $g.DrawImage($original, 0, 0, $sz, $sz)
        $g.Dispose()

        $ms = [System.IO.MemoryStream]::new()
        if ($sz -ge 256) {
            # Modern ICO: embed PNG directly for 256+
            $bmp.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png)
        } else {
            # Classic ICO: embed as 32-bpp BMP (DIB format)
            $bmp.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png)
        }
        $blobs += ,$ms.ToArray()
        $ms.Dispose()
        $bmp.Dispose()
    }
    $original.Dispose()

    # Write ICO container
    # Layout: 6-byte ICONDIR + (16-byte ICONDIRENTRY * N) + image blobs
    $headerSize = 6
    $entrySize  = 16
    $dataOffset = $headerSize + $entrySize * $Sizes.Count

    # Create (not OpenWrite) so a smaller icon does not leave old bytes behind.
    $stream = [System.IO.File]::Create($Destination)
    $w = [System.IO.BinaryWriter]::new($stream)

    # ICONDIR
    $w.Write([uint16]0)              # Reserved
    $w.Write([uint16]1)              # Type: 1 = ICO
    $w.Write([uint16]$Sizes.Count)  # Image count

    # ICONDIRENTRY[] — write entries first, then blobs
    $offset = [uint32]$dataOffset
    for ($i = 0; $i -lt $Sizes.Count; $i++) {
        $sz   = [int]$Sizes[$i]
        $bLen = [uint32]$blobs[$i].Length
        $w.Write([byte]$(if ($sz -ge 256) { 0 } else { $sz }))  # Width  (0 = 256)
        $w.Write([byte]$(if ($sz -ge 256) { 0 } else { $sz }))  # Height (0 = 256)
        $w.Write([byte]0)                # ColorCount
        $w.Write([byte]0)                # Reserved
        $w.Write([uint16]1)              # Planes
        $w.Write([uint16]32)             # BitCount
        $w.Write([uint32]$bLen)          # SizeInBytes
        $w.Write([uint32]$offset)        # Offset
        $offset += $bLen
    }

    # Image data
    foreach ($blob in $blobs) { $w.Write($blob) }

    $w.Close()
    $stream.Close()
}

ConvertPngToIco -Source $PngPath -Destination $IcoPath -Sizes @(16, 32, 48, 256)
Write-Host "    Icon written to: $IcoPath" -ForegroundColor Green

# ---------------------------------------------------------------------------
# 3. Create / update the desktop shortcut
# ---------------------------------------------------------------------------
Write-Host "`n==> Creating desktop shortcut: $ShortcutPath ..." -ForegroundColor Cyan

# Remove shortcuts left by builds named "Antibody Panels", "Antibody Panel Management System", or "AntigramIQ".
foreach ($oldName in @('Antibody Panels.lnk', 'Antibody Panel Management System.lnk', 'AntigramIQ.lnk')) {
    $oldPath = Join-Path ([Environment]::GetFolderPath('Desktop')) $oldName
    if (Test-Path $oldPath) { Remove-Item $oldPath; Write-Host "    Removed old shortcut: $oldName" }
}

$WshShell  = New-Object -ComObject WScript.Shell
$shortcut  = $WshShell.CreateShortcut($ShortcutPath)
$shortcut.TargetPath       = $ExePath
$shortcut.WorkingDirectory = Split-Path $ExePath
$shortcut.IconLocation     = "$IcoPath,0"
$shortcut.Description      = 'HemoIQ'
$shortcut.Save()

Write-Host "    Shortcut created -> $ExePath" -ForegroundColor Green
Write-Host "`nDone! Double-click 'HemoIQ' on your desktop to launch the app.`n" -ForegroundColor Yellow
