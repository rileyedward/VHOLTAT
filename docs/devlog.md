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
