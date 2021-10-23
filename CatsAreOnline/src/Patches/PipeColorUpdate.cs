using CaLAPI.Patches;

using HarmonyLib;

using PipeSystem;

using UnityEngine;

namespace CatsAreOnline.Patches {
    // ReSharper disable once UnusedType.Global
    internal class PipeColorUpdate : IPatch {
        public void Apply() => On.PipeSystem.PipeCatRepresentation.LateUpdate += (orig, self) => {
            orig(self);
            foreach(Material material in (Material[])AccessTools.Field(typeof(PipeCatRepresentation), "materials")
                .GetValue(self)) {
                if(!material) continue;
                CapturedData.catPipeColor = material.color;
                break;
            }
        };
    }
}
