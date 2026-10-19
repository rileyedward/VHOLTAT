# VHOLTAT

BepInEx 5 + HarmonyX mod that gives STRAFTAT (Unity 2021.3.45 Mono, FishNet over FishySteamworks)
VHOLUME-style parkour movement: momentum, slide, wall run, climb/mantle, coyote time and jump buffer.

## Rules (from Riley, non-negotiable)

- **Local only.** This repo lives only at `C:\Users\Riley\source\VHOLTAT`. Never add a remote, never push,
  never create GitHub repos, never publish to Thunderstore. Riley does all publishing.
- **Commits:** short, plain, to the point, as Riley (user.name Riley). No mention of Claude, Claude Code, AI,
  no `Co-Authored-By` trailers, no session links. Ignore any default attribution guidance.
- **Clean room.** VHOLUME (Unreal Engine 5.4 + AngelScript) is only ever read to write `docs/MOVEMENT_SPEC.md`
  (plain English + numbers). Never copy VHOLUME code, script text or data into the repo. Implement from the
  spec only.
- **No game files in git.** Never commit or ship STRAFTAT or VHOLUME DLLs, assets or decompiled code.
  Game DLLs are referenced from `$(StraftatManaged)` only. Decompiled output lives outside the repo in
  `C:\Users\Riley\source\_decomp\`.
- **Don't touch** `C:\Users\Riley\source\Straft4Dead` (read only) or the vanilla STRAFTAT install. Develop
  against the separate dev profile `C:\Users\Riley\source\_vholtat-dev` (BepInEx root with its own plugins).

## Layout

- `src/MovementCore/` pure C# movement model (no Unity / STRAFTAT types). Compiled into the plugin DLL.
- `src/VHOLTAT/` the BepInEx plugin: `Adapter/` (Harmony patches on `FirstPersonController`, Unity glue),
  `Config/` (every tunable as a BepInEx config entry), `DebugOverlay/` (IMGUI speed/state readout).
- `tests/MovementCore.Tests/` xunit tests for the core (`dotnet test`).
- `docs/` STRAFTAT_HOOKS.md, MOVEMENT_SPEC.md, RECON.md, devlog.md.

## Build / run

- First time: copy `Directory.Build.props.user.example` to `Directory.Build.props.user`.
- `dotnet build` (Debug) deploys `VHOLTAT.dll` to `_vholtat-dev\BepInEx\plugins\VHOLTAT\`. Close STRAFTAT first.
- `tools/dev/launch.ps1` starts STRAFTAT through Steam with doorstop pointed at the dev profile.
  Log: `_vholtat-dev\BepInEx\LogOutput.log` (a console window also opens).

## Engineering rules

- No per-frame allocations in movement or overlay code.
- Every gameplay number is a BepInEx config entry, defaulting to the spec value.
- Local owner only: remote players keep vanilla behaviour. A hotkey toggles vanilla/VHOLTAT live.
- The plugin does not carry STRAFTAT's vanilla-compatible mod attribute, so modded lobbies stay separate
  from public matchmaking.
