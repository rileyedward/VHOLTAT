# VHOLUME momentum: how speed above the caps is gained and lost (clean-room draft)

Plain-language behavior plus numbers. There is no code. Units are cm/s (÷100 gives m/s), and each sample interval is 0.05 s.

## Sources and tags
- **C-ghost:** measured from the 93 shipped developer ghost runs (≈157k samples).
- **C-data:** values read from the shipped script data.
- **INF:** inferred from names and numbers.

All "rates" below are fitted to the ghost traces.

---

## 0. The core model (read this first)

VHOLUME does **not** build momentum through physics. Slopes, air strafing and hop timing do not add speed.

Instead, horizontal speed is always:

> **speed cap = base speed of the current state + SpeedBoost**

**SpeedBoost** is a single scalar that persists across state changes. It is gained only from explicit sources (§1) and decays exponentially by state (§2).

Base speeds by state:

| State | Base speed | Source |
|---|---|---|
| Run | 1300 | C-data + ghost |
| Crouch | 500 | ghost |
| Slide | 1600 | C-data SLIDE_SPEED + ghost |
| Wall run | 1650 | C-data WALLRUN_SPEED + ghost |
| Air | No cap. Horizontal speed is simply preserved. | ghost (median dH/dt = 0 in air) |

What happens on a state switch:
- The player's speed is re-targeted to **new base + current boost**, essentially immediately (within one 0.05 s sample).
- Going **into a higher-base state** gives an instant gain equal to the base difference:
  - run → slide: +300
  - run or air (from run) → wall run: +350
- Going **into a lower-base state** gives an instant loss:
  - wall run → land on run: −350
  - slide → stand up: −300

The difference between bases is **not** boost. Only true boost survives a downgrade.

This is the most likely reason the playtester feels momentum "isn't gaining". The fix:
- keep a separate boost value;
- add it on top of every state's base;
- carry it through transitions;
- in every grounded/wall state, clamp speed to base + boost each frame;
- never clamp in the air.

### Verification from the ghosts
**Wall-run entry, by entry speed.** Held wall-run speed = 1650 + (entry speed − 1300):

| Entry speed | Held wall-run speed |
|---|---|
| 1300 | 1653 |
| 1384 | 1731 |
| 1493 | 1831 |
| 1674 | 2026 |
| 1600, coming out of a slide (boost 0) | 1651 |

**Wall jump, then land on the run.** Landing speed = 1300 + (air speed − 1650):

| Air speed | Speed after landing |
|---|---|
| 1916 | 1567 |
| 1731 | 1378 |
| 1650 | 1300 |

**Run → slide.** Slide speed = entry speed + ~300, minus a little boost decay:

| Entry speed | Change |
|---|---|
| 1300 | +300 |
| 1400–1500 | +286 / +276 |
| 1600–1700 | +258 |

---

## 1. Where speed above 1650 is gained (ghost event census)

These are sample-to-sample increases where the new horizontal speed is above 1650. Teleports are removed.

| Situation (before → after) | Events | Typical gain per event | Notes |
|---|---|---|---|
| Air → air, in level wind or fan volumes | 794 | +163 per sample, i.e. **≈+3300 cm/s² continuous** (p90 +215/sample) | 92% of these are on wind/fan and machine levels (windfans 356, Dam 155, dreamland 107, Mechanisms 76). Level hazard, not a player skill. |
| Run → run on ground (speed-modifier floor pads) | 118 | **+500 per pad, as a step** (median 448 because some are caught partway; p90 500, max 544) | Mostly ConaptProject pads. Chains of pads stack: 1300 → 1800 → 2300 → … The ground maximum observed was 4641. |
| Air → wall run (entry) | 211 | **+350 (the 1650 − 1300 base swap)**; +46 to +77 if entry is already ≥1650 | Wall-run speed is 1650 + boost. |
| Ground contact → wall run (brushing the wall from the ground) | 58 | +311 to +353 | Same base swap. |
| Run → slide | ~430 | **+300** (base swap) | §3 |
| Slide → slide on downhill ground | 76 | Median +208/sample | **Not slope physics.** On every slope bin (−40° to +10°), ground slide horizontal speed stays at 1600 + boost and run stays at 1300 + boost (median dH/dt = 0 per slope bin). These gains are boost pads on slide surfaces or landings. |
| Slide in air → slide in air | 139 | +128/sample (≈+3500 cm/s²) | Fan/wind volumes again (test01 42, Showers 18, …) |
| Wall run → air (wall jump) | 18 | Usually 0. The jump keeps 1650 + boost (direction rotates ≥30° off the wall). | No speed bonus on a wall jump. ENABLE_WALLRUN_SPEEDBOOST_ON_WALLRUN_END = false. |
| Perfect bunny hop | — | **0 gain.** A perfect hop only avoids a loss (§4). | |
| Jump pads | rare | **Velocity override** from an arc solver, so arbitrary speed | Source of the extreme peaks |

