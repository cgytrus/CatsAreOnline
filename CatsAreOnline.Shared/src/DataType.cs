namespace CatsAreOnline.Shared {
    public enum DataType : byte {
        RestartReconnect,
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
