using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;
using System.Threading;

using CatsAreOnline.Shared;

using CatsAreOnlineServer.Configuration;
using CatsAreOnlineServer.MessageHandlers;
using CatsAreOnlineServer.SyncedObjects;

using Lidgren.Network;

using NLog;

namespace CatsAreOnlineServer;

public static class Server {
    private static readonly Logger logger = LogManager.GetCurrentClassLogger();
    private static readonly Logger successLogger = logger.WithProperty("success", true);
    private static readonly Logger progressLogger = logger.WithProperty("progress", true);
    private static readonly Logger actionLogger = LogManager.GetLogger("").WithProperty("layout", "playerAction");
    private static readonly Logger chatLogger = LogManager.GetLogger("").WithProperty("layout", "chat");

    private const string SemVersion = "0.5.2";
#if DEBUG
    public const string Version = $"{SemVersion}-debug";
#else
    public const string Version = SemVersion;
#endif
    // ReSharper disable once MemberCanBePrivate.Global
    public static TimeSpan targetTickTime { get; private set; }

    public static IReadOnlyDictionary<NetConnection, Player> players => playerRegistry;
    public static TimeSpan uptime => _uptimeStopwatch.Elapsed;

    public static Config config { get; private set; }
    // ReSharper disable once MemberCanBePrivate.Global
    public static Commands commands { get; private set; }

    private static readonly List<IPEndPoint> reconnectEndPoints = new();

    private static readonly Dictionary<NetConnection, Player> playerRegistry = new();
    private static readonly Dictionary<Guid, SyncedObject> syncedObjectRegistry = new();

    private static bool _mainRunning = true;

    private static NetServer _server;
    private static Thread _serverThread;
    private static Thread _consoleThread;
    private static Stopwatch _uptimeStopwatch;

    private static MessageHandler _messageHandler;

    private static void Main() {
        while(_mainRunning) {
            LogManager.Configuration = LogManager.Configuration;
            Initialize();
            _consoleThread!.Join();
            _consoleThread = null;
            _server = null;
        }
    }

    private static void Initialize() {
        NetPeerConfiguration peerConfig = new(SharedConfig.AppId);

        logger.Trace("Initializing config");

        config = new Config("config.json");
        config.Load();
        config.AddValue("port", new ConfigValue<int>(SharedConfig.Port)).valueChanged += (_, _) => {
            logger.Info("Port was changed, this requires a server restart to take effect!");
        };
        config.AddValue("upnp", new ConfigValue<bool>(false)).valueChanged += (_, _) => {
            logger.Info("UPnP was changed, this requires a server restart to take effect!");
        };
        config.AddValue("maxUsernameLength", new ConfigValue<int>(64));
        config.AddValue("maxDisplayNameLength", new ConfigValue<int>(64));
        config.AddValue("targetTickTime", new ConfigValue<double>(0.01d)).valueChanged += (_, _) => {
            targetTickTime = TimeSpan.FromSeconds(config.GetValue<double>("targetTickTime").value);
        };
        config.Save();

        config.GetValue<double>("targetTickTime").ForceUpdateValue();

        logger.Trace("Initializing commands");

        commands = new Commands();

        peerConfig.Port = config.GetValue<int>("port").value;
        peerConfig.EnableUPnP = config.GetValue<bool>("upnp").value;

        peerConfig.EnableMessageType(NetIncomingMessageType.ConnectionApproval);
        peerConfig.EnableMessageType(NetIncomingMessageType.UnconnectedData);
        peerConfig.EnableMessageType(NetIncomingMessageType.ConnectionLatencyUpdated);

#if DEBUG
        peerConfig.EnableMessageType(NetIncomingMessageType.DebugMessage);
        peerConfig.EnableMessageType(NetIncomingMessageType.VerboseDebugMessage);
#else
        peerConfig.DisableMessageType(NetIncomingMessageType.DebugMessage);
        peerConfig.DisableMessageType(NetIncomingMessageType.VerboseDebugMessage);
#endif

        peerConfig.DisableMessageType(NetIncomingMessageType.Receipt);
        peerConfig.DisableMessageType(NetIncomingMessageType.DiscoveryRequest); // enable later
        peerConfig.DisableMessageType(NetIncomingMessageType.DiscoveryResponse); // enable later
        peerConfig.DisableMessageType(NetIncomingMessageType.NatIntroductionSuccess);

        _server = new NetServer(peerConfig);

        string port = peerConfig.Port.ToString(CultureInfo.InvariantCulture);
        string upnp = peerConfig.EnableUPnP ? " (UPnP)" : "";
        progressLogger.Info($"Starting server (v{Version}) on port {port}{upnp}");

        _server.Start();

        _server.UPnP?.ForwardPort(peerConfig.Port, "Cats are Liquid - A Better Place");

        while(_server.Status != NetPeerStatus.Running) { }
        _uptimeStopwatch = Stopwatch.StartNew();

        successLogger.Info("Server started");

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

        _serverThread = new Thread(ServerThread);
        _consoleThread = new Thread(ConsoleThread);
        _serverThread.Start();
        _consoleThread.Start();
    }

    private static void ServerThread() {
        successLogger.Info("Server thread started");

        TrySendReconnectMessage();

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

        while(_server.Socket is not null) { }
    }

