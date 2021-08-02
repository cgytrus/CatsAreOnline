namespace CatsAreOnline.Shared {
    public enum DataType : byte {
        RegisterPlayer,
        PlayerJoined,
        PlayerLeft,
        PlayerChangedRoom,
        PlayerChangedControllingObject,
        SyncedObjectAdded,
        SyncedObjectRemoved,
        SyncedObjectChangedState,
        ChatMessage,
        Command
    }
}
