using System.Diagnostics.CodeAnalysis;

using HarmonyLib;

using UnityEngine;

namespace CatsAreOnline.Patches {
    [SuppressMessage("ReSharper", "InconsistentNaming")]
    public static class CurrentIceUpdates {
        [HarmonyPatch(typeof(Cat.CatControls), "ActivateIce")]
        [HarmonyPostfix]
        private static void ActivateIce(GameObject ___currentCatIce) {
            if(!___currentCatIce) {
                CapturedData.inIce = false;
                return;
            }
            CapturedData.iceBlock = ___currentCatIce.GetComponent<IceBlock>();
            CapturedData.inIce = true;
        }

        [HarmonyPatch(typeof(Cat.CatControls), "DeactivateIce")]
        [HarmonyPostfix]
        private static void DeactivateIce() {
            CapturedData.inIce = false;
            CapturedData.iceBlock = null;
        }
    }
}
