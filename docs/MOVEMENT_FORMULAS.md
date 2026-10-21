# VHOLUME movement formulas (clean room, pass 2)

Behaviour spec for the VHOLTAT implementer. It contains plain English, pseudo-math and numbers only. No VHOLUME code, bytecode or script text is reproduced here.

**Units.** Distances are in cm, with metres in brackets (100 cm = 1 m). Speeds are in cm/s, accelerations in cm/s², times in seconds and angles in degrees. "H" means horizontal speed. "|v|" means the full 3D speed.

## Tags

| Tag | Meaning |
|---|---|
| **CONFIRMED-CODE** | Read from the decoded control flow and arithmetic of the shipped script bytecode. |
| **CONFIRMED-DATA** | A constant or class default read from the script data. Its role is clear from the code that uses it. |
| **MEASURED** | Fitted from ghost traces: 93 dev ghosts plus 35 of Riley's ghosts, sampled at 20 Hz. |
| **INFERRED** | Deduced. It is not directly read and not directly measured. |
| **UNKNOWN** | Not recovered. |

**How this pass differs from the first.** The first pass only had constants and property names, so many roles were INF. This pass decoded the precompiled AngelScript bytecode itself, recovering the opcode set, function layout and call targets (tooling is listed at the end). All gameplay script functions of `NewParkourMovement` and `NewParkourCharacter` were read as control flow. The `[DIFF]` markers flag places where this pass contradicts `VHOLTAT/docs/MOVEMENT_SPEC.md` or `MOMENTUM_SPEC.md`.

**Architecture fact (CONFIRMED-CODE).** The script never writes velocity through physics forces. It does exactly two things:

- **(a) It sets `MaxWalkSpeed` on the CharacterMovement component every tick.** All accel, braking, friction and air control is then UE's stock walking/falling physics with Blueprint default values. Those defaults are not in script; see §1.
- **(b) It calls `LaunchCharacter`, a velocity override, for parkour moves.** Wall run, wall climb, wall jump, ledge jump and pads each override horizontal velocity, vertical velocity, or both, every tick or once.

Gravity scale is the only physics value the script changes: 0 during wall run, climb and vault. `GroundFriction` and `BrakingDecelerationWalking` are only restored to their initial values on every parkour mode change. They are never changed to anything else.

---

## 1. Base physics (CharacterMovement defaults)

| Parameter | Value | Tag | Notes |
|---|---|---|---|
| Gravity scale | 2.2 → g = 2156 cm/s² (21.56 m/s²) | CONFIRMED-CODE (reset-gravity sets 2.2) + MEASURED (median air dvz/dt = -2156) | |
| Terminal fall speed | 4000 cm/s (40 m/s) | MEASURED (UE default) | |
| Jump vertical velocity (JumpZVelocity) | 800 cm/s (8 m/s) | MEASURED | Apex is 148.2 cm (1.48 m) over 3491 jumps; √(2·2156·148.2) = 799. |
| Max acceleration | ≈10000 cm/s² (100 m/s²) | MEASURED | p90 of ground accel with forward-only input is 10045. Air accel is exactly 3500 = 0.35 × 10000. |
| Braking with no input on ground | dv/dt = -(B + K·v), with **K = 8 /s** and **B ≈ 2048 cm/s²** | MEASURED | Release-to-stop sequences fit exactly, e.g. 704→378→163→21→0 per 0.05 s. K = GroundFriction × BrakingFrictionFactor. B = BrakingDecelerationWalking (UE default 2048). |
| GroundFriction / BrakingFrictionFactor split | The product is 8. Turn-rate fit weakly favours GroundFriction ≈ 6–8 with factor 1, over 4 × 2. | MEASURED (weak) | Only the product matters for braking. GroundFriction alone sets how fast velocity turns toward the input direction. |
| Above-cap braking on ground | The same formula applies, but it never takes speed below MaxWalkSpeed while input points forward. | CONFIRMED-CODE (the landing-restore code uses exactly F·factor·v + BrakingDecel) + UE stock | From 1650 with cap 1300, decel ≈ 2048 + 8·1500 ≈ 14000, so the cap is reached in ~0.025 s. |
| AirControl | **0.35** | MEASURED (air accel 3500) + CONFIRMED-CODE | Assist ("Roberta") mode sets 0.35 after the first 0.4 s of falling. |
| Assist-mode AirControl | 0.65 while falling for < 0.4 s, otherwise 0.35 | CONFIRMED-CODE | **[DIFF]** The spec said 0.4. |
| FallingLateralFriction / BrakingDecelerationFalling | ≈0 | MEASURED (median air dH/dt = 0) | Air speed above the cap is kept. Input can only rotate it. |
| Max speed in air | MaxWalkSpeed (UE falling uses walk speed) | CONFIRMED-CODE + UE | Air input cannot push H above 1300 + boost. Speed already above that is kept. |
| Native wall slide (VholumeCharacterMovement) | Exists: bEnableWallSlide, WallSlipMultiplier, WallSlideOutwardBias, MinWallSlideAngle, MaxWallSlideOutwardDistance | CONFIRMED-DATA (names only), values UNKNOWN | This is native C++ collision sliding. It is not in script. |

