using UnityEngine;
using VHOLTAT.Movement;

namespace VHOLTAT.Adapter
{
    /// <summary>Fills <see cref="MovementSensors"/> from physics around the local player. Allocation free.</summary>
    internal static class SensorProbe
    {
        private const QueryTriggerInteraction NoTriggers = QueryTriggerInteraction.Ignore;

        /// <summary>Extra reach beyond the capsule radius for side walls (spec: side traces of 75 cm from centre).</summary>
        public static float SideReach = 0.45f;
        public static float FrontReach = 0.5f;
        /// <summary>Lowest ledge (above the feet) that counts as a mantle rather than a step.</summary>
        public static float MantleMinHeight = 0.5f;
        /// <summary>Highest ledge above the feet the mantle can reach.</summary>
        public static float MantleMaxHeight = 2.2f;

        public static void Probe(FirstPersonController fpc, CharacterController cc, Vector3 lookFlat, int mask, ref MovementSensors s)
        {
            Transform t = fpc.transform;
            Vector3 pos = t.position;
            Bounds b = cc.bounds;
            float feet = b.min.y;
            float radius = cc.radius;
            Vector3 centre = b.center;

            s.Position = ToVec(pos);
            s.Grounded = cc.isGrounded;

            if (Physics.Raycast(new Vector3(centre.x, feet + 0.1f, centre.z), Vector3.down, out RaycastHit ground, 20f, mask, NoTriggers))
            {
                s.HeightAboveGround = ground.distance - 0.1f;
                s.GroundNormal = s.Grounded || s.HeightAboveGround < 0.3f ? ToVec(ground.normal) : Vec3.Up;
            }
            else
            {
                s.HeightAboveGround = 20f;
                s.GroundNormal = Vec3.Up;
            }

            float standHeight = Mathf.Max(cc.height, 1.8f);
            s.CeilingBlocked = Physics.Raycast(new Vector3(centre.x, feet + 0.3f, centre.z), Vector3.up, standHeight, mask, NoTriggers);

            Vector3 right = Vector3.Cross(Vector3.up, lookFlat);
            s.WallRight = Wall(centre, right, radius + SideReach, mask, out s.WallRightNormal);
            s.WallLeft = Wall(centre, -right, radius + SideReach, mask, out s.WallLeftNormal);
            s.WallFront = Wall(centre, lookFlat, radius + FrontReach, mask, out s.WallFrontNormal);

            s.LedgeFound = false;
            if (s.WallFront && !s.Grounded)
                s.LedgeFound = FindLedge(cc, pos, feet, radius, lookFlat, mask, ref s);
        }

        private static bool Wall(Vector3 origin, Vector3 dir, float reach, int mask, out Vec3 normal)
        {
            if (Physics.Raycast(origin, dir, out RaycastHit hit, reach, mask, NoTriggers))
            {
                normal = ToVec(hit.normal);
                return true;
            }
            normal = Vec3.Zero;
            return false;
        }

        /// <summary>
        /// Looks for a walkable top surface just past the wall in front, within reach of the hands, with room for
        /// the capsule to stand on it.
        /// </summary>
        private static bool FindLedge(CharacterController cc, Vector3 pos, float feet, float radius, Vector3 fwd, int mask, ref MovementSensors s)
        {
            float top = feet + MantleMaxHeight + 0.2f;
            Vector3 origin = new Vector3(pos.x, top, pos.z) + fwd * (radius + FrontReach + 0.15f);
            // Nothing may block the path over the top of the wall.
            if (Physics.Raycast(new Vector3(pos.x, top, pos.z), fwd, radius + FrontReach + 0.15f, mask, NoTriggers))
                return false;
            if (!Physics.Raycast(origin, Vector3.down, out RaycastHit hit, MantleMaxHeight + 0.2f - MantleMinHeight, mask, NoTriggers))
                return false;
            if (hit.normal.y < 0.88f)
                return false;

            float ledgeFeet = hit.point.y + 0.02f;
            float height = cc.height;
            Vector3 bottom = new Vector3(hit.point.x, ledgeFeet + radius + 0.01f, hit.point.z);
            Vector3 upper = bottom + Vector3.up * Mathf.Max(0f, height - 2f * radius);
            if (Physics.CheckCapsule(bottom, upper, radius * 0.95f, mask, NoTriggers))
                return false;

            // Target is expressed in transform-position space (the transform may not sit at the feet).
            float offset = pos.y - feet;
            s.LedgeTarget = new Vec3(hit.point.x, ledgeFeet + offset, hit.point.z);
            return true;
        }

        public static Vec3 ToVec(Vector3 v) => new Vec3(v.x, v.y, v.z);
        public static Vector3 ToUnity(Vec3 v) => new Vector3(v.X, v.Y, v.Z);
    }
}
