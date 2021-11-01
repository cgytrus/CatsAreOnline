namespace CatsAreOnline.Shared {
    public enum DataType : byte {
        PlayerJoined,
        PlayerLeft,
        PlayerChangedWorldPack,
        PlayerChangedWorld,
        PlayerChangedRoom,
        PlayerChangedControllingObject,
        SyncedObjectAdded,
        SyncedObjectRemoved,
        SyncedObjectChangedState,
        ChatMessage,
        Command
    }
}
