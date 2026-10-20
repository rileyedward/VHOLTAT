# Dev only: a second STRAFTAT copy on this PC for two-player tests without a second person (same idea as Straft4Dead's).
#   powershell -ExecutionPolicy Bypass -File tools\dev\make_client_instance.ps1
# STRAFTAT's single-instance mutex is named after the exe path, so an exe in another folder can run alongside.
# Game data and Mono are directory junctions to the real install; BepInEx core/plugins are junctions to the VHOLTAT
# dev profile (so Debug deploys reach both copies). Config and logs are the copy's own.
param(
    [string] $Straftat = "C:\Program Files (x86)\Steam\steamapps\common\STRAFTAT",
    [string] $Profile = "",
    [string] $Out = ""
)
$ErrorActionPreference = "Stop"
$propsUser = Join-Path $PSScriptRoot "..\..\Directory.Build.props.user"
function Get-DevProfile {
    if (-not (Test-Path $propsUser)) { throw "Copy Directory.Build.props.user.example to Directory.Build.props.user and set DevProfile." }
    $p = ([xml](Get-Content $propsUser)).Project.PropertyGroup.DevProfile | Where-Object { $_ } | Select-Object -First 1
    if (-not $p) { throw "DevProfile is not set in Directory.Build.props.user" }
    return $p
}
if (-not $Profile) { $Profile = Get-DevProfile }
if (-not $Out) { $Out = Join-Path $Profile "client-instance" }
New-Item -ItemType Directory -Force $Out, "$Out\BepInEx", "$Out\BepInEx\config", "$Out\BepInEx\cache" | Out-Null
foreach ($f in "STRAFTAT.exe", "UnityPlayer.dll", "winhttp.dll", ".doorstop_version") {
    if (Test-Path "$Straftat\$f") { Copy-Item "$Straftat\$f" "$Out\$f" -Force }
}
Set-Content "$Out\doorstop_config.ini" "[General]`r`nenabled = true`r`ntarget_assembly = BepInEx\core\BepInEx.Preloader.dll`r`n"
Set-Content "$Out\steam_appid.txt" "2386720" -NoNewline   # lets the exe start outside Steam's launcher
function Junction($link, $target) {
    if (-not (Test-Path $link)) { cmd /c mklink /J "$link" "$target" | Out-Null }
}
Junction "$Out\STRAFTAT_Data" "$Straftat\STRAFTAT_Data"
Junction "$Out\MonoBleedingEdge" "$Straftat\MonoBleedingEdge"
Junction "$Out\Straftat_SupporterEdition" "$Straftat\Straftat_SupporterEdition"
Junction "$Out\BepInEx\core" "$Profile\BepInEx\core"
Junction "$Out\BepInEx\plugins" "$Profile\BepInEx\plugins"
Copy-Item "$Profile\BepInEx\config\*.cfg" "$Out\BepInEx\config\" -Force
Write-Host "Client instance ready in $Out"