## 2. Max speed and SpeedBoost (the core loop)

`CalculateMaxSpeed` runs every tick. Its pseudo-math is below. All of it is CONFIRMED-CODE unless noted.

**Step 1: when boost is allowed.** If assist mode is off and the ManualSpeedBoosting flag is off, set SpeedBoost = 0. This means boost exists only while a boost source has set that flag.

**Step 2: no input on ground.** If there is no movement input and the player is not falling:

- MaxWalkSpeed = InitialWalkSpeed. This is the default MaxWalkSpeed, MEASURED as 1300.
- WallrunSpeed = 0.

**Step 3: slide and crouch.** In Slide or Crouch, skip to Step 6. SlideUpdate (§6) and CrouchStart (§7) own MaxWalkSpeed in those modes.

**Step 4: set MaxWalkSpeed.**

- `target = 1300 + SpeedBoost` if falling, or if crouch alpha < 0.5.
- Otherwise (standing on ground with crouch alpha ≥ 0.5), `target = InitialCrouchSpeed` (§7).
- `MaxWalkSpeed = target × M`, where M = GetMaxSpeedModifier().
  - M is native. It is > 1 on speed-modifier floors and pads, and = 1 elsewhere. The values are per level actor and UNKNOWN.
  - Normal mode sets the value directly each tick. Assist mode instead uses FInterpTo at rate 100.

**Step 5: set WallrunSpeed.**

- While wall running: `WallrunSpeed = 1650 × M + SpeedBoost`.
- In Climb: WallrunSpeed = the current vertical velocity.
- Otherwise: WallrunSpeed = MaxWalkSpeed.
- Then, if RecoveryMultiplier ≠ 1, multiply MaxWalkSpeed by it. This is a Blueprint slow-down after hits. It is reset to 1 on every hop.

**Step 6: boost decay.** This step only runs while ManualSpeedBoosting is on (or in assist mode).

- If |v| (3D) ≤ 1000: SpeedBoost = 0, and the flag turns off.
- Otherwise there is **no decay** while any of these hold:
  - the hop buffer is open (§4) and the player is wall running;
  - the hop buffer is open and the mode is None, Slide or Crouch;
  - the "conserve" window is active.
- Otherwise: `SpeedBoost *= exp(-k·dt)`, with k = **0.1 /s while wall running** and **0.8 /s in every other mode, including mid-air**.
  - dt is trimmed so decay starts exactly at the conserve end time.
  - There is a landing-frame correction so air decay stops at the exact landing time.
  - If boost becomes ≈0, it is set to 0 and the flag turns off.

### [DIFF] Changes from the existing specs

- MOMENTUM_SPEC said boost has no air decay. The boost **scalar** does decay at 0.8 /s in the air. Air horizontal **speed** is still preserved, because UE does not brake in the air. The effect is that the decayed boost lowers the cap you return to after landing.
- MOVEMENT_SPEC said the cap is "plus extra terms from speed-modifier floors". The pad term is a **multiplier** M on the 1300 base, on 1650 for wall run, and on 1600 or crouch for slide. It is not added.

### SpeedBoost sources (CONFIRMED-CODE)

1. **Bunny hop (§4).** Gain = 0.2 × (S − 1300). The total is capped at 1000.
2. **Boost pads (DoSpeedBoost, called by level Blueprints).**
   - SpeedBoost = clamp(additive ? SpeedBoost + amount : amount, 0, max), where max = 999999 if 0 is passed.
   - Also sets ManualSpeedBoosting, refills slide time (SlideTimePercent = 45) and resets the wall-run gravity clock (wallRunZPas = 0).
   - Per-pad amounts are level data. MEASURED: +500 steps on ConaptProject pads.
3. **Assist mode only.** On entering a wall run, SpeedBoost = clamp(|v| − 1200, 0, 4000).
4. **Wall-run end bonus.** Code exists (500 × wall-run duration, capped at 1000), but it is **disabled by a constant**.

### Things that clear boost

- **Crouch-fix slowdown.** This runs each tick while crouch alpha > 0.01 but the parkour mode is neither Crouch nor Slide and no crouch/uncrouch tween is running (e.g. stuck under a ceiling). It does all of the following (CONFIRMED-CODE):
  - Boost = 0 and the flag off. The conserve window, hop buffer and stored hop speed are cleared.
  - Horizontal velocity is clamped to 1300, or 1650 if wall running, or 1600 if sliding.
  - MaxWalkSpeed is clamped to the same value.
- **Respawn and level reset.**

## 3. Jump, coyote time, jump buffer

All CONFIRMED-CODE unless noted.

### Ground jump

