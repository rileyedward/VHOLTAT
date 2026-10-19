# The Launcher

`VHOLTAT-Launcher.exe` installs VHOLTAT and starts STRAFTAT with it in one window. It's a .NET Framework 4.8 WinForms app (built into Windows 10 and 11, nothing to install first) in `launcher/`, adapted from Straft 4 Dead's launcher. The plugin DLL is embedded in the exe, so the exe is the whole download.

## The window

- **STRAFTAT** shows the folder it found (+ **Browse…** to pick one by hand).
- A status line: not installed / installed version / update available / download progress.
- **Install** → **Play** (or **Get STRAFTAT** if it isn't found, **Update** if a newer plugin is available).
- **Uninstall** asks whether to keep your VHOLTAT settings. **Open folder** opens `%APPDATA%\VHOLTAT`.
- In game: F6 switches VHOLTAT / vanilla movement, F7 toggles the speed overlay.

## What Install does

| Step | What happens |
|------|--------------|
| Find STRAFTAT | Reads Steam's install path from the registry and its `libraryfolders.vdf`, then looks for STRAFTAT (app 2386720) via its app manifest. A folder counts as STRAFTAT if it has `STRAFTAT.exe` |
| BepInEx | Downloads `BepInEx_win_x64_5.4.23.5.zip` from BepInEx's GitHub release, checks its pinned SHA-256, caches it and unpacks it into the profile |
| VHOLTAT | Writes the embedded `VHOLTAT.dll` to `profile\BepInEx\plugins\VHOLTAT\`. The installed version is the DLL's assembly version |
| Game folder | Puts `winhttp.dll` and a switched-off `doorstop_config.ini` into the STRAFTAT folder (see below) |

## Where it puts things

Everything lives in `%APPDATA%\VHOLTAT`:

```text
profile\      BepInEx, its doorstop files, the plugin and its config (BepInEx\config\dev.riley.vholtat.cfg)
cache\        the pinned BepInEx download
game-backup\  anything of STRAFTAT's that the launcher replaced
state.json    found path, installed version, the files placed in the game folder
launcher.log  everything the launcher did
```

The game's BepInEx log is `profile\BepInEx\LogOutput.log`.

## How Play works

STRAFTAT's folder only gets BepInEx's `winhttp.dll` and a `doorstop_config.ini` that's switched **off**, so starting the game from Steam stays vanilla. An existing file the launcher replaces is saved to `game-backup\` first. Play starts STRAFTAT through Steam with doorstop arguments that turn it on for that launch and point it at the profile's BepInEx:

```text
steam.exe -applaunch 2386720 --doorstop-enabled true --doorstop-target-assembly "%APPDATA%\VHOLTAT\profile\BepInEx\core\BepInEx.Preloader.dll"
```

If STRAFTAT's folder already has its own BepInEx (`BepInEx\core`), the launcher leaves its doorstop files alone and relies on the arguments. Uninstall removes the two files (only if they're still the launcher's, restoring anything it backed up), then the profile and cache, optionally keeping the settings file.

## Updates

`LauncherConfig.GitHubRepo` is empty for now, so there is no update check: the launcher installs the plugin it carries, and a newer launcher exe with a newer plugin shows **Update**. Once the repo exists, set `GitHubRepo` to `owner/repo`; the launcher then checks the latest release on start and installs its `VHOLTAT-<ver>-manual.zip` when it's newer than the embedded plugin (with a `.sha256` check if the release has one), and mentions a newer `VHOLTAT-Launcher*.exe`.

## Command line

Every step also runs without the window, which is how it's tested:

```bash
VHOLTAT-Launcher.exe status
VHOLTAT-Launcher.exe install   [--zip VHOLTAT-0.1.0-manual.zip]
VHOLTAT-Launcher.exe play      [--dry-run]
VHOLTAT-Launcher.exe uninstall [--keep-config]
```

`--home <folder>` uses another folder instead of `%APPDATA%\VHOLTAT`, and `--straftat <folder>` overrides the STRAFTAT folder found through Steam.

## Building it

```bash
dotnet build src\VHOLTAT\VHOLTAT.csproj -c Release
dotnet build launcher\VHOLTAT.Launcher.csproj -c Release
```

The launcher build embeds `src\VHOLTAT\bin\Release\netstandard2.1\VHOLTAT.dll` and stops with an error if it's missing. `tools\release\build-release.ps1` does both and writes `dist\VHOLTAT-Launcher-<ver>.exe` next to the two zips. The launcher isn't in `VHOLTAT.slnx`, so a plain Debug `dotnet build` doesn't need the Release plugin.
