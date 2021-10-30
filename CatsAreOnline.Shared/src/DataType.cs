namespace CatsAreOnline.Shared {
    public enum DataType : byte {
        RegisterPlayer,
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
