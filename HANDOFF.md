# Handoff — VHOLTAT 0.3.1

## Where things stand

- **VHOLTAT 0.3.1** works: VHOLUME movement (momentum carry, slide, wall run and wall-to-wall chains, wall jump,
  climb and climb-to-wall-run, mantle, vault, coyote time, jump buffer) inside STRAFTAT. STRAFTAT's rules, weapons,
  damage and knockback are unchanged.
- **Locked by Riley:** the Vholume momentum preset, `JumpRiseGravity = 1.12`, and the VHOLUME camera (100 degree
  horizontal FOV). Don't change these defaults without asking.
- **Measured against VHOLUME itself:** run 1300 u/s (13 m/s), jump about 8 m/s, FOV 100 horizontal. The 0.2 "flying"
  feeling was STRAFTAT's vertical FOV, not the movement numbers.
- **Multiplayer:**
  - Host settings sync, a join-time version check, remote animation and smoothing all work.
  - Tested with two game copies on one PC over a local link, including simulated 120 ms ping and 2 % loss, and a real
    match with death and respawn.
- **Safety:** if a STRAFTAT update changes something VHOLTAT hooks, VHOLTAT stays off with a notice
  (`src/VHOLTAT/Compat.cs`). It was made for STRAFTAT 1.4.9f, build 0.0.3302.
- **Release files:** `tools/release/build-release.ps1` writes four files to `dist/`:
  - `VHOLTAT-0.3.1-windows.zip`: launcher, manual zip and how-to.
  - `VHOLTAT-0.3.1-thunderstore.zip`.
  - `VHOLTAT-0.3.1-manual.zip`.
  - `VHOLTAT-Launcher-0.3.1.exe`.

## Build, run, test

See README "Building from a fresh clone". Day to day:

- `dotnet test tests/MovementCore.Tests` runs the 31 core tests.
- `dotnet build` deploys to the dev profile. Close STRAFTAT first.
- Self-tests drive the game and print results:
  - `bash tools/dev/selftest.sh basic|parkour|sweep|menu`.
  - `bash tools/dev/map_sweep.sh` sweeps 5 maps for stuck spots.
- Two players on one PC:
  - Run `tools/dev/make_client_instance.ps1` once.
  - `bash tools/dev/lan_test.sh [secs] [parkour|match]`. Optionally set `LAG="--vholtat-lag 60 --vholtat-loss 0.02"`.
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
3. `git push -u origin main` and `git push origin v0.3.1`
4. Set `GitHubRepo = "<owner>/<repo>"` in `launcher/LauncherConfig.cs`, rebuild with `build-release.ps1`, and attach
   the four files from `dist/` to a GitHub release named v0.3.1.
5. Optional: upload `VHOLTAT-0.3.1-thunderstore.zip` to thunderstore.io/c/straftat.
