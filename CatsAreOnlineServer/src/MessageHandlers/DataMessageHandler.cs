using System;
using System.Collections.Generic;
using System.Linq;

using CatsAreOnline.Shared;

using CatsAreOnlineServer.SyncedObjects;

using Lidgren.Network;

namespace CatsAreOnlineServer.MessageHandlers {
    public class DataMessageHandler {
        public NetServer server { private get; init; }
        public Dictionary<Guid, Player> playerRegistry { private get; init; }
        public Dictionary<Guid, SyncedObject> syncedObjectRegistry { private get; init; }

        private readonly IReadOnlyDictionary<DataType, Action<NetBuffer>> _messages;

        private static readonly List<NetConnection> tempConnections = new();

        public DataMessageHandler() => _messages = new Dictionary<DataType, Action<NetBuffer>> {
            { DataType.PlayerChangedWorldPack, PlayerChangedWorldPackReceived },
            { DataType.PlayerChangedWorld, PlayerChangedWorldReceived },
            { DataType.PlayerChangedRoom, PlayerChangedRoomReceived },
            { DataType.PlayerChangedControllingObject, PlayerChangedControllingObjectReceived },
            { DataType.SyncedObjectAdded, SyncedObjectAddedReceived },
            { DataType.SyncedObjectRemoved, SyncedObjectRemovedReceived },
            { DataType.SyncedObjectChangedState, SyncedObjectChangedStateReceived },
            { DataType.ChatMessage, ChatMessageReceived },
            { DataType.Command, CommandReceived }
        };

        public void MessageReceived(NetBuffer message) {
            DataType type = (DataType)message.ReadByte();

            if(_messages.TryGetValue(type, out Action<NetBuffer> action)) action(message);
            else {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine($"[WARN] Unknown message type received ({type.ToString()})");
                Console.ResetColor();
            }
        }

        private void PlayerChangedWorldPackReceived(NetBuffer message) {
            (Player player, bool registered) = Server.GetClientData(message);
            if(!registered) return;

            string worldPackGuid = message.ReadString();
            string worldPackName = message.ReadString();

            if(player.LocationEqual(worldPackGuid, player.worldGuid, player.roomGuid)) return;

            List<Guid> toRemove = (from syncedObject in syncedObjectRegistry
                                   where syncedObject.Value.owner.username == player.username
                                   select syncedObject.Key).ToList();
            foreach(Guid id in toRemove) syncedObjectRegistry.Remove(id);

            player.worldPackGuid = worldPackGuid;
            player.worldPackName = worldPackName;

            Server.LogPlayerAction(player, $"changed world pack to {worldPackName} ({worldPackGuid})");

            NetOutgoingMessage notifyMessage = server.CreateMessage();
            notifyMessage.Write((byte)DataType.PlayerChangedWorldPack);
            notifyMessage.Write(player.username);
            notifyMessage.Write(player.worldPackGuid);
            notifyMessage.Write(player.worldPackName);
            server.SendToAll(notifyMessage, DeliveryMethods.Reliable);
        }

        private void PlayerChangedWorldReceived(NetBuffer message) {
            (Player player, bool registered) = Server.GetClientData(message);
            if(!registered) return;

            string worldGuid = message.ReadString();
            string worldName = message.ReadString();

            if(player.LocationEqual(player.worldPackGuid, worldGuid, player.roomGuid)) return;

            List<Guid> toRemove = (from syncedObject in syncedObjectRegistry
                                   where syncedObject.Value.owner.username == player.username
                                   select syncedObject.Key).ToList();
            foreach(Guid id in toRemove) syncedObjectRegistry.Remove(id);

            player.worldGuid = worldGuid;
            player.worldName = worldName;

            Server.LogPlayerAction(player, $"changed world to {worldName} ({worldGuid})");

            NetOutgoingMessage notifyMessage = server.CreateMessage();
            notifyMessage.Write((byte)DataType.PlayerChangedWorld);
            notifyMessage.Write(player.username);
            notifyMessage.Write(player.worldGuid);
            notifyMessage.Write(player.worldName);
            server.SendToAll(notifyMessage, DeliveryMethods.Reliable);
        }

        private void PlayerChangedRoomReceived(NetBuffer message) {
            (Player player, bool registered) = Server.GetClientData(message);
            if(!registered) return;

            string roomGuid = message.ReadString();
            string roomName = message.ReadString();

            if(player.LocationEqual(player.worldPackGuid, player.worldGuid, roomGuid)) return;

            List<Guid> toRemove = (from syncedObject in syncedObjectRegistry
                                   where syncedObject.Value.owner.username == player.username
                                   select syncedObject.Key).ToList();
            foreach(Guid id in toRemove) syncedObjectRegistry.Remove(id);

            player.roomGuid = roomGuid;
            player.roomName = roomName;

            Server.LogPlayerAction(player, $"changed room to {roomName} ({roomGuid})");

            NetOutgoingMessage notifyMessage = server.CreateMessage();
            notifyMessage.Write((byte)DataType.PlayerChangedRoom);
            notifyMessage.Write(player.username);
            notifyMessage.Write(player.roomGuid);
            notifyMessage.Write(player.roomName);
            server.SendToAll(notifyMessage, DeliveryMethods.Reliable);
        }

