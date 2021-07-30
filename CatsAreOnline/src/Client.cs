using System;
using System.Collections.Generic;

using Cat;

using Lidgren.Network;

using UnityEngine;
using UnityEngine.UI;

using Object = UnityEngine.Object;

namespace CatsAreOnline {
    public static class Client {
        public const NetDeliveryMethod GlobalDeliveryMethod = NetDeliveryMethod.UnreliableSequenced;
        public const NetDeliveryMethod LessReliableDeliveryMethod = NetDeliveryMethod.ReliableSequenced;
        public const NetDeliveryMethod ReliableDeliveryMethod = NetDeliveryMethod.ReliableOrdered;
        
        public static CatPartManager playerPartManager { get; set; }
        public static CatControls playerControls { get; set; }
        public static Sprite catSprite { get; set; }
        public static Sprite iceSprite { get; set; }
        public static Color iceColor { get; set; }

        public static bool displayOwnCat {
            get => _displayOwnCat;
            set {
                _displayOwnCat = value;
                if(username != null && playerRegistry.TryGetValue(username, out Player player))
                    player.gameObject.SetActive(_displayOwnCat);
            }
        }

        public bool playerCollisions {
            get => _playerCollisions;
            set {
                _playerCollisions = value;
                foreach(Player player in playerRegistry.Values)
                    player.collider.enabled = player.username != username && _playerCollisions;
            }
        }

        public static RectTransform nameTags { get; set; }
        public static Font nameTagFont { get; set; }
        public static Camera nameTagCamera { get; set; }
        
        public static bool inJunction { get; set; }
        public static Vector2 junctionPosition { get; set; }

        public static bool canConnect => playerPartManager && playerControls && catSprite && iceSprite && nameTags &&
                                         nameTagFont && nameTagCamera;

        public static string username { get; private set; }
        public static string displayName { get; private set; }
        public static PlayerState state { get; } = new PlayerState();
        public static Player spectating { get; set; }
        
        public static readonly Dictionary<string, Player> playerRegistry = new Dictionary<string, Player>();
        public static readonly Dictionary<string, Command> commandRegistry = new Dictionary<string, Command>();

        public static readonly ClientDebug debug = new ClientDebug();

        private static string _guid;

        private static bool _displayOwnCat;
        private static NetClient _client;
        private static readonly Vector2 nameTagOffset = Vector2.up;

        public static Vector2 currentCatPosition => inJunction ? junctionPosition :
            (FollowPlayer.customFollowTarget || Boiler.PlayerBoilerCounter > 0) && spectating == null ?
            (Vector2)FollowPlayer.LookAt.position :
            playerPartManager ? (Vector2)playerPartManager.GetCatCenter() : Vector2.zero;

        public static void Initialize() {
            NetPeerConfiguration config = new NetPeerConfiguration("mod.cgytrus.plugin.calOnline");
            
            config.DisableMessageType(NetIncomingMessageType.Receipt);
            config.DisableMessageType(NetIncomingMessageType.ConnectionApproval);
            config.DisableMessageType(NetIncomingMessageType.DebugMessage);
            config.DisableMessageType(NetIncomingMessageType.DiscoveryRequest); // enable later
            config.DisableMessageType(NetIncomingMessageType.DiscoveryResponse); // enable later
            config.DisableMessageType(NetIncomingMessageType.UnconnectedData);
            config.DisableMessageType(NetIncomingMessageType.ConnectionLatencyUpdated);
            config.DisableMessageType(NetIncomingMessageType.NatIntroductionSuccess);
            config.DisableMessageType(NetIncomingMessageType.VerboseDebugMessage);

            _client = new NetClient(config);
            _client.Start();
        }

        public static void Connect(string ip, int port, string username, string displayName) {
            if(_client.ConnectionStatus == NetConnectionStatus.Connected) Disconnect();
                
            Client.username = string.IsNullOrWhiteSpace(username) ? "<Unknown>" : username;
            Client.displayName = string.IsNullOrWhiteSpace(displayName) ? username : displayName;
            
            _client.Connect(ip, port);
        }

