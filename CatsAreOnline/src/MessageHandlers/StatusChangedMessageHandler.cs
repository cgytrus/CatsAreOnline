using System;
using System.Collections.Generic;
using System.Net;

using BepInEx.Logging;

using CatsAreOnline.Shared;
using CatsAreOnline.SyncedObjects;

using Lidgren.Network;

namespace CatsAreOnline.MessageHandlers {
    public class StatusChangedMessageHandler {
        public IPEndPoint lastConnection { get; private set; }

        private readonly Client _client;
        private readonly ManualLogSource _logger;
        private readonly Dictionary<string, Player> _playerRegistry;
        private readonly Dictionary<Guid, SyncedObject> _syncedObjectRegistry;

        private readonly IReadOnlyDictionary<NetConnectionStatus, Action<NetIncomingMessage>> _messages;

        public StatusChangedMessageHandler(Client client, ManualLogSource logger,
            Dictionary<string, Player> playerRegistry, Dictionary<Guid, SyncedObject> syncedObjectRegistry) {
            _client = client;
            _logger = logger;
            _playerRegistry = playerRegistry;
            _syncedObjectRegistry = syncedObjectRegistry;

            _messages = new Dictionary<NetConnectionStatus, Action<NetIncomingMessage>> {
                { NetConnectionStatus.Connected, ConnectedMessageReceived },
                { NetConnectionStatus.Disconnected, DisconnectedMessageReceived }
            };
        }

        public void MessageReceived(NetIncomingMessage message) {
            NetConnectionStatus status = (NetConnectionStatus)message.ReadByte();

            if(_messages.TryGetValue(status, out Action<NetIncomingMessage> action)) action(message);
            else _logger.LogInfo($"[UNHANDLED] {message.MessageType.ToString()} {status} {message.ReadString()}");
        }

        private void ConnectedMessageReceived(NetIncomingMessage message) {
            _logger.LogInfo("Connected to the server");

            NetIncomingMessage hail = message.SenderConnection.RemoteHailMessage;
            lastConnection = hail.SenderEndPoint;

            int playerCount = hail.ReadInt32();
            for(int i = 0; i < playerCount; i++) {
                Player player = new(hail.ReadString(), hail.ReadString()) {
                    worldPackGuid = hail.ReadString(),
                    worldPackName = hail.ReadString(),
                    worldGuid = hail.ReadString(),
                    worldName = hail.ReadString(),
                    roomGuid = hail.ReadString(),
                    roomName = hail.ReadString(),
                    controlling = Guid.Parse(hail.ReadString())
                };
                _logger.LogInfo($"Registering player {player.username}");
                _playerRegistry.Add(player.username, player);
            }

            int syncedObjectCount = hail.ReadInt32();
            for(int i = 0; i < syncedObjectCount; i++) {
                string username = hail.ReadString();
                SyncedObjectType type = (SyncedObjectType)hail.ReadByte();
                Guid id = Guid.Parse(hail.ReadString());
                if(!_playerRegistry.TryGetValue(username, out Player player)) { // this should never happen
                    // skip the next data, as it's useless since we can't create the object
                    // because its owner doesn't exist (how?????)
                    _logger.LogError("wtf, the owner didn't exist somehow");
                    SyncedObject.Create(_client, type, id, _client.ownPlayer, hail).Remove();
                    continue;
                }

                SyncedObject syncedObject = SyncedObject.Create(_client, type, id, player, hail);
                _syncedObjectRegistry.Add(syncedObject.id, syncedObject);
            }

            _client.AddCat();

            bool inCompanion = _client.companionId != Guid.Empty && _client.companionState != null;
            if(inCompanion)
                _client.AddSyncedObject(_client.companionId, SyncedObjectType.Companion, _client.companionState, true);
        }

        private void DisconnectedMessageReceived(NetBuffer message) {
            string reason = message.ReadString();
            _logger.LogInfo($"Disconnected from the server ({reason})");
            MultiplayerPlugin.connected.Value = false;
            foreach(KeyValuePair<Guid, SyncedObject> syncedObject in _syncedObjectRegistry)
                syncedObject.Value.Remove();
            _syncedObjectRegistry.Clear();
            _playerRegistry.Clear();
            Chat.Chat.AddMessage($"Disconnected from the server ({reason})");
        }
    }
}
