# Changelog

## 0.4.0

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

## 0.3.1

- Optional VHOLUME-style speedometer (u = cm/s) and speed effect, both off by default (settings window).
- Startup compatibility guard: if a STRAFTAT update changes something VHOLTAT hooks, it stays off with a notice
  instead of half-working. Logs the STRAFTAT build it was made for.
- Launcher: in-place updates keep your settings, "Play vanilla", clearer errors.

## 0.3.0

- VHOLUME camera is the new default: 100 degree horizontal FOV like VHOLUME (converted for your screen), small
  FOV kicks (+5 running, +10 sliding / wall running), 6 degree wall-run roll toward the wall. The 0.2 camera used
  STRAFTAT's 90 degree vertical FOV (about 121 horizontal, ~134 while running), which made the same speeds feel far
  faster than in VHOLUME. It stays selectable as "STRAFTAT wide" ([Camera] Style / settings menu).
- Movement numbers unchanged: measured in VHOLUME itself (run 1300 u/s = 13 m/s, jump ~8 m/s up) they already match.

## 0.2.0

- Vault: running into waist-high cover goes over it, keeping 95 / 80 / 70 % of your speed by obstacle height, quicker
  the faster you run (VHOLUME's vault bands). Plays STRAFTAT's vault sound.
- Strafe during a wall climb to turn it into a wall run along the same wall.
- Camera: VHOLUME's head tilt into strafes (3 degrees, scaled by speed). Optional VHOLUME wall-run roll (6 degrees
  toward the wall) and VHOLUME FOV kicks in [Camera].
- In-game settings: a VHOLTAT button in the pause menu opens a window for on/off, overlay, camera, sounds and
  multiplayer smoothing. Movement numbers stay host-controlled (reset only when solo or hosting).
- General release packaging: VHOLTAT-<version>-windows.zip, how-to for any player; developer keys off by default.

## 0.1.1

- Ladders behave like vanilla again (no wall run or wall climb while on a ladder).
- Smoother display of fast-moving players on poor connections (8-tick remote buffer, configurable).
- Join-time version check: a mismatched VHOLTAT version gets a banner and vanilla movement in that lobby.
- Knockback cancels carried momentum before pushing, like vanilla; weapon fire slowdown applies.
- Slide and wall-run sounds, landing camera dip.
- F10 spot marker for bug reports (opt-in).
- Launcher and the main Windows download.

## 0.1.0

- First playable version: VHOLUME-style momentum (Vholume preset), slide, wall run, wall jump, wall climb, mantle,
  coyote time, jump buffer, snappier jump takeoff (JumpRiseGravity 1.12), host settings sync, remote animation.