        private void PlayerChangedControllingObjectReceived(NetBuffer message) {
            (Player player, bool registered) = Server.GetClientData(message);
            if(!registered) return;

            player.controlling = Guid.Parse(message.ReadString());

            NetOutgoingMessage notifyMessage = server.CreateMessage();
            notifyMessage.Write((byte)DataType.PlayerChangedControllingObject);
            notifyMessage.Write(player.username);
            notifyMessage.Write(player.controlling.ToString());
            server.SendToAll(notifyMessage, DeliveryMethods.Reliable);
        }

        private void SyncedObjectAddedReceived(NetBuffer message) {
            (Player player, bool registered) = Server.GetClientData(message);
            if(!registered) return;

            SyncedObjectType type = (SyncedObjectType)message.ReadByte();
            Guid id = Guid.Parse(message.ReadString());
            SyncedObject syncedObject = SyncedObject.Create(type, id, player, message);
            syncedObjectRegistry.Add(id, syncedObject);

            NetOutgoingMessage notifyMessage = server.CreateMessage();
            notifyMessage.Write((byte)DataType.SyncedObjectAdded);
            syncedObject.Write(notifyMessage);
            server.SendToAll(notifyMessage, DeliveryMethods.Reliable);
        }

        private void SyncedObjectRemovedReceived(NetBuffer message) {
            (Player player, bool registered) = Server.GetClientData(message);
            if(!registered) return;

            Guid id = Guid.Parse(message.ReadString());
            if(!syncedObjectRegistry.TryGetValue(id, out SyncedObject syncedObject)) return;
            if(syncedObject.owner != player) return;

            syncedObjectRegistry.Remove(id);

            NetOutgoingMessage notifyMessage = server.CreateMessage();
            notifyMessage.Write((byte)DataType.SyncedObjectRemoved);
            notifyMessage.Write(id.ToString());
            server.SendToAll(notifyMessage, DeliveryMethods.Reliable);
        }

        private void SyncedObjectChangedStateReceived(NetBuffer message) {
            (Player player, bool registered) = Server.GetClientData(message);
            if(!registered) return;

            Guid id = Guid.Parse(message.ReadString());
            if(!syncedObjectRegistry.TryGetValue(id, out SyncedObject syncedObject)) return;
            if(syncedObject.owner != player) return;

            NetOutgoingMessage notifyMessage = null;
            NetDeliveryMethod deliveryMethod = DeliveryMethods.Global;

            while(message.ReadByte(out byte stateTypeByte)) {
                if(notifyMessage == null) {
                    notifyMessage = server.CreateMessage();
                    notifyMessage.Write((byte)DataType.SyncedObjectChangedState);
                    notifyMessage.Write(syncedObject.id.ToString());
                }

                syncedObject.ReadChangedState(message, notifyMessage, stateTypeByte, ref deliveryMethod);
            }

            if(notifyMessage == null) return;
            SendToAllInCurrentRoom(player, notifyMessage, deliveryMethod);
        }

        private void ChatMessageReceived(NetBuffer message) {
            (Player player, bool registered) = Server.GetClientData(message);
            if(!registered) return;

            string text = message.ReadString();

            Console.WriteLine($"[{player.username}] {text}");

            NetOutgoingMessage resendMessage = server.CreateMessage();
            resendMessage.Write((byte)DataType.ChatMessage);
            resendMessage.Write(player.username);
            resendMessage.Write(text);
            server.SendMessage(resendMessage, playerRegistry.Select(ply => ply.Value.connection).ToList(),
                DeliveryMethods.Reliable, 0);
        }

        private static void CommandReceived(NetBuffer message) {
            (Player player, bool registered) = Server.GetClientData(message);
            if(!registered) return;

            string command = message.ReadString();
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine($"Received a command from {player.username} : '{command}'");
            Console.ResetColor();
            Server.ExecuteCommand(player, command);
        }

        private void SendToAllInCurrentRoom(Player fromPlayer, NetOutgoingMessage message, NetDeliveryMethod method) {
            tempConnections.Clear();
            // ReSharper disable once ForeachCanBePartlyConvertedToQueryUsingAnotherGetEnumerator
            foreach((Guid _, Player player) in playerRegistry) {
                if(!player.LocationEqual(fromPlayer)) continue;
                tempConnections.Add(player.connection);
            }
            if(tempConnections.Count > 0) server.SendMessage(message, tempConnections, method, 0);
        }
    }
}
