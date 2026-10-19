using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;
using VHOLTAT.Adapter;
using VHOLTAT.Config;

namespace VHOLTAT
{
    // Deliberately not marked [StraftatMod(isVanillaCompatible: true)]: STRAFTAT then folds this plugin into its
    // matchmaking key, so VHOLTAT players only meet lobbies running the same VHOLTAT version.
    [BepInPlugin(MyPluginInfo.PLUGIN_GUID, MyPluginInfo.PLUGIN_NAME, MyPluginInfo.PLUGIN_VERSION)]
    public class Plugin : BaseUnityPlugin
    {
        internal static new ManualLogSource Logger;
        internal static PluginConfig Settings;

        private void Awake()
        {
            Logger = base.Logger;
            Settings = new PluginConfig(Config);
            MotorDriver.Init(Settings.Movement, Settings.Enabled.Value);
            Settings.Enabled.SettingChanged += (_, __) => MotorDriver.SetActive(Settings.Enabled.Value);

            new Harmony(MyPluginInfo.PLUGIN_GUID).PatchAll(typeof(Plugin).Assembly);

            // The BepInEx manager object doesn't survive STRAFTAT's startup, so hotkeys and the overlay live on our
            // own persistent object.
            var runtime = new GameObject("VHOLTAT.Runtime") { hideFlags = HideFlags.HideAndDontSave };
            DontDestroyOnLoad(runtime);
            runtime.AddComponent<Runtime>();

            Logger.LogInfo($"{MyPluginInfo.PLUGIN_NAME} {MyPluginInfo.PLUGIN_VERSION} loaded (Unity {Application.unityVersion}, active={MotorDriver.Active})");
        }
    }

    internal sealed class Runtime : MonoBehaviour
    {
        private void Awake()
        {
            gameObject.AddComponent<DebugOverlay.Overlay>();
            gameObject.AddComponent<Dev.AutoTest>();
        }

        private void Update()
        {
            var s = Plugin.Settings;
            if (Input.GetKeyDown(s.ToggleKey.Value))
            {
                s.Enabled.Value = !s.Enabled.Value; // SettingChanged switches the driver
                Plugin.Logger.LogInfo($"movement: {(s.Enabled.Value ? "VHOLTAT" : "vanilla")}");
            }
            if (Input.GetKeyDown(s.OverlayKey.Value))
                s.ShowOverlay.Value = !s.ShowOverlay.Value;
        }
    }
}
