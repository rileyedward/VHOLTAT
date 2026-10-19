# Devlog

## 2026-10-19 — Phase 0 + Phase 1

- Phase 0: toolchain OK (.NET 10 SDK, ilspycmd). Dev profile `C:\Users\Riley\source\_vholtat-dev` (BepInEx 5.4.23.5
  copy, console on), launched through Steam with doorstop args by `tools/dev/launch.ps1`. VHOLUME turned out to be
  Unreal 5.4 + AngelScript; the spec comes from its precompiled script data and dev ghost replays.
  STRAFTAT movement: no CSP, owner-authoritative NetworkTransform, no server checks (docs/STRAFTAT_HOOKS.md).
- Phase 1: MovementCore (ground momentum, slide, coyote, jump buffer, hop window, wall run + wall jump, wall climb,
  mantle) with 19 tests. Adapter replaces ApplyFinalMovements for the local owner; vanilla knockback channels are
  added on top and vertical writes to moveDirection become impulses. F6 toggles vanilla/VHOLTAT, F7 the overlay.
- In-engine self-test ([Dev] AutoTest, Arena_00): run 13.0 m/s, slide ~16 m/s held, slide jump keeps speed, jump
  apex 1.48 m, wall climb 0.8 s, braking to 0 in 0.3 s, no exceptions. Fixed: grounded flicker (ground stick),
  slide-start skin-width pop (slide flagged one frame early).
- Open: speed scale vs vanilla (VHOLTAT run 13 m/s vs STRAFTAT sprint 8.5); wall-run camera tilt and remote
  wall-run animation; mantle needs a real ledge test.

## 2026-10-19 — momentum, presets, parkour test

- Playtests 1–3 with Riley. Momentum now follows VHOLUME's carry rules (docs/MOMENTUM_SPEC.md): speed above each
  state's base is a boost that never decays in air, decays 0.8/s on ground and 0.1/s on walls, bunny hops restore
  landing speed, wall run entry turns extra air speed into boost (wall-to-wall chains stack), air steering only turns.
  Riley rated feel test 3 (Vholume preset) the best and closest to VHOLUME; that feel is the baseline now.
- Playtest 2's skill-fed boost is kept as the Playtest2 preset (F8 switches live).
- Fix: wall bounces stopped after 4–5 walls because each side was blocked until landing; now only the wall just
  left is blocked.
- Host shares its movement settings with clients (FishNet broadcast, 2 s heartbeat); untested with two players.
- Remote animation for wall run / climb; camera FOV by speed and wall-run roll.
- Parkour self-test ([Dev] TestMode = parkour) spawns a 2 m ledge and a side wall: mantle onto the ledge in ~0.3 s
  keeping 95% speed, wall run at 16.5 m/s with the +2 m/s rise, wall jump off it. No exceptions.
- Local release packaging: tools/release/build-release.ps1 -> dist/ (Thunderstore layout + manual zip).

## 2026-10-19 — launcher

- `launcher/` builds `VHOLTAT-Launcher.exe` (net48 WinForms), adapted from Straft 4 Dead's launcher minus everything
  L4D2/asset/tool related. It finds STRAFTAT through Steam, installs the pinned BepInEx 5.4.23.5 (SHA-256 checked,
  cached) and the embedded VHOLTAT.dll into `%APPDATA%\VHOLTAT\profile`, puts only `winhttp.dll` and a switched-off
  `doorstop_config.ini` into the game folder (backing up anything it replaces), and Play turns doorstop on through
  Steam launch arguments. Uninstall restores the game folder and can keep the config. See docs/launcher.md.
- GitHub update check is wired but off until `LauncherConfig.GitHubRepo` is set.
- build-release.ps1 also writes `dist\VHOLTAT-Launcher-<ver>.exe`.
- Tested with the CLI against a fake STRAFTAT folder and a temp home: install (real BepInEx download), status
  (installed 0.1.0), `--zip` install from the manual zip, uninstall restoring a pre-existing doorstop_config.ini,
  and a game folder with its own BepInEx left untouched. Play not run.
