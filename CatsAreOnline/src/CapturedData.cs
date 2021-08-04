using Cat;

using UnityEngine;

namespace CatsAreOnline {
    public static class CapturedData {
        public static CatPartManager catPartManager { get; set; }
        public static CatControls catControls { get; set; }
        public static Sprite catSprite { get; set; }
        public static Color catColor { get; set; }
        public static Color catPipeColor { get; set; }
        public static State catState { get; set; }
        public static float catScale { get; set; }

        public static bool inIce { get; set; }
        public static IceBlock iceBlock { get; set; }
        public static Sprite iceSprite { get; set; }
        public static Color iceColor { get; set; }
        public static float iceRotation { get; set; }

        public static Transform companionTransform { get; set; }
        public static Sprite companionSprite { get; set; }
        public static Color companionColor { get; set; }

        public static bool inJunction { get; set; }
        public static Vector2 junctionPosition { get; set; }
        
        public static Font uiFont { get; set; }
    }
}