        public static void Disconnect() {
            if(_client.ConnectionStatus == NetConnectionStatus.Connected)
                _client.Disconnect("User disconnected");
        }

        public static void Update() {
            NetIncomingMessage message;
            while((message = _client.ReadMessage()) != null) {
                MessageReceived(message);
                _client.Recycle(message);
            }
        }

        public static void UpdateAllNameTagsPositions() {
            foreach(KeyValuePair<string, Player> player in playerRegistry)
                UpdateNameTagPosition(player.Value);
        }

        public static void SendChatMessage(string text) {
            if(_client.ConnectionStatus != NetConnectionStatus.Connected) return;
            
            NetOutgoingMessage message = _client.CreateMessage();
            message.Write((byte)DataType.ChatMessage);
            message.Write(_guid);
            message.Write(text);
            SendMessageToServer(message, ReliableDeliveryMethod);
        }

        public static void SendServerCommand(string command, params string[] args) {
            if(_client.ConnectionStatus != NetConnectionStatus.Connected) {
                Chat.Chat.AddErrorMessage("Not connected to a server");
                return;
            }
            
            NetOutgoingMessage message = _client.CreateMessage();
            message.Write((byte)DataType.Command);
            message.Write(_guid);
            message.Write(command);
            message.Write(args.Length);
            foreach(string arg in args) message.Write(arg);
            SendMessageToServer(message, ReliableDeliveryMethod);
        }

        public static void ExecuteCommand(string command, params string[] args) {
            Chat.Chat.AddMessage($"<color=blue><b>COMMAND:</b> {command} {string.Join(" ", args)}</color>");
            if(commandRegistry.TryGetValue(command, out Command action)) {
                try {
                    action.Execute(args);
                }
                catch(Exception ex) {
                    Chat.Chat.AddErrorMessage(ex.Message);
                }
            }
            else Chat.Chat.AddErrorMessage($"Command '<b>{command}</b>' not found");
        }

        public static float GetScaleFromCatState(State state) {
            switch(state) {
                case State.Liquid: return 1f;
                default: return 1.35f;
            }
        }

        public static void SendStateDeltaToServer(PlayerState state) {
            if(_client.ConnectionStatus != NetConnectionStatus.Connected || !state.anythingChanged) return;

            NetOutgoingMessage message = PrepareMessage(DataType.PlayerChangedState);
            state.WriteDeltaToMessage(message);
            SendMessageToServer(message, state.deliveryMethod);
        }

        public static void RoomChanged() {
            foreach(KeyValuePair<string, Player> player in playerRegistry) {
                player.Value.SetRoom(player.Value.state.room, state.room);
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
                    Debug.LogWarning($"[CaO] {message.ReadString()}");
                    break;
                case NetIncomingMessageType.Error:
                case NetIncomingMessageType.ErrorMessage:
                    Debug.LogError($"[CaO] {message.ReadString()}");
                    break;
                default:
                    Debug.Log($"[CaO] [UNHANDLED] {message.MessageType}");
                    break;
            }
        }

        private static void StatusChangedMessageReceived(NetIncomingMessage message) {
            NetConnectionStatus status = (NetConnectionStatus)message.ReadByte();
            switch(status) {
                case NetConnectionStatus.Connected:
                    Connected();
                    break;
                case NetConnectionStatus.Disconnected:
                    Disconnected(message.ReadString());
                    break;
                default:
                    Debug.Log($"[CaO] [UNHANDLED] {message.MessageType} {status} {message.ReadString()}");
                    break;
            }
        }
        
        private static void Connected() {
            Debug.Log("[CaO] Connected to the server");
            NetOutgoingMessage message = _client.CreateMessage();
            message.Write((byte)DataType.RegisterPlayer);
            message.Write(""); // send an empty guid because we don't have one yet
            message.Write(username);
            message.Write(displayName);
            message.Write(state);
            SendMessageToServer(message, ReliableDeliveryMethod);
        }
        
