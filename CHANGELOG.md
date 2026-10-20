# Changelog

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
