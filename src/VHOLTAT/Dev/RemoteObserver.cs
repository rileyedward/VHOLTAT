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
        private static readonly Vector3[] Dirs =
        {
            new Vector3(1f, 0f, -0.4f), new Vector3(-1f, 0f, -0.4f), new Vector3(0f, 0f, -1f), new Vector3(0.7f, 0f, 0.7f),
            new Vector3(-0.7f, 0f, 0.7f), new Vector3(0f, 0f, 1f),
        };

        /// <summary>A camera spot around the target with line of sight: the first direction that is clear for the
        /// longest distance (up to 4.5 m), pulled in short of any wall.</summary>
        private static Vector3 ClearView(Transform target)
        {
            Vector3 eye = target.position + Vector3.up * 1.2f;
            Vector3 best = eye + Vector3.up * 2f;
            float bestDist = 0f;
            foreach (var d in Dirs)
            {
                Vector3 dir = (target.rotation * d).normalized + Vector3.up * 0.25f;
                float dist = 3.2f;
                if (Physics.SphereCast(eye, 0.3f, dir.normalized, out RaycastHit hit, dist, ~0, QueryTriggerInteraction.Ignore))
                    dist = Mathf.Max(0.8f, hit.distance - 0.4f);
                if (dist > bestDist + 0.5f)
                {
                    bestDist = dist;
                    best = eye + dir.normalized * dist;
                }
            }
            return best;
        }

        private static float nextAim;
        private static int playerMask = -1;
        /// <summary>Per remote state: body hits, head hits, samples.</summary>
        private static readonly System.Collections.Generic.Dictionary<string, int[]> aimTally =
            new System.Collections.Generic.Dictionary<string, int[]>();

        private static void AimTest(Transform remote, Vector3 from)
        {
            if (playerMask == -1)
            {
                var w = Object.FindObjectOfType<Weapon>(true);
                playerMask = w != null && w.playerLayer.value != 0 ? w.playerLayer.value : ~0;
                Plugin.Logger.LogInfo($"[lan] aim mask {playerMask:X}");
            }
            // Aim where the model is drawn: the torso and head bones (hit colliders hang off the bones).
            Transform torso = FindBone(remote, "Torso"), headBone = FindBone(remote, "Head");
            if (torso == null || headBone == null)
                return;
            var nob = remote.GetComponent<FishNet.Object.NetworkObject>();
            string state = nob != null && Net.PoseSync.States.TryGetValue(nob.ObjectId, out var pose) ? pose.Key.ToString() : "unknown";
            if (!aimTally.TryGetValue(state, out var tally))
                aimTally[state] = tally = new int[6];
            tally[2]++;
            Vector3 body = torso.position, head = headBone.position;
            int rb = Hits(remote, from, body), rh = Hits(remote, from, head);
            if (rb == 1) tally[0]++;
            if (rh == 1) tally[1]++;
            if (rb == 2 || rh == 2) tally[3]++;
            if (InsideHitbox(remote, body)) tally[4]++;
            if (InsideHitbox(remote, head)) tally[5]++;
        }

        /// <summary>The same test Gun uses: the first collider on the player mask is on layer 11 and belongs to this player.</summary>
        /// <returns>1 = hit this player, 2 = something else was in the way, 0 = missed.</returns>
        private static int Hits(Transform remote, Vector3 from, Vector3 at)
        {
            Vector3 dir = at - from;
            if (!Physics.Raycast(from, dir.normalized, out RaycastHit hit, dir.magnitude + 2f, playerMask))
                return 0;
            if (hit.transform.gameObject.layer == 11 && hit.transform.root == remote.root)
                return 1;
            return hit.distance < dir.magnitude - 0.5f ? 2 : 0;
        }

        private static Transform FindBone(Transform root, string name)
        {
            foreach (var t in root.GetComponentsInChildren<Transform>(false))
                if (t.name == name)
                    return t;
            return null;
        }

        private static readonly System.Collections.Generic.List<Collider> boxes = new System.Collections.Generic.List<Collider>();

        /// <summary>Is a point of the drawn model inside one of the player's hit colliders (layer 11)?</summary>
        private static bool InsideHitbox(Transform remote, Vector3 p)
        {
            remote.GetComponentsInChildren(false, boxes);
            foreach (var c in boxes)
            {
                if (c.gameObject.layer != 11 || !c.enabled || c is MeshCollider m && !m.convex)
                    continue;
                if ((c.ClosestPoint(p) - p).sqrMagnitude < 1e-4f)
                    return true;
            }
            return false;
        }

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
            int frames = 0;
            Camera spectator = null;
            var shotStates = new System.Collections.Generic.HashSet<string>();
            string poseKey = null;
            float poseSince = -1f;
            for (float t = 0f; t < 120f; t += Time.deltaTime)
            {
                yield return null;
                if (remote == null || !remote.gameObject.activeInHierarchy)
                {
                    // The host's player is replaced on respawn; follow the new one.
                    remote = null;
                    foreach (var fpc in Object.FindObjectsOfType<FirstPersonController>())
                        if (!fpc.IsOwner)
                        {
                            remote = fpc.transform;
                            anim = HarmonyLib.Traverse.Create(fpc).Field("animator").GetValue<Animator>() ?? fpc.GetComponentInChildren<Animator>(true);
                            last = remote.position;
                            Plugin.Logger.LogInfo($"[lan] observer: now watching {remote.name} at {last}");
                        }
                    if (remote == null) continue;
                }
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
                // Keep STRAFTAT's info popup ("mods detected") out of the shots.
                var popup = HarmonyLib.Traverse.Create(PauseManager.Instance).Field("infoPopup").GetValue<GameObject>();
                if (popup != null && popup.activeSelf) popup.SetActive(false);

                // Spectator camera beside the host so their model (and its pose) is in view.
                if (spectator == null)
                {
                    var go = new GameObject("vholtat_spectator");
                    spectator = go.AddComponent<Camera>();
                    spectator.depth = 100f;
                    spectator.fieldOfView = 45f;
                }
                Vector3 want = ClearView(remote);
                spectator.transform.position = Vector3.Lerp(spectator.transform.position, want, 1f - Mathf.Exp(-8f * Time.deltaTime));
                spectator.transform.LookAt(remote.position + Vector3.up * 1.0f);

                // Combat check: aim from the spectator at the drawn model (body centre and head) and cast the gun's
                // player ray; tally hits per remote movement state.
                if (Time.time >= nextAim && spectator != null)
                {
                    nextAim = Time.time + 0.1f;
                    AimTest(remote, spectator.transform.position);
                }

                // Screenshot each pose state once, a moment after it starts.
                var nob = remote.GetComponent<FishNet.Object.NetworkObject>();
                if (nob != null && Net.PoseSync.States.TryGetValue(nob.ObjectId, out var pose))
                {
                    string key = pose.Key.ToString();
                    if (pose.Key != Movement.MoveState.Ground && pose.Key != Movement.MoveState.Air && !shotStates.Contains(key))
                    {
                        if (poseSince < 0f || poseKey != key) { poseKey = key; poseSince = t; }
                        else if (t - poseSince > 0.15f)
                        {
                            shotStates.Add(key);
                            ScreenCapture.CaptureScreenshot(Path.Combine(Paths.BepInExRootPath, $"vholtat_pose_{key}.png"));
                            Plugin.Logger.LogInfo($"[lan] pose screenshot: {key}");
                        }
                    }
                    else if (poseKey != key) { poseKey = key; poseSince = -1f; }
                }
            }
            foreach (var pair in aimTally)
                Plugin.Logger.LogInfo($"[lan] aim {pair.Key}: n={pair.Value[2]} ray body {pair.Value[0]} head {pair.Value[1]} blocked {pair.Value[3]} | inside hitbox body {pair.Value[4]} head {pair.Value[5]}");
            Plugin.Logger.LogInfo("[lan] observer: done");
        }
    }
}
