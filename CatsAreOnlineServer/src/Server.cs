using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;
using System.Threading;

using CatsAreOnline.Shared;

using CatsAreOnlineServer.MessageHandlers;
using CatsAreOnlineServer.SyncedObjects;

using Lidgren.Network;

namespace CatsAreOnlineServer {
    public static class Server {
        public const string Version = "0.5.0";
        public static TimeSpan targetTickTime { get; } = TimeSpan.FromSeconds(0.01d);

        public static IReadOnlyDictionary<Guid, Player> players => playerRegistry;
        public static TimeSpan uptime => _uptimeStopwatch.Elapsed;

        private const int MaxUsernameLength = 64;
        private const int MaxDisplayNameLength = 64;

        private static readonly Dictionary<Guid, Player> playerRegistry = new();
        private static readonly Dictionary<Guid, SyncedObject> syncedObjectRegistry = new();

        private static NetServer _server;
        private static Stopwatch _uptimeStopwatch;

        private static MessageHandler _messageHandler;

        public static void Main(string[] args) {
            bool upnp = false;
            if(args.Length <= 0 || !int.TryParse(args[0], out int port) ||
               args.Length >= 2 && !bool.TryParse(args[1], out upnp)) {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("Invalid arguments.");
                return;
            }

            Commands.Initialize();

            NetPeerConfiguration config = new("mod.cgytrus.plugins.calOnline") {
                Port = port,
                EnableUPnP = true
            };

            config.DisableMessageType(NetIncomingMessageType.Receipt);
            config.EnableMessageType(NetIncomingMessageType.ConnectionApproval);
            config.DisableMessageType(NetIncomingMessageType.DebugMessage);
            config.DisableMessageType(NetIncomingMessageType.DiscoveryRequest); // enable later
            config.DisableMessageType(NetIncomingMessageType.DiscoveryResponse); // enable later
            config.DisableMessageType(NetIncomingMessageType.UnconnectedData);
            config.DisableMessageType(NetIncomingMessageType.ConnectionLatencyUpdated);
            config.DisableMessageType(NetIncomingMessageType.NatIntroductionSuccess);
            config.DisableMessageType(NetIncomingMessageType.VerboseDebugMessage);

            _server = new NetServer(config);

            string portStr = port.ToString(CultureInfo.InvariantCulture);
            Console.ForegroundColor = ConsoleColor.DarkGreen;
            Console.WriteLine($"Starting server (v{Version}) on port {portStr}{(upnp ? " (UPnP)" : "")}");
            Console.ResetColor();
            _server.Start();

            // doesn't seem to work?
            if(upnp) _server.UPnP.ForwardPort(port, "Cats are Liquid - A Better Place");

            while(_server.Status != NetPeerStatus.Running) { }
            _uptimeStopwatch = Stopwatch.StartNew();

            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("Server started");
            Console.ResetColor();

            _messageHandler = new MessageHandler {
                server = _server,
                playerRegistry = playerRegistry,
                syncedObjectRegistry = syncedObjectRegistry,
                statusChangedMessageHandler = new StatusChangedMessageHandler {
                    server = _server,
                    playerRegistry = playerRegistry,
                    syncedObjectRegistry = syncedObjectRegistry
                },
                dataMessageHandler = new DataMessageHandler {
                    server = _server,
                    playerRegistry = playerRegistry,
                    syncedObjectRegistry = syncedObjectRegistry
                }
            };

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
            _uptimeStopwatch.Stop();
            Console.WriteLine("Stopping server...");
            _server.Shutdown("Server closed");
            _server.UPnP.DeleteForwardingRule(_server.Port);
        }

        private static void ServerThread() {
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("Server thread started");
            Console.ResetColor();

            Stopwatch tickStopwatch = Stopwatch.StartNew();
            while(_server.Status == NetPeerStatus.Running) {
                tickStopwatch.Restart();

                NetIncomingMessage message;
                while((message = _server.ReadMessage()) != null) {
                    _messageHandler.MessageReceived(message);
                    _server.Recycle(message);
                }

                TimeSpan timeout = targetTickTime - tickStopwatch.Elapsed;
                if(timeout.Ticks > 0L) Thread.Sleep(timeout);
            }
        }

        public static string ValidateRegisteringPlayer(string username, string displayName, Guid id) {
            if(username.Length > MaxUsernameLength) {
                string maxLength = MaxUsernameLength.ToString(CultureInfo.InvariantCulture);
                return $"Username too long (max length = {maxLength})";
            }

            if(displayName.Length > MaxDisplayNameLength) {
                string maxLength = MaxDisplayNameLength.ToString(CultureInfo.InvariantCulture);
                return $"Display name too long (max length = {maxLength})";
            }

            if(username.Length <= 0) return "Username empty";

            Regex alphanumRegex = new("^[a-zA-Z0-9]*$");
            if(!alphanumRegex.IsMatch(username)) return "Username contains non-alphanumeric characters";

            if(playerRegistry.ContainsKey(id)) return "GUID already taken (what, you're lucky ig)";

            // ReSharper disable once ConvertIfStatementToReturnStatement
            if(playerRegistry.Any(ply => ply.Value.username == username)) return "Username already taken";

            return null;
        }

        public static void ExecuteCommand(Player player, string command) {
            try { Commands.dispatcher.Execute(command, player); }
            catch(Exception ex) { SendChatMessage(null, player, ServerErrorMessage(ex.Message)); }
        }

        public static void SendChatMessageToAll(Player from, string message) {
            Console.WriteLine(message);
            NetOutgoingMessage response = _server.CreateMessage();
            response.Write((byte)DataType.ChatMessage);
            response.Write(from?.username);
            response.Write(message);
            _server.SendToAll(response, DeliveryMethods.Reliable);
        }

        public static void SendChatMessage(Player from, Player to, string message) {
            Console.WriteLine(message);
            NetOutgoingMessage response = _server.CreateMessage();
            response.Write((byte)DataType.ChatMessage);
            response.Write(from?.username);
            response.Write(message);
            if(to is not null) _server.SendMessage(response, to.connection, DeliveryMethods.Reliable);
        }

        public static string ServerDebugMessage(string message) => $"<color=grey><b>DEBUG:</b> {message}</color>";
        public static string ServerWarningMessage(string message) => $"<color=yellow><b>WARN:</b> {message}</color>";
        public static string ServerErrorMessage(string message) => $"<color=red><b>ERROR:</b> {message}</color>";

        public static (Player player, bool registered) GetClientData(NetBuffer message) =>
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