    private static void ConsoleThread() {
        successLogger.Info("Console thread started");

        while(_server.Status == NetPeerStatus.Running) {
            string command = Console.ReadLine();
            if(_server.Status != NetPeerStatus.Running) break;
            ExecuteCommand(null, command);
        }
    }

    public static void Stop() {
        _mainRunning = false;
        Stop("Server closed");
    }

    public static void Restart() {
        SendChatMessageToAll(null, "The server is being restarted, you are going to be reconnected automatically");
        // ReSharper disable once HeapView.ObjectAllocation
        reconnectEndPoints.AddRange(_server.Connections.Select(connection => connection.RemoteEndPoint));
        Stop("Server restarting");
    }

    private static void Stop(string reason) {
        logger.Info($"Stopping server ({reason})");

        _uptimeStopwatch.Stop();
        _uptimeStopwatch = null;

        _server.Shutdown(reason);
        _server.UPnP?.DeleteForwardingRule(_server.Port);
        _serverThread.Join();
        _messageHandler = null;
        _serverThread = null;

        config.Save();
        config = null;

        commands = null;

        playerRegistry.Clear();
        syncedObjectRegistry.Clear();
    }

    private static void TrySendReconnectMessage() {
        if(reconnectEndPoints.Count <= 0) return;
        NetOutgoingMessage message = _server.CreateMessage();
        message.Write((byte)DataType.RestartReconnect);
        _server.SendUnconnectedMessage(message, reconnectEndPoints);
        reconnectEndPoints.Clear();
    }

    public static string ValidateRegisteringPlayer(string username, string displayName, int protocol) {
        if(protocol != SharedConfig.Protocol) {
            string client = protocol.ToString(CultureInfo.InvariantCulture);
            string server = SharedConfig.Protocol.ToString(CultureInfo.InvariantCulture);
            string ending = protocol < SharedConfig.Protocol ? "please update your client" :
                "please connect with an older client or ask the administrator of the server to update";
            return $"Client protocol ({client}) version doesn't match server protocol version ({server}), {ending}";
        }

        int maxUsernameLength = config.GetValue<int>("maxUsernameLength").value;
        if(username.Length > maxUsernameLength) {
            string maxLength = maxUsernameLength.ToString(CultureInfo.InvariantCulture);
            return $"Username too long (max length = {maxLength})";
        }

        int maxDisplayNameLength = config.GetValue<int>("maxDisplayNameLength").value;
        if(displayName.Length > maxDisplayNameLength) {
            string maxLength = maxDisplayNameLength.ToString(CultureInfo.InvariantCulture);
            return $"Display name too long (max length = {maxLength})";
        }

        if(username.Length <= 0) return "Username empty";

        Regex alphanumRegex = new("^[a-zA-Z0-9]*$");
        if(!alphanumRegex.IsMatch(username)) return "Username contains non-alphanumeric characters";

        // ReSharper disable once ConvertIfStatementToReturnStatement
        if(playerRegistry.Any(ply => ply.Value.username == username)) return "Username already taken";

        return null;
    }

    public static void ExecuteCommand(Player player, string command) {
        try { commands.dispatcher.Execute(command, player); }
        catch(Exception ex) { SendChatMessage(null, player, ServerErrorMessage(ex.Message)); }
    }

    private static readonly KeyValuePair<object, object> whisperFalseProperty = new("whisper", false);
    public static void SendChatMessageToAll(Player from, string message) {
        chatLogger.Log(
            new LogEventInfo(LogLevel.Info, from?.username, message) { Properties = { whisperFalseProperty } });
        NetOutgoingMessage response = _server.CreateMessage();
        response.Write((byte)DataType.ChatMessage);
        response.Write(from?.username);
        response.Write(message);
        _server.SendToAll(response, DeliveryMethods.Reliable);
    }

    private static readonly KeyValuePair<object, object> whisperTrueProperty = new("whisper", true);
    public static void SendChatMessage(Player from, Player to, string message) {
        chatLogger.Log(
            new LogEventInfo(LogLevel.Info, from?.username, message) {
                Properties = { whisperTrueProperty, { "to", to?.username } }
            });
        NetOutgoingMessage response = _server.CreateMessage();
        response.Write((byte)DataType.ChatMessage);
        response.Write(from?.username);
        response.Write(message);
        if(to is not null) _server.SendMessage(response, to.connection, DeliveryMethods.Reliable);
    }

    public static string ServerDebugMessage(string message) => $"<color=grey><b>DEBUG:</b> {message}</color>";
    public static string ServerWarningMessage(string message) => $"<color=yellow><b>WARN:</b> {message}</color>";
    public static string ServerErrorMessage(string message) => $"<color=red><b>ERROR:</b> {message}</color>";

    public static Player GetPlayer(NetIncomingMessage message) =>
        playerRegistry.TryGetValue(message.SenderConnection, out Player player) ? player : null;

    public static void LogPlayerAction(Player player, string action) =>
        LogPlayerAction(player.username, action);

    private static void LogPlayerAction(string username, string action) =>
        actionLogger.Log(new LogEventInfo(LogLevel.Info, username, action));
}
