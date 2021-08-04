using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Threading;

using CatsAreOnline.Shared;

using Lidgren.Network;

namespace CatsAreOnlineServer {
    public static class Server {
        public const string Version = "0.3.0";
        public static TimeSpan targetTickTime { get; } = TimeSpan.FromSeconds(0.01d);

        public static int playerCount => playerRegistry.Count;

        private static readonly Dictionary<Guid, Player> playerRegistry = new();
        private static readonly Dictionary<Guid, SyncedObject> syncedObjectRegistry = new();
        
        private static NetServer _server;
        private static readonly List<NetConnection> tempConnections = new();
        
        private static IReadOnlyDictionary<DataType, Action<NetBuffer>> _receivingDataMessages;

        public static void Main(string[] args) {
            bool upnp = false;
            if(args.Length <= 0 || !int.TryParse(args[0], out int port) ||
               args.Length >= 2 && !bool.TryParse(args[1], out upnp)) {
                Console.WriteLine("Invalid arguments.");
                return;
            }

            _receivingDataMessages = new Dictionary<DataType, Action<NetBuffer>> {
                { DataType.PlayerChangedRoom, PlayerChangedRoomReceived },
                { DataType.PlayerChangedControllingObject, PlayerChangedControllingObjectReceived },
                { DataType.SyncedObjectAdded, SyncedObjectAddedReceived },
                { DataType.SyncedObjectRemoved, SyncedObjectRemovedReceived },
                { DataType.SyncedObjectChangedState, SyncedObjectChangedStateReceived },
                { DataType.ChatMessage, ChatMessageReceived },
                { DataType.Command, CommandReceived }
            };
            
            Commands.Initialize();
            
            NetPeerConfiguration config = new("mod.cgytrus.plugin.calOnline") {
                Port = port,
                EnableUPnP = true
            };
            
            config.DisableMessageType(NetIncomingMessageType.Receipt);
            config.DisableMessageType(NetIncomingMessageType.ConnectionApproval);
            config.DisableMessageType(NetIncomingMessageType.DebugMessage);
            config.DisableMessageType(NetIncomingMessageType.DiscoveryRequest); // enable later
            config.DisableMessageType(NetIncomingMessageType.DiscoveryResponse); // enable later
            config.DisableMessageType(NetIncomingMessageType.UnconnectedData);
            config.DisableMessageType(NetIncomingMessageType.ConnectionLatencyUpdated);
            config.DisableMessageType(NetIncomingMessageType.NatIntroductionSuccess);
            config.DisableMessageType(NetIncomingMessageType.VerboseDebugMessage);

            _server = new NetServer(config);

            Console.WriteLine($"Starting server (v{Version}) on port {port.ToString(CultureInfo.InvariantCulture)}");
            _server.Start();
            
            if(upnp) _server.UPnP.ForwardPort(port, "Cats are Liquid - A Better Place");

            Stopwatch stopwatch = Stopwatch.StartNew();
            while(true) {
                stopwatch.Restart();
                
                NetIncomingMessage message;
                while((message = _server.ReadMessage()) != null) {
                    MessageReceived(message);
                    _server.Recycle(message);
                }

                TimeSpan timeout = targetTickTime - stopwatch.Elapsed;
                if(timeout.Ticks > 0L) Thread.Sleep(timeout);
            }
        }

        public static void Stop() {
            _server.Shutdown("Server closed");
            _server.UPnP.DeleteForwardingRule(_server.Port);
        }

        private static void MessageReceived(NetIncomingMessage message) {
            switch(message.MessageType) {
                case NetIncomingMessageType.Data:
                    DataMessageReceived(message);
                    break;
                case NetIncomingMessageType.StatusChanged:
                    StatusChangedMessageReceived(message);
                    break;
                case NetIncomingMessageType.WarningMessage:
                    Console.WriteLine($"[WARN] {message.ReadString()}");
                    break;
                case NetIncomingMessageType.Error:
                case NetIncomingMessageType.ErrorMessage:
                    Console.WriteLine($"[ERROR] {message.ReadString()}");
                    break;
                default:
                    Console.WriteLine($"[UNHANDLED] {message.MessageType}");
                    break;
            }
        }

        private static void StatusChangedMessageReceived(NetIncomingMessage message) {
            NetConnectionStatus status = (NetConnectionStatus)message.ReadByte();
            switch(status) {
                case NetConnectionStatus.Connected:
                case NetConnectionStatus.RespondedConnect:
                    Console.WriteLine(message.ReadString());
                    break;
                case NetConnectionStatus.Disconnected:
                    ClientDisconnected(message);
                    break;
                default:
                    Console.WriteLine($"[UNHANDLED] {message.MessageType} {status} {message.ReadString()}");
                    break;
            }
        }