- Stock UE jump: Velocity.Z = 800 (MEASURED). Horizontal speed is untouched.
- In mode None, Slide or Crouch on the ground, the jump also runs CheckForBunnyHop (§4).

### Coyote time

Arming:

- Any walking → falling transition in mode None, Slide or Crouch arms it. It starts a falling timer and sets FallingJumpAvailable.
- It is also armed when a wall run ends because the wall ran out (ArmCoyoteWindow).

The falling timer only advances while the vertical velocity is below 0. It resets to 0 whenever vz ≥ 0.

A coyote jump is allowed when all of these hold:

- the stock CanJump fails;
- the timer is tracking;
- **falling time < 0.33 s**;
- FallingJumpAvailable is set;
- **time since the last jump > 0.5 s**.

The tick after any jump, FallingJumpAvailable = 0 and the time since the last jump = 0.

### Jump buffer

- Pressing jump while falling and not wall running records the press time for both ground and wall-run buffering.
- **On landing:** if the press was ≤ **0.1 s** ago, the jump fires on the next tick as a buffered jump with landing-speed restore (§4).
- **On a wall-run start:** if the press was ≤ **0.1 s** ago, the jump fires on the next tick. Since it is wall running, this becomes a wall jump.
- Press times older than ~0.2 s are discarded. Press time is recorded as the press time minus half a frame.

### Other jump timing

- **Jump input pulse.** The jump input bit stays set for 0.05 s. This only matters for networking and ghost recording.
- **Jump-pad suppression.** Jumps are ignored for **0.2 s** after a pad launch (YeetJumpSuppressWindow). A duplicate pad launch within **0.18 s** is ignored.

## 4. Landing, hop buffer, bunny-hop boost, perfect hop

All CONFIRMED-CODE.

### Landing

**Real landing.** A landing counts as real for boost purposes if any of these hold:

- airborne for ≥ 0.1 s;
- peak fall speed ≥ 200 cm/s;
- crouch input in the last 0.25 s.

**Hop buffer from a landing.** On a real landing in mode None or Slide (not Crouch):

- Stored hop speed S_land = **|v| at the exact landing time, as a 3D vector** (vertical part corrected to the impact instant).
- The hop buffer opens for **0.15 s**.

**Hop buffer from a wall run.** On any movement-mode change while wall running (e.g. wall run → ground):

- S_land = |v| (3D);
- the hop buffer opens for **0.25 s**.

The landing also:

- ends any wall run or climb (no gate cooldown);
- zeroes the wall-run vertical force;
- closes the wall-run and climb gates. They reopen on the next walking → falling transition.

### Hop boost (CheckForBunnyHop)

This runs when you jump on the ground while the hop buffer is open.

- `S = max(|v| now (3D), S_land)`.
- If S > 1300:
  - `gain = 0.2 × (S − 1300)`.
  - **Same-contact guard (Slide/Crouch only):** skip if the last hop boost was in the same mode, less than 0.12 s ago and within 150 cm (1.5 m).
  - Otherwise: `SpeedBoost = min(1000, SpeedBoost + gain)`.
  - Set ManualSpeedBoosting.
  - Open the conserve window: no decay for **0.5 s**.
- RecoveryMultiplier is reset to 1.
- The buffer and S_land are then cleared.
- The edge-chain auto-hop is disabled by a constant.

**[DIFF]** MOMENTUM_SPEC says "perfect hop / hops give 0 gain". Hops **do** add boost: 20% of the 3D excess over 1300 per buffered hop. The ghosts confirm this. Landings at H ≈ 1300 with 3D speed ≈ 1491, followed by an immediate jump, take off at **1342** (predicted 1338).

### Buffered-jump landing restore (RestoreLandingFrameSpeed)

This applies only to jumps pressed **before** landing (§3 buffer). It runs on the next tick.

**At landing, store:**

- the landing horizontal velocity V_L (2D);
- cap C = the MaxWalkSpeed at landing.

**Next tick, with V_now the current 2D velocity** (already braked once by UE), skip the restore if any of these hold:

- |V_now| < 1;
- |V_L| ≤ |V_now|;
- dot(dir V_L, dir V_now) < 0.9;
- respawning, a recent pad launch, or not on the ground.

**Otherwise compute the candidate speed:**

`R = min(|V_L|, |V_now| + 1.25 · dt · (K·|V_L| + B))`

This undoes about 1.25 ticks of braking. K = 8 and B = 2048 (§1).

**Then apply:**

- If R ≤ C: no restore.
- If R ≤ 1.01·C: R = C, graded as perfect.
- Otherwise: `R = max(R − loss, C)`, with loss from the perfect-hop grading below.
- Set horizontal velocity = dir(V_now) × R. Vertical is unchanged.

### Perfect-hop grading

**Earliness.** e = landing time − the jump press time. Only presses within (landing − 0.1 s − dt, landing] count.

**Grade.**

`grade = 1 − clamp((e − 0.034) / 0.066, 0, 1)^0.6`

