using System;

using CatsAreOnline.Shared;

using Lidgren.Network;

namespace CatsAreOnline {
    public class ClientDebug {
        [Flags]
        public enum DataTypeFlag {
            None = 0,
            All = -1,
            RegisterPlayer = 1,
            PlayerJoined = 1 << 1,
            PlayerLeft = 1 << 2,
            PlayerChangedState = 1 << 3,
            ChatMessage = 1 << 4,
            Command = 1 << 5
        }
        
        public bool enabled = false;

        public DataTypeFlag client = DataTypeFlag.All;
        public DataTypeFlag server = DataTypeFlag.None;
        
        private void PrintClient(DataType dataType) {
            if(!enabled || ((int)client & 1 << (int)dataType) == 0) return;
            Chat.Chat.AddDebugMessage($"[CLIENT] {dataType.ToString()}");
        }

        public void PrintClient(NetOutgoingMessage message) => PrintClient((DataType)message.PeekByte());
        
        public void PrintServer(DataType dataType) {
            if(!enabled || ((int)server & 1 << (int)dataType) == 0) return;
            Chat.Chat.AddDebugMessage($"[SERVER] {dataType.ToString()}");
        }
    }
}
