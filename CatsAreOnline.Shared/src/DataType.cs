namespace CatsAreOnline.Shared {
    public enum DataType : byte {
        RegisterPlayer,
        PlayerJoined,
        PlayerLeft,
        PlayerChangedState,
        ChatMessage,
        Command
    }
}