- e ≤ **0.034 s** gives grade 1 (perfect).
- e ≥ 0.1 s gives grade 0.

**Base loss.**

`baseLoss(R) = max(0, 29 + (R − 2600) × 32/900)`

- This is a line through (2600, 29) and (3500, 61), **extrapolated** both ways.
- So the loss starts at **R ≈ 1784** and keeps rising linearly above 3500 (e.g. ≈207 at 7600).
- The separate 3500→7600 segment exists but is disabled by a constant.

**Loss.**

`loss = baseLoss × (1 − grade)`

**[DIFF]** MOVEMENT_SPEC said: no penalty below 2600; a separate top band; and "late jumps not graded". In fact the penalty starts at ~1784, and grading only exists for jumps pressed before landing. A jump pressed after landing is simply a normal jump inside the 0.15 s hop buffer. It gets no restore, so the ground braking of the frames spent on the ground applies.

## 5. Air

- **Input acceleration** is AirControl × MaxAcceleration = **3500 cm/s²** (MEASURED). It cannot raise H above MaxWalkSpeed (1300 + boost, or the slide value while still in Slide mode).
- **Above the cap**, speed is kept (no drag) and input only turns it. Turn rate ≈ 3500 / H rad/s (INFERRED from UE falling physics).
- **Slide mode persists in the air** after a slide jump while crouch is held. MaxWalkSpeed is not updated in the air in that mode, so it stays at the last ground slide value (1600 + boost). This is the air-control cap during a slide jump.
- **Releasing crouch in the air** ends the slide:
  - MaxWalkSpeed = min(current, 1200) + boost;
  - the next tick sets it back to 1300 + boost.

## 6. Slide

All CONFIRMED-CODE unless noted.

### Starting a slide

**Can slide** when both hold:

- forward input component > 0 (input · actor forward);
- |v| (3D) > **700**. Interlude mode uses 640 instead.

**On crouch press:**

- On ground and able to slide: SlideStart.
- On ground and not able: Crouch.
- In the air and able to slide: queue the slide for the landing.
- In the air otherwise: queue a crouch for the landing.

**SlideStart:**

- Mode becomes Slide. SlideTimePercent = **45**.
- **Boost downgrade:** if SpeedBoost ≥ 300, the flag "downgrade" is set. It costs 0 boost. Its only effect is that a "steep" slope class is treated as "medium".
- If |v| < 1200, MaxWalkSpeed is set to min(1.5·|v|, 1400) + boost for one frame only. SlideUpdate overwrites it on the same or next tick.

### Each tick in Slide mode (SlideUpdate)

**Ground probe:**

- A line trace 50 cm down. If that misses while not falling, a fallback trace 120 cm down is used.
- A "slippery" object trace 120 cm down is also used.
- Surfaces tagged as infinite-slide are excluded.

If there is no ground, nothing changes except the slide sound.

**Slope class.** d = dot(floor normal, actor forward):

| d | Class |
|---|---|
| ≤ -0.5 | Steep upslope |
| ≤ -0.25 | Medium upslope |
| ≤ -0.1 | Gentle upslope |
| < 0.1 | Flat |
| ≥ 0.1 | Downslope |

**SlideTimePercent.** It moves toward a target at a constant rate in percent per second (FInterpConstantTo):

| Class | Target | Rate (%/s) | Effect |
|---|---|---|---|
| Flat, Gentle up | 0 | 50 | From the start value 45, full-speed slide on flat lasts ≈ **0.8 s** (until ≤ 5 %). |
| Downslope | 100 | 20 | Refills; slides last indefinitely downhill. |
| Medium up | 0 | 100 | |
| Steep up | 0 | 300 | Treated as medium if the boost downgrade is active. |

On a speed-modifier floor (M > 1), SlideTimePercent is forced to 45.

**Slide max speed:**

`MaxWalkSpeed = (base + SpeedBoost) × M`

- base = **1600** while SlideTimePercent > 5, otherwise InitialCrouchSpeed (500).
- base is also 1600 on a modifier floor.

**Speed behaviour:**

- Speed rises from 1300 to 1600 by normal ground acceleration. This needs forward input, which CanSlide demanded anyway.
- With no input, UE braking applies. That is INFERRED from the code; there is no input data in the dev ghosts, and Riley always holds forward.

**End conditions:**

- **|v| ≤ InitialCrouchSpeed + 50 (≈550):** the slide ends into Crouch.
- **Crouch released:** the slide ends into None, and MaxWalkSpeed = min(current, 1200) + boost.
- **Jump:** the slide continues in the air (see §5).

**Strafe input** while sliding is scaled by **0.3**.

### Ghost checks (MEASURED)

- Every ground slide longer than 1 s in the data is on a downslope (dz ≈ −45 cm per 0.05 s). On those, H stays at exactly 1600, or 1600 + a decaying boost (e.g. 1767 → 1631 over 2.2 s, k ≈ 0.77/s).
- Flat ground slides are all shorter than 0.8 s.
- Entry: 1300 → 1568 → 1600 within 0.1 s.

