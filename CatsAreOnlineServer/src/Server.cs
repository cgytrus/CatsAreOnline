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
        public const string Version = "0.5.0";
        public static TimeSpan targetTickTime { get; } = TimeSpan.FromSeconds(0.01d);

        public static int playerCount => playerRegistry.Count;

        private const int MaxUsernameLength = 64;
        private const int MaxDisplayNameLength = 64;

        private static readonly Dictionary<Guid, Player> playerRegistry = new();
        private static readonly Dictionary<Guid, SyncedObject> syncedObjectRegistry = new();

        private static NetServer _server;
        private static readonly List<NetConnection> tempConnections = new();

        private static IReadOnlyDictionary<DataType, Action<NetBuffer>> _receivingDataMessages;

        public static void Main(string[] args) {
            bool upnp = false;
            if(args.Length <= 0 || !int.TryParse(args[0], out int port) ||
               args.Length >= 2 && !bool.TryParse(args[1], out upnp)) {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("Invalid arguments.");
                return;
            }

            _receivingDataMessages = new Dictionary<DataType, Action<NetBuffer>> {
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

            Commands.Initialize();

            NetPeerConfiguration config = new("mod.cgytrus.plugins.calOnline") {
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

            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"Starting server (v{Version}) on port {port.ToString(CultureInfo.InvariantCulture)}");
            Console.ResetColor();
            _server.Start();

            // doesn't seem to work?
            if(upnp) _server.UPnP.ForwardPort(port, "Cats are Liquid - A Better Place");

            while(_server.Status != NetPeerStatus.Running) { }

            Thread serverThread = new(ServerThread);
            serverThread.Start();

            while(_server.Status == NetPeerStatus.Running) {
                string command = Console.ReadLine();
                if(_server.Status != NetPeerStatus.Running) break;
                ExecuteCommand(null, command);
            }

            try { serverThread.Join(); }
            catch(Exception) { /* ignored */ }
        }

        public static void Stop() {
            Console.WriteLine("Stopping server...");
            _server.Shutdown("Server closed");
            _server.UPnP.DeleteForwardingRule(_server.Port);
        }

        private static void ServerThread() {
            Stopwatch stopwatch = Stopwatch.StartNew();
            while(_server.Status == NetPeerStatus.Running) {
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

        private static void MessageReceived(NetIncomingMessage message) {
            switch(message.MessageType) {
                case NetIncomingMessageType.Data:
                    DataMessageReceived(message);
                    break;
                case NetIncomingMessageType.StatusChanged:
                    StatusChangedMessageReceived(message);
                    break;
                case NetIncomingMessageType.WarningMessage:
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    Console.WriteLine($"[WARN] {message.ReadString()}");
                    Console.ResetColor();
                    break;
                case NetIncomingMessageType.Error:
                case NetIncomingMessageType.ErrorMessage:
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine($"[ERROR] {message.ReadString()}");
                    Console.ResetColor();
                    break;
                default:
                    Console.ForegroundColor = ConsoleColor.DarkGray;
                    Console.WriteLine($"[UNHANDLED] {message.MessageType}");
                    Console.ResetColor();
                    break;
            }
        }

        private static void StatusChangedMessageReceived(NetIncomingMessage message) {
            NetConnectionStatus status = (NetConnectionStatus)message.ReadByte();
            switch(status) {
                case NetConnectionStatus.Connected:
                    Console.ForegroundColor = ConsoleColor.DarkGreen;
                    Console.WriteLine(message.ReadString());
                    Console.ResetColor();
                    break;
                case NetConnectionStatus.RespondedConnect:
                    Console.ForegroundColor = ConsoleColor.DarkGray;
                    Console.WriteLine(message.ReadString());
                    Console.ResetColor();
                    break;
                case NetConnectionStatus.Disconnected:
                    ClientDisconnected(message);
                    break;
                default:
                    Console.ForegroundColor = ConsoleColor.DarkGray;
                    Console.WriteLine($"[UNHANDLED] {message.MessageType} {status} {message.ReadString()}");
                    Console.ResetColor();
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
            else {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine($"[WARN] Unknown message type received ({type.ToString()})");
                Console.ResetColor();
            }
        }

        private static void RegisterPlayerReceived(NetIncomingMessage message) {
            (Player _, bool registered) = GetClientData(message);
            if(registered) return;
            IPEndPoint ip = message.SenderEndPoint;
            string username = message.ReadString();
            string displayName = message.ReadString();

            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"Registering player {username} @ {ip}");
            Console.ResetColor();

            void RegisterPlayerError(string reason) {
                message.SenderConnection.Disconnect($"{reason}");
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"Could not register player {username} @ {ip} ({reason})");
                Console.ResetColor();
            }

            Guid guid = Guid.NewGuid();
            if(playerRegistry.ContainsKey(guid)) {
                RegisterPlayerError("GUID already taken (what)");
                return;
            }

            if(playerRegistry.Any(ply => ply.Value.username == username)) {
                RegisterPlayerError("Username already taken");
                return;
            }

            if(username.Length > MaxUsernameLength) {
                string maxLength = MaxUsernameLength.ToString(CultureInfo.InvariantCulture);
                RegisterPlayerError($"Username too long (max length = {maxLength})");
                return;
            }

            if(displayName.Length > MaxDisplayNameLength) {
                string maxLength = MaxDisplayNameLength.ToString(CultureInfo.InvariantCulture);
                RegisterPlayerError($"Display name too long (max length = {maxLength})");
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

        private static void PlayerChangedWorldPackReceived(NetBuffer message) {
            (Player player, bool registered) = GetClientData(message);
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

            LogPlayerAction(player, $"changed world pack to {worldPackName} ({worldPackGuid})");
            
            NetOutgoingMessage notifyMessage = _server.CreateMessage();
            notifyMessage.Write((byte)DataType.PlayerChangedWorldPack);
            notifyMessage.Write(player.username);
            notifyMessage.Write(player.worldPackGuid);
            notifyMessage.Write(player.worldPackName);
            _server.SendToAll(notifyMessage, DeliveryMethods.Reliable);
        }

        private static void PlayerChangedWorldReceived(NetBuffer message) {
            (Player player, bool registered) = GetClientData(message);
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

            LogPlayerAction(player, $"changed world to {worldName} ({worldGuid})");
            
            NetOutgoingMessage notifyMessage = _server.CreateMessage();
            notifyMessage.Write((byte)DataType.PlayerChangedWorld);
            notifyMessage.Write(player.username);
            notifyMessage.Write(player.worldGuid);
            notifyMessage.Write(player.worldName);
            _server.SendToAll(notifyMessage, DeliveryMethods.Reliable);
        }

        private static void PlayerChangedRoomReceived(NetBuffer message) {
            (Player player, bool registered) = GetClientData(message);
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

            LogPlayerAction(player, $"changed room to {roomName} ({roomGuid})");
            
            NetOutgoingMessage notifyMessage = _server.CreateMessage();
            notifyMessage.Write((byte)DataType.PlayerChangedRoom);
            notifyMessage.Write(player.username);
            notifyMessage.Write(player.roomGuid);
            notifyMessage.Write(player.roomName);
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

            Console.WriteLine($"[{player.username}] {text}");

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
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine($"Received a command from {player.username} : '{command}'");
            Console.ResetColor();
            ExecuteCommand(player, command);
        }

        private static void ExecuteCommand(Player player, string command) {
            try { Commands.dispatcher.Execute(command, player); }
            catch(Exception ex) { SendChatMessage(player, ServerErrorMessage(ex.Message)); }
        }

        // ReSharper disable once SuggestBaseTypeForParameter
        private static void SendToAllInCurrentRoom(Player fromPlayer, NetOutgoingMessage message, NetDeliveryMethod method) {
            tempConnections.Clear();
            // ReSharper disable once ForeachCanBePartlyConvertedToQueryUsingAnotherGetEnumerator
            foreach((Guid _, Player player) in playerRegistry) {
                if(!player.LocationEqual(fromPlayer)) continue;
                tempConnections.Add(player.connection);
            }
            if(tempConnections.Count > 0) _server.SendMessage(message, tempConnections, method, 0);
        }

        public static void SendChatMessage(Player player, string message) {
            Console.WriteLine(message);
            if(player is null) return;
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

        public static void LogPlayerAction(string id, string username, string action) {
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine($"Player {username} ({id}) {action}");
            Console.ResetColor();
        }
    }
}
