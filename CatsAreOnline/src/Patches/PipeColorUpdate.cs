using System.Diagnostics.CodeAnalysis;

using HarmonyLib;

using PipeSystem;

using UnityEngine;

namespace CatsAreOnline.Patches {
    [SuppressMessage("ReSharper", "InconsistentNaming")]
    public static class PipeColorUpdate {
        [HarmonyPatch(typeof(PipeCatRepresentation), "LateUpdate")]
        [HarmonyPostfix]
        public static void UpdatePipeColor(Material[] ___materials) {
            foreach(Material material in ___materials) {
                if(!material) continue;
                PatchesClientProvider.client.catState.color = material.color;
                break;
            }
        }
    }
}
