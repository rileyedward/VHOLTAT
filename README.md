# VHOLTAT

VHOLUME-style parkour movement for STRAFTAT.

> **Status:** Early development — 0.1.0 is the first public release. It has been played on one PC and tested with two copies of the game; a real match with friends over Steam hasn't been played yet.

## Overview

### What is VHOLTAT?

VHOLTAT is a STRAFTAT mod that swaps your player's movement for VHOLUME's parkour: carried momentum, slides, wall runs, wall jumps, wall climbs, vaults, mantles, coyote time and jump buffering. Everything else stays STRAFTAT: its game rules, weapons, damage and knockback are exactly as they are, so you play normal STRAFTAT matches with friends that just move like VHOLUME.

The movement is clean-room: it was rebuilt from written behaviour specs ([docs/MOVEMENT_SPEC.md](docs/MOVEMENT_SPEC.md), [docs/MOMENTUM_SPEC.md](docs/MOMENTUM_SPEC.md), [docs/MOVEMENT_FORMULAS.md](docs/MOVEMENT_FORMULAS.md)) and runs in a movement core that doesn't depend on STRAFTAT, so it can be tuned and tested on its own.

VHOLTAT is not produced, approved or endorsed by the makers of VHOLUME or STRAFTAT. It ships no code or assets from either game.

### Key Features

- **VHOLUME Movement**: Run 13 m/s, jump 8 m/s, and VHOLUME's gravity, acceleration and braking.
- **Bunny Hops and Momentum**: Jump within 0.15 s of landing to keep your speed and gain a fifth of the speed above run speed. Extra speed is carried and fades slowly (slower still on walls).
- **Slides, Wall Runs and Climbs**: Slide out of a run, chain wall runs wall to wall, wall jump toward where you look, climb walls and turn a climb into a wall run.
- **Vaults and Mantles**: Jump at a ledge or cover to vault onto it in the air, as in VHOLUME, or climb up to a ledge and mantle onto it.
- **VHOLUME Camera**: A 100 degree horizontal FOV (adjustable), small FOV kicks when running, sliding and wall running, and a 6 degree wall-run roll. Optional VHOLUME-style speedometer and speed effect.
- **Multiplayer**: The host's movement settings apply to everyone in the lobby, a version check falls back to vanilla movement on a mismatch, and other players' slides and wall runs are animated and heard.
- **Update Guard**: If a STRAFTAT update changes something VHOLTAT hooks, it stays off with a short notice instead of breaking.
- **One-Click Launcher**: Finds STRAFTAT, installs BepInEx and VHOLTAT into its own folder and leaves plain Steam launches vanilla.

## Getting Started

### Prerequisites

1. **STRAFTAT** on Steam (Windows, or Steam Deck / Linux through Proton). VHOLTAT was made for STRAFTAT 1.4.9f (build 0.0.3302); a STRAFTAT update may switch it off until it's updated.
2. **BepInEx 5** (x64, 5.4.23.5). The launcher installs it for you.

### Installation

**With the launcher (easiest):**

