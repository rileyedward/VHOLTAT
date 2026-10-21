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
    internal enum MomentumPreset
    {
        /// <summary>VHOLUME's own carry rules (default).</summary>
        Vholume,
        /// <summary>Playtest 2 build: chained moves feed the boost (slide-hops, wall jumps, perfect hops, landings, downhill).</summary>
        Playtest2,
    }

    internal enum MovementModel
    {
        /// <summary>VHOLUME's own movement rules, decoded from the game (docs/MOVEMENT_FORMULAS.md). Default.</summary>
        Vholume,
        /// <summary>The 0.3 tuning (locked feel from the playtests); Preset picks its momentum rules.</summary>
        Locked03,
    }

    internal enum CameraStyle
    {
        /// <summary>VHOLUME's camera: fixed horizontal FOV (100 by default), small FOV kicks, 6 degree wall-run roll toward the wall.</summary>
        Vholume,
        /// <summary>The 0.2 camera: STRAFTAT's FOV setting and sprint FOV plus a speed-based kick, 10 degree roll away from the wall.</summary>
        StraftatWide,
    }

    internal sealed class PluginConfig
    {
        public readonly ConfigEntry<MovementModel> Model;
        public readonly ConfigEntry<MomentumPreset> Preset;
        public readonly ConfigEntry<bool> Enabled;
        public readonly ConfigEntry<KeyCode> ToggleKey;
        public readonly ConfigEntry<KeyCode> PresetKey;
        public readonly ConfigEntry<KeyCode> JumpStyleKey;
        public readonly ConfigEntry<bool> ShowOverlay;
        public readonly ConfigEntry<KeyCode> OverlayKey;
        public readonly ConfigEntry<bool> DevKeys;
        public readonly ConfigEntry<bool> MarkSpots;
        public readonly ConfigEntry<KeyCode> MarkSpotKey;
        public readonly ConfigEntry<KeyCode> RecordKey;
        public readonly ConfigEntry<KeyCode> CombatMenuKey;
        public readonly ConfigEntry<float> MaxSpeed;
        public readonly ConfigEntry<bool> VanillaVault;
        public readonly ConfigEntry<float> FovSpeedBonus;
        public readonly ConfigEntry<float> FovFullSpeed;
        public readonly ConfigEntry<float> FovLerp;
        public readonly ConfigEntry<float> WallRunRoll;
        public readonly ConfigEntry<float> StrafeTilt;
        public readonly ConfigEntry<CameraStyle> Camera;
        public readonly ConfigEntry<float> VholumeFov;
        public readonly ConfigEntry<bool> MoveSounds;
        public readonly ConfigEntry<bool> SpeedBlur;
        public readonly ConfigEntry<bool> Speedometer;
        public readonly ConfigEntry<bool> LandingDip;
        public readonly ConfigEntry<bool> UseHostSettings;
        public readonly ConfigEntry<int> RemoteInterpolationTicks;
        public readonly ConfigEntry<bool> RemotePoses;
        public readonly ConfigEntry<bool> DevAutoTest;
        public readonly ConfigEntry<string> DevTestMap;
        public readonly ConfigEntry<string> DevTestMode;
        public readonly ConfigEntry<string> DevFakeVersion;

        public readonly MovementSettings Movement = new MovementSettings();
        public readonly VholumeSettings Vholume = new VholumeSettings();

        /// <summary>The model in use: the local Model entry, or the host's while their settings apply.</summary>
        public MovementModel ActiveModel { get; private set; }

        private readonly List<KeyValuePair<FieldInfo, ConfigEntry<float>>> movementEntries =
            new List<KeyValuePair<FieldInfo, ConfigEntry<float>>>();
        private readonly List<KeyValuePair<FieldInfo, ConfigEntry<float>>> vholumeEntries =
            new List<KeyValuePair<FieldInfo, ConfigEntry<float>>>();

        public PluginConfig(ConfigFile file)
        {
            Enabled = file.Bind("General", "Enabled", true,
                "Use VHOLTAT movement for your own player. Toggle in game with ToggleKey.");
            Model = file.Bind("General", "Model", MovementModel.Vholume,
                "Vholume = VHOLUME's own movement rules (numbers in [Vholume]). Locked03 = the 0.3 tuning " +
                "(numbers in the other sections, momentum rules from Preset).");
            Preset = file.Bind("General", "Preset", MomentumPreset.Vholume,
                "Momentum rules for the Locked03 model. Vholume = VHOLUME's carry rules. Playtest2 = skill-fed boost from playtest 2 " +
                "(its numbers are in [Preset.Playtest2]). Switch live to compare.");
            PresetKey = file.Bind("General", "PresetKey", KeyCode.F8,
                "Cycles live (needs DevKeys): Vholume model, Locked03 with the Vholume preset, Locked03 with the Playtest2 preset.");
            JumpStyleKey = file.Bind("General", "JumpStyleKey", KeyCode.F9,
                "Switches live between the snappier jump (JumpRiseGravity) and VHOLUME's exact jump, for comparison.");
            ToggleKey = file.Bind("General", "ToggleKey", KeyCode.F6, "Switches between vanilla and VHOLTAT movement live.");
            VanillaVault = file.Bind("Mantle", "VanillaVault", false,
                "Keep STRAFTAT's own vault (hop over waist-high walls) alongside VHOLTAT's mantle.");
            FovSpeedBonus = file.Bind("Camera", "FovSpeedBonus", 15f, "Extra FOV (degrees) at FovFullSpeed, starting from run speed.");
            FovFullSpeed = file.Bind("Camera", "FovFullSpeed", 26f, "Speed (m/s) where the FOV bonus is complete.");
            FovLerp = file.Bind("Camera", "FovLerp", 6f, "How fast the FOV follows speed.");
            WallRunRoll = file.Bind("Camera", "WallRunRoll", 10f, "Camera roll (degrees) away from the wall while wall running.");
            MoveSounds = file.Bind("Camera", "MoveSounds", true, "Slide and wall-run sound loops and the vault sound.");
            LandingDip = file.Bind("Camera", "LandingDip", true, "Camera dip on hard landings.");
            SpeedBlur = file.Bind("Camera", "SpeedBlur", false,
                "VHOLUME-style speed effect: a subtle colour fringe that grows above run speed.");
            Speedometer = file.Bind("Camera", "Speedometer", false,
                "VHOLUME-style speed readout in the top-right corner, in VHOLUME units (u = cm/s, run = 1300u).");
            StrafeTilt = file.Bind("Camera", "StrafeTilt", 3f,
                "Head tilt (degrees) into the strafe direction, scaled by speed up to run speed (VHOLUME: 3). 0 = off.");
            Camera = file.Bind("Camera", "Style", CameraStyle.Vholume,
                "Vholume = VHOLUME's camera (fixed horizontal FOV, small kicks, wall-run roll toward the wall). " +
                "StraftatWide = the 0.2 camera (STRAFTAT's FOV setting, sprint widening and speed kick).");
            VholumeFov = file.Bind("Camera", "VholumeFov", 100f,
                "Horizontal FOV for the Vholume camera style (VHOLUME's default is 100). Converted for your screen shape.");
            UseHostSettings = file.Bind("Multiplayer", "UseHostSettings", true,
                "In someone else's lobby, move with the host's VHOLTAT settings (preset and every number) so everyone " +
                "plays by the same rules. Your own config comes back when you leave.");
            RemotePoses = file.Bind("Multiplayer", "RemotePoses", true,
                "Pose other VHOLTAT players' models on your screen: lean on wall runs and climbs, tuck on vaults and mantles.");
            RemoteInterpolationTicks = file.Bind("Multiplayer", "RemoteInterpolationTicks", 8,
                "Position buffer (network ticks, 60 per second) used to draw other players on your screen. Higher = smoother " +
                "at high speed on bad connections, slightly more delay. STRAFTAT's default is 5; 0 leaves it alone.");
            DevAutoTest = file.Bind("Dev", "AutoTest", false,
                "Developer self-test: enters TestMap from the menu, drives a scripted run/slide/jump and logs it.");
            DevFakeVersion = file.Bind("Dev", "FakeVersion", "", "Developer only: pretend to be this VHOLTAT version (version-check test). Leave empty.");
            DevTestMode = file.Bind("Dev", "TestMode", "basic", "basic = run/slide/jump sequence; parkour = spawned ledge + side wall (mantle, wall run, wall jump).");
            DevTestMap = file.Bind("Dev", "TestMap", "Arena_00", "Map for the self-test (exploration mode).");
            ShowOverlay = file.Bind("Debug", "ShowOverlay", false, "Shows speed, state, grounded and wall info.");
            OverlayKey = file.Bind("Debug", "OverlayKey", KeyCode.F7, "Shows/hides the debug overlay.");
            DevKeys = file.Bind("Debug", "DevKeys", false,
                "Enables the developer comparison keys: PresetKey (F8, momentum preset) and JumpStyleKey (F9, VHOLUME's exact jump).");
            MarkSpots = file.Bind("Debug", "MarkSpots", false,
                "Lets MarkSpotKey write the current spot (map, position, state, nearby geometry) to BepInEx/vholtat_spots.txt for bug reports.");
            CombatMenuKey = file.Bind("Debug", "CombatMenuKey", KeyCode.F12, "Shift + this key opens the combat tuning panel.");
            MaxSpeed = file.Bind("Combat", "MaxSpeed", 0f,
                "Hard limit on horizontal speed (m/s) for everyone in your lobby. 0 = no limit (VHOLUME has none).");
            RecordKey = file.Bind("Debug", "RecordKey", KeyCode.F11,
                "Starts/stops a movement recording (BepInEx/recordings/*.csv) when DevKeys or MarkSpots is on.");
            MarkSpotKey = file.Bind("Debug", "MarkSpotKey", KeyCode.F10, "Marks the current spot when MarkSpots is on.");

            foreach (var field in typeof(MovementSettings).GetFields(BindingFlags.Public | BindingFlags.Instance))
            {
                if (field.FieldType != typeof(float))
                    continue;
                var entry = file.Bind(SectionFor(field.Name), field.Name, (float)field.GetValue(Movement),
                    "Metres / seconds. Default from docs/MOVEMENT_SPEC.md.");
                field.SetValue(Movement, entry.Value);
                movementEntries.Add(new KeyValuePair<FieldInfo, ConfigEntry<float>>(field, entry));
            }
            foreach (var field in typeof(VholumeSettings).GetFields(BindingFlags.Public | BindingFlags.Instance))
            {
                if (field.FieldType != typeof(float))
                    continue;
                var entry = file.Bind("Vholume", field.Name, (float)field.GetValue(Vholume),
                    "Vholume model. Metres / seconds. Default from docs/MOVEMENT_FORMULAS.md.");
                field.SetValue(Vholume, entry.Value);
                vholumeEntries.Add(new KeyValuePair<FieldInfo, ConfigEntry<float>>(field, entry));
            }
            Movement.Playtest2Model = Preset.Value == MomentumPreset.Playtest2;
            ActiveModel = Model.Value;
            ActiveMaxSpeed = MaxSpeed.Value;
            foreach (var f in GetType().GetFields(BindingFlags.Public | BindingFlags.Instance))
                if (f.FieldType.IsGenericType && f.FieldType.GetGenericTypeDefinition() == typeof(ConfigEntry<>) && f.GetValue(this) == null)
                    throw new System.InvalidOperationException("config entry not bound: " + f.Name);

            file.SettingChanged += (_, __) => Apply();

            // BepInEx only reads the file at startup; watch it so saved edits apply while the game runs.
            this.file = file;
            try
            {
                watcher = new System.IO.FileSystemWatcher(System.IO.Path.GetDirectoryName(file.ConfigFilePath),
                    System.IO.Path.GetFileName(file.ConfigFilePath))
                {
                    NotifyFilter = System.IO.NotifyFilters.LastWrite | System.IO.NotifyFilters.Size,
                };
                watcher.Changed += (_, __) => fileChanged = true;
                watcher.EnableRaisingEvents = true;
            }
            catch (System.Exception ex)
            {
                Plugin.Logger.LogWarning($"config file watcher unavailable: {ex.Message}");
            }
        }

        private readonly ConfigFile file;
        private readonly System.IO.FileSystemWatcher watcher;
        private volatile bool fileChanged;
        private float reloadAt;

        /// <summary>Called every frame from the main thread: reloads the file shortly after it was saved.</summary>
        public void PollFileReload()
        {
            if (fileChanged)
            {
                fileChanged = false;
                reloadAt = Time.unscaledTime + 0.25f; // let the editor finish writing
            }
            if (reloadAt <= 0f || Time.unscaledTime < reloadAt)
                return;
            reloadAt = 0f;
            try
            {
                file.Reload();
                Apply();
                Plugin.Logger.LogInfo("config reloaded from file");
            }
            catch (System.Exception ex)
            {
                Plugin.Logger.LogWarning($"config reload failed: {ex.Message}");
            }
        }

        /// <summary>Puts every movement number (and the preset) back to its default and saves the file.</summary>
        public void ResetMovement()
        {
            var defaults = new MovementSettings();
            foreach (var pair in movementEntries)
                pair.Value.Value = (float)pair.Key.GetValue(defaults);
            var vdefaults = new VholumeSettings();
            foreach (var pair in vholumeEntries)
                pair.Value.Value = (float)pair.Key.GetValue(vdefaults);
            Preset.Value = MomentumPreset.Vholume;
            Model.Value = MovementModel.Vholume;
            MaxSpeed.Value = 0f;
            Apply();
        }

        /// <summary>True while the host's settings are applied over the local config.</summary>
        public bool RemoteActive { get; private set; }

        /// <summary>Speed limit in use (local or the host's); 0 = none.</summary>
        public float ActiveMaxSpeed { get; private set; }

        /// <summary>The config entry for a [Vholume] number, by field name (combat panel).</summary>
        public ConfigEntry<float> VholumeEntry(string name)
        {
            foreach (var pair in vholumeEntries)
                if (pair.Key.Name == name)
                    return pair.Value;
            return null;
        }

        public int MovementFieldCount => movementEntries.Count + vholumeEntries.Count;

        /// <summary>Writes the local model, preset and movement values (config order) for the host's broadcast.</summary>
        public void WriteLocal(System.IO.BinaryWriter w)
        {
            w.Write((byte)Preset.Value);
            w.Write((byte)Model.Value);
            w.Write(movementEntries.Count);
            w.Write(vholumeEntries.Count);
            foreach (var pair in movementEntries)
                w.Write(pair.Value.Value);
            foreach (var pair in vholumeEntries)
                w.Write(pair.Value.Value);
            w.Write(MaxSpeed.Value);
        }

        /// <summary>Applies the host's values on top of the local config. Returns false if the layout doesn't match.</summary>
        public bool ApplyRemote(System.IO.BinaryReader r)
        {
            var preset = (MomentumPreset)r.ReadByte();
            var model = (MovementModel)r.ReadByte();
            int n = r.ReadInt32();
            int nv = r.ReadInt32();
            if (n != movementEntries.Count || nv != vholumeEntries.Count)
                return false;
            foreach (var pair in movementEntries)
                pair.Key.SetValue(Movement, r.ReadSingle());
            foreach (var pair in vholumeEntries)
                pair.Key.SetValue(Vholume, r.ReadSingle());
            ActiveMaxSpeed = r.ReadSingle();
            Movement.Playtest2Model = preset == MomentumPreset.Playtest2;
            ActiveModel = model;
            RemoteActive = true;
            return true;
        }

        /// <summary>Back to the local config (left the lobby, or became host).</summary>
        public void ClearRemote()
        {
            if (!RemoteActive)
                return;
            RemoteActive = false;
            Apply();
        }

        private void Apply()
        {
            if (RemoteActive)
                return;
            foreach (var pair in movementEntries)
                pair.Key.SetValue(Movement, pair.Value.Value);
            foreach (var pair in vholumeEntries)
                pair.Key.SetValue(Vholume, pair.Value.Value);
            Movement.Playtest2Model = Preset.Value == MomentumPreset.Playtest2;
            ActiveModel = Model.Value;
            ActiveMaxSpeed = MaxSpeed.Value;
        }

        private static string SectionFor(string name)
        {
            if (name.StartsWith("P2")) return "Preset.Playtest2";
            if (name.StartsWith("Boost") || name.Contains("Hop") || name == "WallRunAirCarry") return "Momentum";
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
