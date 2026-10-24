# Handoff — VHOLTAT 0.1.0 (first public release)

## Where things stand

- **VHOLTAT 0.1.0** works (Riley tested it: "works amazing"). Earlier dev builds were numbered up to 0.4.0;
  public numbering restarted at 0.1.0 (history in docs/devlog.md). It has: VHOLUME movement (momentum carry, slide, wall run and wall-to-wall chains, wall jump,
  climb and climb-to-wall-run, mantle, vault, coyote time, jump buffer) inside STRAFTAT. STRAFTAT's rules, weapons,
  damage and knockback are unchanged.
- **Two movement models** (`[General] Model`):
  - `Vholume` (default): `src/MovementCore/VholumeMotor.cs`, built only from the decoded spec
    `docs/MOVEMENT_FORMULAS.md`. Numbers in `[Vholume]` (`VholumeSettings.cs`).
  - `Locked03`: the earlier dev-build feel Riley locked in playtests (`MovementMotor.cs`, Vholume preset, `JumpRiseGravity = 1.12`).
  - Both share `MotorBase`; the driver switches live (F8 with DevKeys). The host's model and numbers sync.
- **Locked by Riley:** the VHOLUME camera (100 degree horizontal FOV). Fast-target combat stays as is; limits only
  live in the hidden Shift+F12 panel (`Settings/CombatPanel.cs`), off by default.
- **Playtest data:** F11 (with DevKeys) records a CSV; `tools/dev/compare_traces.py` compares it with VHOLUME ghosts.
- **Measured against VHOLUME itself:** run 1300 u/s (13 m/s), jump about 8 m/s, FOV 100 horizontal. The 0.2 "flying"
  feeling was STRAFTAT's vertical FOV, not the movement numbers.
- **Multiplayer:**
  - Host settings sync, a join-time version check, remote animation and smoothing all work.
  - Tested with two game copies on one PC over a local link, including simulated 120 ms ping and 2 % loss, and a real
    match with death and respawn (dev build 0.3.1). With the current model the two-copy parkour and aim tests ran; the
    two-copy match test hasn't been rerun.
- **Safety:** if a STRAFTAT update changes something VHOLTAT hooks, VHOLTAT stays off with a notice
  (`src/VHOLTAT/Compat.cs`). It was made for STRAFTAT 1.4.9f, build 0.0.3302.
- **Release files:** `tools/release/build-release.ps1` writes four files to `dist/`:
  - `VHOLTAT-0.1.0-windows.zip`: launcher, manual zip and how-to.
  - `VHOLTAT-0.1.0-thunderstore.zip`.
  - `VHOLTAT-0.1.0-manual.zip`.
  - `VHOLTAT-Launcher-0.1.0.exe`.

## Build, run, test

See README "Building from a fresh clone". Day to day:

- `dotnet test tests/MovementCore.Tests` runs the 48 core tests (17 pin the VHOLUME model to the spec numbers).
- `dotnet build` deploys to the dev profile. Close STRAFTAT first.
- Self-tests drive the game and print results:
  - `bash tools/dev/selftest.sh basic|parkour|sweep|menu`.
  - `bash tools/dev/map_sweep.sh` sweeps 5 maps for stuck spots.
- Two players on one PC:
  - Run `tools/dev/make_client_instance.ps1` once.
  - `bash tools/dev/lan_test.sh [secs] [parkour|match]`. Optionally set `LAG="--vholtat-lag 60 --vholtat-loss 0.02"`.
  - In parkour mode the client logs `[lan] aim` lines: the gun's hit check aimed at the host's drawn torso and head,
    per movement state, plus pose screenshots.
- `python tools/dev/compare_traces.py --vholtat <recordings> --vholume <ghost dir> --out report.html` compares
  recordings with VHOLUME replays (event-aligned medians, text table + HTML charts).
- `bash tools/dev/play_lobby.sh [map|random]` hosts a real match with an idle second player, for manual play.
- `tools/dev/launch.ps1 -Extra --vholtat-compat-test` simulates a STRAFTAT update to test the compatibility guard.

## On hold until Riley says go

- **License.** Not chosen yet. Riley picks one; until a LICENSE file exists the code is all rights reserved by default.
- **A real match with a friend over Steam.** It's the only remaining way to check:
  - the round-start countdown freeze (it never triggered over the local link);
  - real-ping feel;
  - STRAFTAT's own teleporters and jump pads on real maps (tested with spawned objects).
- **Thunderstore upload.** The package is ready in `dist/`.
- **Optional:** a custom showcase map. Riley skipped it; it needs CustomMapKit + Custom Levels Reborn.

## Publishing (Riley)

The repo is github.com/rileyedward/VHOLTAT (remote `origin`). Its `main` holds the older commit cc93972, which local
`main` builds on, so the push is a fast-forward. The launcher checks this repo's latest release for updates.

1. `git push -u origin main`
2. `git push origin v0.1.0`
3. Optional: GitHub still has an old `v0.0.1` tag; remove it with `git push origin --delete v0.0.1`.
4. Make a GitHub release from tag v0.1.0 and attach the four files from `dist/`.
5. Optional: upload `VHOLTAT-0.1.0-thunderstore.zip` to thunderstore.io/c/straftat.
6. Anyone who installed a dev build (0.2-0.4) with the launcher: run `VHOLTAT-Launcher-0.1.0.exe uninstall --keep-config`,
   then `VHOLTAT-Launcher-0.1.0.exe install` (or Install in its window), or the launcher keeps the higher-numbered dev build and lobbies see a version mismatch.
