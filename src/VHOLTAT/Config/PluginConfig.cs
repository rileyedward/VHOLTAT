using System.Collections.Generic;
using System.Reflection;
using BepInEx.Configuration;
using UnityEngine;
using VHOLTAT.Movement;

namespace VHOLTAT.Config
{
    /// <summary>
    /// BepInEx config. Every public float on <see cref="MovementSettings"/> becomes an entry (default = spec value)
    /// and is written back into the live settings object whenever it changes, so edits apply without a restart.
    /// </summary>
    internal sealed class PluginConfig
    {
        public readonly ConfigEntry<bool> Enabled;
        public readonly ConfigEntry<KeyCode> ToggleKey;
        public readonly ConfigEntry<bool> ShowOverlay;
        public readonly ConfigEntry<KeyCode> OverlayKey;

        public readonly MovementSettings Movement = new MovementSettings();

        private readonly List<KeyValuePair<FieldInfo, ConfigEntry<float>>> movementEntries =
            new List<KeyValuePair<FieldInfo, ConfigEntry<float>>>();

        public PluginConfig(ConfigFile file)
        {
            Enabled = file.Bind("General", "Enabled", true,
                "Use VHOLTAT movement for your own player. Toggle in game with ToggleKey.");
            ToggleKey = file.Bind("General", "ToggleKey", KeyCode.F6, "Switches between vanilla and VHOLTAT movement live.");
            ShowOverlay = file.Bind("Debug", "ShowOverlay", false, "Shows speed, state, grounded and wall info.");
            OverlayKey = file.Bind("Debug", "OverlayKey", KeyCode.F7, "Shows/hides the debug overlay.");

            foreach (var field in typeof(MovementSettings).GetFields(BindingFlags.Public | BindingFlags.Instance))
            {
                if (field.FieldType != typeof(float))
                    continue;
                var entry = file.Bind(SectionFor(field.Name), field.Name, (float)field.GetValue(Movement),
                    "Metres / seconds. Default from docs/MOVEMENT_SPEC.md.");
                field.SetValue(Movement, entry.Value);
                movementEntries.Add(new KeyValuePair<FieldInfo, ConfigEntry<float>>(field, entry));
            }
            file.SettingChanged += (_, __) => Apply();
        }

        private void Apply()
        {
            foreach (var pair in movementEntries)
                pair.Key.SetValue(Movement, pair.Value.Value);
        }

        private static string SectionFor(string name)
        {
            if (name.StartsWith("Slide") || name.StartsWith("Slope")) return "Slide";
            if (name.StartsWith("WallRun") || name.StartsWith("WallJump") || name.StartsWith("WallMax")) return "WallRun";
            if (name.StartsWith("WallClimb")) return "WallClimb";
            if (name.StartsWith("Mantle")) return "Mantle";
            if (name.StartsWith("Jump") || name.StartsWith("Coyote") || name.StartsWith("Hop")) return "Jump";
            if (name.StartsWith("Air") || name == "Gravity" || name == "TerminalFallSpeed") return "Air";
            return "Ground";
        }
    }
}
