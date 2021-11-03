using System;
using System.Collections.Generic;

using BepInEx.Logging;

using CatsAreOnline.Shared;

using Lidgren.Network;

namespace CatsAreOnline.MessageHandlers {
    public class UnconnectedDataMessageHandler {
        private readonly ManualLogSource _logger;
        private readonly StatusChangedMessageHandler _statusChangedMessageHandler;

        private readonly IReadOnlyDictionary<DataType, Action<NetIncomingMessage>> _messages;

        public UnconnectedDataMessageHandler(ManualLogSource logger,
            StatusChangedMessageHandler statusChangedMessageHandler) {
            _logger = logger;
            _statusChangedMessageHandler = statusChangedMessageHandler;

            _messages = new Dictionary<DataType, Action<NetIncomingMessage>> {
                { DataType.RestartReconnect, RestartReconnectReceived }
            };
        }

        public void MessageReceived(NetIncomingMessage message) {
            DataType type = (DataType)message.ReadByte();

            if(_messages.TryGetValue(type, out Action<NetIncomingMessage> action)) action(message);
            else _logger.LogWarning($"[WARN] Unknown unconnected data message type received: {type.ToString()}");
        }

        private void RestartReconnectReceived(NetIncomingMessage message) {
            if(!Equals(message.SenderEndPoint, _statusChangedMessageHandler.lastConnection)) return;
            MultiplayerPlugin.connected.Value = true;
        }
    }
}
