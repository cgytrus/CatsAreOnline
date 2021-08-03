using System;
using System.Collections.Generic;
using System.Linq;

using Cat;

using CatsAreOnline.Shared;
using CatsAreOnline.SyncedObjects;

using Lidgren.Network;

using UnityEngine;
using UnityEngine.UI;

namespace CatsAreOnline {
    public class Client {
        public const NetDeliveryMethod GlobalDeliveryMethod = NetDeliveryMethod.UnreliableSequenced;
        public const NetDeliveryMethod LessReliableDeliveryMethod = NetDeliveryMethod.ReliableSequenced;
        public const NetDeliveryMethod ReliableDeliveryMethod = NetDeliveryMethod.ReliableOrdered;
        
        public CatPartManager playerPartManager { get; set; }
        public CatControls playerControls { get; set; }
        public Sprite catSprite { get; set; }
        public Sprite iceSprite { get; set; }
        public Color iceColor { get; set; }

        public bool displayOwnCat {
            get => _displayOwnCat;
            set {
                _displayOwnCat = value;
                // ReSharper disable once ForeachCanBePartlyConvertedToQueryUsingAnotherGetEnumerator
                foreach(KeyValuePair<Guid, SyncedObject> syncedObject in syncedObjectRegistry)
                    syncedObject.Value.UpdateRoom();
            }
        }

        public bool playerCollisions {
            get => _playerCollisions;
            set {
                _playerCollisions = value;
                // ReSharper disable once ForeachCanBePartlyConvertedToQueryUsingAnotherGetEnumerator
                foreach(KeyValuePair<Guid, SyncedObject> syncedObject in syncedObjectRegistry)
                    syncedObject.Value.UpdateRoom();
            }
        }

        public RectTransform nameTags { get; set; }
        public Font nameTagFont { get; set; }
        public Camera nameTagCamera { get; set; }
        
        public bool inJunction { get; set; }
        public Vector2 junctionPosition { get; set; }

        public bool canConnect => playerPartManager && playerControls && catSprite && iceSprite && nameTags &&
                                         nameTagFont && nameTagCamera;

        public Player ownPlayer { get; private set; } =
            new Player(null, null, null, Guid.Empty);
        public CatSyncedObjectState catState { get; } = new CatSyncedObjectState();
        public CompanionSyncedObjectState companionState { get; set; }
        public Guid catId { get; private set; }
        public Guid companionId { get; set; }
        public Player spectating { get; set; }
        public bool restoreFollowPlayerHead { get; set; }
        public Transform restoreFollowTarget { get; set; }
        
        public readonly Dictionary<string, Player> playerRegistry = new Dictionary<string, Player>();
        public readonly Dictionary<Guid, SyncedObject> syncedObjectRegistry = new Dictionary<Guid, SyncedObject>();

        public readonly ClientDebug debug = new ClientDebug();

        private readonly IReadOnlyDictionary<DataType, Action<NetBuffer>> _receivingMessages;

        private string _guid;

        private bool _displayOwnCat;
        private bool _playerCollisions;
        private NetClient _client;
        private readonly Vector2 _nameTagOffset = Vector2.up;

        private Guid _tempSpawnGuid;
        private bool _waitingForSpawn;
        private bool _switchControllingAfterSpawn;

        public Vector2 currentCatPosition => inJunction ? junctionPosition :
            (FollowPlayer.customFollowTarget || Boiler.PlayerBoilerCounter > 0) && spectating == null ?
            (Vector2)FollowPlayer.LookAt.position :
            playerPartManager ? (Vector2)playerPartManager.GetCatCenter() : Vector2.zero;

