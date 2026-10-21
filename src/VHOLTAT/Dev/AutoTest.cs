using System.Collections;
using System.IO;
using System.Reflection;
using BepInEx;
using UnityEngine;
using UnityEngine.SceneManagement;
using VHOLTAT.Adapter;
using VHOLTAT.Movement;

namespace VHOLTAT.Dev
{
    /// <summary>
    /// Developer self-test ([Dev] AutoTest = true): enters the test map from the main menu, then drives a scripted
    /// run / slide / slide-jump / strafe sequence through the motor and logs speed and state every 0.1 s.
    /// </summary>
    internal sealed class AutoTest : MonoBehaviour
    {
        private bool entered, tested, holdW;

        private void Update()
        {
            if (holdW && UnityEngine.InputSystem.Keyboard.current != null)
                UnityEngine.InputSystem.InputSystem.QueueStateEvent(UnityEngine.InputSystem.Keyboard.current,
                    new UnityEngine.InputSystem.LowLevel.KeyboardState(UnityEngine.InputSystem.Key.W));
        }

        private void OnEnable() => SceneManager.sceneLoaded += OnSceneLoaded;
        private void OnDisable() => SceneManager.sceneLoaded -= OnSceneLoaded;

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (scene.name == "MainMenu" && DevNet.Lan)
                StartCoroutine(DevNet.OnMainMenu());
            if (!Plugin.Settings.DevAutoTest.Value || DevNet.IsClientRole)
                return;
            if (scene.name == "MainMenu" && !entered)
            {
                entered = true;
                StartCoroutine(Plugin.Settings.DevTestMode.Value == "match" || Plugin.Settings.DevTestMode.Value == "lobby" ? EnterMatch() : Enter());
            }
            else if (scene.name == Plugin.Settings.DevTestMap.Value && !tested)
            {
                tested = true;
                StartCoroutine(Run());
            }
        }

        private IEnumerator EnterMatch()
        {
            yield return new WaitForSeconds(8f);
            while (!DevNet.Ready) yield return new WaitForSeconds(1f);
            yield return new WaitForSeconds(2f);
            tested = true; // the match drives itself
            yield return Plugin.Settings.DevTestMode.Value == "lobby" ? MatchTest.HostForPlay() : MatchTest.Host();
        }

        private IEnumerator Enter()
        {
            yield return new WaitForSeconds(8f);
            while (!DevNet.Ready) yield return new WaitForSeconds(1f); // LAN test: wait for the Tugboat NetworkManager
            yield return new WaitForSeconds(2f);
            var map = Plugin.Settings.DevTestMap.Value;
            Plugin.Logger.LogInfo($"[test] entering {map} in exploration mode");
            SteamLobby.Instance.EnterExplorationMap(map);
        }

