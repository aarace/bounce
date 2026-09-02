<#
.SYNOPSIS
    Installs (or uninstalls) Bounce as the active screensaver for the
    current Windows user, without needing admin rights or copying
    anything into System32.

.DESCRIPTION
    The Screen Saver Settings dropdown only lists .scr files that live in
    System32 (or SysWOW64), which requires admin rights to copy into.
    Separately, though, Windows will actually *run* whatever screensaver
    is named in HKCU\Control Panel\Desktop\SCRNSAVE.EXE after the idle
    timeout - and that value can point at a file anywhere the user can
    already read, no admin rights or copying required. It just won't show
    up as a selectable option in the Settings UI; this script is the
    equivalent of picking it there.

.PARAMETER Path
    Full path to Bounce.scr (or Bounce.exe). If omitted, the script looks
    for a build under Bounce\bin\ next to this script (publish output
    preferred, since that's self-contained; falls back to Release, then
    Debug).

.PARAMETER TimeoutSeconds
    Idle seconds before the screensaver starts. Optional - if omitted, the
    user's existing timeout (or Windows' own default) is left alone.

.PARAMETER Uninstall
    Clears the registry values this script set, so Windows falls back to
    whatever screensaver (if any) was configured before.

.EXAMPLE
    .\Install-Screensaver.ps1
    Finds the built Bounce.scr automatically and installs it.

.EXAMPLE
    .\Install-Screensaver.ps1 -Path 'C:\source\bounce\publish\Bounce.scr' -TimeoutSeconds 300

.EXAMPLE
    .\Install-Screensaver.ps1 -Uninstall
#>
[CmdletBinding()]
param(
    [string]$Path,
    [int]$TimeoutSeconds,
    [switch]$Uninstall
)

$ErrorActionPreference = 'Stop'
$desktopKey = 'HKCU:\Control Panel\Desktop'

if ($Uninstall) {
    if (Test-Path $desktopKey) {
        Remove-ItemProperty -Path $desktopKey -Name 'SCRNSAVE.EXE' -ErrorAction SilentlyContinue
        Set-ItemProperty -Path $desktopKey -Name 'ScreenSaveActive' -Value '0' -Type String
    }
    Write-Host "Bounce uninstalled: SCRNSAVE.EXE cleared and ScreenSaveActive set to 0."
    return
}

if (-not $Path) {
    $scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
    $candidates = @(
        (Join-Path $scriptDir 'publish\Bounce.scr')
        (Join-Path $scriptDir 'Bounce\bin\Release\net10.0-windows\win-x64\publish\Bounce.scr')
        (Join-Path $scriptDir 'Bounce\bin\Release\net10.0-windows\Bounce.scr')
        (Join-Path $scriptDir 'Bounce\bin\Debug\net10.0-windows\Bounce.scr')
    )
    $Path = $candidates | Where-Object { Test-Path $_ } | Select-Object -First 1

    if (-not $Path) {
        throw "Couldn't find a built Bounce.scr in any of the usual output folders. Build the project first (or publish it - see README.md), or pass -Path explicitly."
    }
}

if (-not (Test-Path $Path)) {
    throw "No file at '$Path'."
}

$resolvedPath = (Resolve-Path $Path).Path

Set-ItemProperty -Path $desktopKey -Name 'SCRNSAVE.EXE' -Value $resolvedPath -Type String
Set-ItemProperty -Path $desktopKey -Name 'ScreenSaveActive' -Value '1' -Type String

if ($PSBoundParameters.ContainsKey('TimeoutSeconds')) {
    Set-ItemProperty -Path $desktopKey -Name 'ScreenSaveTimeOut' -Value "$TimeoutSeconds" -Type String
}

Write-Host "Installed as the active screensaver for this user: $resolvedPath"
Write-Host "It'll run automatically after your idle timeout, or test it right now with:"
Write-Host "  & '$resolvedPath' /s"
Write-Host "To undo: .\Install-Screensaver.ps1 -Uninstall"
