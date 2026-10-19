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

Developer keys (off unless `[Debug] DevKeys = true`): F8 momentum preset (Vholume / Playtest2), F9 compare the jump
with VHOLUME's exact jump. F10 marks a problem spot to `BepInEx/vholtat_spots.txt` when `[Debug] MarkSpots = true`.

## Moves

- Hold crouch while running to slide; jump out of a slide to keep its speed.
- Jump within 0.15 s of landing to keep your speed (bunny hop).
- Jump next to a wall while holding forward to wall run; jump again to wall jump. Wall to wall builds speed.
- Hold forward into a wall in the air to climb it; reach a ledge to mantle onto it.
- Speed above each move's base is carried: never lost in the air, fades slowly on the ground.

Every number is in `BepInEx/config/dev.riley.vholtat.cfg`; saved edits apply while the game runs.

## Development

- `dotnet build` (Debug) deploys to the dev profile; `dotnet test tests/MovementCore.Tests` runs the core tests.
- `tools/dev/launch.ps1` starts STRAFTAT on the dev profile. Self-tests: `tools/dev/selftest.sh <basic|parkour|sweep>`,
  two-copy multiplayer: `tools/dev/make_client_instance.ps1` then `tools/dev/lan_test.sh [secs] [parkour|match]`
  (optionally `LAG="--vholtat-lag 60 --vholtat-loss 0.02"`), map sweep: `tools/dev/map_sweep.sh`.
- `tools/release/build-release.ps1` writes the zips, the launcher and the main download (`VHOLTAT-<version>-windows.zip`) to `dist/` (local only).
- Docs: `docs/STRAFTAT_HOOKS.md`, `docs/MOVEMENT_SPEC.md`, `docs/MOMENTUM_SPEC.md`, `docs/RECON.md`,
  `docs/launcher.md`, `docs/devlog.md`.