        private static void ClientDisconnected(NetBuffer message) {
            foreach((Guid id, Player player) in new Dictionary<Guid, Player>(playerRegistry)) {
                if(player.connection.Status == NetConnectionStatus.Connected) continue;

                string username = player.username;
                string reason = message.ReadString();
                
                LogPlayerAction(player, $"left ({reason})");
                
                NetOutgoingMessage notifyMessage = _server.CreateMessage();
                notifyMessage.Write((byte)DataType.PlayerLeft);
                notifyMessage.Write(username);
                _server.SendToAll(notifyMessage, DeliveryMethods.Global);

                List<Guid> toRemove = (from syncedObject in syncedObjectRegistry
                                       where syncedObject.Value.owner.username == player.username
                                       select syncedObject.Key).ToList();
                foreach(Guid objId in toRemove) syncedObjectRegistry.Remove(objId);                
                playerRegistry.Remove(id);
            }
        }

        private static void DataMessageReceived(NetIncomingMessage message) {
            DataType type = (DataType)message.ReadByte();
            
            if(type == DataType.RegisterPlayer) RegisterPlayerReceived(message);
            else if(_receivingDataMessages.TryGetValue(type, out Action<NetBuffer> action)) action(message);
            else Console.WriteLine($"[WARN] Unknown message type received ({type.ToString()})");
        }

        private static void RegisterPlayerReceived(NetIncomingMessage message) {
            (Player _, bool registered) = GetClientData(message);
            if(registered) return;
            IPEndPoint ip = message.SenderEndPoint;
            string username = message.ReadString();
            
            Console.WriteLine($"Registering player {username} @ {ip}");

            Guid guid = Guid.NewGuid();
            if(playerRegistry.ContainsKey(guid)) {
                message.SenderConnection.Disconnect("GUID already taken, try reconnecting.");
                Console.WriteLine($"Could not register player {username} @ {ip} (GUID already taken)");
                return;
            }

            if(playerRegistry.Any(ply => ply.Value.username == username)) {
                message.SenderConnection.Disconnect("Username already taken.");
                Console.WriteLine($"Could not register player {username} @ {ip} (Username already taken)");
                return;
            }

            Player player = new() {
                connection = message.SenderConnection,
                id = guid,
                username = username,
                displayName = message.ReadString(),
                room = message.ReadString(),
                controlling = Guid.Parse(message.ReadString())
            };
            playerRegistry.Add(guid, player);
            
            NetOutgoingMessage secretMessage = _server.CreateMessage();
            secretMessage.Write((byte)DataType.RegisterPlayer);
            secretMessage.Write(guid.ToString());
            secretMessage.Write(playerRegistry.Count - 1);
            foreach((Guid regGuid, Player regPlayer) in playerRegistry) {
                if(regGuid == guid) continue;
                regPlayer.Write(secretMessage);
            }
            secretMessage.Write(syncedObjectRegistry.Count);
            foreach((Guid _, SyncedObject syncedObject) in syncedObjectRegistry) syncedObject.Write(secretMessage);

            _server.SendMessage(secretMessage, message.SenderConnection, DeliveryMethods.Reliable);
            
            // notifies all the players about the joined player
            NetOutgoingMessage notifyMessage = _server.CreateMessage();
            notifyMessage.Write((byte)DataType.PlayerJoined);
            player.Write(notifyMessage);
            _server.SendToAll(notifyMessage, DeliveryMethods.Reliable);
        }

        private static void PlayerChangedRoomReceived(NetBuffer message) {
            (Player player, bool registered) = GetClientData(message);
            if(!registered) return;
            
            string room = message.ReadString();

            if(!player.RoomEqual(room)) {
                List<Guid> toRemove = (from syncedObject in syncedObjectRegistry
                                              where syncedObject.Value.owner.username == player.username
                                              select syncedObject.Key).ToList();
                foreach(Guid id in toRemove) syncedObjectRegistry.Remove(id);
            }

            player.room = room;
            
            LogPlayerAction(player, $"changed room to {room}");
            
            NetOutgoingMessage notifyMessage = _server.CreateMessage();
            notifyMessage.Write((byte)DataType.PlayerChangedRoom);
            notifyMessage.Write(player.username);
            notifyMessage.Write(player.room);
            _server.SendToAll(notifyMessage, DeliveryMethods.Reliable);
        }

        private static void PlayerChangedControllingObjectReceived(NetBuffer message) {
            (Player player, bool registered) = GetClientData(message);
            if(!registered) return;

            player.controlling = Guid.Parse(message.ReadString());
            
            NetOutgoingMessage notifyMessage = _server.CreateMessage();
            notifyMessage.Write((byte)DataType.PlayerChangedControllingObject);
            notifyMessage.Write(player.username);
            notifyMessage.Write(player.controlling.ToString());
            _server.SendToAll(notifyMessage, DeliveryMethods.Reliable);
        }
        
        private static void SyncedObjectAddedReceived(NetBuffer message) {
            (Player player, bool registered) = GetClientData(message);
            if(!registered) return;

            SyncedObjectType type = (SyncedObjectType)message.ReadByte();
            Guid id = Guid.Parse(message.ReadString());
            SyncedObject syncedObject = SyncedObject.Create(type, id, player, message);
            syncedObjectRegistry.Add(id, syncedObject);

            NetOutgoingMessage notifyMessage = _server.CreateMessage();
            notifyMessage.Write((byte)DataType.SyncedObjectAdded);
            syncedObject.Write(notifyMessage);
            _server.SendToAll(notifyMessage, DeliveryMethods.Reliable);
        }