        private IEnumerator Run()
        {
            FirstPersonController fpc = null;
            for (float t = 0f; t < 30f && fpc == null; t += 0.5f)
            {
                foreach (var c in FindObjectsOfType<FirstPersonController>())
                    if (c.IsOwner) fpc = c;
                yield return new WaitForSeconds(0.5f);
            }
            if (fpc == null)
            {
                Plugin.Logger.LogError("[test] no local FirstPersonController");
                yield break;
            }
            LogVanillaTuning(fpc);
            if (Plugin.Settings.DevTestMode.Value == "enter")
            {
                Plugin.Logger.LogInfo("[test] done (entered the map for manual play)");
                yield break;
            }
            if (DevNet.Lan)
            {
                // Wait for the client copy to join, then give it time to load the map and find us.
                for (float w = 0f; w < 90f && FishNet.InstanceFinder.ServerManager.Clients.Count < 2; w += 1f)
                    yield return new WaitForSeconds(1f);
                Plugin.Logger.LogInfo($"[lan] host: {FishNet.InstanceFinder.ServerManager.Clients.Count} connections, starting the run");
                yield return new WaitForSeconds(10f);
            }
            yield return new WaitForSeconds(4f);
            Plugin.Settings.ShowOverlay.Value = true;
            if (Plugin.Settings.DevTestMode.Value == "menu")
            {
                var pauseMenu = HarmonyLib.Traverse.Create(PauseManager.Instance).Field("pauseMenu").GetValue<GameObject>();
                pauseMenu.SetActive(true); // what the Escape key does
                Settings.SettingsMenu.ForceOpen = true;
                yield return new WaitForSeconds(1.5f);
                Shot("vholtat_menu.png");
                yield return new WaitForSeconds(1f);
                pauseMenu.SetActive(false);
                Settings.CombatPanel.ForceOpen = true;
                yield return new WaitForSeconds(1.5f);
                Shot("vholtat_combat.png");
                yield return new WaitForSeconds(1f);
                Settings.CombatPanel.ForceOpen = false;
                Plugin.Logger.LogInfo("[test] done");
                yield break;
            }
            if (Plugin.Settings.DevTestMode.Value == "sweep")
            {
                yield return Sweep(fpc);
                yield break;
            }
            if (Plugin.Settings.DevTestMode.Value == "parkour")
            {
                yield return Parkour(fpc);
                yield break;
            }

            Recorder.StartForTest();
            MotorDriver.ScriptActive = true;
            PerfProbe.Reset();
            PerfProbe.On = true;
            yield return Phase("stand", 0.5f, Vector2.zero, false);
            MotorDriver.ScriptJump = true;
            yield return Phase("jump", 1.0f, Vector2.zero, false);
            yield return Phase("run", 1.2f, Vector2.up, false);
            yield return Phase("slide", 0.05f, Vector2.up, true);
            Shot("vholtat_slide.png");
            yield return Phase("slide", 0.9f, Vector2.up, true);
            MotorDriver.ScriptJump = true;
            yield return Phase("slidejump", 0.9f, Vector2.up, true);
            yield return Phase("run-after", 0.6f, Vector2.up, false);
            yield return Phase("strafe", 0.6f, Vector2.right, false);
            yield return Phase("stop", 0.6f, Vector2.zero, false);
            MotorDriver.ScriptActive = false;
            Recorder.StopForTest();
            PerfProbe.On = false;
            Plugin.Logger.LogInfo(PerfProbe.Report());
            Plugin.Settings.ShowOverlay.Value = false;
            PerfProbe.Reset();
            PerfProbe.On = true;
            yield return new WaitForSeconds(3f); // same work with the overlay hidden (idle standing)
            PerfProbe.On = false;
            Plugin.Logger.LogInfo(PerfProbe.Report() + " (overlay off, idle)");
            Shot("vholtat_end.png");
            Plugin.Logger.LogInfo("[test] done");
        }

