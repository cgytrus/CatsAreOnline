using CalApi.Patches;

using Cat;

using HarmonyLib;

namespace CatsAreOnline.Patches {
    // ReSharper disable once UnusedType.Global
    internal class ChatControlBlock : IPatch {
        public void Apply() {
            On.Cat.CatControls.InputCheck += (orig, self) => {
                if(!Chat.Chat.chatFocused) {
                    orig(self);
                    return;
                }

                AccessTools.Field(typeof(CatControls), "movementDirection").SetValue(self, 0f);
            };

            On.PauseScreen.Update += (orig, self) => {
                if(!Chat.Chat.chatFocused) orig(self);
            };
        }
    }
}
