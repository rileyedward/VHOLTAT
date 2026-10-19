# VHOLUME parkour movement: behavior spec (clean room)

Written by the spec writer for a separate implementer. It contains no game code, no decompiled logic and no script text. It describes behavior in plain language and lists numeric parameters.

## Units
- Unreal units are centimetres. 100 cm = 1 m. Values are given as `cm (m)`.
- Times are in seconds. Angles are in degrees.

## Evidence tags
| Tag | Meaning |
|---|---|
| **C-data** | Read directly from the shipped precompiled script data: tweak constants, class property defaults, or numeric literals inside a named function. |
| **C-ghost** | Measured from the shipped developer ghost replays: 93 runs, about 157k samples at 20 Hz, all Gold/Silver/Bronze developer runs. |
| **INF** | Inferred. The number is known, but its exact role is deduced from the function and property names it appears with. Treat it as a strong hint, not ground truth. |
| **UNK** | Unknown or not recovered. |

## Data sources (for the record)
- `PrecompiledScript.Cache` is the serialized AngelScript bytecode for all modules. Global tweak constants carry their literal values. Class defaults were recovered by matching each constructor's member stores to properties, using a recomputed field layout. Types and alignments matched at every checkpoint.
- The developer ghosts are gzip'd JSON (4-byte prefix). Each frame holds position, velocity, `mode` (parkour sub-state), `movementMode` (1 = walking, 3 = falling, 0 = scripted/none), crouch alpha and a slide flag.
- Parkour sub-state enum order, taken from the exe reflection strings: 0 None, 1 LeftWallRun, 2 RightWallRun, 3 WallClimb, 4 LedgeGrab (used for ziplines), 5 Mantle, 6 Slide, 7 Sprint, 8 Crouch.
- Not used: the 5.6 GB pak. It is unencrypted but Oodle-compressed, and cooked Blueprint defaults need an unversioned-property schema. Some values that would live there (e.g. the CharacterMovement component's walk speed and jump velocity) were therefore measured from the ghosts instead.

---

## 1. Global physics

| Parameter | Value | Tag | Notes |
|---|---|---|---|
| Gravity | 2156 cm/s² (21.56 m/s²) downward | C-data + C-ghost | Gravity scale is 2.2 × 980. A "reset gravity" routine restores scale 2.2. The jump-pad preview gravity default is -2156. Ghost air samples have a median of -2156. |
| Terminal fall speed | 4000 cm/s (40 m/s) | C-data + C-ghost | Jump-pad preview default. In the ghosts, vertical speed clamps at exactly -4000 (1113 samples). |
| Capsule half-height (standing) | 88 cm (0.88 m) | C-data | Jump-pad preview default. A crouch routine references 96 (INF: possibly the camera/eye height or the full capsule height). |
| Fall-damage threshold | 2500 cm/s (25 m/s) impact speed | C-data (INF meaning) | Fall damage is a gameplay toggle (on by default). Holding crouch within 0.05 s around landing avoids it. See §4. |

## 2. Ground locomotion (run)

There is no separate sprint key in practice. The Sprint sub-state exists in the enum but never appears in 93 developer runs. The leftover sprint property (120) is unused. Running is the default ground state.

| Parameter | Value | Tag |
|---|---|---|
| Max run speed | 1300 cm/s (13 m/s) | C-ghost (90th percentile is exactly 1300) and C-data (1300 is the base in the max-speed calculation) |
| Ground acceleration | ≈10000 cm/s² (100 m/s²). 0 → 1300 takes about 0.13 s. | C-ghost (acceleration samples cap at 10000) |
| Ground braking (input released) | Median ≈5000 cm/s², range 3000–9500 | C-ghost (INF: UE-style friction plus braking) |
| Crouch-walk speed | 500 cm/s (5 m/s) | C-ghost (crouch state is always exactly 500) |
| Crouch speed factor property | 0.5 | C-data (role unclear: 0.5 × 1300 ≠ 500) |
| Look rates (gamepad) | 85 deg/s turn and look-up. Gamepad look multiplier 2.0. | C-data |

**Speed above the cap.** When the player lands with more than 1300 horizontal speed and does not jump again, speed drops back to 1300 within one to two samples (≤0.1 s). This is fast, but not a single-frame snap. See bunny hopping (§5) for how extra speed is kept.

**Speed boost.** A scalar "SpeedBoost" is added on top of the state's base max speed. The max speed for any moment is built from:
- base 1300 on foot, 1650 on a wall run, or the crouch speed;
- plus the current boost;
- plus extra terms from speed-modifier floors and pads.

Boost decays:
- on the ground at rate 0.8 (BUNNY_HOP_GROUND_DECAY_SPEED);
- during a wall run at rate 0.1 (SPEEDBOOST_WALL_RUN_DECAY_SPEED).

Units are INF (likely a fraction per second, or a lerp factor). Numbers seen inside the max-speed function: 1300, 1650, 100, 0.5, 1000, 0.1, 0.8.

## 3. Air

| Parameter | Value | Tag |
|---|---|---|
| Horizontal speed in air | Preserved. There is no air drag, and strafing gives no speed gain. Median dH/dt in air is 0. | C-ghost |
| Air control (steering) | Direction can change at any speed. Below the run cap, the player can accelerate up to ≈3600 cm/s². This implies an air-control fraction of about 0.35 of the 10000 ground acceleration. | C-ghost (INF on the fraction) |
| Assist-mode air control | 0.4 | C-data. A "Roberta" accessibility/assist mode raises air control to 0.4. It also uses a smaller minimum wall-jump angle (20° vs 30°). |

## 4. Jump

| Parameter | Value | Tag |
|---|---|---|
| Jump vertical velocity | ≈800 cm/s (8 m/s) | C-ghost. Apex height is a consistent 148 cm (1.48 m); √(2·2156·148) = 799. |
| Jump apex height | 148 cm (1.48 m) | C-ghost. The same height applies from run, from slide, and after a slide jump. |
| Horizontal on jump | Unchanged. Takeoff keeps the current ground or slide speed. | C-ghost |
| Variable jump height | Not observed. Every ordinary jump reaches the same apex. Assume a fixed impulse. | C-ghost (INF) |
| Coyote time | 0.33 s after leaving a ledge (walking → falling without a jump) | C-data (JUMP_COYOTE_TIME). The 0.33 also appears in the per-tick movement update. It is armed on the walking → falling transition. |
| Jump buffer (toward ground) | 0.10 s | C-data (JUMP_BUFFER_TIME_GROUND) |
| Jump buffer (toward wall-run) | 0.10 s | C-data (JUMP_BUFFER_TIME_WALLRUN) |
| Jump input pulse length | 0.05 s | C-data. The press counts as "held" for this long, for networking and input robustness. |
| Jump-pad double-jump suppression | 0.20 s after a pad launch, plus a 0.18 s pad debounce | C-data (YeetJumpSuppressWindow, YEET_DEBOUNCE_WINDOW) |

**Buffered jump.** Pressing jump within the buffer before touching ground or a wall-run surface fires the jump automatically on contact. Landing-frame speed is stored and restored on a buffered hop. See §5.

**Fall damage and landing.**
- Lethal fall-damage logic exists, keyed on impact speed 2500 cm/s.
- Pressing crouch within 0.05 s before or after landing negates it (NO_FALL_DAMAGE_CROUCH/UNCROUCH_TIMER).
- Landing camera shake has three tiers, at impact speeds of about 800, 1500 and 2100 cm/s (C-data, INF ordering).

## 5. Bunny hop / speed-keeping

Defaults:
- PERFECT_HOP_ENABLED = true
- BUNNY_HOP_CONSERVE_SPEEDBOOST = true

| Parameter | Value | Tag |
|---|---|---|
| Hop buffer (after landing, keep speed if you jump again) | 0.15 s | C-data |
| Hop buffer when the next contact is a wall run | 0.25 s | C-data. Enabled. |
| Speed-boost conserve time after a hop | 0.5 s | C-data |
| Max boost the buffer may carry | 1000 cm/s | C-data |
| Fraction of boost kept through the buffer | 0.2 | C-data |
| Landing friction loss per hop | 21 (cm/s per hop, INF) | C-data |
| Restore landing-frame speed | On | C-data |
| "Same contact" guard (ignore re-triggers on the same spot) | Within 150 cm and 0.12 s. Applies to slide hops only. | C-data |
| Fake-landing gate (only real landings grant hop/boost logic) | Needs ≥0.1 s airborne and ≥200 cm/s peak fall speed. Crouch input must have been pressed within 0.25 s. The gate is active for the first 5 s of a level (INF). | C-data |
| Edge-chain auto hop | Disabled (ENABLE_BUNNY_HOP_EDGE_AUTOCHAIN = false). Parameters: window 0.35 s, max 6, max distance 600 cm. | C-data |
| Slide-jump allowed to bunny hop | Yes | C-data |

**Perfect-hop grading.** This system applies once you are already going fast.
- Jumping within **0.034 s** (≈2 frames at 60 fps) of landing counts as "perfect". It uses half-frame correction. Late jumps are not graded.
- Below **2600 cm/s** there is no grading penalty. 2600 is both the base and the feedback threshold.
- A miss loses **29** units per hop below **3500 cm/s** and **61** units at or above 3500.
- A perfect hop loses **0**.
- The top speed reference is **7600 cm/s**. The top-band loss is -1, which means "use the high value".
- Curve power is 0.6, with no extra bonus and a kept margin of 0.
- INF: the loss is applied as a braking amount to the stored horizontal speed. Interpolation runs between the base, high and top speeds using the power curve.

Ghost check: hops with a single-sample ground contact change speed by a median of +40 cm/s. The 10th–90th percentile range is -306 to +353.

## 6. Crouch

| Parameter | Value | Tag |
|---|---|---|
| Crouch-walk speed | 500 cm/s | C-ghost |
| Crouch transition (camera/capsule tween) | 0.15 s down and 0.15 s up | C-data |
| Uncrouch blocked by ceiling | A periodic re-check runs until there is room to stand | C-data (names) |
| No wall run right after uncrouch | On (ENABLE_NO_WALLRUN_ON_UNCROUCH) | C-data. Uses a fall-from-uncrouch check with numbers 300, 35, 80 (INF: speed and height thresholds). |
| Crouch clears speed boost | On (CROUCH_FIX_CLEAR_SPEEDBOOST) | C-data |
| Crouch-fix speed caps | 1300 when crouched/running, 1600 when sliding, 1650 when wall running | C-data (ENABLE_CROUCH_FIX_SLOWDOWN = true) |

Crouch input while moving fast starts a slide (§7). Crouch input while slow, or while standing, enters the Crouch state.

## 7. Slide

Ghost behavior:
- Slides are almost always started from a run at 1300.
- On entry, speed is raised to **1600** within one sample.
- On flat ground, speed is held at **exactly 1600** for as long as the slide lasts. The longest sustained ground slide observed was over 1.4 s, with no decay.
- If you enter above 1600 (e.g. 1650 off a wall run, or 1800 from a hop), the higher speed is kept.
- Most slides (≈80%) end with a jump. The slide state persists through the slide jump and continues airborne while crouch is held.
- When a slide ends on the ground, speed drops toward run speed (1600 → 1300–1550 on the next sample).

| Parameter | Value | Tag |
|---|---|---|
| Slide speed (entry target and flat hold) | 1600 cm/s (16 m/s) | C-data (SLIDE_SPEED) + C-ghost |
| Minimum speed to start a slide | INF ≈700 cm/s. The can-slide check uses 700 (and 10). An alternate speed is used in "interlude" mode. | C-data numbers, INF meaning |
| Slide-start numbers | 1200, 1400, 1.5, 45, 300 | C-data. INF: 1200 is the minimum-speed / end threshold (also used in slide end). 1400 is a boost cap. 1.5 is a duration/scale. 45° is a slope angle. 300 is the boost-downgrade threshold. |
| Slide ends when speed falls below | INF 1200 cm/s | C-data (slide-end literal) |
| Deceleration target, flat/downslope | 20 | C-data (SLIDE_DEFAULT_DECELERATION_TARGET). Units INF: cm/s². Effectively no loss, which matches the ghosts. |
| Deceleration target, medium upslope | 100 | C-data |
| Deceleration target, steep/inverse upslope | 300 | C-data |
| Slope classification | The dot product of the floor slope direction with velocity sets the class: Flat; Downslope; GentleUpslope (≤ -0.1); MediumUpslope (≤ -0.25); SteepUpslope (≤ -0.5) | C-data (thresholds and enum names) |
| Ground probe | 50 cm trace. If it misses, a 120 cm fallback trace is used. Slippery-surface trace is on. | C-data |
| Boost downgrade on slide start | On. Threshold 300, steep-only, cost 0. | C-data |
| Reslide cooldown | 0 s (no cooldown). Not applied to all slides. | C-data |
| Respawn slide | Allowed once per spawn, with a 1.0 s buffer | C-data |
| Surfaces tagged "infinite slide" | Slide never decays | C-data (tag name exists) |

Downhill slides: gravity along the slope adds speed (INF). The deceleration targets only apply to flat and upslope classes.

## 8. Wall run

**Ghost behavior.**
- Entry usually happens airborne. The median prior state is about 1325 horizontal and -154 vertical.
- On attach, horizontal speed is set to **1650**. Wall-run horizontal speed stays locked at 1650 (plus any boost) for the whole run.
- Vertical speed starts at **+200 cm/s** and is held for about 0.4 s. It then eases into a gravity curve: +190 at 0.5 s, +90 at 0.9 s, 0 at about 1.05 s, -190 at 1.3 s, -400 at 1.5 s.
- Net rise is about 1.7 m at 1.1 s.
- Typical duration is 0.35 s (median). The 90th percentile is 0.8–0.9 s.

| Parameter | Value | Tag |
|---|---|---|
| Wall-run speed | 1650 cm/s (16.5 m/s) | C-data (WALLRUN_SPEED) + C-ghost |
| Max wall-run duration (gravity curve length) | 1.5 s | C-data (WallRunTime) |
| Initial upward speed | +200 cm/s | C-ghost. The data has 200 and ±400 in the wall-run gravity setter. |
| Wall-run gravity target | 0.5 × normal gravity at the end of the curve | C-data (WallrunGravityTarget 0.5). The ghost slope at 1.5 s is about -1050 cm/s² ≈ 0.49 × 2156. |
| Max upward force clamp | 1500 | C-data (literal in movement reset; INF: ZForceMax) |
| Wall validity | The wall normal must be close to horizontal: \|normal.z\| < 0.52 (walls within about 31° of vertical) | C-data |
| Obtuse-angle check | 30° | C-data |
| Side detection traces | ±75 cm to the left and right of the player | C-data |
| Velocity alignment | Velocity must be aligned enough with the wall-run direction. Numbers 0.8, 0.1 and 0.2 appear in the wall-run update. | C-data numbers, INF meaning |
| "Back" tolerance | 0.5 | C-data (BackWallrunTolerance) |
| Pushing away from the wall ends the run | Lateral input beyond ±0.2 away from the wall | C-data (INF) |
| Top-edge check | On. Offset vector (0,0,3) or (3,0,0); order ambiguous. | C-data |
| Same-wall / same-direction rule | After a wall run ends, that side (left or right) is blocked until the player touches ground or the gate re-opens. Running the opposite direction is allowed. Wall-jump same-direction timeout is 0.35 s. Parkour cancel delay is 0.35 s. | C-data |
| Boost at wall-run end | Disabled (ENABLE_WALLRUN_SPEEDBOOST_ON_WALLRUN_END = false). Multiplier 500, max 1000. | C-data |
| Camera roll during wall run | Tilts toward the wall. Numbers 6 (deg), ±3 (input tilt), 15/4/0.8 (interp). Camera also auto-aligns yaw toward the run direction (numbers 0.25, 3, 8, 6, -40). | C-data (INF exact roles) |
| Wall-run → stand on ground | Ghosts show brief walking samples with the wall-run state still set (≈1300–1650) | C-ghost |

**Wall jump** (jump during a wall run, or within the stored/coyote window after leaving one).
- Vertical: about **750 cm/s** upward. C-data (WallRunJumpHeight 750). Ghost post-jump vertical is 650–730 after up to one 0.05 s sample.
- Horizontal speed: **kept at the wall-run speed** (≈1650). Ghost median post-jump horizontal speed is 1650.
- Direction: turns away from the wall. The minimum separation from the wall plane is **30°** (assist mode: 20°). Ghost median direction change is ≈31°, from |Δv| ≈ 890 at 1650.
- INF: the launch direction is the look direction when it points away from the wall by at least 30°. Otherwise it is clamped to 30° off the wall.
- Push-off force constant: **1400 cm/s**. C-data (WallRunJumpOffForce). INF: used when jumping off a wall climb, or as the perpendicular component.
- A "stored wall jump" lets you still wall-jump shortly after falling off a wall (fall-jump enabled; no-wall variant disabled).

## 9. Wall climb (running straight up a wall)

**Ghost behavior.**
- Entered from a run at about 1300 facing a wall.
- Vertical speed jumps to **+400 cm/s** and holds for about 0.4 s, then decays: 386 at 0.5 s, 341 at 0.7 s.
- Horizontal speed is about 128 for the first 0.25 s (still pressing into the wall), then 0.
- Climb segments last 0.2 s (median) to 0.6 s (90th percentile). The maximum observed is 1.2 s.
- Typical height gain is 0.8–2.8 m.

| Parameter | Value | Tag |
|---|---|---|
| Climb speed | 400 cm/s observed. The script default is 300; INF: overridden at runtime. | C-ghost / C-data |
| Climb → wall-run transition velocity multiplier | (1, 1, 0.1), probably x, y, z; order INF | C-data. Lerp factor 1.0. Resets when the lerp completes. |
| Wall-climb entry check | Forward wall within about 80 cm, facing dot ≥ 0.85 | C-data (INF) |
| Climb-combo block | Can't re-climb within 50 cm of the last climb spot | C-data (INF) |
| Climb cut-off downward clamp | -600 | C-data (INF) |

Jumping during a climb pushes off the wall (see the wall jump section). Lateral steering can convert a climb into a wall run.

## 10. Mantle and vault

There are two systems. `ENABLE_NEW_VAULT` is true.

### Mantle (sub-state 5)
Pulls the player up onto a ledge with a scripted position move: velocity reads 0 and position rises.
- Ghosts: median duration 0.15 s, maximum 0.45 s. The player rises at about 450–500 cm/s.

| Parameter | Value | Tag |
|---|---|---|
| Mantle height parameter | 40 cm | C-data |
| Forward trace distance | Set at runtime (script default 0) | C-data / UNK |
| Ledge traces | Forward offset 50 cm. Line checks use 95, 5, 200 cm (INF: chest height, step, down-trace length). Ledge surface must have normal.z ≥ 0.88 (≈28° max tilt). Near-mantle probe 40 cm. | C-data (INF roles) |
| Mantle interpolation speeds | Quick 30, normal 8 (interp rate) | C-data |
| Mantle movement numbers | 10, 0.5, 500, 200 | C-data (INF: 500 cm/s cap, 200 extra lift) |
| Fit check | On (capsule fit test at the destination) | C-data |

### Vault (character-level, tweened over obstacles)

| Parameter | Value | Tag |
|---|---|---|
| Vault detection range | 150 cm | C-data |
| Vault size cut-offs | 110 and 40 cm (INF: obstacle-height bands for small/medium/large) | C-data |
| Preserve momentum | Yes. Exit velocity = stored pre-vault velocity × multiplier. | C-data |
| Momentum multiplier | Small 0.95, medium 0.80, large 0.70 | C-data |
| Minimum exit speed | 300 cm/s | C-data |
| Duration scales with speed | Yes. Momentum also scales with height. | C-data |
| Speed bands (duration scale min, threshold min–max) | Small 0.5, 600–1400. Medium 0.5, 700–1400. Large 0.6, 800–1300 cm/s. | C-data |
| Velocity history used to pick the pre-vault speed | 1.0 s window | C-data |
| Easing | Custom easing is off, so the default ease index 0 is used (INF: linear). If it were on: small 2, medium 18, large 18 (tween-library enum indices, UNK names). | C-data |

INF behavior:
- The duration interpolates from 1.0 down to the "scale min" as speed goes from the threshold min to the threshold max. Faster means a shorter vault.
- The exit speed is the pre-vault speed × multiplier, never below 300.

## 11. Ledge grab / zipline (sub-state 4)
"LedgeGrab" is the zipline hang state.
- Jumping off adds 400 cm/s up and 300 cm/s forward (C-data: LedgeGrabJumpHeight 400, LedgeGrabJumpOffForce 300).
- Numbers 1000, 100 and 2000 appear in the jump-off routine (INF: caps).
- Zipline speed and exit force factor are per-actor and UNK.

## 12. Jump pads
Jump pads solve a ballistic arc from the pad to a target point:

| Parameter | Value | Tag |
|---|---|---|
| Default apex clearance above the higher end | 300 cm | C-data |
| Max apex clearance | 6000 cm | C-data |
| Auto-apex ratio | 0.06 × horizontal distance, with numbers 12 and 150 (INF: min clearance 150) | C-data |
| Clearance sweep radius | 42 cm | C-data |
| Ignore pad collision after launch | 0.15 s | C-data |
| Air time / horizontal speed / launch speed overrides | 0 by default (unused) | C-data |
| Target is a floor point | true | C-data |

Behavior:
- The pad overrides the player's velocity with the solved launch velocity.
- The solve uses gravity 2156 and terminal velocity 4000.
- Jump is suppressed for 0.2 s after a launch.
- A missed-pad check also runs on floor change.

## 13. Moving platforms
- When standing on a moving actor, its velocity is added to the player's.
- The smoothing factor is 0.6. Inherited velocity is clamped to ±1000 cm/s per axis (C-data, INF).
- That inherited velocity is stored and carried into wall jumps.

## 14. Camera / feel (optional)
- Running camera shake triggers above about 1000 cm/s.
- FOV offsets have numbers 10, 5 and 20 (INF: +FOV when fast or wall running).
- Head tilt follows strafe input (±3) and speed up to 1300.
- Radial speed blur:
  - starts at effect value 0.1;
  - falloff power 4;
  - 8 samples;
  - velocity smoothing 0.01;
  - chromatic aberration 0.1;
  - luma boost 10.

## 15. Input (actions; key bindings UNK)
- Actions: Move forward, back, left and right (separate actions), Turn, LookUp, Jump, Crouch (also slide), Interact, RaiseHands, Reset, RestartLevel, Pause, InputViewer, ToggleGhost.
- Physical keys live in the cooked input mapping context and were not recovered.
- RaiseHands toggles a hands-up state that blocks parkour gates ("ForceNoParkour").

## 16. Feature flags (defaults)

| Flag | Default |
|---|---|
| NEW_VAULT | true |
| WALL_SLIDE | true |
| STORED_WALL_JUMP_ON_FALL_JUMP | true |
| STORED_WALL_JUMP_ON_NO_WALL | false |
| NO_WALLRUN_ON_UNCROUCH | true |
| CROUCH_FIX_SLOWDOWN | true |
| SLIDE_BOOST_DOWNGRADE | true |
| SLIDE_GROUND_TRACE_FALLBACK | true |
| SLIDE_SLIPPERY_GROUND_TRACE | true |
| FAKE_LANDING_BOOST_GATE | true |
| BUNNY_HOP_SAME_CONTACT_GUARD | true |
| BUNNY_HOP_EDGE_AUTOCHAIN | false |
| WALLRUN_SPEEDBOOST_ON_WALLRUN_END | false |
| PERFECT_HOP | true |
| RESPAWN_SLIDE | true |
| Fall damage | true |
| Camera shake | true |
| Head-tilt look input | true |
| Mantle line-trace checks always | true |
| Mantle fit check | true |
| Wall-run top-edge check | true |

## 17. State transition summary

| From | Trigger | To |
|---|---|---|
| Run | Crouch pressed with speed above the slide threshold (INF ≈700) | Slide (speed → max(cur, 1600)) |
| Run | Crouch pressed while slow | Crouch (500) |
| Run / Air | Jump (or buffered jump within 0.1 s, or coyote within 0.33 s) | Air, +800 up |
| Run | Facing a wall closely, moving into it | Wall climb (+400 up) |
| Air / Climb | Wall at the side, normal near horizontal, velocity aligned, side not blocked | Wall run L/R (1650 along the wall, +200 up, gravity curve over 1.5 s) |
| Wall run | Jump | Air: 750 up, ≥30° off the wall, speed kept |
| Wall run | Curve ends, wall ends, input away from the wall, or ground | Air (that side blocked) / Run |
| Air / Run | Ledge in front within mantle traces | Mantle (scripted ≈0.15–0.45 s) |
| Run | Vault obstacle within 150 cm | Vault (tween, then exit at speed × 0.95/0.8/0.7) |
| Slide | Jump | Air; slide state and speed kept while crouch is held |
| Slide | Speed below about 1200 or crouch released | Crouch / Run |
| Land | Jump within 0.15 s (0.25 s to a wall run) | Hop that keeps the stored speed. Perfect-hop grading applies above 2600 cm/s. |
| Land | No jump | Speed decays to the 1300 cap in about 0.1 s |
