using System.Diagnostics.CodeAnalysis;

using HarmonyLib;

using PipeSystem;

namespace CatsAreOnline.Patches {
    [SuppressMessage("ReSharper", "InconsistentNaming")]
    public static class JunctionUpdates {
        [HarmonyPatch(typeof(PipeJunction), "EnterJunction", typeof(PipeObject))]
        [HarmonyPostfix]
        private static void EnterClientJunction(PipeJunction __instance, PipeObject pipeObject) {
            if(AccessTools.Field(typeof(PipeObject), "controller").GetValue(pipeObject) == null) return;
            Client.junctionPosition = __instance.transform.position;
            Client.inJunction = true;
        }
        
        [HarmonyPatch(typeof(PipeJunction), "ExitJunction", typeof(PipeObject), typeof(PipeEndPoint))]
        [HarmonyPostfix]
        private static void ExitClientJunction(PipeJunction __instance, PipeObject pipeObject) {
            if(AccessTools.Field(typeof(PipeObject), "controller").GetValue(pipeObject) == null) return;
            Client.inJunction = false;
        }
    }
}
