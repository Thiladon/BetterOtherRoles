# Prepares the files of a GitHub release of BOR-MiraAPI in release/<version>/:
#   - BetterOtherRolesMiraAPI.dll: the mod (the mark of a BOR-MiraAPI release for the updater and the installer);
#   - BOR-MiraAPI-plugins.zip: the mod and its dependencies (MiraAPI, Reactor, CorsacCosmetics), in the versions of
#     AmongUs.props, extracted in BepInEx/plugins;
#   - release.json: the BepInEx the release needs (version of BepInEx.Core.dll and download address).
# The installer (BOR-MiraAPI-Installer.exe) is copied too, for its own release (not attached to every release).
#
# Usage: powershell -NoProfile -ExecutionPolicy Bypass -File tools/Prepare-Release.ps1

$ErrorActionPreference = 'Stop'

# BepInEx of the game (change them together): 64 and 32 bits (the Steam game can be either).
$BepInExVersion = '6.0.0-be.735+5fef3570f212b2fb5fbe9c1d20487c13c2fa90cb'
$BepInExUrl = 'https://builds.bepinex.dev/projects/bepinex_be/735/BepInEx-Unity.IL2CPP-win-x64-6.0.0-be.735%2B5fef357.zip'
$BepInExUrlX86 = 'https://builds.bepinex.dev/projects/bepinex_be/735/BepInEx-Unity.IL2CPP-win-x86-6.0.0-be.735%2B5fef357.zip'

# Steam depots of the version of Among Us of the release (SteamDB, app 945360, Windows depots, manifest of that
# version): the installer downloads them with the Steam console (download_depot) when the game of the player is not
# that version. Change them with the version of AmongUs.GameLibs.Steam (AmongUs.props). One Windows depot for
# 2026.9.29 (64 bits); with several, x64 tells them apart.
$SteamDepots = @(
    [ordered]@{ depot = '945361'; manifest = '990035194164819049'; x64 = $true }
)

$root = Split-Path -Parent $PSScriptRoot
$props = [xml](Get-Content -LiteralPath (Join-Path $root 'Directory.Build.props') -Raw)
$prefix = ($props.Project.PropertyGroup | Where-Object { $_.VersionPrefix }).VersionPrefix
$suffix = ($props.Project.PropertyGroup | Where-Object { $_.VersionPrefix }).VersionSuffix
$version = if ($suffix) { "$prefix-$suffix" } else { $prefix }

Write-Host "BOR-MiraAPI $version"
dotnet build (Join-Path $root 'BetterOtherRolesMiraAPI.sln') -c Release --nologo -v quiet
if ($LASTEXITCODE -ne 0) { throw 'Build failed.' }

# Versions of the dependencies, from AmongUs.props.
$packages = @{}
([xml](Get-Content -LiteralPath (Join-Path $root 'AmongUs.props') -Raw)).Project.ItemGroup.PackageReference |
    Where-Object { $_.Include } | ForEach-Object { $packages[$_.Include] = $_.Version }
$nuget = if ($env:NUGET_PACKAGES) { $env:NUGET_PACKAGES } else { Join-Path $env:USERPROFILE '.nuget\packages' }

function Get-PackageDll([string] $package, [string] $dll) {
    $folder = Join-Path $nuget ("{0}\{1}\lib" -f $package.ToLowerInvariant(), $packages[$package])
    $file = Get-ChildItem -LiteralPath $folder -Recurse -Filter $dll | Select-Object -First 1
    if (-not $file) { throw "$dll not found in $folder." }
    return $file.FullName
}

$out = Join-Path $root "release\$version"
if (Test-Path -LiteralPath $out) { Remove-Item -LiteralPath $out -Recurse -Force }
$staging = Join-Path $out 'plugins'
New-Item -ItemType Directory -Force -Path $staging | Out-Null

$mod = Join-Path $root 'BetterOtherRoles\bin\Release\BetterOtherRolesMiraAPI.dll'
Copy-Item -LiteralPath $mod -Destination $out
Copy-Item -LiteralPath $mod -Destination $staging
Copy-Item -LiteralPath (Get-PackageDll 'AllOfUs.MiraAPI' 'MiraAPI.dll') -Destination $staging
Copy-Item -LiteralPath (Get-PackageDll 'Reactor' 'Reactor.dll') -Destination $staging
Copy-Item -LiteralPath (Get-PackageDll 'CorsacCosmetics' 'CorsacCosmetics.dll') -Destination $staging

Add-Type -AssemblyName System.IO.Compression.FileSystem
[System.IO.Compression.ZipFile]::CreateFromDirectory($staging, (Join-Path $out 'BOR-MiraAPI-plugins.zip'))
Remove-Item -LiteralPath $staging -Recurse -Force

# Version of Among Us: the one of the game libraries the mod is built with.
$info = [ordered]@{
    bepinexVersion = $BepInExVersion
    bepinexUrl = $BepInExUrl
    bepinexUrlX86 = $BepInExUrlX86
    gameVersion = $packages['AmongUs.GameLibs.Steam']
    steamDepots = @($SteamDepots)
}
# UTF-8 without BOM (Set-Content -Encoding UTF8 writes one in PowerShell 5).
[System.IO.File]::WriteAllText((Join-Path $out 'release.json'), ($info | ConvertTo-Json), (New-Object System.Text.UTF8Encoding $false))

Copy-Item -LiteralPath (Join-Path $root 'Installer\bin\Release\net48\BOR-MiraAPI-Installer.exe') -Destination $out

Get-ChildItem -LiteralPath $out | Format-Table Name, Length -AutoSize