        private static void Disconnected(string reason) {
            Debug.Log("[CaO] Disconnected from the server");
            MultiplayerPlugin.connected.Value = false;
            _guid = null;
            foreach(KeyValuePair<string, Player> player in playerRegistry) RemovePlayer(player.Value);
            playerRegistry.Clear();
            Chat.Chat.AddMessage($"Disconnected from the server ({reason})");
        }

        private static void DataMessageReceived(NetBuffer message) {
            DataType type = (DataType)message.ReadByte();
            
            debug.PrintServer(type);

            switch(type) {
                case DataType.RegisterPlayer:
                    RegisterPlayerReceived(message);
                    break;
                case DataType.PlayerJoined:
                    PlayerJoinedReceived(message);
                    break;
                case DataType.PlayerLeft:
                    PlayerLeftReceived(message);
                    break;
                case DataType.PlayerChangedState:
                    PlayerChangedStateReceived(message);
                    break;
                case DataType.ChatMessage:
                    ChatMessageReceived(message);
                    break;
                default:
                    Debug.Log("[WARN] Unknown message type received");
                    break;
            }
        }

        private static void RegisterPlayerReceived(NetBuffer message) {
            _guid = message.ReadString();
            int count = message.ReadInt32();
            for(int i = 0; i < count; ++i) {
                Player player = SpawnPlayer(message);
                Debug.Log($"[CaO] Registering player {player.username}");
                playerRegistry.Add(player.username, player);
            }
        }

        private static void PlayerJoinedReceived(NetBuffer message) {
            Player player = SpawnPlayer(message);
            Debug.Log($"[CaO] Registering player {player.username}");
            playerRegistry.Add(player.username, player);
            Chat.Chat.AddMessage($"Player {player.displayName} joined");
        }
        
        private static void PlayerLeftReceived(NetBuffer message) {
            string username = message.ReadString();
            
            Debug.Log($"[CaO] Player {playerRegistry[username].username} left");
            Chat.Chat.AddMessage($"Player {playerRegistry[username].displayName} left");
            
            RemovePlayer(playerRegistry[username]);
            playerRegistry.Remove(username);
        }
        
        private static void PlayerChangedStateReceived(NetBuffer message) {
            string username = message.ReadString();
            Player player = playerRegistry[username];
            while(message.ReadByte(out byte stateTypeByte)) {
                PlayerState.Type stateType = (PlayerState.Type)stateTypeByte;
                switch(stateType) {
                    case PlayerState.Type.Position:
                        player.SetPosition(message.ReadVector2());
                        break;
                    case PlayerState.Type.Room:
                        player.SetRoom(message.ReadString(), state.room);
                        break;
                    case PlayerState.Type.Color:
                        player.SetColor(message.ReadColor());
                        break;
                    case PlayerState.Type.Scale:
                        player.SetScale(message.ReadFloat());
                        break;
                    case PlayerState.Type.Ice:
                        player.SetIce(message.ReadBoolean());
                        break;
                    case PlayerState.Type.IceRotation:
                        player.SetIceRotation(message.ReadFloat());
                        break;
                }
            }
        }

        private static void ChatMessageReceived(NetBuffer message) {
            string username = message.ReadString();
            
            Player player = playerRegistry[username];
            string text = message.ReadString();

            Debug.Log($"[{player.username} ({username})] {text}");
            Chat.Chat.AddMessage($"[{player.displayName}] {text}");
        }
        
        private static Player SpawnPlayer(NetBuffer message) => SpawnPlayer(message.ReadString(), message.ReadString(),
            message.ReadVector2(), message.ReadString(), message.ReadColor(), message.ReadFloat(),
            message.ReadBoolean(), message.ReadFloat());

