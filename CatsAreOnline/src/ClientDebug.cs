using System;

using Lidgren.Network;

namespace CatsAreOnline {
    public class ClientDebug {
        [Flags]
        public enum DataTypeFlag {
            None = 0,
            RegisterPlayer = 0b1,
            PlayerJoined = 0b10,
            PlayerLeft = 0b100,
            PlayerChangedState = 0b1000,
            ChatMessage = 0b10000,
            Command = 0b100000,
            All = 0b111111
        }
        
        public bool enabled = false;

        public DataTypeFlag client = DataTypeFlag.All;
        public DataTypeFlag server = DataTypeFlag.None;
        
        private void PrintClient(DataType dataType) {
            if(!enabled || !client.HasFlag((DataTypeFlag)(1 << (int)dataType))) return;
            Chat.Chat.AddDebugMessage($"[CLIENT] {dataType.ToString()}");
        }

        public void PrintClient(NetOutgoingMessage message) => PrintClient((DataType)message.PeekByte());
        
        public void PrintServer(DataType dataType) {
            if(!enabled || !server.HasFlag((DataTypeFlag)(1 << (int)dataType))) return;
            Chat.Chat.AddDebugMessage($"[SERVER] {dataType.ToString()}");
        }
    }
}
