# STRAFTAT hooks

Research notes on STRAFTAT's player movement (decompiled Assembly-CSharp + player prefab values), written for VHOLTAT.
What VHOLTAT actually patches is listed in `src/VHOLTAT/Adapter/Patches.cs`; the reasoning is below.


Snapshot 2026-10-19. Game: Unity 2021.3.45f2, Mono, FishNet 3.10.x, FishySteamworks.

Sources:

- Decompiled with ilspycmd into `C:\Users\Riley\source\_decomp\straftat\Assembly-CSharp\` and `...\FishNet.Runtime\`. The paths below are relative to those folders.
- Prefab values come from `data.unity3d`, read with UnityPy 1.25.4. The type trees are stripped, so the raw MonoBehaviour bytes were decoded by hand against the decompiled field order. Each decode used every byte exactly (FPC 3544/3544 bytes, NetworkTransform 152/152), which shows the field order was right.
- Prior findings from `C:\Users\Riley\source\Straft4Dead\docs\systems-map.md` (S4D).

Evidence tags: **[code]** from the decompiled C#, **[prefab]** from the decoded asset bytes, **[S4D]** from the earlier mod's notes and runtime logs, **[inferred]** where a conclusion is reasoned rather than shown.

---

## 0. Player object at a glance

The runtime player is the prefab **`PlayerIK`**, seen as `PlayerIK(Clone)` at runtime [S4D]. It is spawned once per life by `PlayerManager.SpawnPlayer(int,int,Vector3,Quaternion)`, which calls `Instantiate(characterPrefab)` and then `Spawn(go, Owner)`. Death despawns it, so **every respawn is a new instance** [code].

Components on the `PlayerIK` root [prefab]:

- `CharacterController`
- `CustomAddForce`
- `PlayerHealth`
- `SlopeSlide`
- `PlayerPickup`
- `FirstPersonController`
- `PlayerSetup`
- `PlayerValues`
- `Slope`
- 2× `AudioSource`
- `SetPlayerVolume`
- **`NetworkTransform`**
- `NetworkObject`
- `NetworkObserver`
- `NetworkAudioSource`
- `NetworkAudioSyncFishNet`
- `InputReset`

There is **no `PredictedObject` and no `ColliderRollback`** on it.

Hierarchy (networked parts only) [prefab]:

```
PlayerIK                      NetworkTransform (client-auth), NetworkObject
 ├ CameraLeanPivot            NetworkTransform (client-auth)    = FPC.leanCamera
 │  └ LandBobPivot            (FPC.landBobPivot)
 │     └ CameraHolder         NetworkTransform (client-auth)    = FPC.playerCameraHolder (pitch + roll)
 │        └ Camera            CameraEffect, CameraShakeConstrains (FPC.playerCamera; recoil/shake tweens)
 │           ├ BobPosition/FPArms   FPArms (first-person arm sway)
 │           ├ WeaponCam, UICam
 ├ Graphics/PF_Aboubi_04/SK_Aboubi_00   Animator + NetworkAnimator (client-auth)
 ├ SlopeDetection, Canvas, CamPointPosition
