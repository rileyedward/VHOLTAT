namespace VHOLTAT.Movement
{
    /// <summary>
    /// What the engine adapter needs from a movement model. Two models implement it: <see cref="VholumeMotor"/>
    /// (VHOLUME's rules as decoded in docs/MOVEMENT_FORMULAS.md, the default) and <see cref="MovementMotor"/> (the
    /// 0.3 tuning Riley locked before the decode, kept as an option).
    /// </summary>
    public abstract class MotorBase
    {
        public Vec3 Velocity;
        public MoveState State { get; protected set; } = MoveState.Air;
        public MoveEvents Events { get; protected set; }
        public float StateTime { get; protected set; }
        /// <summary>Normal of the wall being run on or climbed (valid in those states).</summary>
        public Vec3 WallNormal { get; protected set; }
        /// <summary>Momentum on top of the base speeds (m/s).</summary>
        public float Boost { get; protected set; }

        public float HorizontalSpeed => Velocity.Flat.Magnitude;
        public bool IsGroundState => State == MoveState.Ground || State == MoveState.Crouch || State == MoveState.Slide;
        public bool WantsCrouchedCapsule => State == MoveState.Crouch || State == MoveState.Slide;

        /// <summary>Base running speed (m/s), for camera and effects scaling.</summary>
        public abstract float RunSpeed { get; }
        /// <summary>Lowest speed at which a crouch press starts a slide.</summary>
        public abstract float SlideStartSpeed { get; }
        /// <summary>Short label for overlays and recordings.</summary>
        public abstract string ModelName { get; }

        /// <summary>Clears all state (respawn, teleport, toggling the mod on).</summary>
        public abstract void Reset(Vec3 velocity, bool grounded);
        /// <summary>External velocity change (explosions, knockback, jump pads).</summary>
        public abstract void AddImpulse(Vec3 deltaV);
        /// <summary>Knockback that replaces movement: drop horizontal speed and momentum.</summary>
        public abstract void CancelHorizontal();
        public abstract void Step(in MovementInput input, in MovementSensors sensors, float dt);
    }
}
