# Devlog



## 2026-10-19 — Phase 0 + Phase 1



- Phase 0: toolchain OK (.NET 10 SDK, ilspycmd). Dev profile `the dev profile folder` (BepInEx 5.4.23.5

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



## 2026-10-19 (evening) — match readiness, 0.1.1



- Riley locked the feel after feel test 5: Vholume preset + JumpRiseGravity 1.12 (jump ~1.33 m / 0.33 s). Defaults

  match it; don't change without Riley.

- STRAFTAT rules: AddForce/AddHorizontalForce (all weapon/hit knockback) now cancel carried momentum like vanilla;

  vanilla writes to moveDirection.y are applied as "set vertical speed"; weapon fire slowdown (SetSpeed) scales ground

  speed; ladders stay vanilla (wall/climb sensors off while onLadder); boosted teleports (pads) launch correctly.

- Multiplayer: two-copy LAN test ported from Straft4Dead (tools/dev/make_client_instance.ps1, lan_test.sh, Dev/DevNet.cs)

  with a client-side observer. A real match (lobby -> StartGameSteam -> Arena_00 -> death -> respawn) works; the

  round-start freeze didn't trigger over the LAN link, check it in the first Steam match. Join-time version check

  (banner + vanilla movement on mismatch; STRAFTAT itself also rejects joins with different mods).

- Simulated 120 ms RTT + 2 % loss (--vholtat-lag/--vholtat-loss): remote players stuttered a little at speed; an

  8-tick remote NetworkTransform buffer ([Multiplayer] RemoteInterpolationTicks) cut the unevenness by ~30 %.

- Feedback: STRAFTAT slide/wall-slide loops (networked) and landing dip. Perf: ~0.04 ms/frame, no per-frame allocation

  of ours (the only allocation is vanilla's OnControllerColliderHit inside CharacterController.Move).

- Stuck-spot sweep (TestMode = sweep, tools/dev/map_sweep.sh) on Corridor_00, Scaffoldingue_00, Chateaux_01,

  Neo_Arena_03, Backrooms_00; F10 spot marker ([Debug] MarkSpots) for spots found in normal play.

- Launcher (launcher/, VHOLTAT-Launcher.exe), friends bundle, README, CHANGELOG; release zips now use forward-slash

  entry names (PowerShell 5's Compress-Archive wrote backslashes).



## 2026-10-20 — VHOLUME camera (0.3.0), hardening (0.3.1)



- Riley found 0.2 "like flying". Measured VHOLUME in its own game (scripted input + its speedometer): run 1300 u/s

  (13 m/s), jump ~8 m/s up, FOV 100 horizontal; Riley's 30 local ghosts: median 17.1 m/s. Movement already matched.

  Root cause was the camera: STRAFTAT FOV is vertical (90 -> ~121 horizontal, ~134 running with sprint FOV + our kick).

  0.3.0 ships the VHOLUME camera (100 horizontal, +5/+10 kicks, 6 degree roll toward the wall) as default; the old

  camera stays as "STRAFTAT wide". Riley: "much much better". Camera and movement are locked again.

- 0.3.1: optional speedometer / speed effect (off), startup compatibility guard (Compat.cs, --vholtat-compat-test to

  simulate a game update), launcher polish, docs (Steam Deck note).


## 2026-10-21 — VHOLUME model, combat panel (0.4.0)

- VHOLUME's movement script decoded into docs/MOVEMENT_FORMULAS.md (plain English and numbers). VholumeMotor built
  from it and made the default; the 0.3 motor stays as Model = Locked03 (shared MotorBase, live switch).
- 17 unit tests pin the model to the spec. Event-aligned trace comparison against 35 of Riley's ghosts: run start,
  jump arc, wall-run entry and climb start line up.
- Fixed the 0.3.x unbound config entries (sounds, landing dip, speedometer, speed effect).
- Remote poses (lean, tuck) on other screens; hitboxes are on the model's bones so they follow. Two-copy aim test:
  the gun's hit check lands on drawn torso and head in every movement state.
- Hidden Shift+F12 combat panel. Two-copy match test not rerun for 0.4.0 yet; Riley's feel check pending.

## Pre-release version history (before the public 0.1.0)

These dev builds were never published; the public numbering restarted at 0.1.0.

### 0.4.0

- New default movement model, rebuilt from VHOLUME's own rules (docs/MOVEMENT_FORMULAS.md): run 13 m/s, jump 8 up,
  gravity 21.56, Unreal-style acceleration and braking; bunny hops add 20 % of the speed above run speed within
  0.15 s of landing, boost fades 0.8/s (0.1/s on walls); VHOLUME's slide timer; wall runs at 16.5 with VHOLUME's
  rise-then-sink curve and no time limit; climbs at 4 up then curving down; wall jumps toward where you look at
  max(14, wall-run speed); coyote jump 0.33 s; jump buffer 0.1 s with perfect-hop grading.
- Vaults happen in the air, as in VHOLUME (a column check just ahead of you, no wall needed). Running into cover no
  longer hops you over it; `[Mantle] VanillaVault = true` adds STRAFTAT's ground vault back.
- The 0.3 movement stays available: `[General] Model = Locked03`. The host's model and numbers apply to the lobby.
- Hidden combat tuning panel (Shift+F12): speed limit (off), hop boost, wall and slide speeds, other players'
  smoothing and poses. Fast targets are unchanged by default.
- Other players running VHOLTAT lean on wall runs, lean into climbs and tuck on vaults on your screen; their hitboxes
  follow the pose.
- Fixed: the move sounds, landing dip, speedometer and speed effect settings were missing from the config in 0.3.x
  (sounds and the landing dip silently didn't play).
- Movement recorder (F11 with DevKeys) for playtests.

### 0.3.1

- Optional VHOLUME-style speedometer (u = cm/s) and speed effect, both off by default (settings window).
- Startup compatibility guard: if a STRAFTAT update changes something VHOLTAT hooks, it stays off with a notice
  instead of half-working. Logs the STRAFTAT build it was made for.
- Launcher: in-place updates keep your settings, "Play vanilla", clearer errors.

### 0.3.0

- VHOLUME camera is the new default: 100 degree horizontal FOV like VHOLUME (converted for your screen), small
  FOV kicks (+5 running, +10 sliding / wall running), 6 degree wall-run roll toward the wall. The 0.2 camera used
  STRAFTAT's 90 degree vertical FOV (about 121 horizontal, ~134 while running), which made the same speeds feel far
  faster than in VHOLUME. It stays selectable as "STRAFTAT wide" ([Camera] Style / settings menu).
- Movement numbers unchanged: measured in VHOLUME itself (run 1300 u/s = 13 m/s, jump ~8 m/s up) they already match.

### 0.2.0

- Vault: running into waist-high cover goes over it, keeping 95 / 80 / 70 % of your speed by obstacle height, quicker
  the faster you run (VHOLUME's vault bands). Plays STRAFTAT's vault sound.
- Strafe during a wall climb to turn it into a wall run along the same wall.
- Camera: VHOLUME's head tilt into strafes (3 degrees, scaled by speed). Optional VHOLUME wall-run roll (6 degrees
  toward the wall) and VHOLUME FOV kicks in [Camera].
- In-game settings: a VHOLTAT button in the pause menu opens a window for on/off, overlay, camera, sounds and
  multiplayer smoothing. Movement numbers stay host-controlled (reset only when solo or hosting).
- General release packaging: VHOLTAT-<version>-windows.zip, how-to for any player; developer keys off by default.

### 0.1.1

- Ladders behave like vanilla again (no wall run or wall climb while on a ladder).
- Smoother display of fast-moving players on poor connections (8-tick remote buffer, configurable).
- Join-time version check: a mismatched VHOLTAT version gets a banner and vanilla movement in that lobby.
- Knockback cancels carried momentum before pushing, like vanilla; weapon fire slowdown applies.
- Slide and wall-run sounds, landing camera dip.
- F10 spot marker for bug reports (opt-in).
- Launcher and the main Windows download.

### 0.1.0

- First playable version: VHOLUME-style momentum (Vholume preset), slide, wall run, wall jump, wall climb, mantle,
  coyote time, jump buffer, snappier jump takeoff (JumpRiseGravity 1.12), host settings sync, remote animation.
