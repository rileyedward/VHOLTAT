using System.Collections;
using System.IO;
using System.Reflection;
using BepInEx;
using UnityEngine;
using UnityEngine.SceneManagement;
using VHOLTAT.Adapter;

namespace VHOLTAT.Dev
{
    /// <summary>
    /// Developer self-test ([Dev] AutoTest = true): enters the test map from the main menu, then drives a scripted
    /// run / slide / slide-jump / strafe sequence through the motor and logs speed and state every 0.1 s.
    /// </summary>
    internal sealed class AutoTest : MonoBehaviour
    {
        private bool entered, tested;

        private void OnEnable() => SceneManager.sceneLoaded += OnSceneLoaded;
        private void OnDisable() => SceneManager.sceneLoaded -= OnSceneLoaded;

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (!Plugin.Settings.DevAutoTest.Value)
                return;
            if (scene.name == "MainMenu" && !entered)
            {
                entered = true;
                StartCoroutine(Enter());
            }
            else if (scene.name == Plugin.Settings.DevTestMap.Value && !tested)
            {
                tested = true;
                StartCoroutine(Run());
            }
        }

        private IEnumerator Enter()
        {
            yield return new WaitForSeconds(8f);
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
            yield return new WaitForSeconds(4f);
            Plugin.Settings.ShowOverlay.Value = true;
            if (Plugin.Settings.DevTestMode.Value == "parkour")
            {
                yield return Parkour(fpc);
                yield break;
            }

            MotorDriver.ScriptActive = true;
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
            Shot("vholtat_end.png");
            Plugin.Logger.LogInfo("[test] done");
        }

        /// <summary>Spawns a 1.3 m ledge ahead (mantle), then a long wall on the right (wall run + wall jump).</summary>
        private IEnumerator Parkour(FirstPersonController fpc)
        {
            Transform t = fpc.transform;
            Vector3 start = t.position;
            Vector3 fwd = t.forward; fwd.y = 0f; fwd.Normalize();
            Vector3 right = Vector3.Cross(Vector3.up, fwd);
            int layer = FirstLayer(fpc.landLayer.value);
            var cc = fpc.characterController;
            float feet = cc.bounds.min.y;

            var ledge = Box("vholtat_test_ledge", start + fwd * 7f, fwd, new Vector3(4f, 1.3f, 2f), feet, layer);
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
            MotorDriver.ScriptActive = false;
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
                    Plugin.Logger.LogInfo($"[test] {name,-10} t={t:0.00} state={m.State,-12} h={m.HorizontalSpeed:0.00} boost={m.Boost:0.00} vy={m.Velocity.Y:0.00} grounded={MotorDriver.Sensors.Grounded} pos=({p.x:0.0},{p.y:0.00},{p.z:0.0})");
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
            sb.Append($" cc.height={cc.height} radius={cc.radius} center={cc.center} step={cc.stepOffset} slope={cc.slopeLimit} scale={fpc.transform.lossyScale}");
            Plugin.Logger.LogInfo(sb.ToString());
        }
    }
}
