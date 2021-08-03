using System.Diagnostics.CodeAnalysis;

using HarmonyLib;

using UnityEngine;

namespace CatsAreOnline.Patches {
    [SuppressMessage("ReSharper", "InconsistentNaming")]
    public static class CurrentIceUpdates {
        public static IceBlock currentIce;

        [HarmonyPatch(typeof(Cat.CatControls), "ActivateIce")]
        [HarmonyPostfix]
        private static void ActivateIce(GameObject ___currentCatIce) {
            if(!___currentCatIce) return;
            currentIce = ___currentCatIce.GetComponent<IceBlock>();
        }

        [HarmonyPatch(typeof(Cat.CatControls), "DeactivateIce")]
        [HarmonyPostfix]
        private static void DeactivateIce() => currentIce = null;
    }
}
