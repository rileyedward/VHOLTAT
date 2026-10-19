using HarmonyLib;
using UnityEngine.InputSystem;

namespace VHOLTAT.Adapter
{
    /// <summary>
    /// Harmony patches on FirstPersonController. Each one only acts for the local owner while VHOLTAT is active;
    /// remote players and the vanilla toggle state run the original code untouched.
    /// </summary>
    [HarmonyPatch(typeof(FirstPersonController))]
    internal static class FirstPersonControllerPatches
    {
        private static bool Ours(FirstPersonController fpc) => MotorDriver.Active && MotorDriver.IsLocal(fpc);

        [HarmonyPrefix, HarmonyPatch("ApplyFinalMovements")]
        private static bool ApplyFinalMovements(FirstPersonController __instance)
        {
            if (!Ours(__instance))
                return true;
            return !MotorDriver.Drive(__instance);
        }

        /// <summary>
        /// Vanilla picks the capsule skin width (0.07 crouched, 0.2 otherwise) at the top of Update, before our motor
        /// decides to slide. Flagging the slide one frame early avoids a skin flip that pops the player upward.
        /// </summary>
        [HarmonyPrefix, HarmonyPatch("Update")]
        private static void UpdatePrefix(FirstPersonController __instance)
        {
            if (Ours(__instance))
                MotorDriver.BeforeUpdate(__instance);
        }

        [HarmonyPostfix, HarmonyPatch("Update")]
        private static void Update(FirstPersonController __instance)
        {
            if (Ours(__instance))
                MotorDriver.AfterUpdate(__instance);
        }

        [HarmonyPrefix, HarmonyPatch(nameof(FirstPersonController.Jump))]
        private static bool Jump(FirstPersonController __instance)
        {
            if (!Ours(__instance))
                return true;
            MotorDriver.QueueJump();
            return false;
        }

        /// <summary>Vanilla slide start. With toggle-crouch it also flips crouch, which we keep.</summary>
        [HarmonyPrefix, HarmonyPatch(nameof(FirstPersonController.Slide))]
        private static bool Slide(FirstPersonController __instance, InputAction.CallbackContext ctx)
        {
            if (!Ours(__instance))
                return true;
            if (__instance.crouchToggle)
                __instance.isCrouching = !__instance.isCrouching;
            return false;
        }

        /// <summary>Teleporters turn the player; momentum turns with them (same yaw delta).</summary>
        [HarmonyPrefix, HarmonyPatch(nameof(FirstPersonController.Teleport))]
        private static void TeleportPrefix(FirstPersonController __instance, out float __state) =>
            __state = __instance.transform.eulerAngles.y;

        [HarmonyPostfix, HarmonyPatch(nameof(FirstPersonController.Teleport))]
        private static void TeleportPostfix(FirstPersonController __instance, float __state)
        {
            if (Ours(__instance))
                MotorDriver.OnTeleport(__instance, __instance.transform.eulerAngles.y - __state);
        }

        [HarmonyPrefix, HarmonyPatch("HandleSlide")]
        private static bool HandleSlide(FirstPersonController __instance) => !Ours(__instance);

        [HarmonyPrefix, HarmonyPatch("CheckForVault")]
        private static bool CheckForVault(FirstPersonController __instance) =>
            !Ours(__instance) || Plugin.Settings.VanillaVault.Value;
    }
}
