using System;
using System.Collections.Generic;
using System.Net;

using CatsAreOnlineServer.SyncedObjects;

using Lidgren.Network;

namespace CatsAreOnlineServer.MessageHandlers {
    public class MessageHandler {
        public NetServer server { private get; init; }
        public Dictionary<Guid, Player> playerRegistry { private get; init; }
        public Dictionary<Guid, SyncedObject> syncedObjectRegistry { private get; init; }
        public StatusChangedMessageHandler statusChangedMessageHandler { private get; init; }
        public DataMessageHandler dataMessageHandler { private get; init; }

        private readonly IReadOnlyDictionary<NetIncomingMessageType, Action<NetIncomingMessage>> _messages;

        public MessageHandler() => _messages = new Dictionary<NetIncomingMessageType, Action<NetIncomingMessage>> {
            { NetIncomingMessageType.ConnectionApproval, ConnectionApprovalReceived },
            { NetIncomingMessageType.StatusChanged, StatusChangedReceived },
            { NetIncomingMessageType.Data, DataReceived },
            { NetIncomingMessageType.VerboseDebugMessage, VerboseDebugMessageReceived },
            { NetIncomingMessageType.DebugMessage, DebugMessageReceived },
            { NetIncomingMessageType.WarningMessage, WarningMessageReceived},
            { NetIncomingMessageType.Error, ErrorMessageReceived },
            { NetIncomingMessageType.ErrorMessage, ErrorMessageReceived }
        };

        public void MessageReceived(NetIncomingMessage message) {
            if(_messages.TryGetValue(message.MessageType, out Action<NetIncomingMessage> action)) action(message);
            else {
                Console.ForegroundColor = ConsoleColor.DarkGray;
                Console.WriteLine($"[UNHANDLED] {message.MessageType.ToString()}");
                Console.ResetColor();
            }
        }

        private void ConnectionApprovalReceived(NetIncomingMessage message) {
            IPEndPoint ip = message.SenderEndPoint;
            string username = message.ReadString();
            string displayName = message.ReadString();

            Console.ForegroundColor = ConsoleColor.DarkGreen;
            Console.WriteLine($"Registering player {username} @ {ip}");
            Console.ResetColor();

            Guid guid = Guid.NewGuid();

            string error = Server.ValidateRegisteringPlayer(username, displayName, guid);
            if(error is not null) {
                message.SenderConnection.Deny(error);
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"Could not register player {username} @ {ip} ({error})");
                Console.ResetColor();
                return;
            }

            Player player = new() {
                connection = message.SenderConnection,
                id = guid,
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
            playerRegistry.Add(guid, player);

            NetOutgoingMessage secretMessage = server.CreateMessage();
            secretMessage.Write(guid.ToString());
            secretMessage.Write(playerRegistry.Count - 1);
            foreach((Guid regGuid, Player regPlayer) in playerRegistry) {
                if(regGuid == guid) continue;
                regPlayer.Write(secretMessage);
            }
            secretMessage.Write(syncedObjectRegistry.Count);
            foreach((Guid _, SyncedObject syncedObject) in syncedObjectRegistry) syncedObject.Write(secretMessage);

            message.SenderConnection.Approve(secretMessage);
        }

        private void StatusChangedReceived(NetIncomingMessage message) =>
            statusChangedMessageHandler.MessageReceived(message);

        private void DataReceived(NetBuffer message) => dataMessageHandler.MessageReceived(message);

        private static void VerboseDebugMessageReceived(NetBuffer message) {
            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.WriteLine($"[VERBOSE DEBUG] {message.ReadString()}");
            Console.ResetColor();
        }

        private static void DebugMessageReceived(NetBuffer message) {
            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.WriteLine($"[DEBUG] {message.ReadString()}");
            Console.ResetColor();
        }

        private static void WarningMessageReceived(NetBuffer message) {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine($"[WARN] {message.ReadString()}");
            Console.ResetColor();
        }

        private static void ErrorMessageReceived(NetBuffer message) {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"[ERROR] {message.ReadString()}");
            Console.ResetColor();
        }
    }
}
