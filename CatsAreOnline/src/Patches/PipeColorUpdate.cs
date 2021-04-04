﻿using System.Diagnostics.CodeAnalysis;

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
                Client.state.color = material.color;
                break;
            }
        }
    }
}