        /// <summary>Spawns a 2 m ledge ahead (mantle), then a long wall on the right (wall run + wall jump).</summary>
        private IEnumerator Parkour(FirstPersonController fpc)
        {
            Transform t = fpc.transform;
            Vector3 start = t.position;
            Vector3 fwd = t.forward; fwd.y = 0f; fwd.Normalize();
            Vector3 right = Vector3.Cross(Vector3.up, fwd);
            int layer = FirstLayer(fpc.landLayer.value);
            var cc = fpc.characterController;
            float feet = cc.bounds.min.y;

            Recorder.StartForTest();
            var ledge = Box("vholtat_test_ledge", start + fwd * 7f, fwd, new Vector3(4f, 2.0f, 2f), feet, layer);
            MotorDriver.ScriptActive = true;
            yield return Phase("pk-stand", 0.3f, Vector2.zero, false);
            yield return Phase("pk-run", 0.3f, Vector2.up, false);
            MotorDriver.ScriptJump = true;
            yield return Phase("pk-mantle", 1.2f, Vector2.up, false);
            Shot("vholtat_mantle.png");
            yield return Phase("pk-stop", 0.5f, Vector2.zero, false);
            Destroy(ledge);

            cc.enabled = false;
            t.position = start;
            cc.enabled = true;
            MotorDriver.SetActive(true);
            MotorDriver.Motor.Reset(VHOLTAT.Movement.Vec3.Zero, true); // tests start from rest
            yield return Phase("pk-reset", 0.5f, Vector2.zero, false);

            var wall = Box("vholtat_test_wall", start + fwd * 12f + right * 1.0f, fwd, new Vector3(0.5f, 5f, 18f), feet, layer);
            yield return Phase("wr-run", 0.4f, Vector2.up, false);
            MotorDriver.ScriptJump = true;
            yield return Phase("wr-wallrun", 0.6f, Vector2.up, false);
            Shot("vholtat_wallrun.png");
            yield return Phase("wr-wallrun", 0.3f, Vector2.up, false);
            MotorDriver.ScriptJump = true;
            yield return Phase("wr-walljump", 1.2f, Vector2.up, false);
            yield return Phase("wr-stop", 0.6f, Vector2.zero, false);
            Destroy(wall);

            // Knockback the way weapons apply it: STRAFTAT's AddForce stops the player, then pushes.
            yield return Phase("kb-run", 0.4f, Vector2.up, false);
            fpc.AddForce(new Vector3(0f, 0.5f, 0f) - fwd, 10f);
            Plugin.Logger.LogInfo("[test] knockback applied");
            yield return Phase("kb-push", 1.2f, Vector2.zero, false);
            // Vault: a 0.9 m crate 6 m ahead of a run should be vaulted, keeping ~80 % of the run speed.
            cc.enabled = false; t.position = start; cc.enabled = true;
            MotorDriver.SetActive(true);
            MotorDriver.Motor.Reset(VHOLTAT.Movement.Vec3.Zero, true); // tests start from rest
            yield return Phase("vt-reset", 0.3f, Vector2.zero, false);
            var crate = Box("vholtat_test_crate", start + fwd * 6f, fwd, new Vector3(2f, 0.9f, 1f), feet, layer);
            yield return Phase("vt-run", 1.2f, Vector2.up, false);
            Destroy(crate);

            // Ladder: a 4 m panel on STRAFTAT's ladder layer straight ahead; holding forward should climb at ladder speed.
            cc.enabled = false; t.position = start; cc.enabled = true;
            MotorDriver.SetActive(true);
            MotorDriver.Motor.Reset(VHOLTAT.Movement.Vec3.Zero, true); // tests start from rest
            yield return Phase("ld-reset", 0.3f, Vector2.zero, false);
            var ladderMask = HarmonyLib.Traverse.Create(fpc).Field("ladderLayer").GetValue<LayerMask>();
            var ladder = Box("vholtat_test_ladder", start + fwd * 1.2f, fwd, new Vector3(1.5f, 4f, 0.3f), feet, FirstLayer(ladderMask.value));
            // Vanilla only climbs when its own move action reads input, so hold a real W key through the input system.
            holdW = true;
            yield return Phase("ld-climb", 1.5f, Vector2.up, false);
            holdW = false;
            UnityEngine.InputSystem.InputSystem.QueueStateEvent(UnityEngine.InputSystem.Keyboard.current, new UnityEngine.InputSystem.LowLevel.KeyboardState());
            Destroy(ladder);
            yield return Phase("ld-off", 0.8f, Vector2.zero, false);

            // Jump pad: a boosted teleport (what STRAFTAT's pads and teleporters do) launching up and forward.
            var pad = new GameObject("vholtat_test_pad").transform;
            pad.position = t.position;
            pad.rotation = Quaternion.LookRotation((fwd + Vector3.up).normalized, Vector3.up);
            fpc.Teleport(t.position, 0f, true, pad, 12f, 8f, true);
            Plugin.Logger.LogInfo("[test] pad launch applied");
            yield return Phase("pad", 1.5f, Vector2.zero, false);
            Destroy(pad.gameObject);

            MotorDriver.ScriptActive = false;
            Recorder.StopForTest();
            Plugin.Logger.LogInfo("[test] done");
        }

