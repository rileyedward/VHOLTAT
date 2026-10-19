using System;

namespace VHOLTAT.Movement
{
    /// <summary>Minimal float vector so the core has no Unity dependency. Y is up, metres.</summary>
    public struct Vec3
    {
        public float X, Y, Z;

        public Vec3(float x, float y, float z) { X = x; Y = y; Z = z; }

        public static readonly Vec3 Zero = new Vec3(0f, 0f, 0f);
        public static readonly Vec3 Up = new Vec3(0f, 1f, 0f);

        public static Vec3 operator +(Vec3 a, Vec3 b) => new Vec3(a.X + b.X, a.Y + b.Y, a.Z + b.Z);
        public static Vec3 operator -(Vec3 a, Vec3 b) => new Vec3(a.X - b.X, a.Y - b.Y, a.Z - b.Z);
        public static Vec3 operator -(Vec3 a) => new Vec3(-a.X, -a.Y, -a.Z);
        public static Vec3 operator *(Vec3 a, float s) => new Vec3(a.X * s, a.Y * s, a.Z * s);
        public static Vec3 operator *(float s, Vec3 a) => new Vec3(a.X * s, a.Y * s, a.Z * s);
        public static Vec3 operator /(Vec3 a, float s) => new Vec3(a.X / s, a.Y / s, a.Z / s);

        public float SqrMagnitude => X * X + Y * Y + Z * Z;
        public float Magnitude => (float)Math.Sqrt(SqrMagnitude);
        public Vec3 Flat => new Vec3(X, 0f, Z);

        public Vec3 Normalized
        {
            get
            {
                float m = Magnitude;
                return m > 1e-6f ? this / m : Zero;
            }
        }

        public static float Dot(Vec3 a, Vec3 b) => a.X * b.X + a.Y * b.Y + a.Z * b.Z;

        public static Vec3 Cross(Vec3 a, Vec3 b) =>
            new Vec3(a.Y * b.Z - a.Z * b.Y, a.Z * b.X - a.X * b.Z, a.X * b.Y - a.Y * b.X);

        /// <summary>Removes the component of v along unit normal n.</summary>
        public static Vec3 ProjectOnPlane(Vec3 v, Vec3 n) => v - n * Dot(v, n);

        public static Vec3 ClampMagnitude(Vec3 v, float max)
        {
            float sq = v.SqrMagnitude;
            return sq > max * max ? v * (max / (float)Math.Sqrt(sq)) : v;
        }

        public override string ToString() => $"({X:0.00}, {Y:0.00}, {Z:0.00})";
    }
}
