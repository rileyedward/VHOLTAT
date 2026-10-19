# Recon: what VHOLTAT reuses

Notes on existing work we can lean on. Nothing here is copied code; it records patterns and decisions.

## Straft4Dead (`C:\Users\Riley\source\Straft4Dead`, read only)

Reused as-is (as patterns, re-typed for this repo):

- **Build setup.** netstandard2.1 plugin, `BepInEx.Core 5.4.*` + `BepInEx.PluginInfoProps` from
  nuget.bepinex.dev, game DLLs referenced from `$(StraftatManaged)` with `Private=false`, machine paths in a
  git-ignored `Directory.Build.props.user` with a committed `.example`, a `CheckGamePaths` target with a clear
  error. VHOLTAT differs in one place: Debug builds deploy into a separate dev profile, never the STRAFTAT folder.
- **Launching a profile without touching the game folder.** Straft4Dead's launcher starts STRAFTAT with
  `steam -applaunch 2386720 --doorstop-enabled true --doorstop-target-assembly <profile>\BepInEx\core\BepInEx.Preloader.dll`.
  Doorstop 4's command-line args override the game folder's `doorstop_config.ini`, so a profile can live anywhere.
  VHOLTAT's `tools/dev/launch.ps1` does exactly this for `C:\Users\Riley\source\_vholtat-dev` (verified
  2026-10-19: only VHOLTAT loads; the Straft4Dead plugin in the STRAFTAT folder is not picked up).
- **Matchmaking key.** STRAFTAT ships `ComputerysModdingUtilities.dll`; `AssemblyScanner.CreateMatchMakingKey`
  hashes every loaded plugin assembly (name@version) that is not marked `[StraftatMod(isVanillaCompatible: true)]`.
  VHOLTAT must not carry that attribute (or carries it with `false`), so modded players never land in public
  vanilla lobbies and everybody in a lobby has the same VHOLTAT version. This is the built-in version gate.
- **Runtime object.** The BepInEx manager object doesn't survive STRAFTAT's startup; long-lived behaviour
  (overlay, hotkeys) must run on our own `DontDestroyOnLoad` GameObject with `HideAndDontSave`.
- **Custom networking without Mycelium.** Straft4Dead sends one FishNet broadcast (`IBroadcast` struct with a
  `byte[]` payload) and registers its serializer by hand because FishNet's codegen doesn't process plugin
  assemblies. Good fallback if VHOLTAT needs to sync anything (e.g. host-forced settings, remote anim states)
  without adding a dependency.
- **Testing.** Solo: `TrainingRange_00` / `TutorialScene` exist; Straft4Dead's `[Dev] AutoTest` entered exploration
  mode on a map from the menu and took screenshots. Two-copy LAN test: `tools/dev/make_client_instance.ps1` +
  `lan_test.sh` and a `--s4d-lan host|client` arg (both copies share Riley's save file). Test one map per launch.
- **Lessons.** Kill STRAFTAT before deploying; log to `BepInEx\LogOutput.log`; Harmony on private
  `FirstPersonController` methods by name works fine (Eights does the same).

## Straftat Eights (MRog40ST, github.com/MRog40/StraftatEights, Thunderstore `MRog40ST-Eights` 0.4.87)

Read from a shallow clone in `C:\Users\Riley\source\_decomp\eights` (outside this repo).

- **Movement precedent.** Eights already patches `FirstPersonController` by name: `Update`, `HandleMovementInput`,
  `ApplyFinalMovements`, `HandleCrouch`, `Slide`, `Jump`, `HandleAddingForce`, `BForce`/`HandleBForce`,
  `OnControllerColliderHit`. It tunes private fields through reflection (`walkSpeed` 12-ish ground sprint vs
  `sprintAirSpeed` 14, seven acceleration fields defaulting to 15, `bforcefinal`/`bfactor` knockback). It changes
  movement locally on every client with host-pushed settings and has worked in real matches, which hints that
  movement is owner-driven (confirmation is in STRAFTAT_HOOKS.md).
- **Sync via MyceliumNetworking** (`straftatmodding-MyceliumNetworking-1.1.17`, Steam P2P RPCs + lobby data).
  Their documented contract, worth following if VHOLTAT syncs settings:
  - Host-authoritative settings are pushed as latest-value snapshots with host Steam ID + round ID + revision;
    clients reject stale/foreign snapshots.
  - Push periodically (they use a 0.75 s heartbeat), not once: Mycelium's `RPC()` returning doesn't prove delivery.
  - Mirror important small state in Steam lobby data as a second channel (read on lobby enter and on
    `LobbyDataUpdated`).
  - Mycelium only serializes primitives and some arrays; flatten anything else.
- **Version gate.** Eights has no explicit handshake; it relies on `[StraftatMod(isVanillaCompatible: false)]`
  (matchmaking key) plus Thunderstore dependency pins. Same plan for VHOLTAT.
- **Decision for VHOLTAT:** Phase 1 needs no networking. If we later want "host's tuning applies to everyone",
  prefer Straft4Dead's single FishNet broadcast (no extra dependency, one-click install stays simple). Use
  Mycelium only if we also need lobby-data mirroring before the FishNet session exists.

## LoopbackLab

Nothing by that name is on this PC (searched `source`, Desktop, Downloads, Documents, AppData) or on
Thunderstore/web. **Open question for Riley:** where does LoopbackLab live? If it is a tool for running two
STRAFTAT instances on one PC, Straft4Dead's `make_client_instance.ps1` + `--s4d-lan` flow covers the same need.
