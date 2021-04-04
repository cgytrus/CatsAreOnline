using System.Diagnostics.CodeAnalysis;

using HarmonyLib;

using UnityEngine;

namespace CatsAreOnline.Patches {
    [SuppressMessage("ReSharper", "InconsistentNaming")]
    public static class IceRotationUpdates {
        [HarmonyPatch(typeof(Cat.CatControls), "FixedUpdate")]
        [HarmonyPostfix]
        private static void UpdateIceRotation(Cat.CatControls __instance, GameObject ___currentCatIce) {
            if(__instance != Client.playerControls || !___currentCatIce) return;
            Client.state.iceRotation = ___currentCatIce.transform.eulerAngles.z;
        }
    }
}
