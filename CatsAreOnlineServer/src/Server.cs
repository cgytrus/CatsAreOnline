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
        public static TimeSpan TargetTickTime { get; } = TimeSpan.FromSeconds(0.01d);
        
        private const NetDeliveryMethod GlobalDeliveryMethod = NetDeliveryMethod.UnreliableSequenced;
        private const NetDeliveryMethod LessReliableDeliveryMethod = NetDeliveryMethod.ReliableSequenced;
        private const NetDeliveryMethod ReliableDeliveryMethod = NetDeliveryMethod.ReliableOrdered;

        public static readonly Dictionary<Guid, Player> playerRegistry = new();
        
        private static NetServer _server;
        private static readonly List<NetConnection> tempConnections = new();

        public static void Main(string[] args) {
            bool upnp = false;
            if(args.Length <= 0 || !int.TryParse(args[0], out int port) ||
               args.Length >= 2 && !bool.TryParse(args[1], out upnp)) return;
            
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

                TimeSpan timeout = TargetTickTime - stopwatch.Elapsed;
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
            foreach((Guid guid, Player player) in new Dictionary<Guid, Player>(playerRegistry)) {
                if(player.connection.Status == NetConnectionStatus.Connected) continue;

                string username = player.username;
                string reason = message.ReadString();
                
                LogPlayerAction(player, $"left ({reason})");
                
                NetOutgoingMessage notifyMessage = _server.CreateMessage();
                notifyMessage.Write((byte)DataType.PlayerLeft);
                notifyMessage.Write(username);
                _server.SendToAll(notifyMessage, GlobalDeliveryMethod);
                playerRegistry.Remove(guid);
            }
        }

        private static void DataMessageReceived(NetIncomingMessage message) {
            DataType type = (DataType)message.ReadByte();

            switch(type) {
                case DataType.RegisterPlayer:
                    RegisterPlayerReceived(message);
                    break;
                case DataType.PlayerChangedState:
                    PlayerChangedStateReceived(message);
                    break;
                case DataType.ChatMessage:
                    ChatMessageReceived(message);
                    break;
                case DataType.Command:
                    CommandReceived(message);
                    break;
                default:
                    Console.WriteLine("[WARN] Unknown message type received");
                    break;
            }
        }

        private static void RegisterPlayerReceived(NetIncomingMessage message) {
            (Player _, bool registered) = GetClientData(message);
            if(registered) return;
            IPEndPoint ip = message.SenderEndPoint;
            string username = message.ReadString();
            
            Console.WriteLine($"Registering player {username} @ {ip}");

            Guid guid = Guid.NewGuid();
            if(playerRegistry.ContainsKey(guid)) {
                message.SenderConnection.Disconnect("GUID already taken");
                Console.WriteLine($"Could not register player {username} @ {ip} (GUID already taken)");
                return;
            }

            if(playerRegistry.Any(ply => ply.Value.username == username)) {
                message.SenderConnection.Disconnect("Username already taken");
                Console.WriteLine($"Could not register player {username} @ {ip} (Username already taken)");
                return;
            }

            Player player = new() {
                connection = message.SenderConnection,
                guid = guid,
                username = username,
                displayName = message.ReadString(),
                posX = message.ReadFloat(),
                posY = message.ReadFloat(),
                room = message.ReadString(),
                colorR = message.ReadFloat(),
                colorG = message.ReadFloat(),
                colorB = message.ReadFloat(),
                colorA = message.ReadFloat(),
                scale = message.ReadFloat(),
                ice = message.ReadBoolean(),
                iceRotation = message.ReadFloat()
            };
            playerRegistry.Add(guid, player);
            
            NetOutgoingMessage secretMessage = _server.CreateMessage();
            secretMessage.Write((byte)DataType.RegisterPlayer);
            secretMessage.Write(guid.ToString());
            secretMessage.Write(playerRegistry.Count - 1);
            foreach((Guid regGuid, Player regPlayer) in playerRegistry) {
                if(regGuid == guid) continue;
                secretMessage.Write(regPlayer);
            }

            _server.SendMessage(secretMessage, message.SenderConnection, ReliableDeliveryMethod);
            
            // notifies all the players about the joined player
            NetOutgoingMessage notifyMessage = _server.CreateMessage();
            notifyMessage.Write((byte)DataType.PlayerJoined);
            notifyMessage.Write(player);
            _server.SendToAll(notifyMessage, ReliableDeliveryMethod);
        }

        private static void PlayerChangedStateReceived(NetBuffer message) {
            (Player player, bool registered) = GetClientData(message);
            if(!registered) return;
            
            NetOutgoingMessage notifyMessage = null;
            NetDeliveryMethod deliveryMethod = GlobalDeliveryMethod;
            bool sendToAll = false;

            while(message.ReadByte(out byte stateTypeByte)) {
                if(notifyMessage == null) {
                    notifyMessage = _server.CreateMessage();
                    notifyMessage.Write((byte)DataType.PlayerChangedState);
                    notifyMessage.Write(player.username);
                }
                
                ReadPlayerChangedState(message, notifyMessage, player, stateTypeByte, ref sendToAll,
                    ref deliveryMethod);
            }

            if(notifyMessage == null) return;
            if(sendToAll) _server.SendToAll(notifyMessage, deliveryMethod);
            else SendToAllInCurrentRoom(player, notifyMessage, deliveryMethod);
        }
        
        private static void ReadPlayerChangedState(NetBuffer message, NetBuffer notifyMessage, Player player,
            byte stateTypeByte, ref bool sendToAll, ref NetDeliveryMethod deliveryMethod) {
            Player.StateType stateType = (Player.StateType)stateTypeByte;
            switch(stateType) {
                case Player.StateType.Position:
                    player.posX = message.ReadFloat();
                    player.posY = message.ReadFloat();
                    notifyMessage.Write(stateTypeByte);
                    notifyMessage.Write(player.posX);
                    notifyMessage.Write(player.posY);
                    SetDeliveryMethod(GlobalDeliveryMethod, ref deliveryMethod);
                    break;
                case Player.StateType.Room:
                    player.room = message.ReadString();
                    LogPlayerAction(player, $"changed room to {player.room}");
                    notifyMessage.Write(stateTypeByte);
                    notifyMessage.Write(player.room);
                    SetDeliveryMethod(LessReliableDeliveryMethod, ref deliveryMethod);
                    sendToAll = true;
                    break;
                case Player.StateType.Color:
                    player.colorR = message.ReadFloat();
                    player.colorG = message.ReadFloat();
                    player.colorB = message.ReadFloat();
                    player.colorA = message.ReadFloat();
                    notifyMessage.Write(stateTypeByte);
                    notifyMessage.Write(player.colorR);
                    notifyMessage.Write(player.colorG);
                    notifyMessage.Write(player.colorB);
                    notifyMessage.Write(player.colorA);
                    SetDeliveryMethod(LessReliableDeliveryMethod, ref deliveryMethod);
                    break;
                case Player.StateType.Scale:
                    player.scale = message.ReadFloat();
                    notifyMessage.Write(stateTypeByte);
                    notifyMessage.Write(player.scale);
                    SetDeliveryMethod(LessReliableDeliveryMethod, ref deliveryMethod);
                    break;
                case Player.StateType.Ice:
                    player.ice = message.ReadBoolean();
                    notifyMessage.Write(stateTypeByte);
                    notifyMessage.Write(player.ice);
                    SetDeliveryMethod(LessReliableDeliveryMethod, ref deliveryMethod);
                    break;
                case Player.StateType.IceRotation:
                    player.iceRotation = message.ReadFloat();
                    notifyMessage.Write(stateTypeByte);
                    notifyMessage.Write(player.iceRotation);
                    SetDeliveryMethod(GlobalDeliveryMethod, ref deliveryMethod);
                    break;
            }
        }
        
        private static void SetDeliveryMethod(NetDeliveryMethod method, ref NetDeliveryMethod deliveryMethod) {
            if(method > deliveryMethod) deliveryMethod = method;
        }

        private static void ChatMessageReceived(NetBuffer message) {
            (Player player, bool registered) = GetClientData(message);
            if(!registered) return;

            string text = message.ReadString();

            Console.WriteLine($"[{player.username} ({player.ip} | {player.guid.ToString()})] {text}");

            NetOutgoingMessage resendMessage = _server.CreateMessage();
            resendMessage.Write((byte)DataType.ChatMessage);
            resendMessage.Write(player.username);
            resendMessage.Write(text);
            _server.SendMessage(resendMessage, playerRegistry.Select(ply => ply.Value.connection).ToList(),
                ReliableDeliveryMethod, 0);
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

        private static void SendToAllInCurrentRoom(Player fromPlayer, NetOutgoingMessage message, NetDeliveryMethod method) {
            tempConnections.Clear();
            // ReSharper disable once ForeachCanBePartlyConvertedToQueryUsingAnotherGetEnumerator
            foreach((Guid _, Player player) in playerRegistry) {
                if(player.room != fromPlayer.room) continue;
                tempConnections.Add(player.connection);
            }
            _server.SendMessage(message, tempConnections, method, 0);
        }

        public static void SendChatMessage(Player player, string message) {
            NetOutgoingMessage response = _server.CreateMessage();
            response.Write((byte)DataType.ChatMessage);
            response.Write(player.username);
            response.Write(message);
            _server.SendMessage(response, player.connection, ReliableDeliveryMethod);
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

        private static void LogPlayerAction(Player player, string action) =>
            LogPlayerAction(player.ip.ToString(), player.guid.ToString(), player.username, action);

        private static void LogPlayerAction(string ip, string guid, string username, string action) =>
            Console.WriteLine($"Player {username} ({ip} | {guid}) {action}");
    }
}
