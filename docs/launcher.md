# The Launcher

`VHOLTAT-Launcher.exe` installs VHOLTAT and starts STRAFTAT with it in one window. It's a .NET Framework 4.8 WinForms app (built into Windows 10 and 11, nothing to install first) in `launcher/`, adapted from Straft 4 Dead's launcher. The plugin DLL is embedded in the exe, so the exe is the whole download.

## The window

- **STRAFTAT** shows the folder it found (+ **Browse…** to pick one by hand).
- A status line: not installed / installed version / update available / download progress, or the last problem and how to fix it (in red).
- The main button: **Install** → **Play**. **Get STRAFTAT** (opens its Steam store page) if STRAFTAT isn't found, **Update to X** if the installed VHOLTAT is older than the one the launcher has.
- **Play vanilla** starts plain STRAFTAT through Steam (see below). Its tooltip and the line under the buttons say that starting STRAFTAT from Steam as usual is vanilla too.
- **Uninstall** asks whether to keep your VHOLTAT settings. **Open folder** opens `%APPDATA%\VHOLTAT`.
- In game: F6 switches VHOLTAT / vanilla movement, F7 toggles the speed overlay.

## What Install does

| Step | What happens |
|------|--------------|
| Find STRAFTAT | Reads Steam's install path from the registry and its `libraryfolders.vdf`, then looks for STRAFTAT (app 2386720) via its app manifest. A folder counts as STRAFTAT if it has `STRAFTAT.exe` |
| BepInEx | Downloads `BepInEx_win_x64_5.4.23.5.zip` from BepInEx's GitHub release, checks its pinned SHA-256, caches it and unpacks it into the profile |
| VHOLTAT | Writes the embedded `VHOLTAT.dll` to `profile\BepInEx\plugins\VHOLTAT\`. The installed version is the DLL's assembly version |
| Game folder | Puts `winhttp.dll` and a switched-off `doorstop_config.ini` into the STRAFTAT folder (see below) |

### Updating in place

The installed version (`ModVersion` in `state.json`) is compared with the plugin the launcher would install:

| Installed vs launcher | Main button | `install` does |
|---|---|---|
| not installed | **Install** | installs BepInEx and VHOLTAT, reports `installed 0.3.0` |
| older (e.g. 0.2.0) | **Update to 0.3.0** | replaces only `plugins\VHOLTAT\`, keeps BepInEx and `BepInEx\config\dev.riley.vholtat.cfg`, re-checks the two game files; reports `updated 0.2.0 -> 0.3.0` |
| same | **Play** | reinstalls the plugin (a repair), reports `reinstalled 0.3.0` |
| newer (e.g. 0.2.0) | **Play** (status line notes it) | keeps the newer one, re-checks the game files, reports `kept 0.2.0` |

BepInEx is only unpacked again if one of its files the launcher needs (`BepInEx\core\BepInEx.Preloader.dll`, `winhttp.dll`) is missing. `install --zip` installs exactly that zip's plugin, older or not.

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

If STRAFTAT's folder already has its own BepInEx (`BepInEx\core`), the launcher leaves its doorstop files alone and relies on the arguments.

**Play vanilla** runs `steam.exe -applaunch 2386720` with no doorstop arguments, so the game folder's doorstop stays off and it's plain STRAFTAT, exactly like starting it from the Steam library. It works whether or not VHOLTAT is installed. If the game folder has its own BepInEx, or a `doorstop_config.ini` with `enabled = true`, it says so in the log, since those would load mods anyway.

Before Play / Play vanilla the launcher checks that Steam is found and that its `steam.exe` is running. If Steam isn't running, the window offers to start Steam (just the client, not the game); press Play again once you're signed in. The command line stops with the same message.

## Problems and fixes

Each of these shows a one-line problem plus fix in the status line, the log (`Error:` / `  Fix:` lines, also in `launcher.log`) and on the command line (exit code 1):

| Problem | Suggested fix |
|---|---|
| Steam not found (registry, or `--steam`) | Install Steam and sign in; if it's installed, start it once so Windows knows where it is |
| Steam not running (Play) | Window: offers to start Steam. CLI: start Steam and sign in, then try again |
| STRAFTAT not found | Browse to its folder or press Get STRAFTAT (CLI: `--straftat <folder>`) |
| STRAFTAT running (Install, Update, Play, Play vanilla, Uninstall, Browse) | Window: asks you to close it, with Retry. CLI: close the game and try again. Only a `STRAFTAT.exe` running from the folder the launcher works on counts |
| BepInEx download failed | Check the internet connection / firewall / proxy and press Install again, or download the zip yourself into `cache\` (it's checked against the pinned SHA-256 either way) |
| BepInEx checksum mismatch | Press Install again; a proxy or antivirus can damage downloads; or place the zip in `cache\` by hand |
| Can't write `winhttp.dll` / `doorstop_config.ini` into the game folder | Access denied: run the launcher as administrator once, or clear a read-only flag. Other I/O errors: close STRAFTAT and anything using its folder. Uninstall gets the same messages | Uninstall removes the two files (only if they're still the launcher's, restoring anything it backed up), then the profile and cache, optionally keeping the settings file.

## Updates

`LauncherConfig.GitHubRepo` is empty for now, so there is no update check and no network access besides the BepInEx download: the launcher installs the plugin it carries, and a newer launcher exe with a newer plugin shows **Update to X**. Once the repo exists, set `GitHubRepo` to `owner/repo`; the launcher then checks the latest release on start and installs its `VHOLTAT-<ver>-manual.zip` when it's newer than the embedded plugin (with a `.sha256` check if the release has one), and mentions a newer `VHOLTAT-Launcher*.exe`.

## Command line

Every step also runs without the window, which is how it's tested:

```bash
VHOLTAT-Launcher.exe status
VHOLTAT-Launcher.exe install      [--zip VHOLTAT-0.1.0-manual.zip]
VHOLTAT-Launcher.exe play         [--dry-run]
VHOLTAT-Launcher.exe play-vanilla [--dry-run]
VHOLTAT-Launcher.exe uninstall    [--keep-config]
```

`--home <folder>` uses another folder instead of `%APPDATA%\VHOLTAT`, `--straftat <folder>` overrides the STRAFTAT folder found through Steam, and `--steam <folder>` overrides Steam's folder from the registry (a folder without `steam.exe` counts as "Steam not found"). `--dry-run` makes every check Play would (Steam found and running, STRAFTAT found and closed) and prepares the game folder, then prints the Steam arguments instead of starting anything. `status` also says whether Steam is running and whether an update (or a newer installed version) applies.

Testing without touching the real game: point `--straftat` at a scratch folder holding any file named `STRAFTAT.exe`, and `--home` at a scratch folder. Always pass `--straftat` (or a fake `--steam`) when testing `install`/`uninstall`, or they act on the real STRAFTAT folder.

## Building it

```bash
dotnet build src\VHOLTAT\VHOLTAT.csproj -c Release
dotnet build launcher\VHOLTAT.Launcher.csproj -c Release
```

The launcher build embeds `src\VHOLTAT\bin\Release\netstandard2.1\VHOLTAT.dll` and stops with an error if it's missing. `tools\release\build-release.ps1` does both and writes `dist\VHOLTAT-Launcher-<ver>.exe` next to the two zips. The launcher isn't in `VHOLTAT.slnx`, so a plain Debug `dotnet build` doesn't need the Release plugin.
