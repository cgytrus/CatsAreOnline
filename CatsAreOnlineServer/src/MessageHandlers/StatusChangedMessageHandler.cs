using System;
using System.Collections.Generic;
using System.Linq;

using CatsAreOnline.Shared;

using CatsAreOnlineServer.SyncedObjects;

using Lidgren.Network;

using NLog;

namespace CatsAreOnlineServer.MessageHandlers;

public class StatusChangedMessageHandler {
    private static readonly Logger logger = LogManager.GetCurrentClassLogger();
    private static readonly Logger successLogger = logger.WithProperty("success", true);
    private static readonly Logger progressLogger = logger.WithProperty("progress", true);
    private static readonly Logger progressStartLogger = logger.WithProperty("progressStart", true);
    private static readonly Logger unhandledLogger = logger.WithProperty("unhandled", true);

    public NetServer server { private get; init; }
    public Dictionary<NetConnection, Player> playerRegistry { private get; init; }
    public Dictionary<Guid, SyncedObject> syncedObjectRegistry { private get; init; }

    private readonly IReadOnlyDictionary<NetConnectionStatus, Action<NetIncomingMessage>> _messages;

    public StatusChangedMessageHandler() => _messages =
        new Dictionary<NetConnectionStatus, Action<NetIncomingMessage>> {
            { NetConnectionStatus.RespondedAwaitingApproval, RespondedAwaitingApprovalReceived},
            { NetConnectionStatus.RespondedConnect, RespondedConnectReceived},
            { NetConnectionStatus.Connected, ConnectedReceived},
            { NetConnectionStatus.Disconnected, DisconnectedReceived}
        };

    public void MessageReceived(NetIncomingMessage message) {
        NetConnectionStatus status = (NetConnectionStatus)message.ReadByte();

        if(_messages.TryGetValue(status, out Action<NetIncomingMessage> action)) action(message);
        else
            unhandledLogger.Info(
                $"[UNHANDLED] {message.MessageType.ToString()} {status.ToString()} {message.ReadString()}");
    }

    private static void RespondedAwaitingApprovalReceived(NetBuffer message) =>
        progressStartLogger.Info(message.ReadString());

    private static void RespondedConnectReceived(NetBuffer message) => progressLogger.Info(message.ReadString());

    private void ConnectedReceived(NetIncomingMessage message) {
        successLogger.Info(message.ReadString());

        foreach((NetConnection _, Player player) in playerRegistry) {
            if(player.connection != message.SenderConnection) continue;
            NetOutgoingMessage notifyMessage = server.CreateMessage();
            notifyMessage.Write((byte)DataType.PlayerJoined);
            player.Write(notifyMessage);
            server.SendToAll(notifyMessage, DeliveryMethods.Reliable);
        }
    }

    private void DisconnectedReceived(NetBuffer message) {
        foreach((NetConnection connection, Player player) in
                new Dictionary<NetConnection, Player>(playerRegistry)) {
            if(player.connection.Status == NetConnectionStatus.Connected) continue;

            string username = player.username;
            string reason = message.ReadString();

            Server.LogPlayerAction(player, $"left ({reason})");

            NetOutgoingMessage notifyMessage = server.CreateMessage();
            notifyMessage.Write((byte)DataType.PlayerLeft);
            notifyMessage.Write(username);
            server.SendToAll(notifyMessage, DeliveryMethods.Global);

            List<Guid> toRemove = (from syncedObject in syncedObjectRegistry
                                   where syncedObject.Value.owner.username == player.username
                                   select syncedObject.Key).ToList();
            foreach(Guid objId in toRemove) syncedObjectRegistry.Remove(objId);
            playerRegistry.Remove(connection);
        }
    }
}