```

A second, older prefab named **`Player`** also exists. It has `PredictedObject`, `ColliderRollback` and a non-IK FPC. It is **not** the spawned player and is a leftover; do not let it mislead you [prefab + S4D runtime paths].

CharacterController values on the prefab [prefab]: height 2.0, radius 0.4, center (0,1,0), slopeLimit 65°, stepOffset 0.3, skinWidth 0.1, minMoveDistance 0. At runtime FPC overrides skinWidth every frame: 0.2 normally, 0.07 while crouched and not sliding.

---

## 1. FirstPersonController (`FirstPersonController.cs`, 4.5k lines)

`public class FirstPersonController : NetworkBehaviour`. It has no prediction attributes. Its only SyncVar is `canMove`.

### 1.1 Who runs it

- `PlayerSetup.OnStartClient` disables every Behaviour in `componentsToDisableForEnemy` on non-owner copies. The decoded prefab list is: `PlayerPickup`, **`FirstPersonController`**, `Camera`, `AudioListener`, `WeaponCam`, `PlayerHealth`, `FPArms`, `SlopeSlide`, `Slope`, `VoiceRecorder`, `PostProcessVolume`, `PostProcessLayer` [prefab].
- **So FPC.Update runs only on the owning client.** Remote copies are moved purely by NetworkTransform. This also holds on the host, because the host is a client as well.
- `CustomAddForce` is **not** disabled on remotes. Its Update still writes `customForceFinal`, but nothing consumes it there.
- The remote `CharacterController` stays enabled and acts as a collider.

### 1.2 Input (Unity Input System)

- `playerControls = InputManager.inputActions` is read in `Awake___UserLogic`. This is a static `PlayerControls` (generated class).
- `InputReset` (exec order -1000) calls `InputManager.RestartEngine()` in **every** player's Awake. That replaces the static with a new `PlayerControls`.
- **Pitfall:** read input through the FPC instance's own fields, not through `InputManager.inputActions`. The fields are captured and enabled in `OnEnable`.
- The public `InputAction` fields are `move` (Vector2), `jump`, `run`, `crouch`, `moveUp`, `leanLeft`, `leanRight`, `zoom`, `lookX`, `lookY`, `fire1` (FireHold), `fire2` (RightClick), `reload`, `record` (VoiceChat).
- Callbacks wired in `OnEnable()` and removed in `OnDisable()`:
  - `jump.performed += Jump`
  - `crouch.performed += Slide`
  - `crouch.started += SetCrouch` and `crouch.canceled += SetCrouch`
  - `crouch.canceled += SlideEnd`
  - `zoom.started/canceled += SetZoom` and `zoom.performed += SetZoomToggle`
  - `PauseManager.OnRoundStarted += roundStartEvent`
  - `PauseManager.OnBeforeSpawn += OnBeforeSpawn`
- Mouse: `OnStartClient` (owner only) subscribes `MouseX/MouseY.performed` to `OnMouseX/OnMouseY`, which store the private `Vector2 mouseInput`.
- Everything else is polled each frame with `ReadValue`.
- Settings toggles are public bools: `sprintToggle`, `aimToggle`, `leanToggle`, `crouchToggle`, `reverseSprintBind`, `invertX`, `invertY`.
- Legacy `UnityEngine.Input` is used only for taunts (Alpha0–9) and the editor/testMap flymode (Ctrl+L).

### 1.3 Update order (single `Update()`, no FixedUpdate, no LateUpdate)

All script execution orders are 0 [prefab MonoScript data], so FPC, `SlopeSlide`, `Slope` and `CustomAddForce` run in unspecified relative order. `private void Update()` does this:

1. `isTouchingAnything = false`, then `HandleTimers()` (all private timers count down), then `VoiceChat()`, then refresh FOV targets from `Settings.normalFovValue`.
2. Void check: `y < -300` sets `PlayerHealth.fellVoid` and calls `DespawnObject(gameObject)`. The editor/testMap flymode toggle is also handled here.
3. Sync skinWidth: `setupScript.ChangeSkinWidth(...)` is a ServerRpc with RunLocally, which mirrors it to observers.
4. Colour grading, then lerp `dirForward/dirRight` toward the transform's forward/right at 13/s. **Movement direction is smoothed.**
5. **canMove overrides.** `canMove` is forced to true every frame when any of these hold:
   - `pauseManager.nonSteamworksTransport`
   - `pauseManager.inVictoryMenu`
   - `MapsManager.Instance.inExplorationMap`
   - `SceneMotor.Instance.testMap`
6. If not paused (`!pause && !(steamPlaying && chatting)`):
   - `HandleMouseLook()`, if `!otherPauseBools && (startOfRound || canMove)`
   - `HandleCameraController()`
   - If `canMove`: `CalculateMovementInput()`, `HandlePhysicsCasting()`, `CheckForVault()`, `HandleLadders()`, `HandleCameraLean()`, then `HandleCrouch()` (which calls `HandleSlide()`), `HandleHeadMovement()` and `HandleFootsteps()`.
   - When paused: `currentInput = Vector2.zero`.
7. **Always:** `HandleBForce()`, `HandleMovementInput()` (unless flymode), `HandleAddingForce()`.
8. `HandleAnimation()`, if canMove and not chatting. Then `HandleTaunt()`.
9. **`ApplyFinalMovements()`**, unless flymode. This is the only place `characterController.Move` is called, apart from flymode.
10. Tail, only if `canMove`:
    - fallDistance bookkeeping.
    - **If `characterController.isGrounded`:** `moveDirection.y = -1`, `coyoteTimer = 0.15`, `hitDiagonalCeiling = false`.
    - Compute `safeGrounded`, `isAiming` (`= isZooming` unless holding something in the left hand), sprint toggle logic, `isSprinting`, `isSlideSprinting`, `isWalking`, `isGrounded = cc.isGrounded`, lean bools and `isLeaning`.
    - If grounded, reset `wallJumpsCount`, `jumped`, `allJumped` and `slopeSlideJumped`.
11. Speedometer text from `playerSpeed`.

The flags in step 10 are read by many other scripts (§2), so a replacement controller must keep them correct.

### 1.4 Velocity storage and application

- **`public Vector3 moveDirection`** is the main velocity in world space, in m/s.
  - Its xz is **rebuilt every frame** by `HandleMovementInput()` as `dirForward * currentInput.x + dirRight * currentInput.y`, so vanilla has no horizontal momentum of its own.
  - y carries the vertical velocity.
- `ApplyFinalMovements()` ends with a single `Move(...) * Time.deltaTime` call, skipped while `pauseManager.startRound`. Its argument is the sum of:
  - `moveDirection`
  - `forceAdded`
  - `bforcefinal`
  - `moveAdded`
  - `slopeSlideScript.slopeSlideMove`
  - `slopeSlideScript.steepSlopeSlideMove`
  - `objectCollisionMoveDirection`
  - `customForceFinal`
- The extra velocity channels are the knockback system (§2.3):

| channel | type and visibility | written by | decays in |
|---|---|---|---|
| `forceAdded` | public Vector3 | `AddForce`, `AddAccumulatedForce`, `AddHorizontalForce`, `AddVerticalForce` (set `force`, `forceFactor`, `initforceFactor`, `tempforceSpeed`) | `HandleAddingForce()`: `forceAdded = force * forceFactor`; forceFactor falls by `tempforceSpeed * deceleration` per s; on ground tempforceSpeed = `forceSpeed` (30); in air tempforceSpeed shrinks by `initforceFactor/2` per s (min 2, or 10 if initforceFactor>20), so airborne knockback carries much longer |
| `bforcefinal` | private Vector3 | `BForce(dir, factor, vertical, stopOnGround, decel, aircontrol)` | `HandleBForce()`: horizontal only, `bfactor -= decel*dt`; cleared on landing after 0.5 s if stopOnGround; `baircontrol=false` blocks input acceleration until bfactor ≤ 0.3 |
| `customForceFinal` | public Vector3 | the `CustomAddForce` component (`impact += dir.normalized*force/mass`, mass 3) | `CustomAddForce.Update`: lerps to 0 at `deceleration` (ground) or `airdeceleration` (air); zeroes impact.y when grounded |
| `moveAdded` | public Vector3 | nothing found | — |
| `slopeSlideMove`, `steepSlopeSlideMove` | `SlopeSlide` public | `SlopeSlide.Update` (crouch slope slide, ice) | lerp |
| `objectCollisionMoveDirection` | private | moving platform trigger (`forward*0.01`) | — |

- `playerSpeed` (public float) is `characterController.velocity.magnitude`, rounded, and set in `CalculateMovementInput`. FPArms and the speedometer read it.

### 1.5 Ground check, gravity, slopes, steps

- **Grounded:** `characterController.isGrounded`, taken after the previous Move.
- `isGrounded` (public) is copied from it in the Update tail.
- `safeGrounded` (public) is `cc.isGrounded`, or a 0.5 m down raycast while `landTimer > 0 && !allJumped && y < 0`. `landTimer` is 0.3 s after ground contact. Guns and the crosshair use it for spread.
- `HandlePhysicsCasting()` sets these each frame (all `Physics.Raycast` unless noted):
  - `downRay`: 0.3 m down.
  - `animDownRay`: 0.6 m down.
  - `slideDownRay`: 0.8 m down.
  - `groundSphereCast`: `CheckSphere` with r 0.28 on `landLayer`.
  - `groundNormal`: 8 m ray.
  - `rightHeadCast/leftHeadCast`: 2 m sideways on landLayer.
  - Vault rays.
  - `ladderRay`: OverlapSphere on `ladderLayer` (layer 10).
  - `upSphere`.
- `landLayer` = 17317889 = layers {0 Default, 14 ShootThrough, 19 InteractEnvironment, 24 Glass} [prefab].
- **Gravity (in ApplyFinalMovements):** only while airborne and `y > -40` (a soft terminal velocity), `y -= g * gravityMultiplier * dt`, where g is:
  - `crouchGravity` while crouched,
  - else `jumpGravity` while rising,
  - else `gravity`.
  - Prefab values: all three are **30**.
- `gravityMultiplier` (public, HideInInspector) is changed multiplicatively by `GravityZone`.
- **Slope stick:**
  - If `OnSlopeAir()` (safeGrounded on a 10–65° slope) and there is input, or `OnSlopeIce()` while on ice: extra down acceleration `gravity * slopeForce`, with prefab slopeForce = **400**. This glues the player to slopes.
  - The private bool `jumpSlope` (set by Jump) skips this for one frame.
- **Steep slopes (>65°):** `OnControllerColliderHit` sets `IsSliding` (capital I; this is "on steep surface", not the crouch slide). It stores `hitPointNormal`, which the jump code uses to push away from the slope (`BForce(hitPointNormal, jumpSlopeForce=12, ...)`).
- If `groundSphereCast && angle(groundNormal) > 65 && !jumpSlope && isGrounded`, both `bforcefinal` and `forceAdded` are zeroed.
- **Steps:** the CharacterController's stepOffset (0.3) and slopeLimit (65°). There is no custom step code.

### 1.6 Ground and air speed (vanilla model)

- `CalculateMovementInput()` lerps the private `horizontalInput/verticalInput` toward ±1 or 0:
  - rate `globalAcceleration` (or `globalDeceleration`),
  - multiplied by **`airControl` (0.2) when airborne**,
  - and only while `baircontrol` is true.
- `speedFactor` (public) lerps toward the target speed of the current state. Each state has its own rate (prefab values):

| state | target speed | rate |
|---|---|---|
| sprint-air | `sprintAirSpeed` 9 | `sprintAirAcceleration` 6 |
| air | `airSpeed` 6 | `airAcceleration` 6 |
| crouch | `crouchSpeed` 4 | `crouchAcceleration` 2 |
| sprint | `sprintSpeed` 8.5 | `sprintAcceleration` 20 |
| walk | `walkSpeed` 5.5 | `walkAcceleration` 30 |
| no input, **or leaning** | 1 | `globalDeceleration` 5 |

- Leaning almost stops the player.
- `SetSpeed(float speed, float time)` (public) overrides speedFactor for a time. `Weapon` calls it on fire with `fireSlowDownFactor/Duration`. It is stored in the private `tempSetSpeed` and `setSpeedTimer`.
- `currentInput = (verticalInput, horizontalInput) * speedFactor`.
- `HandleMovementInput()` then does `ClampMagnitude(currentInput, maxForState) * movementFactor` and builds `moveDirection.xz`.
- Unused and decorative prefab values: `slopeSpeed` 1.5, `globalAcceleration` 15.

### 1.7 Jump, coyote time, jump buffer, wall jump, vault

`public void Jump(InputAction.CallbackContext ctx)` (the `jump.performed` callback):

- Returns early if `!canJump || !canMove || slopeSlideScript.onSuperIce`.
- **Wall jump** (runs *before* the pause check): if `CanWallJump`:
  - `wallJumpsCount++`
  - `BForce(wallPointNormal, (input ? jumpSlopeForce : 7) * 0.7, ...)`
  - `moveDirection.y = jumpForce * 0.8 * wallJumpFactor`
  - camera punch, sounds via `WallJumpClip` and `PlaySoundServer(22)`
- Returns if paused or chatting.
- **Prejump (vanilla jump buffer):**
  - If falling and a 1.3 m down ray hits, set private `prejump = true`.
  - `ApplyFinalMovements` fires the jump on the first grounded frame.
  - It is **distance-based, not time-based**, and only clears on landing.
- **Ground jump:** if `cc.isGrounded && !prejump`:
  - `moveDirection.y = jumpForce * jumpFactor` (prefab jumpForce **8**)
  - `networkAnimator.SetTrigger("Jump")`
  - `slideCancelTimer = 2`
  - `PlaySoundServer(10)`
  - On a steep surface (`IsSliding`), also push off the slope with BForce.
- **Coyote time:** `else if (coyoteTimer > 0 && !grounded && y < 0)` does the same jump. The window is **0.15 s**, refilled every grounded frame in the Update tail.
- **After-vault jump:** `aftervaultjumpTimer > 0` (0.5 s after a vault).
- **Wall-jump detection** is in `OnControllerColliderHit(ControllerColliderHit)`. All of these must hold:
  - airborne
  - `wallJumpsCount < maxWallJumps` (prefab 1)
  - `!onLadder`
  - hit layer 0 or 14
  - `!downRay`
  - wall normal 88–100° from up
  - When they do: `CanWallJump = true`, store `wallPointNormal`, set `wallSlideLean = ±10` (camera roll), play the wall-slide clip.
- **There is no wall run:** no gravity change and no velocity along the wall. There is no dash either.
- **Vault / mantle** is `CheckForVault()`, private. Conditions:
  - airborne (`!safeGrounded`), not crouching, forward input
  - the feet ray (1.4 m) hits a near-vertical wall (normal 80–130°)
  - the +1.2 m and +1.8 m rays are clear
  - `vaultActivate` is true (re-armed on ground)
  - When they hold:
    - `moveDirection.y = 9`
    - `BForce(forward, 1.5, decel 3)`
    - camera punch
    - `aftervaultjumpTimer = 0.5`
    - `vault = true`
    - start the coroutine `DeactivateVault()`. After 0.15 s it sets y to -5 (if downRay) or 0, then `RotateBackAfterVault()` clears `vault` after 0.3 s.
- **Ceiling:** in `OnControllerColliderHit`, an upward hit adds a downward `AddVerticalForce`, pushes away with `BForce`, and plays a sound and camera punch.
- **Ladders:** `HandleLadders()` sets `moveDirection.y = ladderSpeed` (6) while in a ladder volume with input.

### 1.8 Crouch and the existing slide

**Crouch:**

- `SetCrouch(ctx)` sets `isCrouching = crouchPress = ctx.started` (hold mode). In toggle mode, `Slide()` flips it instead.
- `HandleCrouch()` forces a crouch if a 2 m up ray hits the ceiling.
- Target height:
  - `slideHeight` 0.8 while sliding or slope sliding,
  - else `crouchHeight` 1.25 (prefab),
  - else `standHeight` 2.
- `AdjustHeight` lerps `cc.height` and `cc.center = (0, h/2, 0)` at `crouchingSpeed` (20).
- `HandleHeadMovement` keeps `leanCamera.localPosition` at `cc.center + (0, h/2 - 2.5, 0)`, so the camera follows the capsule height.

**Slide:**

- **`Slide(ctx)`** (crouch.performed) does nothing if moving backward. Otherwise it sets `deceleration = 1`. If moving and `slideResetTimer < 0` (cooldown `slideResetTime` 1.0), it arms `slideTimer = 0.8` when sprint is held.
- **`HandleSlide()`** (private, called from HandleCrouch) starts the slide when all of these hold:
  - `slideTimer > 0`
  - `slideDownRay`
  - `y < 0`
  - `isCrouching`
  - `isSlideSprinting`
- On start it does:
  - `isSliding = true`
  - `ChangeSlideClipServer(0)` and `SlideAudioPlay()` (networked audio)
  - `StartCoroutine(ActiveTorso())`, which shows the owner's own legs and torso
  - `AddHorizontalForce(forward * impulsion, durationAsFactor)`
- Impulse values [prefab]:
  - sprint slide: `sprintSlideImpulsion` 0.2 × `sprintSlideDuration` 40, so about 8 m/s added, decaying at about 30/s on ground (roughly 1.3 s).
  - walk slide: 0.5 × 25.
  - `isSprinting` is still true from the previous frame's tail on the press frame, so the sprint-slide numbers apply [inferred].
- **The slide ends** when any of these happens:
  - `forceFactor == 0`
  - `!slideDownRay` (more than 0.8 m above ground)
  - crouch is released (`SlideEnd`)
  - toggle-crouch off or `EndSlide()`
  - With a gamepad, also when `forceFactor < 0.1`.
- While sliding, `SpawnVFX(...)` dust is sent every 0.2 s.
- **`SlopeSlide`** (separate component) adds `slopeSlideMove` while crouched on a slope > `minSlopeAngle` (13°), scaled by slope angle and surface. Surface comes from the ground tag (`Footsteps/Ice`, `Footsteps/SuperIce`, `Footsteps/Sand`, default concrete).
  - `steepSlopeSlideMove` handles ice.
  - `isCrouchSlopeSliding` drives FOV, tilt and the animator.
  - `onSuperIce` disables jumping.

### 1.9 Sprint, lean, air control

- `isSprinting = !isLeaning && !isAiming && moveInput != 0 && !isCrouching && funcSprint`.
- `isSlideSprinting` is the same without the `!isCrouching` term.
- `funcSprint` (private) comes from the `run` action (hold, toggle, or reverse).
- **Lean:**
  - Q/E actions; ground only, not while sliding.
  - `CameraLean()` slerps `leanCamera.localRotation` to ±`leanLimit` (30°), with raycasts against walls.
  - Synced to others through the CameraLeanPivot NetworkTransform and the `LeanLeft/LeanRight` animator bools.
- **Air control:** input acceleration × `airControl` (0.2), with lower targets (6 or 9). Directions are smoothed by `dirForward/dirRight`, and `baircontrol` is disabled during BForce.

### 1.10 Camera, head bob, FOV

- **Look:** `HandleMouseLook()`.
  - Pitch is the public `rotationX` (clamped ±90). Together with the private roll `rotationZ` it is written to `playerCameraHolder.localRotation`.
  - Yaw rotates the root `transform` directly.
  - Sensitivity fields `lookSpeedX/Y/Aim/AimNoScope` are set by `Settings`. Gamepad scales them ×14.
- **Roll / tilt:** `HandleCameraController()` calls `SideTilt(target)`. The target is:
  - `-slideTiltAmount` (10) while sliding,
  - ±`tiltAmount` (3.5) while strafing in a sprint,
  - plus `wallSlideLean`, plus mouse incidence.
  - The result is written to `rotationZ`.
  - `wallSlideLean` is zeroed in `HandlePhysicsCasting` whenever `!CanWallJump`.
- **FOV:** `HandleCameraController()` chooses one of:
  - `zoomFOV` (45) while aiming,
  - `runSlideFOV` (83) when sliding while sprinting or slope sliding,
  - `slideFOV` (80),
  - `runFOV` (83) when walking with sprint held,
  - `defaultFOV` (= `Settings.normalFovValue`).
  - It then calls private `ChangeFOV(fov, ease, speed)`, which runs `playerCamera.DOFieldOfView` (DOTween).
  - The run, slide and runSlide FOVs are stored as offsets from the default.
- **Landing:** in ApplyFinalMovements:
  - `landBobPivot.DOLocalMove` down by `landBobAmount` (0.65), recovering at `landRecoverSpeed`.
  - Fall camera punches on `playerCamera` above `fallShakeHeight` (2 m).
  - High-fall shake above `highFallShakeHeight` (10 m), with `CameraShakeConstrains.rotateBack`.
  - Fall wind audio and lens distortion above 15 m/s.
- **Head bob:** the walk, sprint and crouch bob fields in FPC are effectively unused here. Bob is done by **`FPArms`** (arm sway), which reads player state (§2.1).
- **No fall damage:** only void, Killz and DamageZone triggers hurt the player.

### 1.11 Prefab tunables (decoded PlayerIK FPC; the C# defaults differ)

| group | values |
|---|---|
| speeds | walkSpeed 5.5, sprintSpeed 8.5, crouchSpeed 4, slopeSpeed 1.5, airSpeed 6, sprintAirSpeed 9, airControl 0.2 |
| accelerations | global 15/5 (acc/dec), walk 30, sprint 20, crouch 2, air 6, sprintAir 6 |
| factors | movementFactor 1, jumpFactor 1, wallJumpFactor 1, maxWallJumps 1 |
| jump and gravity | jumpForce 8, jumpSlopeForce 12, jumpSlopeDecel 4, gravity 30, crouchGravity 30, jumpGravity 30, gravityMultiplier 1 |
| ladder and vault | ladderDetectionLength 1, ladderSpeed 6, vaultMinimumAngle 80 |
| layers | ladderLayer 1024, landLayer 17317889 |
| crouch | crouchHeight 1.25, slideHeight 0.8, standHeight 2, crouchingSpeed 20 |
| slide | walkSlideImpulsion 0.5, walkSlideDuration 25, sprintSlideImpulsion 0.2, sprintSlideDuration 40, slideResetTime 1 |
| look | lookSpeedX/Y 2, lookSpeedAim 0.8, offset (0,-0.35,0), lean speed 8, leanLimit 30 |
| FOV | zoomFOV 45, runFOV 83, slideFOV 80, runSlideFOV 83, zoomSpeed 0.2, runEaseSpeed 0.3, slide ease speeds 0.6 |
| tilt | tiltAmount 3.5, tiltSpeed 5, slideTiltAmount 10 |
| land and fall | landBobAmount 0.65, landRecoverSpeed 2.9, fallShakeHeight 2, highFallShakeHeight 10 |
| forces | slopeForce 400, slopeForceRayLength 1, forceSpeed 30, movingPlatformEjectForce 50 |

The `CustomAddForce` C# defaults are mass 3, airdeceleration 0.5, deceleration 5; its prefab values were not decoded.

---

## 2. How weapons and items touch movement

### 2.1 State they read (keep these flags correct)

- **Gun, Minigun, LargeRaycastGun, RepulsiveGun:** spread and accuracy from `isAiming`, `isSprinting`, `isGrounded`, `safeGrounded`, `isCrouching`. Firing is gated on `canMove && IsOwner`.
- **Crosshair:** `isSprinting`, `safeGrounded`, `isAiming`, `zoomFOV`, `playerCamera.fieldOfView`.
- **FPArms** (arm bob and sway): `isGrounded`, `jumped`, `isAiming`, `isSprinting`, `isSliding`, `isCrouching`, `isLeaning`, `isWalking`, `playerSpeed`.
- **MeleeWeapon:** `isAiming`; it writes `isZooming`.
- **ItemBehaviour:** writes `isScopeAiming` (scope sensitivity).
- **SlopeSlide:** `isSprinting`, `isCrouching`, `isSliding`, `safeGrounded`.

### 2.2 Speed multipliers and sprint blocking

- `Weapon.WeaponUpdate()`: while held in the right hand, writes the weapon's `movementFactor`, `jumpFactor`, `maxWallJumps` and `wallJumpFactor` into FPC **every frame**. `PlayerPickup` resets them to 1/1/1/1 when the hands are empty.
- `movementFactor` scales the max speed clamp (and footstep rate and audio pitch). `jumpFactor` scales `jumpForce`.
- `Weapon` calls `playerController.SetSpeed(fireSlowDownFactor, fireSlowDownDuration)` on fire to slow the player while shooting.
- **ADS:** `isAiming` (= `isZooming` unless the left hand holds something) blocks sprinting through the `isSprinting` formula and sets zoom FOV and aim sensitivity.
- **Reload:** does not touch movement in FPC. It only affects scope aiming in `ItemBehaviour`.

### 2.3 Knockback and impulses (the controller must keep all of these)

| source | API | channel |
|---|---|---|
| Self-knockback on fire: BeamGun, ChargeGun, DualLauncher, LargeRaycastGun, MeleeWeapon, RepulsiveGun | `FPC.AddForce(-cam.forward, playerKnockback)`. Also zeroes `moveDirection` and sets `moveDirection.y = force.y*factor` | `forceAdded` + `moveDirection.y` |
| BumpGun | `FPC.BForce(-cam.forward, playerKnockback, vertical:true, ...)` | `bforcefinal` + `moveDirection.y` |
| Melee lunge | `FPC.BForce(cam.forward, …)` and `FPC.CustomAddForce(cam.forward, …)` | `bforcefinal`, `customForceFinal` |
| Melee and physics-prop hits on others | Server `TargetRpc` `MeleeWeapon.BumpPlayer` / `PhysicsProp.BumpPlayer` / `PlayerPickup` sets `PlayerHealth.bounceDirection/bounceForce/shouldBounce`; the owner's `PlayerHealth.Update` calls `PlayerHealth.AddForce(dir, force)` → `CustomAddForce.AddForce` | `customForceFinal` |
| BumpBullet explosion | server calls `PlayerHealth.AddForce` directly (only takes effect where that player is simulated) | `customForceFinal` |
| Grenades, mines, Obus, Claymore | **no movement knockback**; only `PlayerHealth.LocalScreenshake` and lens/saturation effects | — |
| ImpulseZone / ForceZone | `player.moveDirection += force` (once on enter, or per frame) | `moveDirection` |
| FlingTrigger (pig) | `AddVerticalForce(Vector3.up, 25)` | `forceAdded` + `moveDirection.y` |
| GravityZone | `gravityMultiplier *=` on enter, `/=` on exit | gravity |
| Propeller item (jetpack) | `moveDirection.y += flySpeed*dt` while `y < 7` | `moveDirection.y` |
| Teleporter (tag `Teleport`, after round start) | `FPC.Teleport(pos, angle, boost, cac, power, decel, dontTranslateRotation)`: sets `transform.position`, rotates yaw, optional `BForce(cac.forward, power, vertical:true, ...)` | position + `bforcefinal` |
| Moving platforms (tag `MovingObject`) | `networkObject.SetParent(...)`, `objectCollisionMoveDirection`; eject via `CustomAddForce(movingVector, 50)` | parenting + `customForceFinal` |
| Taser | server sets `canMove = false` (SyncVar) | freeze |

**Camera recoil:** `Weapon` tweens `cam.transform` with `DOPunchRotation` / `DOLocalRotate` on the `Camera` child. Screenshake uses `PlayerHealth.LocalScreenshake` (`DOShakeRotation` on `playerCamera`). Both are separate from `rotationX` and `rotationZ` on `CameraHolder`. Keep the camera hierarchy and do not overwrite `playerCamera.localRotation`.

**Recommendation:** use `moveDirection` as the controller's own persistent velocity. Every vanilla writer above then becomes an impulse into our momentum automatically. Keep vanilla `HandleBForce` and `HandleAddingForce` running, keep `CustomAddForce` running, and add those channels into the single Move. Note one behaviour change: in vanilla, xz writes to `moveDirection` (ImpulseZone/ForceZone) last one frame, because xz is rebuilt from input; in a momentum model they persist.

---

## 3. Networking of movement: **CSP: NO**

Evidence:

1. **No prediction on the player.**
   - No type in Assembly-CSharp has `[Replicate]`/`[Reconcile]` or `IReplicateData`, and none uses `PredictionManager`. A grep for Replicate/Reconcile/Prediction only hits `BallisticPathFollowSyncer.ReconcileTheThing`, which is an ordinary ObserversRpc for projectiles.
   - `FirstPersonController : NetworkBehaviour` moves with `characterController.Move` in `Update` on the owner only (remote copies have FPC disabled).
   - The spawned `PlayerIK` prefab has **no `PredictedObject`** [prefab]. That component is only on the unused legacy `Player` prefab and on FishNet demo prefabs.
2. **Owner-authoritative NetworkTransform.** `PlayerIK`'s `NetworkTransform` (FishNet `FishNet.Component.Transforming.NetworkTransform`) decodes as [prefab]:

   | field | value |
   |---|---|
   | `_clientAuthoritative` | **true** |
   | `_sendToOwner` | true |
   | `_synchronizeParent` | true |
   | `_componentConfiguration` | Disabled |
   | packing | position/rotation/scale Unpacked |
   | `_interpolation` | 5 ticks |
   | `_extrapolation` | 2 |
   | `_enableTeleport` | **false** |
   | `_teleportThreshold` | 1 |
   | `_enableNetworkLod` | true |
   | `_interval` | 1 |
   | sync position / rotation / scale | yes / yes / no |

   - `CameraLeanPivot` (lean roll) and `CameraHolder` (pitch and roll) each have their own client-authoritative NetworkTransform (interpolation 2).
   - The NetworkManager's TimeManager in MainMenu (`level0`) has `_tickRate` = **60**. The per-map copies say 30; they appear to be scene duplicates [inferred].
3. **No server validation.**
   - FishNet's server path (`ServerUpdateTransform` ServerRpc → `DataReceived(..., asServer:true)`) applies the owner's data and relays it. There is no speed or teleport check in FishNet 3.x NT.
   - Nothing in the game subscribes to `NetworkTransform.OnDataReceived`.
   - The game's `GameManager.CmdKickPlayer` / `KickPlayerTarget` is used only for "Lobby is full". No other kick, speed cap, position check or "cheat" logic exists; the only "cheat" string is a log about a SteamID that is not in the lobby.
   - No position ServerRpc exists in FPC. Its RPCs carry sounds, VFX, root-motion toggle, glass, voice, taunts and despawn only.
   - Remote root motion: `CmdChangeRootMotion` / `ChangeRootMotion` only toggle `animator.applyRootMotion` on moving platforms [code; RpcLogic bodies].
4. **Hit registration is shooter-side.** `Gun.ShootServer` is a local method [S4D] and `PlayerIK` has no `ColliderRollback`. Other clients therefore hit what they see at the interpolated remote position (about 83 ms behind at 60 Hz × 5 ticks). Faster movement makes a player harder to hit, with no lag compensation.
5. **Mod visibility.** `ComputerysModdingUtilities` changes the matchmaking key for non-vanilla-compatible mods, and `ClientInstance.ReportModsToServer` shows mod names to the lobby [S4D]. VHOLTAT should not claim `IsVanillaCompatible`, because the movement advantage is real.

**Conclusion:** the owner moves locally and peers accept the result. The only "sync" a parkour controller needs is to move the CharacterController on the owner. Very fast moves render smoothly because teleport snapping is off; the NT just interpolates faster.

---

## 4. Remote animation

- The third-person `Animator` on `Graphics/PF_Aboubi_04/SK_Aboubi_00` is synced by **`NetworkAnimator`**, decoded as `_clientAuthoritative` true, `_sendToOwner` false, interpolation 2, smoothFloats true [prefab]. FishNet's NetworkAnimator polls parameter changes on the owner each tick and sends them.
- **Parameters are not derived from velocity on the remote side.** The owner sets them in `FirstPersonController.HandleAnimation()` (private, owner only):

| param | type | how the owner sets it |
|---|---|---|
| `Crouch` | bool | `isCrouching` |
| `Grounded` | bool | `slideDownRay && moveDirection.y <= 0` |
| `Slide` | bool | `isSliding \|\| slopeSlideScript.isCrouchSlopeSliding \|\| slideTimer > 0.1` |
| `crouchMove` | float | `|move input|` |
| `MovementSpeed` | float | 1 sprint / 0.5 walk / 0 |
| `Vertical` | float | from `rotationX` (pitch), offset when sliding or sprinting |
| `LeanRight` / `LeanLeft` | bool | lean, grounded, not sliding, based on the leanCamera angle |
| `Jump` | trigger | `networkAnimator.SetTrigger("Jump")` in the jump code |

- `PlayerPickup` sets hand-pose bools and `Weapon` triggers `Shoot` / `Reload` [S4D].
- Body position and yaw come from the root NT. Camera pitch and roll come from the CameraHolder NT, and lean from the CameraLeanPivot NT. The body is a sibling of `CameraLeanPivot`, so it does not inherit lean or roll.
- **What this means for VHOLTAT:**
  - **Slide** looks right to others if we set the `Slide` bool (or `isSliding`) and `Crouch`. Height changes are not visible remotely; only the anim is.
  - **Wall run** would show as airborne or falling: `Grounded` is false and nothing tilts the body. Options:
    - (a) cheap: in a postfix on `HandleAnimation`, set existing params while wall running, for example `LeanLeft/LeanRight = true` toward the wall, or `Grounded = true` with `MovementSpeed = 1` so others see a run cycle. NetworkAnimator then syncs them, and peers need no mod.
    - (b) proper: tilt or animate the body on peers. That needs VHOLTAT on peers, a custom broadcast, and a remote-side driver. Not needed for a first pass.
  - **Mantle** shows as a jump or air pose.
  - **Footsteps, slide audio and slide dust** are owner-sent RPCs: `PlaySoundServer(int)`, `ChangeSlideClipServer(int)`, `SlideAudioPlay()`, `SlideAudioStop()`, `SpawnVFX(Vector3, Quaternion)`. All are private, so call them with AccessTools if our slide replaces vanilla's.
  - Sound clip ids: 1–9 and 12–16, 19, 20 are footsteps by surface; 10 jump, 11 land, 17/18 ladder, 21 high fall, 22 wall jump, 23 teleport.

---

## 5. Lifecycle, freeze states, local player

**Local player detection:**

- `IsOwner` on the FPC or any player NetworkBehaviour; this is the reliable test.
- `Settings.Instance.localPlayer`, set in `PlayerSetup.OnStartClient` for the owner.
- `PlayerManager.player`.
- **Do not trust `FirstPersonController.instance`.** It is assigned in `Awake___UserLogic` for *every* spawned player, so the last one spawned wins. S4D used it in solo tests only.

**Per-life instance:** each respawn is a new `PlayerIK(Clone)`. Attach controller state per instance, for example a companion `MonoBehaviour` added in a postfix on `FirstPersonController.OnStartClient` when `IsOwner`, or keyed by the instance. Reset it on `OnDisable` / despawn.

**`canMove`** is a `[SyncVar(WritePermissions = ClientUnsynchronized)] public bool`. Read it through `SyncAccessor_canMove`; write it with `sync___set_value_canMove(v, true)`, as the game does. Writers:

- `PlayerManager.SetPlayerMove` (ServerRpc) from `WaitForRoundStart`: false during the pre-round wait, true at start.
- `OnBeforeSpawn` → false.
- `PlayerHealth.UnfreezePlayerServer` → true.
- Taser → false.
- `PlayerPickup.SetEnemyParent` → true.
- FPC's own forced true (exploration, testMap, victory menu, non-Steam transport).
- S4D's incap writes `fpc.canMove = false`.
- **Gotcha:** in exploration mode, Taser freezes are undone the next frame.

**Round start:**

- `PauseManager.startRound` is true during the countdown. `ApplyFinalMovements` returns before Move while `startRound || startOfRound`.
- `startOfRound` is true from Awake until canMove first becomes true. Mouse look is allowed while `startOfRound`.
- `roundHasStarted` (set by `PauseManager.OnRoundStarted`) gates the Teleport and Killz triggers.

**Pause and chat:**

- If `pauseManager.pause` or (`steamPlaying && chatting`): no look, no input. `currentInput` is zeroed, but `HandleMovementInput`, `ApplyFinalMovements` and gravity still run.
- `otherPauseBools` blocks look only.
- `Jump` ignores pause for the wall-jump branch only.
- Other fields: `PauseManager.inMainMenu`, `inVictoryMenu`, `onStartRoundScreen`.

**Death:**

- `PlayerHealth.Update` (owner) on health ≤ 0 → `GameManager.PlayerDied`, then despawn.
- Void: y < -300, or the `Killz` trigger → `PlayerHealth.fellVoid` + `FPC.DespawnObject`.
- `DamageZone` trigger: periodic `RemoveHealth`.

**Teleport:** `FPC.Teleport(...)`, called by Teleporter triggers and by `TimerManager` (timer start points). It sets `transform.position` directly while the CharacterController is enabled; this works in vanilla. If our Move fights it, call `Physics.SyncTransforms()`. Vanilla does not rotate velocity (it has no momentum). **VHOLTAT should postfix `Teleport` to rotate stored horizontal velocity by the yaw delta** and clear wall-run state.

**Moving platforms:** `OnTriggerStay` / `OnTriggerExit` use the tag `MovingObject` with `NetworkObject.SetParent/UnsetParent`. The NT has `synchronizeParent` on.

---

## 6. Recommended Harmony patch points

**Goal:** take over locomotion for the local owner only, keep everything else vanilla, and allow a live toggle back to vanilla.

**Guard:** every prefix/postfix should check `__instance.IsOwner && VholtatEnabled`. FPC is disabled on remotes anyway, so these patches never run for remote copies. The guard protects against edge cases and the legacy prefab.

### Recommended approach: surgical replacement, keeping vanilla Update

| method (all on `FirstPersonController` unless noted) | patch | why |
|---|---|---|
| `private void ApplyFinalMovements()` | **Prefix, return false** when active | The single `characterController.Move` site. Replace it with our integrator: read `moveDirection` as velocity; apply accel/friction/gravity/wall-run/slide; sum the vanilla channels `forceAdded + bforcefinal (private) + moveAdded + slopeSlideScript.slopeSlideMove + steepSlopeSlideMove + objectCollisionMoveDirection (private) + customForceFinal`; call `Move` once; write back `moveDirection`. Preserve the early-out on `pauseManager.startRound \|\| startOfRound`. Re-implement or keep the cosmetic parts (landing bob, fall shake, fall audio, prejump, `landBool`, `landTimer = 0.3`); either copy them or reimplement them minimally. |
| `private void HandleMovementInput()` | **Prefix, return false** when active | It overwrites `moveDirection.xz` from input every frame, which kills momentum. |
| `private void CalculateMovementInput()` | Let it run (or postfix) | It sets `playerSpeed` and `speedFactor`/`currentInput`/`currentInputRaw`, which FPArms, footsteps and the speedometer read. Harmless if HandleMovementInput is skipped. |
| `public void Jump(InputAction.CallbackContext)` | **Prefix, return false** when active | This is the `jump.performed` delegate target; patching the method also patches the delegate. Record a timestamp for our jump buffer instead, then do the jump in our integrator (coyote, buffer, wall jump/run, mantle jump). Still call `networkAnimator.SetTrigger("Jump")` (private field `networkAnimator`) and `PlaySoundServer(10)` (private) so peers see and hear it. Respect `canJump` (private), `slopeSlideScript.onSuperIce`, pause/chat, and `jumpFactor`. |
| `public void Slide(ctx)`, `public void SlideEnd(ctx)`, `private void HandleSlide()` | Prefix, return false when our slide is active | Replace with our slide. Keep `SetCrouch` (it sets `isCrouching`/`crouchPress`, which many readers use). If skipping SlideEnd, still mirror its cleanup (`isSliding = false`, legs/torso `SetActive(false)` via private `legLeft/legRight/torso`, `slideTimer = 0`). Set `isSliding` ourselves for FOV, tilt, animator and SlopeSlide. |
| `private void HandleCrouch()` | Optional prefix | Only if you need a custom capsule height for slide. Otherwise let it run: it already lerps to `slideHeight` when `isSliding`. Note it calls `HandleSlide()`. |
| `private void CheckForVault()` | Prefix, return false | Only if replacing the mantle with your own. Otherwise vanilla vault keeps working on top of our velocity, and its `DeactivateVault` coroutine writes `moveDirection.y` 0.15 s later. |
| `private void OnControllerColliderHit(ControllerColliderHit)` | **Postfix** (record contacts for wall run: normal, point, layer, tag); optionally a prefix to suppress the ceiling-bonk forces | Fires during our `Move` call because it is on the same GameObject. Keep vanilla for glass break (`BreakGlassServer`) and steep-slope `IsSliding`. Vanilla `CanWallJump` and `wallJumpsCount` can be reused or ignored. |
| `private void HandleCameraController()` | **Postfix** | Add wall-run roll to private `rotationZ`, or set public `wallSlideLean` after `HandlePhysicsCasting` (which zeroes it when `!CanWallJump`). For speed-based FOV, prefix private `ChangeFOV(float, Ease, float)` and substitute the value. |
| `private void HandleAnimation()` | **Postfix** | Override animator params for wall run, slide or mantle so remote players see something sensible (field `animator`, private). |
| `public void Teleport(...)` | **Postfix** | Rotate our stored velocity by the yaw delta and clear the wall run. |
| `public override void OnStartClient()` | Postfix (IsOwner) | Attach the per-life VHOLTAT state component. |
| `PlayerHealth.Update`, `CustomAddForce.Update`, `HandleBForce`, `HandleAddingForce` | **Do not patch** | They keep producing knockback into the channels we sum. |

### Alternative: full Update takeover

Prefix `Update` to return false and call vanilla private helpers yourself. This is riskier: Update also runs `VoiceChat()` (push-to-talk), `HandleTimers()`, void death, skinWidth RPC sync, colour grading, canMove overrides, sprint/lean/grounded flag computation, taunts and the speedometer, all of which you would have to replicate. Not recommended.

### Live toggle back to vanilla

Because the integrator stores velocity in `moveDirection`, turning the mod off lets vanilla `HandleMovementInput` rebuild xz from input on the next frame. On toggle-off:

- clear wall-run and slide state; set `isSliding = false` and the vanilla slide flags (private `walkSlide`, `sprintSlide`, `slideTimer`)
- reset private `baircontrol` to true
- set `wallSlideLean = 0`
- let `HandleCrouch` restore height, and set legs/torso inactive

The patches check the flag every call, so no unpatching is needed.

### Pitfalls

- **Private members:** `bforcefinal`, `objectCollisionMoveDirection`, `baircontrol`, `coyoteTimer`, `prejump`, `allJumped`, `jumpSlope`, `landTimer`, `funcSprint`, `slideTimer`, `slideResetTimer`, `slideCancelTimer`, `setSpeedTimer`, `tempSetSpeed`, `rotationZ`, `mouseInput`, `dirForward/dirRight`, `animator`, `networkAnimator`, `slideAudio`, `pauseManager` (public), `canJump`, `legLeft/legRight/torso`, `landBobPivot`, `wallPointNormal`, `hitPointNormal`, `onLadder`.
  - Use `AccessTools.FieldRefAccess<FirstPersonController, T>("name")`, cached once; Traverse is too slow per frame.
  - Private methods (`PlaySoundServer`, `SpawnVFX`, `SlideAudioPlay`, `ChangeSlideClipServer`, `SlideFadeOut`, `ChangeFOV`) via `AccessTools.MethodDelegate`.
- **SyncVar:** read `SyncAccessor_canMove`. The `canMove` field is the backing value. Write with `sync___set_value_canMove`.
- **Inlining:** Mono inlines only tiny methods (about 20 IL bytes). All the recommended targets are large, or are invoked by Unity messages or delegates, so they are safe. Do **not** patch `SyncAccessor_canMove` or other trivial getters, which are likely inlined. `SetCrouch` is small but is only invoked through a delegate.
- **Coroutines:** `ActiveTorso`, `DeactivateVault`, `RotateBackAfterVault` and `HighCameraShake` are compiler-generated `IEnumerator` classes. Patch their `MoveNext` only if necessary; avoiding the starting method is easier.
- **Input:** use `__instance.move/jump/run/crouch` (instance fields), not `InputManager.inputActions`, which is replaced whenever any player spawns (`InputReset`).
- **Execution order is undefined** between FPC, `SlopeSlide`, `Slope` and `CustomAddForce`, all of which run in Update at order 0. Values from those components can be one frame old. This is the same in vanilla.
- **Gravity sign convention:** vanilla keeps `moveDirection.y = -1` while grounded; the Update tail does this after our Move. Our integrator should also handle snap-to-ground on slopes, because vanilla relies on the `slopeForce` 400 hack.
- **Exploration and testMap** force `canMove` true every frame (§5).
- **Speed is cosmetic to peers.** No server cap exists, but extreme per-tick distances still interpolate over 5 ticks.

---

## 7. Solo test scenes

- Build scenes include **`TrainingRange_00`** (build index 3) and **`TutorialScene`** (index 4) [S4D log], plus every map.
- The MainMenu scene (`level0`) has two buttons named **`TrainingRound`** whose `Button.onClick` calls `SteamLobby.EnterExplorationMap` with the string argument `"TrainingRange_00"` and `"TutorialScene"` [prefab, level0 objects 12980 / 12397]. They are on the main menu's training/tutorial entry; the tutorial button hides itself after the first click (`TutorialButton`, PlayerPrefs `tutorialClicked`).
- `SteamLobby.Instance.EnterExplorationMap(string sceneName)`:
  - host only;
  - creates a private Steam lobby if needed, sets `MapsManager.inExplorationMap`, and runs `SceneMotor.EnterScene(name)`.
  - The player respawns after death [S4D].
  - `canMove` is forced true.
- Any map works the same way from the map list (`MapInstance.OpenMap`).
- From a plugin: `SteamLobby.Instance.EnterExplorationMap("TrainingRange_00")`. This is what S4D's F8 dev key does (`plugin/src/Dev/DevTools.cs`).
- testMap/editor-only flymode is Ctrl+L, available when `SceneMotor.Instance.testMap` is set.