        /// <summary>
        /// Stuck-spot sweep on a real map: from spawn, 8 headings x 4 s of running with jumps (and slides on every
        /// other heading). Logs a STALL when the player is airborne and hasn't moved 5 cm in 0.5 s (wedged on a lip,
        /// climbing into an overhang...). One map per launch.
        /// </summary>
        private IEnumerator Sweep(FirstPersonController fpc)
        {
            Transform t = fpc.transform;
            var cc = fpc.characterController;
            Vector3 start = t.position;
            string map = SceneManager.GetActiveScene().name;
            int stalls = 0, falls = 0;
            float maxSpeed = 0f;
            MotorDriver.ScriptActive = true;
            for (int h = 0; h < 8; h++)
            {
                if (fpc == null)
                {
                    // Ran off the map: STRAFTAT despawned and respawned the player. Pick up the new one.
                    MotorDriver.ScriptMove = Vector2.zero;
                    for (float w = 0f; w < 20f && fpc == null; w += 0.5f)
                    {
                        foreach (var c in FindObjectsOfType<FirstPersonController>())
                            if (c.IsOwner && c.enabled) fpc = c;
                        yield return new WaitForSeconds(0.5f);
                    }
                    if (fpc == null) break;
                    t = fpc.transform;
                    cc = fpc.characterController;
                    yield return new WaitForSeconds(1f);
                }
                cc.enabled = false; t.position = start; cc.enabled = true;
                MotorDriver.SetActive(true);
            MotorDriver.Motor.Reset(VHOLTAT.Movement.Vec3.Zero, true); // tests start from rest
                MotorDriver.ScriptMove = Vector2.zero;
                yield return new WaitForSeconds(0.3f);
                float a = h * 45f * Mathf.Deg2Rad;
                MotorDriver.ScriptMove = new Vector2(Mathf.Sin(a), Mathf.Cos(a));
                Vector3 last = t.position;
                float stall = 0f, nextSample = 0.25f, nextJump = 0.5f;
                bool reported = false;
                for (float tt = 0f; tt < 4f; tt += Time.deltaTime)
                {
                    if (fpc == null)
                    {
                        falls++;
                        Plugin.Logger.LogInfo($"[test] sweep {map} heading {h * 45}: left the map (void) at {last:F1}");
                        break;
                    }
                    if (tt >= nextJump) { MotorDriver.ScriptJump = true; nextJump += 0.7f; }
                    MotorDriver.ScriptCrouch = h % 2 == 1 && tt % 1.4f < 0.5f;
                    var m = MotorDriver.Motor;
                    maxSpeed = Mathf.Max(maxSpeed, m.HorizontalSpeed);
                    if (tt >= nextSample)
                    {
                        nextSample += 0.25f;
                        float d = (t.position - last).magnitude;
                        last = t.position;
                        stall = !m.IsGroundState && m.State != MoveState.Mantle && d < 0.05f ? stall + 0.25f : 0f;
                        if (stall >= 0.5f && !reported)
                        {
                            reported = true;
                            stalls++;
                            var s = MotorDriver.Sensors;
                            Plugin.Logger.LogWarning($"[test] STALL map={map} heading={h * 45} pos={t.position:F2} state={m.State} vy={m.Velocity.Y:0.00} " +
                                $"h={s.HeightAboveGround:0.00} wallF={s.WallFront} wallL={s.WallLeft} wallR={s.WallRight} ledge={s.LedgeFound} ceil={s.CeilingBlocked}");
                            Shot($"vholtat_stall_{map}_{h * 45}.png");
                        }
                    }
                    yield return null;
                }
                if (fpc != null)
                    Plugin.Logger.LogInfo($"[test] sweep {map} heading {h * 45}: ended at {t.position:F1} state {MotorDriver.Motor.State}");
            }
            MotorDriver.ScriptActive = false;
            MotorDriver.ScriptMove = Vector2.zero;
            Plugin.Logger.LogInfo($"[test] sweep {map}: {stalls} stalls, {falls} runs off the map, top speed {maxSpeed:0.0}");
            Plugin.Logger.LogInfo("[test] done");
        }

