using System.Reflection;

using CalApi.Patches;

using HarmonyLib;

using PipeSystem;

namespace CatsAreOnline.Patches {
    // ReSharper disable once UnusedType.Global
    internal class JunctionUpdates : IPatch {
        private static readonly FieldInfo controller = AccessTools.Field(typeof(PipeObject), "controller");
        
        public void Apply() {
            On.PipeSystem.PipeJunction.EnterJunction += (orig, self, pipeObject) => {
                orig(self, pipeObject);
                if(controller.GetValue(pipeObject) == null) return;
                CapturedData.junctionPosition = self.transform.position;
                CapturedData.inJunction = true;
            };

            On.PipeSystem.PipeJunction.ExitJunction += (orig, self, pipeObject, point) => {
                orig(self, pipeObject, point);
                if(controller.GetValue(pipeObject) == null) return;
                CapturedData.inJunction = false;
            };

            On.Cat.CatPartManager.SpawnCatCoroutine += (orig, self, position) => {
                CapturedData.inJunction = false;
                return orig(self, position);
            };
        }
    }
}
