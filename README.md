# VHOLTAT

VHOLUME-style parkour movement for STRAFTAT. Carried momentum, slides, wall runs, wall jumps, wall climbs, mantles,
coyote time and jump buffering replace STRAFTAT's movement for your own player. STRAFTAT's game rules, weapons,
damage and knockback stay exactly as they are.

BepInEx 5 plugin for STRAFTAT (Unity 2021.3, FishNet). Clean-room: the movement was rebuilt from a written behaviour
spec (`docs/MOVEMENT_SPEC.md`, `docs/MOMENTUM_SPEC.md`); no VHOLUME code or assets are used.

## Install

- **Launcher (recommended):** run `VHOLTAT-Launcher-<version>.exe`, press **Install**, then **Play**. It finds
  STRAFTAT through Steam, installs BepInEx and VHOLTAT into `%APPDATA%\VHOLTAT`, and leaves plain Steam launches
  vanilla. **Uninstall** puts the game folder back exactly as it was. See `docs/launcher.md`.
- **Manual:** unzip `VHOLTAT-<version>-manual.zip` into a STRAFTAT folder that already has BepInEx 5.
- **Mod managers:** `VHOLTAT-<version>-thunderstore.zip` is a Thunderstore-format package (depends on
  BepInExPack 5.4.2100).

## Camera

By default VHOLTAT uses VHOLUME's camera: 100 degree horizontal FOV (adjustable), small FOV changes when running,
sliding and wall running, and a 6 degree wall-run roll toward the wall. STRAFTAT's own 90 degree FOV is vertical
(about 121 degrees horizontal), which makes the same speeds feel much faster. The 0.2 camera is still available as
"STRAFTAT wide". Optional: a VHOLUME-style speedometer and speed effect. All of it is in the in-game settings window
(Esc, then **VHOLTAT settings**).

## Steam Deck / Linux (Proton)

BepInEx needs its loader DLL enabled under Proton. Set STRAFTAT's launch options to
`WINEDLLOVERRIDES="winhttp=n,b" %command%` and install with the manual zip (the launcher is Windows-only).

## Game updates

VHOLTAT checks at startup that everything it hooks in STRAFTAT still exists. If a STRAFTAT update changed something,
VHOLTAT stays off (vanilla movement) and shows a short notice instead of breaking; the log names what changed and the
STRAFTAT build VHOLTAT was made for.

## Multiplayer

Everyone in the lobby needs the same VHOLTAT version. STRAFTAT itself refuses joins whose mods differ, and puts
modded players in their own matchmaking pool. VHOLTAT adds:

- the host's movement settings are sent to everyone in the lobby;
- a banner (and vanilla movement) for anyone whose VHOLTAT version doesn't match the host's;
- other players see your slides and wall runs animated, and hear the slide / wall-run sounds;
- smoother display of fast-moving players on poor connections (`[Multiplayer] RemoteInterpolationTicks`).

## Keys

| Key | |
|---|---|
| F6 | VHOLTAT / vanilla movement |
| F7 | overlay: state, speed, boost, walls (off by default) |

Developer keys (off unless `[Debug] DevKeys = true`): F8 cycles the movement model (VHOLUME / 0.3 / 0.3 Playtest2),
F9 compares the 0.3 jump with VHOLUME's exact jump, F11 records movement to `BepInEx/recordings/`. Shift+F12 opens the
combat tuning panel (speed limit, boost and wall speeds, how other players are drawn). F10 marks a problem spot to `BepInEx/vholtat_spots.txt` when `[Debug] MarkSpots = true`.

## Moves

- Hold crouch while running to slide; jump out of a slide to keep its speed.
- Jump within 0.15 s of landing to keep your speed and gain a fifth of the speed above run speed (bunny hop).
- Jump next to a wall while holding forward to wall run; jump again to wall jump. Wall to wall builds speed.
- Jump at a ledge or cover while holding forward to vault onto it (vaults happen in the air, as in VHOLUME;
  `[Mantle] VanillaVault = true` adds STRAFTAT's ground vault back).
- Hold forward into a wall in the air to climb it (strafe to turn it into a wall run); reach a ledge to mantle onto it.
- Extra speed from hops is carried and fades slowly (slower still on walls).

Every number is in `BepInEx/config/dev.riley.vholtat.cfg`; saved edits apply while the game runs.

## Building from a fresh clone

Requirements: Windows, the .NET SDK (8 or newer; the plugin targets netstandard2.1, the launcher net48), and an owned
copy of STRAFTAT (its DLLs are referenced from your install and never committed).

1. Copy `Directory.Build.props.user.example` to `Directory.Build.props.user` and set `GameDir` (your STRAFTAT folder)
   and `DevProfile` (any empty folder you want to use as a BepInEx dev profile).
2. Put BepInEx 5.4.23.5 (win x64) into the dev profile so it has `BepInEx\core\BepInEx.Preloader.dll`.
3. `dotnet test tests/MovementCore.Tests` — movement core tests.
4. `dotnet build` — Debug build of the plugin, copied into `<DevProfile>/BepInEx/plugins/VHOLTAT/`.
5. `powershell -ExecutionPolicy Bypass -File tools/dev/launch.ps1` — starts STRAFTAT through Steam on the dev profile.
6. `powershell -ExecutionPolicy Bypass -File tools/release/build-release.ps1` — Release build, tests, and the release
   files in `dist/` (Thunderstore zip, manual zip, launcher, Windows download).

## License

Not chosen yet — the owner will pick one before publishing. Until then, all rights reserved.

## Development

- `dotnet build` (Debug) deploys to the dev profile; `dotnet test tests/MovementCore.Tests` runs the core tests.
- `tools/dev/launch.ps1` starts STRAFTAT on the dev profile. Self-tests: `tools/dev/selftest.sh <basic|parkour|sweep>`,
  two-copy multiplayer: `tools/dev/make_client_instance.ps1` then `tools/dev/lan_test.sh [secs] [parkour|match]`
  (optionally `LAG="--vholtat-lag 60 --vholtat-loss 0.02"`), map sweep: `tools/dev/map_sweep.sh`.
- `tools/release/build-release.ps1` writes the zips, the launcher and the main download (`VHOLTAT-<version>-windows.zip`) to `dist/` (local only).
- Docs: `docs/STRAFTAT_HOOKS.md`, `docs/MOVEMENT_FORMULAS.md`, `docs/MOVEMENT_SPEC.md`, `docs/MOMENTUM_SPEC.md`,
  `docs/RECON.md`, `docs/launcher.md`, `docs/devlog.md`.
