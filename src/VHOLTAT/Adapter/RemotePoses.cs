using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;
using VHOLTAT.Movement;
using VHOLTAT.Net;

namespace VHOLTAT.Adapter
{
    /// <summary>
    /// Procedural poses for other players running VHOLTAT, applied to their character model on this screen only
    /// (the networked position is untouched): wall runs lean the body away from the wall, wall climbs lean into it,
    /// mantles and vaults tuck the body down, slides lean back a little on top of STRAFTAT's slide animation.
    /// </summary>
    internal sealed class RemotePoses : MonoBehaviour
    {
        private static readonly AccessTools.FieldRef<FirstPersonController, Animator> AnimatorRef =
            AccessTools.FieldRefAccess<FirstPersonController, Animator>("animator");

        private sealed class Model
        {
            public Transform Root;
            public Quaternion BaseRotation;
            public Vector3 BasePosition;
            public float Roll, Pitch, Drop;
        }

        private readonly Dictionary<int, Model> models = new Dictionary<int, Model>();
        private readonly List<int> stale = new List<int>();
        private FirstPersonController[] players = new FirstPersonController[0];
        private float nextScan;

        private void LateUpdate()
        {
            if (!Plugin.Settings.RemotePoses.Value)
                return;
            if (Time.unscaledTime >= nextScan)
            {
                nextScan = Time.unscaledTime + 1f;
                players = FindObjectsOfType<FirstPersonController>(true);
                Prune();
            }

            float k = 1f - Mathf.Exp(-12f * Time.deltaTime);
            foreach (var fpc in players)
            {
                if (fpc == null || fpc.IsOwner || fpc.NetworkObject == null)
                    continue;
                int id = fpc.NetworkObject.ObjectId;
                if (!PoseSync.States.TryGetValue(id, out var entry) || Time.unscaledTime - entry.Value > 3f)
                    continue;
                var model = Get(fpc, id);
                if (model == null)
                    continue;

                float roll = 0f, pitch = 0f, drop = 0f;
                switch (entry.Key)
                {
                    case MoveState.WallRunRight: roll = 22f; break;   // wall on their right: lean left, away from it
                    case MoveState.WallRunLeft: roll = -22f; break;
                    case MoveState.WallClimb: pitch = 12f; break;      // lean into the wall
                    case MoveState.Mantle: pitch = 20f; drop = 0.35f; break;
                    case MoveState.Slide: pitch = -8f; break;
                }
                model.Roll = Mathf.Lerp(model.Roll, roll, k);
                model.Pitch = Mathf.Lerp(model.Pitch, pitch, k);
                model.Drop = Mathf.Lerp(model.Drop, drop, k);
                model.Root.localRotation = model.BaseRotation * Quaternion.Euler(model.Pitch, 0f, model.Roll);
                model.Root.localPosition = model.BasePosition - Vector3.up * model.Drop;
            }
        }

        private Model Get(FirstPersonController fpc, int id)
        {
            if (models.TryGetValue(id, out var m) && m.Root != null)
                return m;
            var anim = AnimatorRef(fpc);
            if (anim == null)
                return null;
            m = new Model { Root = anim.transform, BaseRotation = anim.transform.localRotation, BasePosition = anim.transform.localPosition };
            models[id] = m;
            return m;
        }

        private void Prune()
        {
            stale.Clear();
            foreach (var pair in models)
                if (pair.Value.Root == null) stale.Add(pair.Key);
            foreach (var id in stale)
                models.Remove(id);
        }
    }
}
