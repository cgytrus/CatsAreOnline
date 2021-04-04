namespace CatsAreOnline {
    public enum DataType : byte {
        RegisterPlayer,
        PlayerJoined,
        PlayerLeft,
        PlayerChangedState,
        ChatMessage,
        Command
    }
}