1. Download [`VHOLTAT-Launcher-0.1.0.exe`](https://github.com/rileyedward/VHOLTAT/releases/latest) from the [Releases](../../releases) page, or `VHOLTAT-0.1.0-windows.zip`, which holds the launcher, the manual zip and a short how-to. Close STRAFTAT and run the launcher. Windows may warn that it's from an unknown publisher, because the exe isn't signed: choose More info, then Run anyway.
2. Press **Install**. The launcher finds STRAFTAT through Steam (or press **Browse…** and pick the STRAFTAT folder), downloads BepInEx and installs VHOLTAT into `%APPDATA%\VHOLTAT`.
3. Press **Play**. Start STRAFTAT with the launcher's **Play** button whenever you want VHOLTAT.

STRAFTAT's folder only gets two small loader files that stay switched off, so starting STRAFTAT from Steam (or **Play vanilla** in the launcher) is still plain STRAFTAT. The launcher checks GitHub for newer releases, and **Uninstall** puts the game folder back exactly as it was (it asks whether to keep your settings). See [docs/launcher.md](docs/launcher.md) for how it works.

**Manually** (if you already use BepInEx 5):

1. Download `VHOLTAT-0.1.0-manual.zip` from the [Releases](../../releases) page.
2. Unzip it into your STRAFTAT folder. It contains `BepInEx\plugins\VHOLTAT\VHOLTAT.dll`.

**Steam Deck / Linux (Proton):** the launcher is Windows-only, so use the manual zip. BepInEx needs its loader DLL enabled under Proton; set STRAFTAT's launch options in Steam to:

```text
WINEDLLOVERRIDES="winhttp=n,b" %command%
```

### Playing with Friends

1. **Everyone installs the same VHOLTAT version.** STRAFTAT itself turns away players whose mods differ, so a lobby only works when everyone has the same release.
2. **Everyone starts STRAFTAT with VHOLTAT** (the launcher's **Play**), then the host makes a lobby and invites friends through Steam as usual.
3. **The host's settings apply.** The host's movement settings are sent to everyone in the lobby. If your version doesn't match the host's, a banner tells you which version to install and you get vanilla movement until you update.

Modded players get their own matchmaking pool, so you never land in vanilla lobbies. Other VHOLTAT players are drawn smoothly at speed, and their slides and wall runs are animated with their sounds. On poor connections, `[Multiplayer] RemoteInterpolationTicks` smooths fast-moving players further.

### Moves

- Hold crouch while running to slide; jump out of a slide to keep its speed.
- Jump within 0.15 s of landing to keep your speed and gain a fifth of the speed above run speed (bunny hop).
- Jump next to a wall while holding forward to wall run; jump again to wall jump. Wall to wall builds speed.
- Jump at a ledge or cover while holding forward to vault onto it. Vaults happen in the air, as in VHOLUME; `[Mantle] VanillaVault = true` adds STRAFTAT's ground vault back.
- Hold forward into a wall in the air to climb it (strafe to turn it into a wall run); reach a ledge to mantle onto it.
- Extra speed from hops is carried and fades slowly (slower still on walls).

### Controls

| Key | Action |
|---|---|
| `F6` | Switch between VHOLTAT and vanilla movement |
| `F7` | Overlay: state, speed, boost and walls (off by default) |
| `Esc` | Pause menu, with the **VHOLTAT settings** window |

Everything else is STRAFTAT's own controls.

Developer keys are off unless `[Debug] DevKeys = true`: `F8` cycles the movement model (VHOLUME / 0.3 / 0.3 Playtest2), `F9` compares the 0.3 jump with VHOLUME's exact jump, `F11` records movement to `BepInEx/recordings/`, and `Shift+F12` opens the combat tuning panel (speed limit, boost and wall speeds, how other players are drawn). `F10` marks a problem spot to `BepInEx/vholtat_spots.txt` when `[Debug] MarkSpots = true`.

### Configuration

The camera and the common options are in the in-game settings window (`Esc`, then **VHOLTAT settings**). The default camera is VHOLUME's 100 degree horizontal FOV; STRAFTAT's own 90 degree FOV is vertical (about 121 degrees horizontal), which makes the same speeds feel much faster. The older, wider camera is still available as "STRAFTAT wide".

Every number is in `BepInEx/config/dev.riley.vholtat.cfg`; saved edits apply while the game runs. With the launcher, its **Open folder** button takes you to the install.

### Game Updates

VHOLTAT checks at startup that everything it hooks in STRAFTAT still exists. If a STRAFTAT update changed something, VHOLTAT stays off (vanilla movement) and shows a short notice instead of breaking; the log names what changed and the STRAFTAT build VHOLTAT was made for.

## Development

### Prerequisites

1. **Windows** and an owned copy of **STRAFTAT**. The plugin compiles against the game's own assemblies, which are referenced from your install and never committed.
2. **.NET SDK** (10+) builds the plugin (netstandard2.1), the launcher (net48) and the core tests (net10.0). Check it by running:

    ```bash
    dotnet --version
    ```

3. **BepInEx 5.4.23.5** (win x64) for a dev profile.

### Installation

1. Copy `Directory.Build.props.user.example` to `Directory.Build.props.user` and set `GameDir` (your STRAFTAT folder) and `DevProfile` (any empty folder you want to use as a BepInEx dev profile):

    ```bash
    copy Directory.Build.props.user.example Directory.Build.props.user
    ```

2. Put BepInEx 5.4.23.5 (win x64) into the dev profile so it has `BepInEx\core\BepInEx.Preloader.dll`.
3. Build in Debug. This copies the plugin into `<DevProfile>/BepInEx/plugins/VHOLTAT/`. Close STRAFTAT first:

    ```bash
    dotnet build
    ```

4. Start STRAFTAT through Steam on the dev profile:

    ```bash
    powershell -ExecutionPolicy Bypass -File tools/dev/launch.ps1
    ```

### Project Structure

| Folder | What it is |
|---|---|
| `src/MovementCore/` | The movement core: the VHOLUME model, the earlier 0.3 model and their settings. Doesn't depend on STRAFTAT |
| `src/VHOLTAT/` | The BepInEx plugin: hooks into STRAFTAT's player, multiplayer sync, settings window, overlay, compatibility guard and dev tools |
| `tests/MovementCore.Tests/` | Tests for the movement core, including tests that pin the VHOLUME model to the spec numbers |
| `launcher/` | The Windows launcher (`VHOLTAT-Launcher-<version>.exe`): finds STRAFTAT, installs BepInEx and VHOLTAT into `%APPDATA%\VHOLTAT`, plays, updates and uninstalls. The plugin is embedded in the exe |
| `package/` | The Thunderstore manifest, icon and README, and the how-to that goes in the Windows download |
| `tools/dev/` | Launch, self-test, two-copy multiplayer and map sweep scripts |
| `tools/release/` | The release build script |
| `docs/` | [Movement spec](docs/MOVEMENT_SPEC.md), [momentum spec](docs/MOMENTUM_SPEC.md), [movement formulas](docs/MOVEMENT_FORMULAS.md), [STRAFTAT hooks](docs/STRAFTAT_HOOKS.md), [recon](docs/RECON.md), [the launcher](docs/launcher.md) and the [devlog](docs/devlog.md) |

## Releasing

Releases are built locally, because the plugin compiles against STRAFTAT's assemblies:

```bash
powershell -ExecutionPolicy Bypass -File tools/release/build-release.ps1
```

This runs the core tests, builds the plugin and the launcher in Release, and writes the release files to `dist/` (local only, nothing is published): `VHOLTAT-<version>-windows.zip` (the main download), `VHOLTAT-<version>-manual.zip`, `VHOLTAT-Launcher-<version>.exe` and `VHOLTAT-<version>-thunderstore.zip` (a Thunderstore-format package that depends on BepInExPack 5.4.2100). See [CHANGELOG.md](CHANGELOG.md) for what's in each release.

## Testing

Run the movement core tests:

```bash
dotnet test tests/MovementCore.Tests
```

In-game self-tests drive the game and print results:

```bash
bash tools/dev/selftest.sh basic
bash tools/dev/map_sweep.sh
```

`selftest.sh` also takes `parkour` and `sweep`, and `map_sweep.sh` sweeps maps for stuck spots. To test multiplayer on one PC, build a second copy of the game with `tools/dev/make_client_instance.ps1`, then run `tools/dev/lan_test.sh [secs] [parkour|match]`. Optionally add simulated lag with `LAG="--vholtat-lag 60 --vholtat-loss 0.02"`.

## Contributing

Bug reports, ideas and pull requests are welcome through [Issues](../../issues). This is a personal project I work on in my spare time, so replies can be slow and not every suggestion will make it in. For anything bigger than a small fix, open an issue first so we can talk it through. Bug reports help most with your VHOLTAT version, your STRAFTAT version and `BepInEx\LogOutput.log`.

## Data and Licensing

No license has been chosen yet, so the code is all rights reserved for now.

VHOLTAT contains no VHOLUME or STRAFTAT code or assets. The movement was rebuilt from written behaviour specs, not from VHOLUME's code. STRAFTAT's internals in [docs/STRAFTAT_HOOKS.md](docs/STRAFTAT_HOOKS.md) were mapped by reading the game's code locally; no decompiled code is included. STRAFTAT's DLLs are referenced from your own install and never committed or redistributed.

Built with [BepInEx](https://github.com/BepInEx/BepInEx) (LGPL-2.1) and [Harmony](https://github.com/pardeike/Harmony) (MIT), which are installed separately and not included in the release.

STRAFTAT is developed by Lemaitre Bros.