**Maximum horizontal speed per level (ghosts, teleports excluded).** Most levels top out at 1.7–2.7k. 4–7.5k is reached only on pad, fan or wind levels.

| Level | Max |
|---|---|
| Demo00 Conapt | 2026 |
| Demo02 Fastlife | 5291 |
| Demo03 Sewers00 | 1747 |
| Demo04 Sewers01 | 1742 |
| Demo07 Showers | 2197 |
| Demo10 Dam | 4319 |
| Apogee 00 | 3345 |
| Apogee 01 | 2708 |
| Apogee 02 | 16349 (launcher) |
| ConaptProject 04 | 1744 |
| ConaptProject 05 | 1839 |
| ConaptProject 06 | 4641 |
| ConaptProject 08 | 3322 |
| ConaptProject 09 | 1786 |
| Foire | 2331 |
| LaCiudadBaja | 2271 |
| Mechanisms 01 | 2707 |
| Mechanisms 02 | 6768 |
| OpenOffice 00 | 1653 |
| OpenOffice 01 | 3197 |
| dreamland | 7527 |
| fastlife00 | 1780 |
| fastlife02 | 1909 |
| joan00 | 2662 |
| racetrack | 2264 |
| roseau | 2054 |
| test00 | 2012 |
| test01 | 5141 |
| test02 | 2305 |
| test04 | 2791 |
| windfans | 5001 |

## 2. How boost decays (fitted; units resolved)

The decay constants in the data are **exponential rates per second** applied to the boost, which is the excess over the state's base:

> boost(t + dt) = boost(t) × e^(−rate × dt)

| State | Rate | Constant name (C-data value) | Ghost fit | What it means |
|---|---|---|---|---|
| Run on ground | **0.8 /s** | BUNNY_HOP_GROUND_DECAY_SPEED = 0.8 | log-rate −0.80 /s in every excess band 60–1600; linear equivalent ≈ −0.8 × excess, e.g. −400 cm/s² at +500 | Half-life ≈ 0.87 s. +500 → +225 after 1 s → +100 after 2 s. |
| Slide on ground | **0.8 /s** (same) | — | −0.80 /s | Applies to the boost on top of the 1600 base. |
| Wall run | **0.1 /s** | SPEEDBOOST_WALL_RUN_DECAY_SPEED = 0.1 | median −0.09 /s over 48 wall runs | Boost is almost fully kept through a wall run. |
| Air (any) | **0** | — | Horizontal speed constant | No decay in air. |

The base itself never decays:
- Run holds 1300.
- Slide holds 1600 indefinitely (§3).
- Wall run holds 1650.

## 3. Slide specifics
- **Starting a slide while fast.**
  - From the ground: speed = entry + 300. Boost is kept and the base goes 1300 → 1600.
  - Landing into a slide from the air at ≤1300: +300.
  - Landing into a slide from the air at ≥1600: you **lose** down to 1600 + boost (−43 to −205 typical). The air speed's "wall-run base" part is dropped.
  - So **reslide adds nothing beyond the base swap.** There is no extra start boost on top.
- **Slide-start numbers in the data** (C-data, roles INF):
  - 1200 (also the slide-end literal), 1400, 1.5, 45°, 300.
  - SLIDE_BOOST_DOWNGRADE: on, threshold 300, steep-only, cost 0.
  - Practical effect matches "slide = 1600 + boost". The downgrade costs 0, so it changes nothing.
- **Time limit.** None observed. Ground slides hold exactly 1600 for up to **3.25 s**, the longest in the data, with no decay of the base. Only the boost on top decays, at 0.8/s.
- **Slope.** Horizontal slide speed is unchanged on downhill (−40°) and slight uphill slopes.
  - The upslope "deceleration targets" (20 flat, 100 medium, 300 steep) were not visible in the data, which has few uphill slides. INF: they apply only on steep upslopes.
- **Ending.** Releasing crouch drops speed to 1300 + boost. About 80% of slides end in a jump, and slide-jumps keep 1600 + boost in the air.
- **Slide-hop chains.** Land in a slide and jump again: speed kept at about 0 change (median −0 to −53). The air speed returns to 1600 + boost every hop, so chains preserve speed but do not grow it.

