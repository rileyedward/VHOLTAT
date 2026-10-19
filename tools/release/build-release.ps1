# Builds VHOLTAT in Release, runs the core tests and writes two zips into dist\ (local only, nothing is published):
#   VHOLTAT-<ver>-thunderstore.zip  manifest.json, icon.png, README.md, plugins\VHOLTAT.dll
#   VHOLTAT-<ver>-manual.zip        BepInEx\plugins\VHOLTAT\VHOLTAT.dll (unzip into the STRAFTAT folder)
$ErrorActionPreference = "Stop"
$root = Resolve-Path "$PSScriptRoot\..\.."
Set-Location $root

dotnet test tests\MovementCore.Tests -c Release
if ($LASTEXITCODE -ne 0) { throw "tests failed" }
dotnet build src\VHOLTAT\VHOLTAT.csproj -c Release
if ($LASTEXITCODE -ne 0) { throw "build failed" }

$manifest = Get-Content package\manifest.json -Raw | ConvertFrom-Json
$ver = $manifest.version_number
$projVer = ([xml](Get-Content src\VHOLTAT\VHOLTAT.csproj)).Project.PropertyGroup.Version | Where-Object { $_ } | Select-Object -First 1
if ($projVer -ne $ver) { throw "Version mismatch: csproj $projVer vs manifest.json $ver" }
$dll = "src\VHOLTAT\bin\Release\netstandard2.1\VHOLTAT.dll"
$dist = Join-Path $root "dist"
$stage = Join-Path $dist "stage"
Remove-Item $stage -Recurse -Force -ErrorAction SilentlyContinue

$ts = Join-Path $stage "thunderstore"
New-Item -ItemType Directory -Force "$ts\plugins" | Out-Null
Copy-Item package\manifest.json, package\icon.png, package\README.md $ts
Copy-Item $dll "$ts\plugins"
$tsZip = Join-Path $dist "VHOLTAT-$ver-thunderstore.zip"
Remove-Item $tsZip -ErrorAction SilentlyContinue
Compress-Archive "$ts\*" $tsZip

$man = Join-Path $stage "manual"
New-Item -ItemType Directory -Force "$man\BepInEx\plugins\VHOLTAT" | Out-Null
Copy-Item $dll "$man\BepInEx\plugins\VHOLTAT"
$manZip = Join-Path $dist "VHOLTAT-$ver-manual.zip"
Remove-Item $manZip -ErrorAction SilentlyContinue
Compress-Archive "$man\*" $manZip

Remove-Item $stage -Recurse -Force
Write-Host "Wrote $tsZip and $manZip"
