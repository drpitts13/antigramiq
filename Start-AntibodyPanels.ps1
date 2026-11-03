#Requires -Version 5.1
<#
.SYNOPSIS
    Launches the most recently built copy of the app.
.DESCRIPTION
    The desktop shortcut made by CreateShortcut.ps1 runs this script instead of a
    fixed .exe path, so it always opens the current build. It looks in the main
    checkout and every worktree under .claude\worktrees, across Debug and Release
    and any target framework, and starts whichever app .exe was written last.
    An app .exe is <Project>\bin\<Config>\<TFM>\<Project>.exe, so a renamed
    project is still found; test projects are skipped.
#>
param(
    [Parameter(Mandatory)]
    [string]$RepoRoot
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$searchRoots = @($RepoRoot)
$worktrees = Join-Path $RepoRoot '.claude\worktrees'
if (Test-Path $worktrees) {
    $searchRoots += Get-ChildItem $worktrees -Directory | ForEach-Object FullName
}

$candidates = foreach ($root in $searchRoots) {
    foreach ($proj in Get-ChildItem $root -Directory -ErrorAction SilentlyContinue) {
        if ($proj.Name -like '*.Tests') { continue }
        $bin = Join-Path $proj.FullName 'bin'
        if (-not (Test-Path $bin)) { continue }
        # bin\<Config>\<TFM>\<Project>.exe
        Get-ChildItem $bin -Directory | ForEach-Object {
            Get-ChildItem $_.FullName -Directory | ForEach-Object {
                $exe = Join-Path $_.FullName "$($proj.Name).exe"
                if (Test-Path $exe) { Get-Item $exe }
            }
        }
    }
}

$latest = $candidates | Sort-Object LastWriteTime -Descending | Select-Object -First 1

if (-not $latest) {
    Add-Type -AssemblyName PresentationFramework
    [System.Windows.MessageBox]::Show(
        "No built app was found under:`n$RepoRoot`n`nRun CreateShortcut.ps1 or dotnet build first.",
        'Antibody Panels', 'OK', 'Error') | Out-Null
    exit 1
}

Start-Process -FilePath $latest.FullName -WorkingDirectory $latest.DirectoryName
