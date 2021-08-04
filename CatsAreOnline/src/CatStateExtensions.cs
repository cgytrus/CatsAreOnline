using Cat;

namespace CatsAreOnline {
    public static class CatStateExtensions {
        public static float GetScale(this State state) {
            switch(state) {
                case State.Liquid: return 1f;
                default: return 1.35f;
            }
        }
    }
}
