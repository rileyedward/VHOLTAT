# VHOLTAT

VHOLUME-style parkour movement for STRAFTAT. Carried momentum, slides, wall runs, wall jumps, wall climbs, vaults,
mantles, coyote time and jump buffering replace STRAFTAT's movement. STRAFTAT's rules, weapons, damage and
knockback are unchanged.

## Multiplayer

- Everyone in a lobby needs the same VHOLTAT version. STRAFTAT turns away players whose mods differ and gives modded
  players their own matchmaking pool, so you never land in vanilla lobbies.
- The host's VHOLTAT settings are used by everyone in the lobby.
- A mismatched version gets a banner telling the player which version to install, and vanilla movement until then.
- Other players see your slides and wall runs animated and hear their sounds; fast players are drawn smoothly even on
  poor connections.

## Moves

- Hold crouch while running to slide; jump out of a slide to keep its speed.
- Jump right as you land to keep your speed (bunny hop).
- Jump next to a wall while holding forward to wall run; jump again to wall jump. Wall to wall builds speed.
- Run into waist-high cover to vault it; hold forward into a wall in the air to climb it and mantle onto the top.
- Speed above each move's base is carried: never lost in the air, fades slowly on the ground.

## Keys

- **F6** switch between VHOLTAT and vanilla movement
- **F7** speed / state overlay

## Settings

`BepInEx/config/dev.riley.vholtat.cfg`. Saved changes apply while the game runs. In someone else's lobby the host's
movement settings are used.

Clean-room project: the movement was rebuilt from a written description of VHOLUME's behaviour; no VHOLUME code or
assets are included.
