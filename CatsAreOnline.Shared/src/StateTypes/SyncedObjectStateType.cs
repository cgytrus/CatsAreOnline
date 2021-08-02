namespace CatsAreOnline.Shared.StateTypes {
    public enum SyncedObjectStateType : byte {
        Position,
        Color,
        Scale,
        Rotation,
        Last = Rotation
    }
}
