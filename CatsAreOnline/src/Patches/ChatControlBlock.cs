using System.Diagnostics.CodeAnalysis;

using Cat;

using HarmonyLib;

namespace CatsAreOnline.Patches {
    [SuppressMessage("ReSharper", "InconsistentNaming")]
    [SuppressMessage("ReSharper", "RedundantAssignment")]
    public static class ChatControlBlock {
        [HarmonyPatch(typeof(CatControls), "InputCheck")]
        [HarmonyPrefix]
        private static bool ControlsBlocker(float ___movementDirection) {
            if(!Chat.Chat.chatFocused) return true;
            ___movementDirection = 0f;
            return false;
        }
        [HarmonyPatch(typeof(PauseScreen), "Update")]
        [HarmonyPrefix]
        private static bool PauseBlocker() => !Chat.Chat.chatFocused;
    }
}
