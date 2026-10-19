# Starts STRAFTAT through Steam with doorstop pointed at the VHOLTAT dev profile.
# The STRAFTAT folder's own doorstop/BepInEx are left as they are; the command-line args override them.
param(
    [string]$Profile = "C:\Users\Riley\source\_vholtat-dev",
    [string]$Steam = "C:\Program Files (x86)\Steam\steam.exe"
)
$preloader = Join-Path $Profile "BepInEx\core\BepInEx.Preloader.dll"
if (-not (Test-Path $preloader)) { throw "No BepInEx in $Profile" }
if (Get-Process STRAFTAT -ErrorAction SilentlyContinue) { throw "STRAFTAT is already running" }
& $Steam -applaunch 2386720 --doorstop-enabled true --doorstop-target-assembly "$preloader"
Write-Host "Launched. Log: $Profile\BepInEx\LogOutput.log"