**[DIFF]** MOVEMENT_SPEC described the 20/100/300 numbers as "deceleration targets in cm/s²", and said slides hold 1600 without decay. In fact they are the percent-per-second rates of a slide **timer**:

- 50 %/s on flat, gentle up, and as the default;
- 20 %/s on downslope, toward 100;
- 100 %/s on medium up;
- 300 %/s on steep up.

When the timer runs out, the cap drops to crouch speed and UE braking slows the player into a crouch. The spec's 1200 "end threshold" is also wrong. 1200 only appears in the cap applied when crouch is released, and as the bound for the one-frame start formula. The real end threshold is crouch speed + 50.

## 7. Crouch

- **Crouch speed.** MaxWalkSpeed = InitialCrouchSpeed × M.
  - InitialCrouchSpeed = the CMC MaxWalkSpeedCrouched default, **500** (MEASURED; CONFIRMED-CODE that it is read from the component at start).
  - The class property crouchSpeedFactor 0.5 is unused.
- **Capsule and camera.**
  - The capsule half-height lerps from 96 to the crouched half-height. The camera offset Z lerps from −100 to −50.
  - The tween takes **0.15 s** each way (CONFIRMED-CODE). Capsule half-height after a full reset is 88.
- **Standing up.**
  - Standing back up is blocked while a sphere cast above hits something.
  - The re-check then repeats on a 1 s tween until it is clear (INFERRED).
  - On ground, standing with crouch alpha ≥ 0.5 keeps the crouch speed (§2).
- **No wall run right after uncrouch.** A wall run is refused if the previous mode was Slide or Crouch and a 300 cm down-trace finds ground **35–80 cm** below.

## 8. Wall run

All CONFIRMED-CODE unless noted. The wall-run update runs every tick while the wall-run gate is open, or while a direction is blocked (see "Opposite direction" below).

### Can wall run

All of these must hold:

- falling;
- forward input component > 0;
- mode is None, WallRun or Climb, or Slide/Crouch with crouch alpha < 1;
- not pressing away from the wall (see "Pressing away" below);
- crouch not held;
- the fall-death predictor is not triggered;
- the uncrouch rule (§7) does not apply;
- the gate is open.

**Opposite direction.** When the gate is closed but a direction is blocked, the same test runs, but only the non-blocked side may start.

### Wall detection

- Line traces from the actor to ±75 cm (0.75 m) along the actor's right vector. The right side is tried first (→ RightWallRun, side −1), then the left (→ LeftWallRun, side +1).
- **Wall normal:** |normal.z| ≤ **0.52** (within ~31° of vertical).
- **Obtuse check:** when chaining onto a new wall, the new normal must differ from the current one by ≥ **30°** in the allowed turning direction.
- **Velocity alignment at entry.** This is checked unless already in Climb or WallRun.
  - wallDir = normalize(cross(normal, Up)).
  - It must satisfy dot(wallDir × side, normalize(v 3D)) ≥ **0.5** (BackWallrunTolerance). That is within 60° of the along-wall direction.
  - The full 3D velocity is used, so falling fast reduces the dot.
  - Coming from Climb, the alignment is not checked.

### Velocity each tick (LaunchCharacter, horizontal overridden, vertical overridden)

```
alongWall = cross(normal, Up) × side × WallrunSpeed × mult.X × (forwardAxis > 0 ? 1 : 0)
intoWall  = −100 × normal
vz        = clamp(mult.Z × wallRunZForce, −1500, ZForceMax)
v         = alongWall + intoWall + (0, 0, vz)
```

- **WallrunSpeed = 1650·M + SpeedBoost** (§2). The along-wall speed is set immediately; it does not accelerate.
  - On the very first tick it is still the previous MaxWalkSpeed (1300 + boost). From the second tick it is 1650 + boost.
  - MEASURED: held H = 1653 at boost 0; wall-run speed = 1650 + (entry H − 1300).
- **ZForceMax** = 1500. It is reset on every mode change.
- **Top-edge check.** If a parallel trace offset by (0, 0, 3) cm misses the wall, ZForceMax = 0. That means no further rising above the wall's top edge.
- **mult = (1, 1, 1)** normally. See §9 for the climb → wall-run multiplier.
- The wall normal, side and attached moving platform are stored.

### Vertical force (SetWallRunGravity)

Gravity scale is 0 while wall running.

```
τ += dt / 1.5            (wallRunZPas; WallRunTime = 1.5 s)
c  = curve(min(τ, curve end))
wallRunZForce = 200 − 600·c          (maps c 0→1 to +200 → −400 cm/s)
```

The curve is a Blueprint curve asset (UNKNOWN keys). MEASURED from 1397 wall runs (median vz):

