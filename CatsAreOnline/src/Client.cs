using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;

using BepInEx.Logging;

using CatsAreOnline.MessageHandlers;
using CatsAreOnline.Shared;
using CatsAreOnline.SyncedObjects;

using Lidgren.Network;

using UnityEngine;

using Object = UnityEngine.Object;

namespace CatsAreOnline;

public class Client {
    public bool displayOwnCat {
        get => _displayOwnCat;
        set {
            _displayOwnCat = value;
            // ReSharper disable once ForeachCanBePartlyConvertedToQueryUsingAnotherGetEnumerator
            foreach(KeyValuePair<Guid, SyncedObject> syncedObject in _syncedObjectRegistry)
                syncedObject.Value.UpdateLocation();
        }
    }

    public bool playerCollisions {
        get => _playerCollisions;
        set {
            _playerCollisions = value;
            // ReSharper disable once ForeachCanBePartlyConvertedToQueryUsingAnotherGetEnumerator
            foreach(KeyValuePair<Guid, SyncedObject> syncedObject in _syncedObjectRegistry)
                syncedObject.Value.UpdateLocation();
        }
    }

    public RectTransform? nameTags { get; private set; }

    public bool canConnect => MultiplayerPlugin.capturedData.catPartManager &&
                              MultiplayerPlugin.capturedData.catControls &&
                              MultiplayerPlugin.capturedData.catSprite &&
                              MultiplayerPlugin.capturedData.iceSprite &&
                              nameTags;

    public Player ownPlayer { get; private set; } = new(null, null) { controlling = Guid.Empty };
    public CatSyncedObjectState catState { get; } = new();
    public CompanionSyncedObjectState? companionState { get; private set; }
    public Guid catId { get; private set; }
    public Guid companionId { get; private set; }