        private static GameObject Box(string name, Vector3 centreXZ, Vector3 fwd, Vector3 size, float floorY, int layer)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.layer = layer;
            go.transform.rotation = Quaternion.LookRotation(fwd, Vector3.up);
            go.transform.localScale = size;
            go.transform.position = new Vector3(centreXZ.x, floorY + size.y * 0.5f, centreXZ.z);
            return go;
        }

        private static int FirstLayer(int mask)
        {
            for (int i = 0; i < 32; i++)
                if ((mask & (1 << i)) != 0) return i;
            return 0;
        }

        private IEnumerator Phase(string name, float seconds, Vector2 move, bool crouch)
        {
            MotorDriver.ScriptMove = move;
            MotorDriver.ScriptCrouch = crouch;
            float next = 0f;
            for (float t = 0f; t < seconds; t += Time.deltaTime)
            {
                if (t >= next)
                {
                    next += 0.1f;
                    var m = MotorDriver.Motor;
                    var p = MotorDriver.Current != null ? MotorDriver.Current.transform.position : Vector3.zero;
                    var camNow = MotorDriver.Current != null ? MotorDriver.Current.playerCamera : null;
                    Plugin.Logger.LogInfo($"[test] {name,-10} t={t:0.00} state={m.State,-12} h={m.HorizontalSpeed:0.00} boost={m.Boost:0.00} vy={m.Velocity.Y:0.00} grounded={MotorDriver.Sensors.Grounded} pos=({p.x:0.0},{p.y:0.00},{p.z:0.0}) vfov={(camNow != null ? camNow.fieldOfView : 0f):0.0}");
                }
                yield return null;
            }
        }

        private static void Shot(string file) =>
            ScreenCapture.CaptureScreenshot(Path.Combine(Paths.BepInExRootPath, file));

        private static void LogVanillaTuning(FirstPersonController fpc)
        {
            const BindingFlags f = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            var sb = new System.Text.StringBuilder("[test] vanilla:");
            foreach (var n in new[] { "walkSpeed", "sprintSpeed", "crouchSpeed", "airSpeed", "sprintAirSpeed", "gravity", "jumpGravity",
                         "gravityMultiplier", "jumpForce", "jumpFactor", "airControl", "sprintSlideImpulsion", "sprintSlideDuration", "forceSpeed" })
            {
                var fi = typeof(FirstPersonController).GetField(n, f);
                sb.Append(' ').Append(n).Append('=').Append(fi != null ? fi.GetValue(fpc) : "?");
            }
            var cc = fpc.characterController;
            var cam = fpc.playerCamera;
            if (cam != null)
            {
                float vfov = cam.fieldOfView;
                float hfov = 2f * Mathf.Atan(Mathf.Tan(vfov * 0.5f * Mathf.Deg2Rad) * cam.aspect) * Mathf.Rad2Deg;
                sb.Append($" defaultFOV={fpc.defaultFOV} camVFOV={vfov:0.0} camHFOV={hfov:0.0} aspect={cam.aspect:0.000}");
                sb.Append($" eyeAboveFeet={(cam.transform.position.y - cc.bounds.min.y):0.00}");
                var rf = typeof(FirstPersonController).GetField("runFOV", f);
                sb.Append($" runFOV={(rf != null ? rf.GetValue(fpc) : "?")}");
            }
            sb.Append($" cc.height={cc.height} radius={cc.radius} center={cc.center} step={cc.stepOffset} slope={cc.slopeLimit} scale={fpc.transform.lossyScale}");
            Plugin.Logger.LogInfo(sb.ToString());
        }
    }
}
