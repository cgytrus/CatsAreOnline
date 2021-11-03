using Cat;

namespace CatsAreOnline {
    public static class CatStateExtensions {
        public static float GetScale(this State state) => state switch {
            State.Liquid => 1f,
            _ => 1.35f
        };
    }
}
