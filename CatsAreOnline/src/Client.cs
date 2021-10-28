using System;
using System.Collections.Generic;
using System.Linq;

using CatsAreOnline.Shared;
using CatsAreOnline.SyncedObjects;

using Lidgren.Network;

using UnityEngine;

using Object = UnityEngine.Object;

namespace CatsAreOnline {
    public class Client {
        public bool displayOwnCat {
            get => _displayOwnCat;
            set {
                _displayOwnCat = value;
                // ReSharper disable once ForeachCanBePartlyConvertedToQueryUsingAnotherGetEnumerator
                foreach(KeyValuePair<Guid, SyncedObject> syncedObject in _syncedObjectRegistry)
                    syncedObject.Value.UpdateRoom();
            }
        }

        public bool playerCollisions {
            get => _playerCollisions;
            set {
                _playerCollisions = value;
                // ReSharper disable once ForeachCanBePartlyConvertedToQueryUsingAnotherGetEnumerator
                foreach(KeyValuePair<Guid, SyncedObject> syncedObject in _syncedObjectRegistry)
                    syncedObject.Value.UpdateRoom();
            }
        }

        public RectTransform nameTags { get; private set; }

        public bool canConnect => CapturedData.catPartManager && CapturedData.catControls && CapturedData.catSprite &&
                                  CapturedData.iceSprite && nameTags && _nameTagCamera;

        public Player ownPlayer { get; private set; } =
            new(null, null, null, Guid.Empty);
        public CatSyncedObjectState catState { get; } = new();
        public CompanionSyncedObjectState companionState { get; private set; }
        public Guid catId { get; private set; }
        public Guid companionId { get; private set; }

        public Player spectating {
            get => _spectating;
            set {
                _spectating = value;
                if(value == null) {
                    FollowPlayer.followPlayerHead = _restoreFollowPlayerHead;
                    FollowPlayer.customFollowTarget = _restoreFollowTarget;
                }
                else {
                    _restoreFollowPlayerHead = FollowPlayer.followPlayerHead;
                    _restoreFollowTarget = FollowPlayer.customFollowTarget;
                    FollowPlayer.followPlayerHead = false;
                    FollowPlayer.customFollowTarget = _syncedObjectRegistry[value.controlling].transform;
                }
            }
        }

        public IReadOnlyDictionary<string, Player> playerRegistry => _playerRegistry;
        public IReadOnlyDictionary<Guid, SyncedObject> syncedObjectRegistry => _syncedObjectRegistry;
        public ICollection<Player> players => _playerRegistry.Values;
        public ICollection<SyncedObject> syncedObjects => _syncedObjectRegistry.Values;

        public readonly ClientDebug debug = new();
        
        private readonly Dictionary<string, Player> _playerRegistry = new();
        private readonly Dictionary<Guid, SyncedObject> _syncedObjectRegistry = new();

        private readonly IReadOnlyDictionary<DataType, Action<NetBuffer>> _receivingDataMessages;

        private string _guid;

        private readonly NetClient _client;
        
        private bool _displayOwnCat;
        private bool _playerCollisions;
        
        private Player _spectating;
        private bool _restoreFollowPlayerHead;
        private Transform _restoreFollowTarget;
        
        private Camera _nameTagCamera;

        private Guid _waitingForSpawnGuid;
        private bool _waitingForSpawn;
        private bool _switchControllingAfterSpawn;

        public Vector2 currentCatPosition => CapturedData.inJunction ? CapturedData.junctionPosition :
            (FollowPlayer.customFollowTarget || Boiler.PlayerBoilerCounter > 0) && spectating == null ?
            (Vector2)FollowPlayer.LookAt.position :
            CapturedData.catPartManager ? (Vector2)CapturedData.catPartManager.GetCatCenter() : Vector2.zero;