| t (s) | 0–0.30 | 0.40 | 0.50 | 0.60 | 0.70 | 0.80 | 0.90 | 1.00 | 1.10 | 1.20 | 1.30 | 1.40 | ≥1.50 |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| vz | 200 | 197 | 189 | 175 | 155 | 125 | 87 | 38 | −24 | −98 | −187 | −294 | −400 |

Good fit (MEASURED): **c(τ) = ((τ − 0.2) / 0.8)^2.4 for τ > 0.2, otherwise 0**.

- **[DIFF]** The wall run does **not** end at 1.5 s. After τ = 1 the player keeps sliding down the wall at −400 until another exit condition.
- WallrunGravityTarget 0.5 is never read.
- The clock τ resets when a wall run ends (any exit) and on boost pads.

### Pressing away from the wall

- LeftWallRun: opposed when right input > 0.2.
- RightWallRun: opposed when the left or right axis is < −0.2.

### Exit conditions

**1. Cannot wall run any more:**

- **Forward released while wall running, or pressing away:**
  - If pressing away while wall running, the stored wall-jump data is saved (see below).
  - The wall run ends with the current side **blocked**. The gate is closed for **0.8 s**.
- **Any other failure:** the wall run ends. The gate is closed for 0.2 s (0.8 s if a parkour-blocking combo is active).

**2. Can wall run, but neither side trace hits:**

- If wall running, the code first tries a direct "player-to-wall" trace of 80 cm along the last normal, and continues if it hits.
- Otherwise it tries a climb (§9).
- Otherwise the wall run ends with the side blocked, the gate closed for **0.2 s**, and the **coyote window armed** (§3).

**3. Landing:** the wall run ends with no cooldown.

**4. Jump:** wall jump.

### Stored wall jump

**Stored on:** release or pressing away. The normal, side, climb flag, WallrunSpeed and platform velocity are kept.

**Used:** a jump while falling in mode None performs the stored wall jump when both hold:

- stored data exists;
- FallingJumpAvailable is set.

It blocks that side and closes the gate for 0.35 s.

**Cleared:** once the downward falling time is ≥ **0.33 s**, or on landing.

### Wall-run boost decay

- Boost decays at 0.1 /s while wall running.
- It does not decay inside the 0.25 s hop buffer.

## 9. Wall climb and climb → wall run

All CONFIRMED-CODE unless noted.

### Entry

Entry is checked inside the wall-run update. It needs the same CanWallRun conditions (falling, forward input, etc.). Climb is tried:

- when neither side wall is found; or
- first, when "force climb" applies: a forward trace of 80 cm hits a wall that the actor faces with |dot(forward, normal)| > **0.85**.

Climb trace:

- forward 80 cm from the actor;
- normal valid as in §8 (|n.z| ≤ 0.52);
- falling.

### Velocity each tick (horizontal and vertical override)

- Horizontal = −100 × normal: pushed into the wall at 100 cm/s.
- Vertical = wallRunZForce. In Climb the mapping is **+400 → −400** over the same clock and curve (1.5 s).

MEASURED: vz = 400 for 0–0.3 s, 387 at 0.5 s, 307 at 0.8 s, 225 at 0.95 s. H ≈ 128 for the first frames.

**[DIFF]** The script default WallClimbSpeed 300 and the old "WallClimbMovement" routine (−600 into the wall) are never called. The 400 comes from the force mapping.

### Exit

- **Forward trace misses (top of the wall):** the climb ends and the **wall-run gate closes for 1.0 s**. Vault/ledge detection (§10) runs every tick before this, so a ledge usually triggers a vault first.
- **Jump during a climb:** wall jump, using the climb normal as the direction (§11).

### Climb → wall run

When a side wall is found while in Climb:

- The along-wall multiplier starts at **mult = (X 0.1, Y 1, Z 1)**.
- It lerps to (1, 1, 1) with alpha += dt × 1.0, so it takes **1.0 s**.
- The along-wall speed therefore ramps from 10 % to 100 % of WallrunSpeed over 1 s. The vertical force is unchanged.
- The multiplier resets when the lerp completes.

**[DIFF]** The component order is now confirmed as (X 0.1, Y 1, Z 1). The earlier spec had guessed (1, 1, 0.1).

## 10. Vault (and "mantle")

All CONFIRMED-CODE unless noted. The legacy mantle (mode 5 position interpolation with QuickMantle 30 / Normal 8) is dead code: its gate is never opened. All "mantle" behaviour is the vault system.

### Detection (every tick while the climb gate is open)

Conditions:

- forward input component ≥ 0;
- falling;
- mode None, Climb or WallRun;
- crouch not held.

Pressing jump during a wall run or climb also runs a native "near-mantle" check first. If that check passes, the jump becomes a vault.

**Capsule sweep:** radius 20 cm, half-height 10 cm.

- From: eye point + (0, 0, 50) + forward × 50.
- To: (actor X, Y; actor Z − capsule half-height + MantleHeight **40**) + forward × 50.

In other words, the sweep goes down a column 50 cm ahead, from 50 cm above the eyes to 40 cm above the feet.

