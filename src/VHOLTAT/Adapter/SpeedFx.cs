using UnityEngine;
using UnityEngine.Rendering.PostProcessing;
using VHOLTAT.Movement;

namespace VHOLTAT.Adapter
{
    /// <summary>
    /// VHOLUME-style speed effect on STRAFTAT's own post-processing volume: chromatic aberration that fades in above
    /// run speed (full at twice run speed). STRAFTAT has no radial blur, so this stands in for VHOLUME's speed blur.
    /// </summary>
    internal static class SpeedFx
    {
        private const float MaxIntensity = 0.35f;
        private static PostProcessVolume volume;
        private static ChromaticAberration aberration;

        public static void Apply(FirstPersonController fpc, MotorBase m)
        {
            var v = fpc.volume;
            if (v == null)
                return;
            if (v != volume)
            {
                volume = v;
                aberration = null;
                var profile = v.profile; // per-volume instance, the shared asset stays untouched
                if (profile != null && !profile.TryGetSettings(out aberration))
                    aberration = profile.AddSettings<ChromaticAberration>();
                if (aberration != null)
                {
                    aberration.enabled.Override(true);
                    aberration.intensity.Override(0f);
                }
            }
            if (aberration == null)
                return;

            float target = 0f;
            if (Plugin.Settings.SpeedBlur.Value && MotorDriver.Active)
            {
                float run = m.RunSpeed;
                float k = Mathf.Clamp01((m.HorizontalSpeed - run) / run);
                target = MaxIntensity * k * k;
            }
            float cur = aberration.intensity.value;
            aberration.intensity.value = Mathf.Lerp(cur, target, 1f - Mathf.Exp(-6f * Time.deltaTime));
        }
    }
}
