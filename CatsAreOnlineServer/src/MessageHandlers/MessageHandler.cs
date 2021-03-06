using System;
using System.Collections.Generic;
using System.Net;

using CatsAreOnlineServer.SyncedObjects;

using Lidgren.Network;

using NLog;

namespace CatsAreOnlineServer.MessageHandlers;

public class MessageHandler {
    private static readonly Logger logger = LogManager.GetCurrentClassLogger();
    private static readonly Logger progressLogger = logger.WithProperty("progress", true);
    private static readonly Logger unhandledLogger = logger.WithProperty("unhandled", true);

    public NetServer server { private get; init; }
    public Dictionary<NetConnection, Player> playerRegistry { private get; init; }
    public Dictionary<Guid, SyncedObject> syncedObjectRegistry { private get; init; }
    public StatusChangedMessageHandler statusChangedMessageHandler { private get; init; }
    public DataMessageHandler dataMessageHandler { private get; init; }

    private readonly IReadOnlyDictionary<NetIncomingMessageType, Action<NetIncomingMessage>> _messages;

    public MessageHandler() => _messages = new Dictionary<NetIncomingMessageType, Action<NetIncomingMessage>> {
        { NetIncomingMessageType.ConnectionApproval, ConnectionApprovalReceived },
        { NetIncomingMessageType.StatusChanged, StatusChangedReceived },
        { NetIncomingMessageType.Data, DataReceived },
        { NetIncomingMessageType.ConnectionLatencyUpdated, ConnectionLatencyUpdatedReceived },
        { NetIncomingMessageType.VerboseDebugMessage, VerboseDebugMessageReceived },
        { NetIncomingMessageType.DebugMessage, DebugMessageReceived },
        { NetIncomingMessageType.WarningMessage, WarningMessageReceived },
        { NetIncomingMessageType.Error, ErrorMessageReceived },
        { NetIncomingMessageType.ErrorMessage, ErrorMessageReceived }
    };

    public void MessageReceived(NetIncomingMessage message) {
        if(_messages.TryGetValue(message.MessageType, out Action<NetIncomingMessage> action)) action(message);
        else unhandledLogger.Info($"[UNHANDLED] {message.MessageType.ToString()}");
    }

    private void ConnectionApprovalReceived(NetIncomingMessage message) {
        IPEndPoint ip = message.SenderEndPoint;
        int protocol = message.ReadInt32();
        string username = message.ReadString();
        string displayName = message.ReadString();

        progressLogger.Info($"Registering player {username} @ {ip}");

        string error = Server.ValidateRegisteringPlayer(username, displayName, protocol);
        if(error is not null) {
            message.SenderConnection.Deny(error);
            logger.Error($"Could not register player {username} @ {ip} ({error})");
            return;
        }

        Player player = new() {
            connection = message.SenderConnection,
            username = username,
            displayName = displayName,
            worldPackGuid = message.ReadString(),
            worldPackName = message.ReadString(),
            worldGuid = message.ReadString(),
            worldName = message.ReadString(),
            roomGuid = message.ReadString(),
            roomName = message.ReadString(),
            controlling = Guid.Parse(message.ReadString())
        };
        playerRegistry.Add(player.connection, player);

        NetOutgoingMessage hail = server.CreateMessage();
        hail.Write(playerRegistry.Count - 1);
        foreach((NetConnection regCon, Player regPlayer) in playerRegistry) {
            if(regCon == player.connection) continue;
            regPlayer.Write(hail);
        }
        hail.Write(syncedObjectRegistry.Count);
        foreach((Guid _, SyncedObject syncedObject) in syncedObjectRegistry) syncedObject.Write(hail);

        message.SenderConnection.Approve(hail);
    }

    private void StatusChangedReceived(NetIncomingMessage message) =>
        statusChangedMessageHandler.MessageReceived(message);

    private void DataReceived(NetIncomingMessage message) => dataMessageHandler.MessageReceived(message);

    private void ConnectionLatencyUpdatedReceived(NetIncomingMessage message) {
        // ReSharper disable once ForeachCanBePartlyConvertedToQueryUsingAnotherGetEnumerator
        foreach((NetConnection _, Player player) in playerRegistry) {
            if(player.connection != message.SenderConnection) continue;
            player.latestPing = message.ReadFloat() / 2f;
        }
    }

    private static void VerboseDebugMessageReceived(NetBuffer message) => logger.Trace(message.ReadString());

    private static void DebugMessageReceived(NetBuffer message) => logger.Debug(message.ReadString());

    private static void WarningMessageReceived(NetBuffer message) => logger.Warn(message.ReadString());

    private static void ErrorMessageReceived(NetBuffer message) => logger.Error(message.ReadString());
}
