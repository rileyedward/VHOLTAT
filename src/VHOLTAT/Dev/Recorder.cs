using System;
using System.Globalization;
using System.IO;
using System.Text;
using BepInEx;
using UnityEngine;
using UnityEngine.SceneManagement;
using VHOLTAT.Adapter;
using VHOLTAT.Movement;

namespace VHOLTAT.Dev
{
    /// <summary>
    /// Movement recorder for playtests ([Debug] RecordKey, default F11; works whenever DevKeys or MarkSpots is on).
    /// While recording, every frame of the local player goes to BepInEx/recordings/vholtat_&lt;map&gt;_&lt;time&gt;.csv:
    /// time, state, position, velocity, speeds, boost, contacts, inputs, events, camera FOV and round state, in metres
    /// and seconds. Works with VHOLTAT on or off (vanilla rows use the CharacterController's velocity).
    /// </summary>
    internal sealed class Recorder : MonoBehaviour
    {
        private const string Header =
            "t,dt,map,vholtat,state,state_t,px,py,pz,vx,vy,vz,hspeed,boost,grounded,ground_ny,height,wall_l,wall_r,wall_f,ledge,vault," +
            "move_x,move_y,jump,crouch,events,vfov,yaw,pitch,can_move,start_round";

        private StreamWriter writer;
        private readonly StringBuilder sb = new StringBuilder(512);
        private float t0;
        private int rows;
        private string path;
        private GUIStyle style;
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        public static bool Recording { get; private set; }
        public static Recorder Instance { get; private set; }

        private void Awake() => Instance = this;

        /// <summary>Self-tests record their runs too.</summary>
        public static void StartForTest() { if (Instance != null && !Recording) Instance.Begin(); }
        public static void StopForTest() { if (Instance != null && Recording) Instance.Stop(); }

        private void Update()
        {
            var cfg = Plugin.Settings;
            bool allowed = cfg.DevKeys.Value || cfg.MarkSpots.Value;
            if (allowed && Input.GetKeyDown(cfg.RecordKey.Value))
            {
                if (Recording) Stop();
                else Begin();
            }
        }

        private void LateUpdate()
        {
            if (!Recording)
                return;
            var fpc = MotorDriver.Current;
            if (fpc == null)
            {
                foreach (var c in FindObjectsOfType<FirstPersonController>())
                    if (c.IsOwner) { fpc = c; break; }
                if (fpc == null) return;
            }
            WriteRow(fpc);
        }

        private void Begin()
        {
            string dir = Path.Combine(Paths.BepInExRootPath, "recordings");
            Directory.CreateDirectory(dir);
            path = Path.Combine(dir, $"vholtat_{SceneManager.GetActiveScene().name}_{DateTime.Now:yyyyMMdd_HHmmss}.csv");
            writer = new StreamWriter(path, false, new UTF8Encoding(false), 1 << 16);
            writer.WriteLine("# VHOLTAT " + MyPluginInfo.PLUGIN_VERSION + " recording, STRAFTAT build " + Application.version +
                             ", model " + (MotorDriver.Motor != null ? MotorDriver.Motor.ModelName : "-") + ", camera " + Plugin.Settings.Camera.Value);
            writer.WriteLine(Header);
            t0 = Time.time;
            rows = 0;
            Recording = true;
            Plugin.Logger.LogInfo($"recording to {path}");
        }

        private void Stop()
        {
            Recording = false;
            writer?.Flush();
            writer?.Dispose();
            writer = null;
            Plugin.Logger.LogInfo($"recording saved: {path} ({rows} frames)");
        }

        private void OnDestroy()
        {
            if (Recording) Stop();
        }

        private void WriteRow(FirstPersonController fpc)
        {
            var m = MotorDriver.Motor;
            var s = MotorDriver.Sensors;
            var inp = MotorDriver.LastInput;
            bool ours = MotorDriver.Active && m != null;
            Vector3 p = fpc.transform.position;
            Vector3 v = ours ? new Vector3(m.Velocity.X, m.Velocity.Y, m.Velocity.Z)
                : fpc.characterController != null ? fpc.characterController.velocity : Vector3.zero;
            var cam = fpc.playerCamera;
            var pm = fpc.pauseManager;

            sb.Length = 0;
            F(Time.time - t0); F(Time.deltaTime);
            sb.Append(SceneManager.GetActiveScene().name).Append(',');
            sb.Append(ours ? 1 : 0).Append(',');
            sb.Append(ours ? m.State.ToString() : "Vanilla").Append(',');
            F(ours ? m.StateTime : 0f);
            F(p.x); F(p.y); F(p.z);
            F(v.x); F(v.y); F(v.z);
            F(new Vector2(v.x, v.z).magnitude);
            F(ours ? m.Boost : 0f);
            sb.Append(s.Grounded ? 1 : 0).Append(',');
            F(s.GroundNormal.Y); F(s.HeightAboveGround);
            sb.Append(s.WallLeft ? 1 : 0).Append(',').Append(s.WallRight ? 1 : 0).Append(',').Append(s.WallFront ? 1 : 0).Append(',');
            sb.Append(s.LedgeFound ? 1 : 0).Append(',').Append(s.VaultFound ? 1 : 0).Append(',');
            F(inp.MoveX); F(inp.MoveY);
            sb.Append(inp.JumpPressed ? 1 : 0).Append(',').Append(inp.CrouchHeld ? 1 : 0).Append(',');
            sb.Append(ours ? (int)m.Events : 0).Append(',');
            F(cam != null ? cam.fieldOfView : 0f);
            F(fpc.transform.eulerAngles.y);
            F(cam != null ? cam.transform.localEulerAngles.x : 0f);
            sb.Append(fpc.sync___get_value_canMove() ? 1 : 0).Append(',');
            sb.Append(pm != null && pm.startRound ? 1 : 0);
            writer.WriteLine(sb.ToString());
            rows++;
        }

        private void F(float x) => sb.Append(x.ToString("0.####", Inv)).Append(',');

        private void OnGUI()
        {
            if (!Recording)
                return;
            if (style == null)
            {
                style = new GUIStyle(GUI.skin.label) { fontSize = 20, fontStyle = FontStyle.Bold };
                style.normal.textColor = new Color(1f, 0.25f, 0.25f);
            }
            GUI.Label(new Rect(16f, Screen.height - 44f, 300f, 30f), "● REC " + Plugin.Settings.RecordKey.Value, style);
        }
    }
}