        public Client() {
            _receivingMessages = new Dictionary<DataType, Action<NetBuffer>> {
                { DataType.RegisterPlayer, RegisterPlayerReceived },
                { DataType.PlayerJoined, PlayerJoinedReceived },
                { DataType.PlayerLeft, PlayerLeftReceived },
                { DataType.PlayerChangedRoom, PlayerChangedRoomReceived },
                { DataType.PlayerChangedControllingObject, PlayerChangedControllingObjectReceived },
                { DataType.SyncedObjectAdded, SyncedObjectAddedReceived },
                { DataType.SyncedObjectRemoved, SyncedObjectRemovedReceived },
                { DataType.SyncedObjectChangedState, SyncedObjectChangedStateReceived },
                { DataType.ChatMessage, ChatMessageReceived }
            };
            
            catState.client = this;
            
            restoreFollowPlayerHead = FollowPlayer.followPlayerHead;
            restoreFollowTarget = FollowPlayer.customFollowTarget;
            
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

        public void Connect(string ip, int port, string username, string displayName) {
            if(_client.ConnectionStatus == NetConnectionStatus.Connected) Disconnect();

            username = string.IsNullOrWhiteSpace(username) ? "<Unknown>" : username;
            displayName = string.IsNullOrWhiteSpace(displayName) ? username : displayName;
            ownPlayer = new Player(username, displayName, ownPlayer.room, ownPlayer.controlling);

            _client.Connect(ip, port);
        }

        public void Disconnect() {
            if(_client.ConnectionStatus == NetConnectionStatus.Connected)
                _client.Disconnect("User disconnected");
        }

        public void Update() {
            NetIncomingMessage message;
            while((message = _client.ReadMessage()) != null) {
                MessageReceived(message);
                _client.Recycle(message);
            }
        }

        public void UpdateAllNameTagsPositions() {
            foreach(KeyValuePair<Guid, SyncedObject> syncedObject in syncedObjectRegistry)
                UpdateNameTagPosition(syncedObject.Value);
        }

        public void SendChatMessage(string text) {
            if(_client.ConnectionStatus != NetConnectionStatus.Connected) {
                Chat.Chat.AddErrorMessage("Not connected to a server");
                return;
            }
            
            NetOutgoingMessage message = _client.CreateMessage();
            message.Write((byte)DataType.ChatMessage);
            message.Write(_guid);
            message.Write(text);
            SendMessageToServer(message, ReliableDeliveryMethod);
        }

        public void SendServerCommand(string command) {
            if(_client.ConnectionStatus != NetConnectionStatus.Connected) {
                Chat.Chat.AddErrorMessage("Not connected to a server");
                return;
            }
            
            NetOutgoingMessage message = _client.CreateMessage();
            message.Write((byte)DataType.Command);
            message.Write(_guid);
            message.Write(command);
            SendMessageToServer(message, ReliableDeliveryMethod);
        }

        public void ExecuteCommand(string command) {
            Chat.Chat.AddMessage($"<color=blue><b>COMMAND:</b> {command}</color>");
            try {
                Commands.dispatcher.Execute(command, this);
            }
            catch(Exception ex) {
                Chat.Chat.AddErrorMessage(ex.Message);
            }
        }

        public static float GetScaleFromCatState(State state) {
            switch(state) {
                case State.Liquid: return 1f;
                default: return 1.35f;
            }
        }

        public void SendStateDeltaToServer(Guid id, SyncedObjectState state) {
            if(_client.ConnectionStatus != NetConnectionStatus.Connected || !state.anythingChanged) return;

            NetOutgoingMessage message = PrepareMessage(DataType.SyncedObjectChangedState);
            message.Write(id.ToString());
            state.WriteDeltaToMessage(message);
            SendMessageToServer(message, state.deliveryMethod);
        }

        public void UpdateRoom() {
            if(_client.ConnectionStatus != NetConnectionStatus.Connected) return;

            NetOutgoingMessage message = PrepareMessage(DataType.PlayerChangedRoom);
            message.Write(ownPlayer.room);
            SendMessageToServer(message, ReliableDeliveryMethod);
            
            foreach(KeyValuePair<Guid, SyncedObject> syncedObject in syncedObjectRegistry)
                syncedObject.Value.UpdateRoom();

            catId = Guid.NewGuid();
            AddSyncedObject(catId, SyncedObjectType.Cat, catState, true);
        }

        private void MessageReceived(NetIncomingMessage message) {
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

        private void StatusChangedMessageReceived(NetIncomingMessage message) {
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
        
        private void Connected() {
            Debug.Log("[CaO] Connected to the server");
            NetOutgoingMessage message = _client.CreateMessage();
            message.Write((byte)DataType.RegisterPlayer);
            message.Write(""); // send an empty guid because we don't have one yet
            ownPlayer.Write(message);
            SendMessageToServer(message, ReliableDeliveryMethod);
        }
        
        private void Disconnected(string reason) {
            Debug.Log("[CaO] Disconnected from the server");
            MultiplayerPlugin.connected.Value = false;
            _guid = null;
            foreach(KeyValuePair<Guid, SyncedObject> syncedObject in syncedObjectRegistry)
                syncedObject.Value.Remove();
            syncedObjectRegistry.Clear();
            playerRegistry.Clear();
            Chat.Chat.AddErrorMessage($"Disconnected from the server ({reason})");
        }

        public void AddSyncedObject(Guid id, SyncedObjectType type, SyncedObjectState state, bool switchControlling) {
            if(_client.ConnectionStatus != NetConnectionStatus.Connected) return;

            _tempSpawnGuid = id;
            NetOutgoingMessage message = PrepareMessage(DataType.SyncedObjectAdded);
            message.Write((byte)type);
            message.Write(_tempSpawnGuid.ToString());
            state.Write(message);
            SendMessageToServer(message, ReliableDeliveryMethod);
            _waitingForSpawn = true;
            _switchControllingAfterSpawn = switchControlling;
        }

        public void RemoveSyncedObject(Guid id) {
            if(_client.ConnectionStatus != NetConnectionStatus.Connected) return;

            NetOutgoingMessage message = PrepareMessage(DataType.SyncedObjectRemoved);
            message.Write(id.ToString());
            SendMessageToServer(message, ReliableDeliveryMethod);
        }

        public void ChangeControllingObject(Guid id) {
            ownPlayer.controlling = id;
            NetOutgoingMessage controllingMessage = PrepareMessage(DataType.PlayerChangedControllingObject);
            controllingMessage.Write(id.ToString());
            SendMessageToServer(controllingMessage, ReliableDeliveryMethod);
        }

        private void DataMessageReceived(NetBuffer message) {
            byte typeByte = message.ReadByte();
            DataType type = (DataType)typeByte;
            
            debug.PrintServer(type);

            if(_receivingMessages.TryGetValue(type, out Action<NetBuffer> action)) action(message);
            else Debug.Log($"[WARN] Unknown message type received: {type.ToString()}");
        }

        private void RegisterPlayerReceived(NetBuffer message) {
            _guid = message.ReadString();
            
            int playerCount = message.ReadInt32();
            for(int i = 0; i < playerCount; i++) {
                Player player = new Player(message.ReadString(), message.ReadString(), message.ReadString(),
                    Guid.Parse(message.ReadString()));
                Debug.Log($"[CaO] Registering player {player.username}");
                playerRegistry.Add(player.username, player);
            }

            int syncedObjectCount = message.ReadInt32();
            for(int i = 0; i < syncedObjectCount; i++) {
                string username = message.ReadString();
                SyncedObjectType type = (SyncedObjectType)message.ReadByte();
                Guid id = Guid.Parse(message.ReadString());
                if(!playerRegistry.TryGetValue(username, out Player player)) { // this should never happen
                    // skip the next data, as it's useless since we can't create the object
                    // because its owner doesn't exist (how?????)
                    Debug.LogError("[CaO] wtf, the owner didn't exist somehow");
                    SyncedObject.Create(this, type, id, ownPlayer, message).Remove();
                    continue;
                }

                SyncedObject syncedObject = SyncedObject.Create(this, type, id, player, message);
                syncedObjectRegistry.Add(syncedObject.id, syncedObject);
            }

            catId = Guid.NewGuid();
            AddSyncedObject(catId, SyncedObjectType.Cat, catState, true);
            
            if(companionId != Guid.Empty && companionState != null)
                AddSyncedObject(companionId, SyncedObjectType.Companion, companionState, true);
        }

        private void PlayerJoinedReceived(NetBuffer message) {
            Player player = new Player(message.ReadString(), message.ReadString(), message.ReadString(),
                Guid.Parse(message.ReadString()));
            Debug.Log($"[CaO] Registering player {player.username}");
            playerRegistry.Add(player.username, player);
            Chat.Chat.AddMessage($"Player {player.displayName} joined");
        }
        
        private void PlayerLeftReceived(NetBuffer message) {
            string username = message.ReadString();
            if(!playerRegistry.TryGetValue(username, out Player player)) return;
            
            Debug.Log($"[CaO] Player {player.username} left");
            Chat.Chat.AddMessage($"Player {player.displayName} left");

            if(spectating?.username == username) {
                FollowPlayer.followPlayerHead = restoreFollowPlayerHead;
                FollowPlayer.customFollowTarget = restoreFollowTarget;
                spectating = null;
                Chat.Chat.AddMessage($"Stopped spectating <b>{username}</b> (player left)");
            }

            List<Guid> toRemove = (from syncedObject in syncedObjectRegistry
                                          where syncedObject.Value.owner.username == username select syncedObject.Key)
                .ToList();
            foreach(Guid id in toRemove) {
                syncedObjectRegistry[id].Remove();
                syncedObjectRegistry.Remove(id);
            }
            playerRegistry.Remove(username);
        }

        private void PlayerChangedRoomReceived(NetBuffer message) {
            string username = message.ReadString();
            if(!playerRegistry.TryGetValue(username, out Player player)) return;

            string room = message.ReadString();

            if(!player.RoomEqual(room)) {
                if(spectating?.username == username) {
                    FollowPlayer.followPlayerHead = restoreFollowPlayerHead;
                    FollowPlayer.customFollowTarget = restoreFollowTarget;
                    spectating = null;
                    Chat.Chat.AddMessage($"Stopped spectating <b>{username}</b> (player changed room)");
                }
                
                List<Guid> toRemove = (from syncedObject in syncedObjectRegistry
                                              where syncedObject.Value.owner.username == username
                                              select syncedObject.Key).ToList();
                foreach(Guid id in toRemove) {
                    syncedObjectRegistry[id].Remove();
                    syncedObjectRegistry.Remove(id);
                }
            }

            player.room = room;
        }

        private void PlayerChangedControllingObjectReceived(NetBuffer message) {
            string username = message.ReadString();
            if(!playerRegistry.TryGetValue(username, out Player player)) return;

            player.controlling = Guid.Parse(message.ReadString());

            if(spectating != player) return;
            FollowPlayer.customFollowTarget = syncedObjectRegistry[player.controlling].transform;
        }

        private void SyncedObjectAddedReceived(NetBuffer message) {
            string username = message.ReadString();
            if(!playerRegistry.TryGetValue(username, out Player player)) return;
            
            SyncedObjectType type = (SyncedObjectType)message.ReadByte();
            Guid id = Guid.Parse(message.ReadString());

            if(syncedObjectRegistry.ContainsKey(id)) return;
            
            SyncedObject syncedObject = SyncedObject.Create(this, type, id, player, message);
            syncedObjectRegistry.Add(id, syncedObject);

            if(!_waitingForSpawn || id != _tempSpawnGuid) return;
            _waitingForSpawn = false;
            if(!_switchControllingAfterSpawn) return;
            ChangeControllingObject(id);
        }

        private void SyncedObjectRemovedReceived(NetBuffer message) {
            Guid id = Guid.Parse(message.ReadString());
            syncedObjectRegistry[id].Remove();
            syncedObjectRegistry.Remove(id);
        }
        
        private void SyncedObjectChangedStateReceived(NetBuffer message) {
            Guid id = Guid.Parse(message.ReadString());
            if(!syncedObjectRegistry.TryGetValue(id, out SyncedObject syncedObject)) return;

            while(message.ReadByte(out byte stateTypeByte)) syncedObject.ReadChangedState(message, stateTypeByte);
        }

        private void ChatMessageReceived(NetBuffer message) {
            string username = message.ReadString();
            if(!playerRegistry.TryGetValue(username, out Player player)) return;
            
            string text = message.ReadString();

            Debug.Log($"[{player.username} ({username})] {text}");
            Chat.Chat.AddMessage($"[{player.displayName}] {text}");
        }

        private void UpdateNameTagPosition(SyncedObject cat) {
            Vector3 playerPos = cat.renderer.transform.position;
            
            Text nameTag = cat.nameTag;
            if(!nameTag) return;
            
            float horTextExtent = nameTag.preferredWidth * 0.5f;
            float vertTextExtent = nameTag.preferredHeight;

            Vector3 camPos = nameTagCamera.transform.position;
            float vertExtent = nameTagCamera.orthographicSize;
            float horExtent = vertExtent * Screen.width / Screen.height;
            float minX = camPos.x - horExtent + horTextExtent + 0.5f;
            float maxX = camPos.x + horExtent - horTextExtent - 0.5f;
            float minY = camPos.y - vertExtent + 0.5f;
            float maxY = camPos.y + vertExtent - vertTextExtent - 0.5f;
                                
            float scale = cat.state.scale;
            nameTag.rectTransform.anchoredPosition =
                new Vector2(Mathf.Clamp(playerPos.x + _nameTagOffset.x * scale, minX, maxX),
                    Mathf.Clamp(playerPos.y + _nameTagOffset.y * scale, minY, maxY));
        }

        private NetOutgoingMessage PrepareMessage(DataType type) {
            NetOutgoingMessage message = _client.CreateMessage();
            message.Write((byte)type);
            message.Write(_guid);
            return message;
        }

        private void SendMessageToServer(NetOutgoingMessage message, NetDeliveryMethod method) {
            debug.PrintClient(message);
            _client.SendMessage(message, method);
        }
    }
}
