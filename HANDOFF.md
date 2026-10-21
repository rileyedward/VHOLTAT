# Handoff — VHOLTAT 0.4.0

## Where things stand

- **VHOLTAT 0.4.0** works: VHOLUME movement (momentum carry, slide, wall run and wall-to-wall chains, wall jump,
  climb and climb-to-wall-run, mantle, vault, coyote time, jump buffer) inside STRAFTAT. STRAFTAT's rules, weapons,
  damage and knockback are unchanged.
- **Two movement models** (`[General] Model`):
  - `Vholume` (default since 0.4.0): `src/MovementCore/VholumeMotor.cs`, built only from the decoded spec
    `docs/MOVEMENT_FORMULAS.md`. Numbers in `[Vholume]` (`VholumeSettings.cs`).
  - `Locked03`: the 0.3 feel Riley locked in playtests (`MovementMotor.cs`, Vholume preset, `JumpRiseGravity = 1.12`).
  - Both share `MotorBase`; the driver switches live (F8 with DevKeys). The host's model and numbers sync.
- **Locked by Riley:** the VHOLUME camera (100 degree horizontal FOV). Fast-target combat stays as is; limits only
  live in the hidden Shift+F12 panel (`Settings/CombatPanel.cs`), off by default.
- **Waiting on Riley:** a feel check of the 0.4.0 VHOLUME model in the Training Round (F11 records a CSV;
  `tools/dev/compare_traces.py` compares it with VHOLUME ghosts).
- **Measured against VHOLUME itself:** run 1300 u/s (13 m/s), jump about 8 m/s, FOV 100 horizontal. The 0.2 "flying"
  feeling was STRAFTAT's vertical FOV, not the movement numbers.
- **Multiplayer:**
  - Host settings sync, a join-time version check, remote animation and smoothing all work.
  - Tested with two game copies on one PC over a local link, including simulated 120 ms ping and 2 % loss, and a real
    match with death and respawn (0.3.1). For 0.4.0 the two-copy parkour and aim tests ran; the two-copy match test is still to rerun.
- **Safety:** if a STRAFTAT update changes something VHOLTAT hooks, VHOLTAT stays off with a notice
  (`src/VHOLTAT/Compat.cs`). It was made for STRAFTAT 1.4.9f, build 0.0.3302.
- **Release files:** `tools/release/build-release.ps1` writes four files to `dist/`:
  - `VHOLTAT-0.4.0-windows.zip`: launcher, manual zip and how-to.
  - `VHOLTAT-0.4.0-thunderstore.zip`.
  - `VHOLTAT-0.4.0-manual.zip`.
  - `VHOLTAT-Launcher-0.4.0.exe`.

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

- **GitHub repo.** Releases and the launcher's update check (`launcher/LauncherConfig.cs`, `GitHubRepo`) wait on it.
- **A real match with a friend over Steam.** It's the only remaining way to check:
  - the round-start countdown freeze (it never triggered over the local link);
  - real-ping feel;
  - STRAFTAT's own teleporters and jump pads on real maps (tested with spawned objects).
- **Thunderstore upload.** The package is ready in `dist/`.
- **License.** Riley picks one before publishing.
- **Optional:** a custom showcase map. Riley skipped it; it needs CustomMapKit + Custom Levels Reborn.

## Pushing once the GitHub repo exists

1. Make the repo on GitHub, empty: no README, license or .gitignore.
2. `git remote add origin https://github.com/<owner>/<repo>.git`
3. `git push -u origin main` and `git push origin v0.4.0`
4. Set `GitHubRepo = "<owner>/<repo>"` in `launcher/LauncherConfig.cs`, rebuild with `build-release.ps1`, and attach
   the four files from `dist/` to a GitHub release named v0.4.0.
5. Optional: upload `VHOLTAT-0.4.0-thunderstore.zip` to thunderstore.io/c/straftat.
