using CalApi.Patches;

namespace CatsAreOnline.Patches {
    // ReSharper disable once UnusedType.Global
    internal class CurrentIceUpdates : IPatch {
        public void Apply() {
            On.Cat.CatControls.ActivateIce += (orig, self) => {
                orig(self);
                CapturedData.inIce = self.IsCatIceActive();
                if(!CapturedData.inIce) return;
                CapturedData.iceBlock = self.GetActiveCatIce().GetComponent<IceBlock>();
            };

            On.Cat.CatControls.DeactivateIce += (orig, self, destroyed) => {
                orig(self, destroyed);
                CapturedData.inIce = false;
                CapturedData.iceBlock = null;
            };
        }
    }
}
