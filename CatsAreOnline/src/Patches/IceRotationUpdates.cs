using System.Diagnostics.CodeAnalysis;

using HarmonyLib;

using UnityEngine;

namespace CatsAreOnline.Patches {
    [SuppressMessage("ReSharper", "InconsistentNaming")]
    public static class IceRotationUpdates {
        [HarmonyPatch(typeof(Cat.CatControls), "FixedUpdate")]
        [HarmonyPostfix]
        private static void UpdateIceRotation(Cat.CatControls __instance, GameObject ___currentCatIce) {
            if(__instance != CapturedData.catControls || !___currentCatIce) return;
            CapturedData.iceRotation = ___currentCatIce.transform.eulerAngles.z;
        }
    }
}