## 4. Hops, landing restore and perfect-hop loss in practice

**Landing on the run and jumping again within 1–5 samples (≤0.25 s):**
- Landing at 1200–1600: about 0 change.
- Landing at 1600–2000: **−240 to −330**. This is the drop from the 1600/1650 base to 1300 + boost; the boost part is kept.
- Landing above 2000: larger losses.

**Landing in a slide** keeps speed (see §3).

**Hop buffer.**
- Within 0.15 s of landing (0.25 s if the next contact is a wall run), jumping restores the **landing-frame speed** (BUNNY_HOP_RESTORE_LANDING_SPEED on). That means no ground decay during the buffer.
- Boost conserved through the buffer: 0.2 × boost, at most 1000 (C-data).
- The conserve window is 0.5 s.

**Perfect-hop loss (C-data):**
- Applies only when the landing speed is ≥2600.
- Window: 0.034 s after landing (half-frame corrected).

| Hop outcome | Speed below 3500 | Speed at or above 3500 (up to 7600) |
|---|---|---|
| Perfect | lose 0 | lose 0 |
| Missed | lose 29 | lose 61 |

- The curve power of 0.6 interpolates the loss between 2600, 3500 and 7600.
- Units are INF: cm/s removed per hop, applied as braking.
- In the ghosts, only a handful of hops are above 2600, so this is untestable there. It only matters on pad levels.
- **There is no speed gain for perfect hops.**

## 5. What to implement (concrete rules)

1. **Boost scalar.** Keep a SpeedBoost value of at least 0. Each tick, cap = base(state) + boost:
   - run 1300
   - crouch 500
   - slide 1600
   - wall run 1650
   - In the air, apply no cap and no drag.
2. **On entering a state,** set horizontal speed to base(new) + boost. Exceptions:
   - In the air, keep the current velocity.
   - On landing, take the min of the current speed and base + boost, unless a hop happens within the 0.15 s buffer; then restore the landing-frame speed.
3. **Boost sources:**
   - Speed pad / floor modifier: +500 per pad (stacks).
   - Wind or fan volume: about +3300 cm/s² of horizontal acceleration while inside.
   - Jump pad: velocity override.
   - If you want skill-based gain, you would have to invent it; VHOLUME's developers don't gain speed from hops, slopes or strafing.
4. **Boost decay (exponential per second):**
   - ground (run or slide): 0.8
   - wall run: 0.1
   - air: 0
5. **Bases not boost:** the +300 slide and +350 wall-run jumps are base swaps. They vanish when returning to run.
6. **Perfect hop (optional, at ≥2600):** a miss costs 29 below 3500 and 61 above; a perfect hop costs 0.
7. **Crouch-fix caps (C-data, enabled):** speeds above 1300 / 1600 / 1650 are clamped in crouch / slide / wall run when the crouch-fix triggers. Crouch also clears the boost (CROUCH_FIX_CLEAR_SPEEDBOOST = true; INF: only for a plain crouch, not a slide).

## 6. Camera feel (C-data numbers; roles INF unless stated)

**FOV / position function numbers:** 10, 5, 10, 20, 10, 10, 0, 10.
- INF: interpolation speed 10, +5 FOV when running fast, +10 to +20 FOV for wall run / slide.
- The base FOV is in the Blueprint and is UNK.
- Raising the hands blocks FOV changes.

**Wall-run camera roll.**
- Tilt amount 6 (deg, INF).
- Head-tilt look input ±3 deg from strafe input.
- Roll interpolation numbers 15 / 4 / 0.8.
- Tilt scales with wall-run vertical force over the range +200 to −400.
- Speed-factor reference 1300.
- Yaw auto-align toward the run direction: rate numbers 0.25, 3, 8, 6, −40.

**Crouch / slide camera.**
- Crouch tween is 0.15 s down and 0.15 s up.
- Crouch tick numbers: 96 (standing height reference), −50, −100, −20 (INF: eye offsets in cm).
- Capsule half-height is 88.

**Shakes.**
- Running shake starts above 1000 cm/s.
- Landing shake has three tiers at impact speeds of 800, 1500 and 2100 cm/s.

**Speed blur (radial).**

| Parameter | Value |
|---|---|
| Starts at | 0.1 of the effect range |
| Intensity | 1 |
| Falloff power | 4 |
| Samples | 8 |
| Velocity smoothing | 0.01 |
| Chromatic aberration | 0.1 |
| Luma boost | 10 |
| Strafe influence | 1 |
| Vertical influence | 1 |
| Center offset max | 0.3 |