**The hit must be:**

- walkable;
- impact normal.z > **0.88** (≤ ~28° tilt).

**Further checks:**

- Line checks at +95, +5 and +200 cm.
- A capsule fit test at the destination (bDoMantleFitCheck, AlwaysDoMantleLineTraceChecks).

**Destination:** the hit location + (0, 0, capsule half-height).

### Vault start

- Mode becomes 5. Movement is disabled. Gravity scale = 0.
- The wall-run and climb gates are closed. Any wall run is ended.

**Size class.** h = destination Z − start Z:

| Class | Height h | Base duration | Momentum multiplier | Speed threshold min–max | Duration scale min |
|---|---|---|---|---|---|
| Small | < 40 cm | 0.05 s | 0.95 | 600–1400 | 0.5 |
| Medium | 40–110 cm | 0.15 s | 0.80 | 700–1400 | 0.5 |
| Large | ≥ 110 cm | 0.40 s | 0.70 | 800–1300 | 0.6 |

**Pre-vault speed:** s = |mean of the velocity samples over the last **1.0 s**| (the velocity history).

**Duration:**

- If s > thresholdMin: `duration = base × lerpClamped(s: thrMin→thrMax ⇒ 1→scaleMin)`.
- Otherwise: duration = base.

**Motion:**

- Position is lerped from start to destination over the duration.
- The ease index is 0 (default; custom easing is off).
- The endpoints are attached to the obstacle so it follows moving platforms.

### Vault end

- **If |stored average velocity| ≥ 300:**
  - `v = normalize(dest − start) (3D) × |avg velocity| × multiplier`.
  - Because the direction is 3D, part of the speed goes upward.
- **Otherwise:** no velocity is applied.
- Movement mode is restored: walking or falling, by the previous mode. Gravity is restored.

**[DIFF]** MOVEMENT_SPEC said "exit speed never below 300". In fact 300 is the **minimum to keep momentum at all**. The direction is the vault path, not the facing. The base durations (0.05 / 0.15 / 0.40 s) are new.

**MEASURED:** vault segments last 0.05 / 0.2 / 0.4 s (p10/50/90). Mean speed is 813 before and 566 just after. H reaches 1300 three samples later.

## 11. Wall jump

All CONFIRMED-CODE. A wall jump is a jump pressed while wall running or climbing, a buffered jump on wall-run start, or a stored wall jump.

**1. End the wall run.** The side is blocked and the gate closes for **0.35 s**. The opposite side stays allowed.

**2. Look test.**

- `look` = the full control-rotation forward vector (3D, including pitch).
- `q = clamp(dot(look, wallNormal), 0, 1)`.
- `thr = MinimumWallJumpAngle / 90`. That is 30/90 = **0.333**; assist mode uses 20/90.

**3. Direction.**

- If q ≥ thr: dir = normalize(look). The look vector is used as is, including pitch.
- Otherwise: dir = the **minimum-angle direction**, which is horizontal. It is the along-wall run direction rotated **30°** away from the wall:
  - cross(rotateZ(normal, side > 0 ? 30° : 150°), Up).
- After a climb, dir = the climb wall normal (straight off the wall).

**4. Speed.** `speed = max(WallRunJumpOffForce 1400, WallrunSpeed)`. Assist mode uses WallrunSpeed × 1.1 in the max.

**5. Final velocity** (horizontal and vertical overridden):

`v = (dir.x·speed, dir.y·speed, **750**) + platform velocity`

**Consequences:**

- Horizontal = speed × the horizontal length of dir. **Looking up or down reduces horizontal speed by cos(pitch).**
- With boost 0: H ≈ 1650 when looking level.

**MEASURED:** post-jump H median 1650 (p10/p90 1638/1658). vz is 679 one sample later (750 minus partial gravity).

The wall-end bonus is disabled.

## 12. Zipline / ledge grab (mode 4)

All CONFIRMED-CODE.

- **Jump off a non-zipline ledge grab:** `v = (forward.x × 100, forward.y × 100, **1000**)`, with horizontal and vertical overridden.
- **Jump off a zipline:** `v = normalize(zipline exit direction) × (zipline factor × 2000)`.
- **Leaving a zipline without jumping:** `v = normalize(end − start) × 2000`.
- In all cases the wall-run gate closes for 0.3 s.

**[DIFF]** LedgeGrabJumpHeight 400 and LedgeGrabJumpOffForce 300 are unused.

## 13. Moving platforms

- Platform velocity = 0.6 × (Δ platform position / dt). Its magnitude is clamped to ≤ **1000** (CONFIRMED-CODE).
- It is added to the wall-jump velocity.

## 14. Camera (reference only; Riley has locked the camera)

All CONFIRMED-CODE.

