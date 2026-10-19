using System.Collections;
using System.IO;
using BepInEx;
using UnityEngine;

namespace VHOLTAT.Dev
{
    /// <summary>
    /// Dev only (LAN client copy): watches the host's player as this client sees it and logs, twice a second, its
    /// speed, how uneven the per-frame motion is (jitter), the synced animator flags, and whether the host's VHOLTAT
    /// settings arrived. Takes a few screenshots.
    /// </summary>
    internal static class RemoteObserver
    {
        public static IEnumerator Watch()
        {
            Transform remote = null;
            Animator anim = null;
            for (float t = 0f; t < 60f && remote == null; t += 1f)
            {
                foreach (var fpc in Object.FindObjectsOfType<FirstPersonController>(true))
                    if (!fpc.IsOwner)
                    {
                        remote = fpc.transform;
                        anim = HarmonyLib.Traverse.Create(fpc).Field("animator").GetValue<Animator>() ?? fpc.GetComponentInChildren<Animator>(true);
                    }
                yield return new WaitForSeconds(1f);
            }
            if (remote == null)
            {
                Plugin.Logger.LogWarning("[lan] observer: no remote player found");
                yield break;
            }
            Plugin.Logger.LogInfo($"[lan] observer: watching {remote.name}, animator {(anim != null ? anim.name : "none")}");

            Vector3 last = remote.position;
            float windowStart = Time.time, sum = 0f, sumSq = 0f, maxStep = 0f;
            int frames = 0, shots = 0;
            for (float t = 0f; t < 70f && remote != null; t += Time.deltaTime)
            {
                yield return null;
                if (remote == null) break;
                Vector3 p = remote.position;
                float dt = Mathf.Max(Time.deltaTime, 1e-4f);
                float speed = new Vector2(p.x - last.x, p.z - last.z).magnitude / dt;
                last = p;
                sum += speed; sumSq += speed * speed; frames++;
                maxStep = Mathf.Max(maxStep, speed);
                if (Time.time - windowStart >= 0.5f && frames > 0)
                {
                    float mean = sum / frames;
                    float sd = Mathf.Sqrt(Mathf.Max(0f, sumSq / frames - mean * mean));
                    string flags = anim == null ? "" :
                        $" anim slide={anim.GetBool("Slide")} grounded={anim.GetBool("Grounded")} leanL={anim.GetBool("LeanLeft")} leanR={anim.GetBool("LeanRight")} move={anim.GetFloat("MovementSpeed"):0.0}";
                    Plugin.Logger.LogInfo($"[lan] remote t={t:0.0} speed={mean:0.0} sd={sd:0.0} peak={maxStep:0.0} y={p.y:0.00}{flags} hostSettings={Plugin.Settings.RemoteActive}");
                    windowStart = Time.time; sum = sumSq = maxStep = 0f; frames = 0;
                }
                if (shots < 3 && t > 20f + shots * 8f)
                {
                    ScreenCapture.CaptureScreenshot(Path.Combine(Paths.BepInExRootPath, $"vholtat_lan_client_{shots}.png"));
                    shots++;
                }
            }
            Plugin.Logger.LogInfo("[lan] observer: done");
        }
    }
}