    public Player? spectating {
        get => _spectating;
        set {
            _spectating = value;
            if(value is null) {
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

    private readonly Dictionary<string, Player> _playerRegistry = new();
    private readonly Dictionary<Guid, SyncedObject> _syncedObjectRegistry = new();

    private readonly NetClient _client;
    private readonly MessageHandler _messageHandler;

    private bool _displayOwnCat;
    private bool _playerCollisions;

    private Player? _spectating;
    private bool _restoreFollowPlayerHead;
    private Transform _restoreFollowTarget;

    private Camera? _nameTagCamera;

    private Guid _waitingForSpawnGuid;
    private bool _waitingForSpawn;
    private bool _switchControllingAfterSpawn;

    // yeah, idk how this works either
    public Vector2 currentCatPosition => MultiplayerPlugin.capturedData.inJunction ?
        MultiplayerPlugin.capturedData.junctionPosition :
        (FollowPlayer.customFollowTarget || Boiler.PlayerBoilerCounter > 0) && spectating == null ?
            (Vector2)FollowPlayer.LookAt.position :
            MultiplayerPlugin.capturedData.catPartManager ?
                MultiplayerPlugin.capturedData.catPartManager!.GetCatCenter() : Vector2.zero;

    public Client(ManualLogSource logger) {
        catState.client = this;

        _restoreFollowPlayerHead = FollowPlayer.followPlayerHead;
        _restoreFollowTarget = FollowPlayer.customFollowTarget;

        NetPeerConfiguration config = new("mod.cgytrus.plugins.calOnline");

        config.EnableMessageType(NetIncomingMessageType.ConnectionApproval);
        config.EnableMessageType(NetIncomingMessageType.UnconnectedData);

        config.DisableMessageType(NetIncomingMessageType.Receipt);
        config.DisableMessageType(NetIncomingMessageType.DebugMessage);
        config.DisableMessageType(NetIncomingMessageType.DiscoveryRequest); // enable later
        config.DisableMessageType(NetIncomingMessageType.DiscoveryResponse); // enable later
        config.DisableMessageType(NetIncomingMessageType.ConnectionLatencyUpdated);
        config.DisableMessageType(NetIncomingMessageType.NatIntroductionSuccess);
        config.DisableMessageType(NetIncomingMessageType.VerboseDebugMessage);

        _client = new NetClient(config);

        StatusChangedMessageHandler statusChangedMessageHandler =
            new(this, logger, _playerRegistry, _syncedObjectRegistry);
        _messageHandler = new MessageHandler(logger, statusChangedMessageHandler,
            new UnconnectedDataMessageHandler(logger, statusChangedMessageHandler),
            new DataMessageHandler(this, logger, _playerRegistry, _syncedObjectRegistry));

        _client.Start();
    }

    public void Connect(IPEndPoint ip, string username, string displayName) {
        if(_client.ConnectionStatus == NetConnectionStatus.Connected) Disconnect();

        username = string.IsNullOrWhiteSpace(username) ? "<Unknown>" : username;
        displayName = string.IsNullOrWhiteSpace(displayName) ? username : displayName;
        ownPlayer = new Player(username, displayName) {
            worldPackGuid = ownPlayer.worldPackGuid,
            worldPackName = ownPlayer.worldPackName,
            worldGuid = ownPlayer.worldGuid,
            worldName = ownPlayer.worldName,
            roomGuid = ownPlayer.roomGuid,
            roomName = ownPlayer.roomName,
            controlling = ownPlayer.controlling
        };

        NetOutgoingMessage approval = _client.CreateMessage();
        ownPlayer.Write(approval);
        _client.Connect(ip, approval);
    }

    public void Disconnect() {
        if(_client.ConnectionStatus == NetConnectionStatus.Connected)
            _client.Disconnect("User disconnected");
    }

    public void Update() {
        NetIncomingMessage message;
        while((message = _client.ReadMessage()) != null) {
            _messageHandler.MessageReceived(message);
            _client.Recycle(message);
        }
    }

    public void UpdateAllNameTagsPositions() {
        if(!_nameTagCamera) _nameTagCamera = Camera.main;

        // should never happen but just in case
        if(!_nameTagCamera) return;

        foreach(KeyValuePair<Guid, SyncedObject> syncedObject in _syncedObjectRegistry)
            syncedObject.Value.UpdateNameTagPosition(_nameTagCamera!);
    }

    public void InitializeNameTags() {
        if(this.nameTags) return;

        GameObject nameTags = new("Name Tags") { layer = LayerMask.NameToLayer("UI") };
        Object.DontDestroyOnLoad(nameTags);

        RectTransform nameTagsTransform = nameTags.AddComponent<RectTransform>();
        nameTagsTransform.anchoredPosition = Vector2.zero;
        nameTagsTransform.localScale = Vector3.one;

        Canvas canvas = nameTags.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
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

    public void UpdateWorldPack() {
        if(_client.ConnectionStatus != NetConnectionStatus.Connected) return;

        NetOutgoingMessage message = PrepareMessage(DataType.PlayerChangedWorldPack);
        message.Write(ownPlayer.worldPackGuid);
        message.Write(ownPlayer.worldPackName);
        SendMessageToServer(message, DeliveryMethods.Reliable);

        foreach(KeyValuePair<Guid, SyncedObject> syncedObject in _syncedObjectRegistry)
            syncedObject.Value.UpdateLocation();
    }

    public void UpdateWorld() {
        if(_client.ConnectionStatus != NetConnectionStatus.Connected) return;

        NetOutgoingMessage message = PrepareMessage(DataType.PlayerChangedWorld);
        message.Write(ownPlayer.worldGuid);
        message.Write(ownPlayer.worldName);
        SendMessageToServer(message, DeliveryMethods.Reliable);

        foreach(KeyValuePair<Guid, SyncedObject> syncedObject in _syncedObjectRegistry)
            syncedObject.Value.UpdateLocation();
    }

    public void UpdateRoom() {
        if(_client.ConnectionStatus != NetConnectionStatus.Connected) return;

        NetOutgoingMessage message = PrepareMessage(DataType.PlayerChangedRoom);
        message.Write(ownPlayer.roomGuid);
        message.Write(ownPlayer.roomName);
        SendMessageToServer(message, DeliveryMethods.Reliable);

        foreach(KeyValuePair<Guid, SyncedObject> syncedObject in _syncedObjectRegistry)
            syncedObject.Value.UpdateLocation();
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

    // ReSharper disable once MemberCanBePrivate.Global
    public void RemoveSyncedObject(Guid id) {
        if(_client.ConnectionStatus != NetConnectionStatus.Connected) return;

        NetOutgoingMessage message = PrepareMessage(DataType.SyncedObjectRemoved);
        message.Write(id.ToString());
        SendMessageToServer(message, DeliveryMethods.Reliable);
    }

    public void AddCat() {
        catId = Guid.NewGuid();
        AddSyncedObject(catId, SyncedObjectType.Cat, catState, true);
    }

    public void AddCompanion() {
        companionId = Guid.NewGuid();
        companionState = new CompanionSyncedObjectState { client = this };
        companionState.Update();
        AddSyncedObject(companionId, SyncedObjectType.Companion, companionState, true);
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

    public void CheckForWaitingObject(Guid id) {
        if(!_waitingForSpawn || id != _waitingForSpawnGuid) return;
        _waitingForSpawn = false;
        if(!_switchControllingAfterSpawn) return;
        ChangeControllingObject(id);
    }

    private NetOutgoingMessage PrepareMessage(DataType type) {
        NetOutgoingMessage message = _client.CreateMessage();
        message.Write((byte)type);
        return message;
    }

    private void SendMessageToServer(NetOutgoingMessage message, NetDeliveryMethod method) =>
        _client.SendMessage(message, method);
}
