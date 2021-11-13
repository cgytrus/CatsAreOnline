using System;
using System.Collections.Generic;

using BepInEx.Logging;

using Lidgren.Network;

namespace CatsAreOnline.MessageHandlers;

public class MessageHandler {
    private readonly ManualLogSource _logger;

    private readonly StatusChangedMessageHandler _statusChangedMessageHandler;
    private readonly UnconnectedDataMessageHandler _unconnectedDataMessageHandler;
    private readonly DataMessageHandler _dataMessageHandler;

    private readonly IReadOnlyDictionary<NetIncomingMessageType, Action<NetIncomingMessage>> _messages;

    public MessageHandler(ManualLogSource logger, StatusChangedMessageHandler statusChangedMessageHandler,
        UnconnectedDataMessageHandler unconnectedDataMessageHandler,
        DataMessageHandler dataMessageHandler) {
        _logger = logger;
        _statusChangedMessageHandler = statusChangedMessageHandler;
        _unconnectedDataMessageHandler = unconnectedDataMessageHandler;
        _dataMessageHandler = dataMessageHandler;

        _messages = new Dictionary<NetIncomingMessageType, Action<NetIncomingMessage>> {
            { NetIncomingMessageType.StatusChanged, StatusChangedMessageReceived },
            { NetIncomingMessageType.UnconnectedData, UnconnectedDataMessageReceived },
            { NetIncomingMessageType.Data, DataMessageReceived },
            { NetIncomingMessageType.WarningMessage, WarningMessageReceived },
            { NetIncomingMessageType.Error, ErrorMessageReceived },
            { NetIncomingMessageType.ErrorMessage, ErrorMessageReceived }
        };
    }

    public void MessageReceived(NetIncomingMessage message) {
        NetIncomingMessageType type = message.MessageType;

        if(_messages.TryGetValue(type, out Action<NetIncomingMessage> action)) action(message);
        else _logger.LogInfo($"[UNHANDLED] {message.MessageType.ToString()}");
    }

    private void StatusChangedMessageReceived(NetIncomingMessage message) =>
        _statusChangedMessageHandler.MessageReceived(message);

    private void UnconnectedDataMessageReceived(NetIncomingMessage message) =>
        _unconnectedDataMessageHandler.MessageReceived(message);

    private void DataMessageReceived(NetIncomingMessage message) => _dataMessageHandler.MessageReceived(message);

    private void WarningMessageReceived(NetBuffer message) => _logger.LogWarning($"{message.ReadString()}");

    private void ErrorMessageReceived(NetBuffer message) => _logger.LogError($"{message.ReadString()}");
}