        private static Player SpawnPlayer(string username, string displayName, Vector2 position, string room,
            Color color, float scale, bool ice, float iceRotation) {
            GameObject obj = new GameObject($"OnlinePlayer_{username}") { layer = 0 };
            Object.DontDestroyOnLoad(obj);

            SpriteRenderer renderer = obj.AddComponent<SpriteRenderer>();
            renderer.sortingOrder = -50;

            Rigidbody2D rigidbody = obj.AddComponent<Rigidbody2D>();
            rigidbody.bodyType = RigidbodyType2D.Kinematic;
            rigidbody.interpolation = RigidbodyInterpolation2D.Extrapolate;
            rigidbody.useFullKinematicContacts = true;

            CircleCollider2D collider = obj.AddComponent<CircleCollider2D>();
            collider.radius = 0.4f;
            
            Player player = obj.AddComponent<Player>();
            player.username = username;
            player.displayName = displayName;
            player.nameTag = CreatePlayerNameTag(username, displayName);
            player.renderer = renderer;
            player.rigidbody = rigidbody;
            player.collider = collider;
            
            player.SetPosition(position);
            player.SetRoom(room, state.room);
            player.SetColor(color);
            player.SetScale(scale);
            player.SetIce(ice);
            player.SetIceRotation(iceRotation);
            
            return player;
        }

        private static Text CreatePlayerNameTag(string username, string displayName) {
            GameObject nameTag = new GameObject($"OnlinePlayerNameTag_{username}") {
                layer = LayerMask.NameToLayer("UI")
            };
            Object.DontDestroyOnLoad(nameTag);

            RectTransform nameTagTransform = nameTag.AddComponent<RectTransform>();
            nameTagTransform.SetParent(nameTags);
            nameTagTransform.sizeDelta = new Vector2(200f, 30f);
            nameTagTransform.pivot = new Vector2(0.5f, 0f);
            nameTagTransform.localScale = Vector3.one;
            
            Text nameTagText = nameTag.AddComponent<Text>();
            nameTagText.font = nameTagFont;
            nameTagText.fontSize = 28;
            nameTagText.alignment = TextAnchor.LowerCenter;
            nameTagText.horizontalOverflow = HorizontalWrapMode.Overflow;
            nameTagText.verticalOverflow = VerticalWrapMode.Overflow;
            nameTagText.supportRichText = true;
            nameTagText.text = displayName;

            return nameTagText;
        }

        private static void UpdateNameTagPosition(Player player) {
            Vector3 playerPos = player.transform.position;
            
            Text nameTag = player.nameTag;
            float horTextExtent = nameTag.preferredWidth * 0.5f;
            float vertTextExtent = nameTag.preferredHeight;

            Vector3 camPos = nameTagCamera.transform.position;
            float vertExtent = nameTagCamera.orthographicSize;
            float horExtent = vertExtent * Screen.width / Screen.height;
            float minX = camPos.x - horExtent + horTextExtent + 0.5f;
            float maxX = camPos.x + horExtent - horTextExtent - 0.5f;
            float minY = camPos.y - vertExtent + 0.5f;
            float maxY = camPos.y + vertExtent - vertTextExtent - 0.5f;
                                
            float scale = player.state.scale;
            nameTag.rectTransform.anchoredPosition =
                new Vector2(Mathf.Clamp(playerPos.x + nameTagOffset.x * scale, minX, maxX),
                    Mathf.Clamp(playerPos.y + nameTagOffset.y * scale, minY, maxY));
        }

        private static void RemovePlayer(Player player) {
            if(spectating == player) {
                spectating = null;
                Chat.Chat.AddMessage($"Stopped spectating <b>{player.username}</b> (player left)");
            }
            Object.Destroy(player.nameTag.gameObject);
            Object.Destroy(player.gameObject);
        }

        private static NetOutgoingMessage PrepareMessage(DataType type) {
            NetOutgoingMessage message = _client.CreateMessage();
            message.Write((byte)type);
            message.Write(_guid);
            return message;
        }

        private static void SendMessageToServer(NetOutgoingMessage message, NetDeliveryMethod method) {
            debug.PrintClient(message);
            _client.SendMessage(message, method);
        }
    }
}
