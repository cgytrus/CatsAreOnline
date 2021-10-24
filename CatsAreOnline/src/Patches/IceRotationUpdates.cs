using CalApi.Patches;

namespace CatsAreOnline.Patches {
    // ReSharper disable once UnusedType.Global
    internal class IceRotationUpdates : IPatch {
        public void Apply() => On.Cat.CatControls.FixedUpdate += (orig, self) => {
            orig(self);
            if(self != CapturedData.catControls || !self.IsCatIceActive()) return;
            CapturedData.iceRotation = self.GetActiveCatIce().transform.eulerAngles.z;
        };
    }
}
