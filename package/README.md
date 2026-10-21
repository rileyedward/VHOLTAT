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
- Jump right as you land to keep your speed and build more (bunny hop).
- Jump next to a wall while holding forward to wall run; jump again to wall jump. Wall to wall builds speed.
- Jump at a ledge while holding forward to vault onto it; hold forward into a wall in the air to climb it.
- Extra speed from hops is carried and fades slowly (slower still on walls).

## Keys

- **F6** switch between VHOLTAT and vanilla movement
- **F7** speed / state overlay

## Steam Deck / Linux

Set STRAFTAT's launch options to `WINEDLLOVERRIDES="winhttp=n,b" %command%`.

## Settings

Esc, then **VHOLTAT settings** in the pause menu (camera, sounds, speedometer...). Everything is also in
`BepInEx/config/dev.riley.vholtat.cfg`; saved changes apply while the game runs. In someone else's lobby the host's
movement settings are used.

Clean-room project: the movement was rebuilt from a written description of VHOLUME's behaviour; no VHOLUME code or
assets are included.
