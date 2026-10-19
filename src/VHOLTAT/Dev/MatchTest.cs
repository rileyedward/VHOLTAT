using System.Collections;
using FishNet;
using UnityEngine;
using VHOLTAT.Adapter;

namespace VHOLTAT.Dev
{
    /// <summary>
    /// Dev only ([Dev] TestMode = match, host of the two-copy LAN test): creates a lobby, waits for the client copy,
    /// starts a real match on the test map and checks round-start freeze, movement during the round, death and
    /// movement after the respawn (a new controller).
    /// </summary>
    internal static class MatchTest
    {
        public static IEnumerator Host()
        {
            Plugin.Logger.LogInfo("[match] creating lobby");
            SteamLobby.Instance.CreateLobbyDirect();
            for (float w = 0f; w < 120f && !(InstanceFinder.IsServer && InstanceFinder.ServerManager.Clients.Count >= 2); w += 1f)
                yield return new WaitForSeconds(1f);
            Plugin.Logger.LogInfo($"[match] {InstanceFinder.ServerManager.Clients.Count} connections");
            yield return new WaitForSeconds(5f);

            var motor = SceneMotor.Instance;
            if (motor.PlayListMaps.Count == 0)
            {
                motor.PlayListMaps.Add(Plugin.Settings.DevTestMap.Value);
                motor.PlayListMapsQueue.Enqueue(Plugin.Settings.DevTestMap.Value);
            }
            Plugin.Logger.LogInfo("[match] starting the game");
            PauseManager.Instance.StartGameSteam();

            // Round 1: freeze, then run + slide.
            FirstPersonController first = null;
            yield return WaitForLocal(null, f => first = f);
            if (first == null) yield break;
            MotorDriver.ScriptActive = true;
            MotorDriver.ScriptMove = Vector2.up;
            yield return Log("r1-freeze", 3f);
            for (float w = 0f; w < 15f && (PauseManager.Instance.startRound || first.startOfRound); w += 0.25f)
                yield return new WaitForSeconds(0.25f);
            yield return Log("r1-run", 1.0f);
            MotorDriver.ScriptCrouch = true;
            yield return Log("r1-slide", 0.8f);
            MotorDriver.ScriptCrouch = false;

            Plugin.Logger.LogInfo("[match] killing the local player");
            first.GetComponent<PlayerHealth>().RemoveHealth(1000f);

            // Round 2: a new controller must start clean and move normally.
            FirstPersonController second = null;
            yield return WaitForLocal(first, f => second = f);
            if (second != null)
            {
                Plugin.Logger.LogInfo($"[match] respawned as a new controller, motor state {MotorDriver.Motor.State} speed {MotorDriver.Motor.HorizontalSpeed:0.0}");
                for (float w = 0f; w < 15f && (PauseManager.Instance.startRound || second.startOfRound); w += 0.25f)
                    yield return new WaitForSeconds(0.25f);
                yield return Log("r2-run", 1.5f);
            }
            MotorDriver.ScriptActive = false;
            Plugin.Logger.LogInfo("[test] done");
        }

        private static IEnumerator WaitForLocal(FirstPersonController old, System.Action<FirstPersonController> found)
        {
            for (float w = 0f; w < 60f; w += 0.5f)
            {
                foreach (var c in Object.FindObjectsOfType<FirstPersonController>())
                    if (c.IsOwner && c != old && c.enabled)
                    {
                        Plugin.Logger.LogInfo($"[match] local controller found in {UnityEngine.SceneManagement.SceneManager.GetActiveScene().name}");
                        found(c);
                        yield break;
                    }
                yield return new WaitForSeconds(0.5f);
            }
            Plugin.Logger.LogWarning("[match] no local controller");
        }

        private static IEnumerator Log(string name, float seconds)
        {
            float next = 0f;
            for (float t = 0f; t < seconds; t += Time.deltaTime)
            {
                if (t >= next)
                {
                    next += 0.2f;
                    var m = MotorDriver.Motor;
                    var c = MotorDriver.Current;
                    var p = c != null ? c.transform.position : Vector3.zero;
                    var pm = PauseManager.Instance;
                    Plugin.Logger.LogInfo($"[test] {name,-10} t={t:0.00} state={m.State,-10} h={m.HorizontalSpeed:0.00} vy={m.Velocity.Y:0.00} startRound={pm.startRound} canMove={(c != null && c.sync___get_value_canMove())} pos=({p.x:0.0},{p.y:0.00},{p.z:0.0})");
                }
                yield return null;
            }
        }
    }
}