        private static void SyncedObjectRemovedReceived(NetBuffer message) {
            (Player player, bool registered) = GetClientData(message);
            if(!registered) return;
            
            Guid id = Guid.Parse(message.ReadString());
            if(!syncedObjectRegistry.TryGetValue(id, out SyncedObject syncedObject)) return;
            if(syncedObject.owner != player) return;

            syncedObjectRegistry.Remove(id);
            
            NetOutgoingMessage notifyMessage = _server.CreateMessage();
            notifyMessage.Write((byte)DataType.SyncedObjectRemoved);
            notifyMessage.Write(id.ToString());
            _server.SendToAll(notifyMessage, DeliveryMethods.Reliable);
        }

        private static void SyncedObjectChangedStateReceived(NetBuffer message) {
            (Player player, bool registered) = GetClientData(message);
            if(!registered) return;

            Guid id = Guid.Parse(message.ReadString());
            if(!syncedObjectRegistry.TryGetValue(id, out SyncedObject syncedObject)) return;
            if(syncedObject.owner != player) return;

            NetOutgoingMessage notifyMessage = null;
            NetDeliveryMethod deliveryMethod = DeliveryMethods.Global;

            while(message.ReadByte(out byte stateTypeByte)) {
                if(notifyMessage == null) {
                    notifyMessage = _server.CreateMessage();
                    notifyMessage.Write((byte)DataType.SyncedObjectChangedState);
                    notifyMessage.Write(syncedObject.id.ToString());
                }
                
                syncedObject.ReadChangedState(message, notifyMessage, stateTypeByte, ref deliveryMethod);
            }

            if(notifyMessage == null) return;
            SendToAllInCurrentRoom(player, notifyMessage, deliveryMethod);
        }

        private static void ChatMessageReceived(NetBuffer message) {
            (Player player, bool registered) = GetClientData(message);
            if(!registered) return;

            string text = message.ReadString();

            Console.WriteLine($"[{player.username} ({player.id.ToString()})] {text}");

            NetOutgoingMessage resendMessage = _server.CreateMessage();
            resendMessage.Write((byte)DataType.ChatMessage);
            resendMessage.Write(player.username);
            resendMessage.Write(text);
            _server.SendMessage(resendMessage, playerRegistry.Select(ply => ply.Value.connection).ToList(), DeliveryMethods.Reliable, 0);
        }

        private static void CommandReceived(NetBuffer message) {
            (Player player, bool registered) = GetClientData(message);
            if(!registered) return;

            string command = message.ReadString();
            Console.WriteLine($"Received a command from {player.username} : '{command}'");
            ExecuteCommand(player, command);
        }

        private static void ExecuteCommand(Player player, string command) {
            try {
                Commands.dispatcher.Execute(command, player);
            }
            catch(Exception ex) {
                SendChatMessage(player, ServerErrorMessage(ex.Message));
            }
        }

        // ReSharper disable once SuggestBaseTypeForParameter
        private static void SendToAllInCurrentRoom(Player fromPlayer, NetOutgoingMessage message, NetDeliveryMethod method) {
            tempConnections.Clear();
            // ReSharper disable once ForeachCanBePartlyConvertedToQueryUsingAnotherGetEnumerator
            foreach((Guid _, Player player) in playerRegistry) {
                if(!player.RoomEqual(fromPlayer)) continue;
                tempConnections.Add(player.connection);
            }
            _server.SendMessage(message, tempConnections, method, 0);
        }

        public static void SendChatMessage(Player player, string message) {
            NetOutgoingMessage response = _server.CreateMessage();
            response.Write((byte)DataType.ChatMessage);
            response.Write(player.username);
            response.Write(message);
            _server.SendMessage(response, player.connection, DeliveryMethods.Reliable);
        }

        public static string ServerMessage(string message) => $"<color=blue>[SERVER]</color> {message}";
        public static string ServerDebugMessage(string message) =>
            ServerMessage($"<color=grey><b>DEBUG:</b> {message}</color>");
        public static string ServerWarningMessage(string message) =>
            ServerMessage($"<color=yellow><b>WARN:</b> {message}</color>");
        public static string ServerErrorMessage(string message) =>
            ServerMessage($"<color=red><b>ERROR:</b> {message}</color>");

        private static (Player player, bool registered) GetClientData(NetBuffer message) =>
            Guid.TryParse(message.ReadString(), out Guid guid) && playerRegistry.TryGetValue(guid, out Player player) ?
                (player, true) : (null, false);

        public static void LogPlayerAction(Player player, string action) =>
            LogPlayerAction(player.id.ToString(), player.username, action);

        public static void LogPlayerAction(string id, string username, string action) =>
            Console.WriteLine($"Player {username} ({id}) {action}");
    }
}
