# Changelog

## 0.1.0 — first public release

VHOLUME's parkour movement inside normal STRAFTAT matches. STRAFTAT's rules, weapons, damage and knockback are
unchanged.

**Movement** (rebuilt from VHOLUME's own rules, see docs/MOVEMENT_FORMULAS.md)
- Run 13 m/s, jump 8 m/s, VHOLUME's gravity, acceleration and braking.
- Bunny hops: jump within 0.15 s of landing to keep your speed and add a fifth of the speed above run speed. The extra
  speed fades slowly (slower still on walls). Perfect-timed hops lose nothing.
- Slide (hold crouch while running), with VHOLUME's slide timer and slope rules; jump out of it to keep its speed.
- Wall run with VHOLUME's rise-then-sink curve, wall-to-wall chains and wall jumps toward where you look.
- Wall climb, and strafe during a climb to turn it into a wall run.
- Vault onto ledges and cover by jumping at them (in the air, as in VHOLUME). `[Mantle] VanillaVault = true` adds
  STRAFTAT's ground vault back.
- Coyote time (0.33 s) and jump buffer (0.1 s).
- The earlier tuning stays available as `[General] Model = Locked03`.

**Camera and feel**
- VHOLUME camera: 100 degree horizontal FOV (converted for your screen), small FOV kicks, wall-run roll, head tilt into
  strafes. "STRAFTAT wide" camera selectable.
- Slide, wall-run and vault sounds, landing dip; optional VHOLUME-style speedometer and speed effect.

**Multiplayer**
- The host's movement settings apply to everyone in the lobby; a join-time version check falls back to vanilla
  movement on a mismatch. Modded lobbies use their own matchmaking pool.
- Other VHOLTAT players are drawn smoothly at speed and lean, climb and tuck with their moves; their hitboxes follow.
- Hidden combat tuning panel (Shift+F12): optional speed limit, boost and wall speeds, how other players are drawn.

**Install and settings**
- One-click launcher (installs BepInEx and VHOLTAT, updates from GitHub releases, "Play vanilla"), manual zip and
  Thunderstore package.
- Pause-menu settings window; every number is in the BepInEx config. F6 switches VHOLTAT / vanilla movement live.
- Startup compatibility guard: if a STRAFTAT update changes something VHOLTAT hooks, it stays off with a notice.