- **FOV:** user FOV + 10 while sliding. It is interpolated with FInterpTo at rate 10.
- **Camera height:** 0.3 × capsule half-height while sliding, otherwise 0.6 ×. It is interpolated at rate 10.
- **Roll:**
  - Wall run: roll = WallrunRollTarget(±6°), blended by the vertical force. The force's range +200 → −400 maps to a blend of 0 → 1.
  - Other modes (CameraTick switch): 15, 4 or 0.8 × the head-tilt value. Which mode gets which value was not mapped with certainty (INFERRED).
  - Head tilt from turn input: curve × setting, clamped ±3, scaled by speed up to 1300.
  - Interpolation uses RInterpTo at rate 7.
- **Wall-run yaw auto-align:**
  - Lerp rate 8 when turning toward the wall direction, decaying to 3 at 6/s.
  - It is skipped when turn input is > 0.01, or when the yaw error is ≤ −40° or ≤ 0.
- **Running shake:** strength = |v| / 1000.
- **Landing shakes:** |vz| < 800, < 1500, < 2100, or ≥ 2100. There are separate shake assets for crouched landings.

---

## 15. Ghost event statistics (MEASURED)

These are from `traces/event_summary.csv`. They cover all 128 ghosts. H values are medians in cm/s. Durations are p10/p50/p90 in s.

| Event | n | H before → at | Notes |
|---|---|---|---|
| Ground jump | 2803 | 1300 → 1300 | vz 738 at the first sample; airtime 0.45/0.75/1.95 |
| Slide jump | 1382 | 1608 → 1629 | vz 729 |
| Landing on run | 3763 | 1349 → 1300 | Ground time 0.05/0.5/2.15 |
| Landing in slide | 984 | 1670 → 1660 | |
| Slide start | 1377 | 1301 → 1568 → 1600 | Duration 0.3/0.6/2.5 |
| Wall-run start | 1778 | 1600 → 1653 | vz 200; duration 0.05/0.25/0.75 |
| Wall jump | 1547 | 1653 → 1650 | vz 679 one sample later |
| Climb start | 235 | 1300 → 129 | vz 400; duration 0.05/0.15/0.6 |
| Vault | 390 | 813 → 0 (during) → 566 at exit | Duration 0.05/0.2/0.4 |

**Hop gain check.** These are jumps within 0.16 s of a run landing, grouped by landing H, with the 3D landing speed shown:

| Landing H | 3D landing speed | Takeoff H |
|---|---|---|
| 1300 | 1491 | 1342 |
| 1400 | 1557 | 1433 |
| 1500 | 1653 | 1559 |
| 1800 | 1933 | 1601 |
| 2000 | 2065 | 1862 |

## 16. Gaps / UNKNOWN

- **Blueprint CDO values** are only measured:
  - MaxAcceleration 10000;
  - the GroundFriction / BrakingFrictionFactor split (the product of 8 is solid);
  - BrakingDecelerationWalking ≈ 2048;
  - JumpZ 800;
  - crouch speed 500;
  - eye height.
- **The wall-run gravity curve keys** (asset). The measured table and fit are given above.
- **Native C++** (not script):
  - GetMaxSpeedModifier pads and floors;
  - wind-zone forces;
  - wall-slide parameters;
  - the near-mantle check used on jump;
  - jump-pad launch (the script solves the arc; the native side applies it);
  - ForceDoJump.
- **The CharaBP Blueprint graph** may add logic not in script, e.g. slide friction. Nothing in the ghosts suggests that it does.
- **Slide with no forward input.** No ghost data. The code implies stock braking.

## Tooling (all under `_decomp`, outside the repo)

**Disassembler and decoders (`vholume\disasm\`):**

- `dis.py`: a disassembler for the AngelScript 2.3x opcode table, with these additions:
  - CALLSYS takes a 64-bit pointer;
  - op 0xBA (1 dword) pops a pointer into a variable;
  - op 0xC8 (3 dwords) is a pointer call;
  - ops 0xD1 and 0xD3 test a variable into the register.
- It resolves:
  - script call IDs, through the function table near the end of the cache;
  - system functions, through pointer/name records;
  - type IDs, through the type table;
  - member offsets, through `move_layout` and `char_layout`;
  - native CharacterMovement offsets, through `exe\cmc_offsets.txt` (parsed from the exe's property tables).
- It finds 3082 functions.

**Generated listings (`vholume\disasm\`):**

- `listing.txt`: the raw listing.
- `pretty.py`: a stack/expression folder that writes `pretty_all.txt`.
- `npm.txt` and `npc.txt`: the movement and character subsets.

**Ghost analysis (`vholume\scripts\`):**

- `gload2.py`: the loader. It handles both ghost formats and the input bits: fwd 0x1, back 0x2, right 0x4, left 0x8, crouch 0x40, jump 0x200.
- `traces.py`: writes the per-run CSVs and events to `traces\runs\*.csv`, plus `events_all.csv` and `event_summary.csv`.
- `fitcmc.py`: the CDO fits, written to `traces\cmc_fit.txt`.
- `plots.py`: writes `traces\plots\*.png`.
