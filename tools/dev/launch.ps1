# Starts STRAFTAT through Steam with doorstop pointed at the VHOLTAT dev profile.
# The STRAFTAT folder's own doorstop/BepInEx are left as they are; the command-line args override them.
param(
    [string]$Profile = "",
    [string]$Steam = "C:\Program Files (x86)\Steam\steam.exe",
    [string]$Extra = ""
)
$propsUser = Join-Path $PSScriptRoot "..\..\Directory.Build.props.user"
function Get-DevProfile {
    if (-not (Test-Path $propsUser)) { throw "Copy Directory.Build.props.user.example to Directory.Build.props.user and set DevProfile." }
    $p = ([xml](Get-Content $propsUser)).Project.PropertyGroup.DevProfile | Where-Object { $_ } | Select-Object -First 1
    if (-not $p) { throw "DevProfile is not set in Directory.Build.props.user" }
    return $p
}
if (-not $Profile) { $Profile = Get-DevProfile }
$preloader = Join-Path $Profile "BepInEx\core\BepInEx.Preloader.dll"
if (-not (Test-Path $preloader)) { throw "No BepInEx in $Profile" }
if (Get-Process STRAFTAT -ErrorAction SilentlyContinue) { throw "STRAFTAT is already running" }
$launchArgs = @("-applaunch", "2386720", "--doorstop-enabled", "true", "--doorstop-target-assembly", $preloader)
if ($Extra) { $launchArgs += $Extra.Split(" ") }
& $Steam @launchArgs
Write-Host "Launched. Log: $Profile\BepInEx\LogOutput.log"