        public Client() {
            _receivingDataMessages = new Dictionary<DataType, Action<NetBuffer>> {
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
            
            _restoreFollowPlayerHead = FollowPlayer.followPlayerHead;
            _restoreFollowTarget = FollowPlayer.customFollowTarget;
            
            NetPeerConfiguration config = new("mod.cgytrus.plugins.calOnline");
            
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
            foreach(KeyValuePair<Guid, SyncedObject> syncedObject in _syncedObjectRegistry)
                syncedObject.Value.UpdateNameTagPosition(_nameTagCamera);
        }

        public void InitializeNameTags() {
            Object.Destroy(this.nameTags);
            _nameTagCamera = Camera.main;
            GameObject nameTags = new("Name Tags") { layer = LayerMask.NameToLayer("UI") };
            Object.DontDestroyOnLoad(nameTags);

            RectTransform nameTagsTransform = nameTags.AddComponent<RectTransform>();
            nameTagsTransform.anchoredPosition = Vector2.zero;
            nameTagsTransform.localScale = Vector3.one;

            Canvas canvas = nameTags.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.worldCamera = _nameTagCamera;
            canvas.scaleFactor = 720f;

            this.nameTags = nameTagsTransform;
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
            SendMessageToServer(message, DeliveryMethods.Reliable);
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
            SendMessageToServer(message, DeliveryMethods.Reliable);
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
            SendMessageToServer(message, DeliveryMethods.Reliable);
            
            foreach(KeyValuePair<Guid, SyncedObject> syncedObject in _syncedObjectRegistry)
                syncedObject.Value.UpdateRoom();

            catId = Guid.NewGuid();
            AddSyncedObject(catId, SyncedObjectType.Cat, catState, true);
        }

        public void AddSyncedObject(Guid id, SyncedObjectType type, SyncedObjectState state, bool switchControlling) {
            if(_client.ConnectionStatus != NetConnectionStatus.Connected) return;

            _waitingForSpawnGuid = id;
            NetOutgoingMessage message = PrepareMessage(DataType.SyncedObjectAdded);
            message.Write((byte)type);
            message.Write(_waitingForSpawnGuid.ToString());
            state.Write(message);
            SendMessageToServer(message, DeliveryMethods.Reliable);
            _waitingForSpawn = true;
            _switchControllingAfterSpawn = switchControlling;
        }

        public void RemoveSyncedObject(Guid id) {
            if(_client.ConnectionStatus != NetConnectionStatus.Connected) return;

            NetOutgoingMessage message = PrepareMessage(DataType.SyncedObjectRemoved);
            message.Write(id.ToString());
            SendMessageToServer(message, DeliveryMethods.Reliable);
        }
        
        public void AddCompanion() {
            companionId = Guid.NewGuid();
            companionState = new CompanionSyncedObjectState { client = this };
            companionState.Update();
            AddSyncedObject(companionId, SyncedObjectType.Companion, companionState,
                true);
        }
        
        public void RemoveCompanion() {
            companionState = null;
            RemoveSyncedObject(companionId);
            companionId = Guid.Empty;
        }

        public void ChangeControllingObject(Guid id) {
            ownPlayer.controlling = id;
            NetOutgoingMessage controllingMessage = PrepareMessage(DataType.PlayerChangedControllingObject);
            controllingMessage.Write(id.ToString());
            SendMessageToServer(controllingMessage, DeliveryMethods.Reliable);
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
            SendMessageToServer(message, DeliveryMethods.Reliable);
        }
        
        private void Disconnected(string reason) {
            Debug.Log("[CaO] Disconnected from the server");
            MultiplayerPlugin.connected.Value = false;
            _guid = null;
            foreach(KeyValuePair<Guid, SyncedObject> syncedObject in _syncedObjectRegistry)
                syncedObject.Value.Remove();
            _syncedObjectRegistry.Clear();
            _playerRegistry.Clear();
            Chat.Chat.AddMessage($"Disconnected from the server ({reason})");
        }

        private void DataMessageReceived(NetBuffer message) {
            byte typeByte = message.ReadByte();
            DataType type = (DataType)typeByte;
            
            debug.PrintServer(type);

            if(_receivingDataMessages.TryGetValue(type, out Action<NetBuffer> action)) action(message);
            else Debug.LogWarning($"[CaO] [WARN] Unknown message type received: {type.ToString()}");
        }

        private void RegisterPlayerReceived(NetBuffer message) {
            _guid = message.ReadString();
            
            int playerCount = message.ReadInt32();
            for(int i = 0; i < playerCount; i++) {
                Player player = new(message.ReadString(), message.ReadString(), message.ReadString(),
                    Guid.Parse(message.ReadString()));
                Debug.Log($"[CaO] Registering player {player.username}");
                _playerRegistry.Add(player.username, player);
            }

            int syncedObjectCount = message.ReadInt32();
            for(int i = 0; i < syncedObjectCount; i++) {
                string username = message.ReadString();
                SyncedObjectType type = (SyncedObjectType)message.ReadByte();
                Guid id = Guid.Parse(message.ReadString());
                if(!_playerRegistry.TryGetValue(username, out Player player)) { // this should never happen
                    // skip the next data, as it's useless since we can't create the object
                    // because its owner doesn't exist (how?????)
                    Debug.LogError("[CaO] wtf, the owner didn't exist somehow");
                    SyncedObject.Create(this, type, id, ownPlayer, message).Remove();
                    continue;
                }

                SyncedObject syncedObject = SyncedObject.Create(this, type, id, player, message);
                _syncedObjectRegistry.Add(syncedObject.id, syncedObject);
            }

            bool inCompanion = companionId != Guid.Empty && companionState != null;

            catId = Guid.NewGuid();
            AddSyncedObject(catId, SyncedObjectType.Cat, catState, !inCompanion);
            
            if(inCompanion) AddSyncedObject(companionId, SyncedObjectType.Companion, companionState, true);
        }

        private void PlayerJoinedReceived(NetBuffer message) {
            Player player = new(message.ReadString(), message.ReadString(), message.ReadString(),
                Guid.Parse(message.ReadString()));
            Debug.Log($"[CaO] Registering player {player.username}");
            _playerRegistry.Add(player.username, player);
            Chat.Chat.AddMessage($"Player {player.displayName} joined");
        }
        
        private void PlayerLeftReceived(NetBuffer message) {
            string username = message.ReadString();
            if(!_playerRegistry.TryGetValue(username, out Player player)) return;
            
            Debug.Log($"[CaO] Player {player.username} left");
            Chat.Chat.AddMessage($"Player {player.displayName} left");

            if(spectating?.username == username) {
                spectating = null;
                Chat.Chat.AddMessage($"Stopped spectating <b>{username}</b> (player left)");
            }

            List<Guid> toRemove = (from syncedObject in _syncedObjectRegistry
                                          where syncedObject.Value.owner.username == username select syncedObject.Key)
                .ToList();
            foreach(Guid id in toRemove) {
                _syncedObjectRegistry[id].Remove();
                _syncedObjectRegistry.Remove(id);
            }
            _playerRegistry.Remove(username);
        }

        private void PlayerChangedRoomReceived(NetBuffer message) {
            string username = message.ReadString();
            if(!_playerRegistry.TryGetValue(username, out Player player)) return;

            string room = message.ReadString();

            if(player.RoomEqual(room)) {
                player.room = room;
                return;
            }

            if(spectating?.username == username) {
                spectating = null;
                Chat.Chat.AddMessage($"Stopped spectating <b>{username}</b> (player changed room)");
            }

            List<Guid> toRemove = (from syncedObject in _syncedObjectRegistry
                                   where syncedObject.Value.owner.username == username
                                   select syncedObject.Key).ToList();
            foreach(Guid id in toRemove) {
                _syncedObjectRegistry[id].Remove();
                _syncedObjectRegistry.Remove(id);
            }

            player.room = room;
        }

        private void PlayerChangedControllingObjectReceived(NetBuffer message) {
            string username = message.ReadString();
            if(!_playerRegistry.TryGetValue(username, out Player player)) return;

            player.controlling = Guid.Parse(message.ReadString());

            if(spectating != player) return;
            FollowPlayer.customFollowTarget = _syncedObjectRegistry[player.controlling].transform;
        }

        private void SyncedObjectAddedReceived(NetBuffer message) {
            string username = message.ReadString();
            if(!_playerRegistry.TryGetValue(username, out Player player)) return;
            
            SyncedObjectType type = (SyncedObjectType)message.ReadByte();
            Guid id = Guid.Parse(message.ReadString());

            if(_syncedObjectRegistry.ContainsKey(id)) return;
            
            SyncedObject syncedObject = SyncedObject.Create(this, type, id, player, message);
            _syncedObjectRegistry.Add(id, syncedObject);

            if(!_waitingForSpawn || id != _waitingForSpawnGuid) return;
            _waitingForSpawn = false;
            if(!_switchControllingAfterSpawn) return;
            ChangeControllingObject(id);
        }

        private void SyncedObjectRemovedReceived(NetBuffer message) {
            Guid id = Guid.Parse(message.ReadString());
            _syncedObjectRegistry[id].Remove();
            _syncedObjectRegistry.Remove(id);
        }
        
        private void SyncedObjectChangedStateReceived(NetBuffer message) {
            Guid id = Guid.Parse(message.ReadString());
            if(!_syncedObjectRegistry.TryGetValue(id, out SyncedObject syncedObject)) return;

            while(message.ReadByte(out byte stateTypeByte)) syncedObject.ReadChangedState(message, stateTypeByte);
        }

        private void ChatMessageReceived(NetBuffer message) {
            string username = message.ReadString();
            if(!_playerRegistry.TryGetValue(username, out Player player)) return;
            
            string text = message.ReadString();

            Debug.Log($"[{player.username} ({username})] {text}");
            Chat.Chat.AddMessage($"[{player.displayName}] {text}");
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
