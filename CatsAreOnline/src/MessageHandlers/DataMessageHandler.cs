using System;
using System.Collections.Generic;
using System.Linq;

using BepInEx.Logging;

using CatsAreOnline.Shared;
using CatsAreOnline.SyncedObjects;

using Lidgren.Network;

namespace CatsAreOnline.MessageHandlers {
    public class DataMessageHandler {
        private readonly Client _client;
        private readonly ManualLogSource _logger;
        private readonly Dictionary<string, Player> _playerRegistry;
        private readonly Dictionary<Guid, SyncedObject> _syncedObjectRegistry;

        private readonly IReadOnlyDictionary<DataType, Action<NetIncomingMessage>> _messages;

        public DataMessageHandler(Client client, ManualLogSource logger, Dictionary<string, Player> playerRegistry,
            Dictionary<Guid, SyncedObject> syncedObjectRegistry) {
            _client = client;
            _logger = logger;
            _playerRegistry = playerRegistry;
            _syncedObjectRegistry = syncedObjectRegistry;

            _messages = new Dictionary<DataType, Action<NetIncomingMessage>> {
                { DataType.PlayerJoined, PlayerJoinedReceived },
                { DataType.PlayerLeft, PlayerLeftReceived },
                { DataType.PlayerChangedWorldPack, PlayerChangedWorldPackReceived },
                { DataType.PlayerChangedWorld, PlayerChangedWorldReceived },
                { DataType.PlayerChangedRoom, PlayerChangedRoomReceived },
                { DataType.PlayerChangedControllingObject, PlayerChangedControllingObjectReceived },
                { DataType.SyncedObjectAdded, SyncedObjectAddedReceived },
                { DataType.SyncedObjectRemoved, SyncedObjectRemovedReceived },
                { DataType.SyncedObjectChangedState, SyncedObjectChangedStateReceived },
                { DataType.ChatMessage, ChatMessageReceived }
            };
        }

        public void MessageReceived(NetIncomingMessage message) {
            DataType type = (DataType)message.ReadByte();

            if(_messages.TryGetValue(type, out Action<NetIncomingMessage> action)) action(message);
            else _logger.LogWarning($"[WARN] Unknown data message type received: {type.ToString()}");
        }

        private void PlayerJoinedReceived(NetBuffer message) {
            Player player = new(message.ReadString(), message.ReadString()) {
                worldPackGuid = message.ReadString(),
                worldPackName = message.ReadString(),
                worldGuid = message.ReadString(),
                worldName = message.ReadString(),
                roomGuid = message.ReadString(),
                roomName = message.ReadString(),
                controlling = Guid.Parse(message.ReadString())
            };
            _logger.LogInfo($"Registering player {player.username}");
            _playerRegistry.Add(player.username, player);
            Chat.Chat.AddMessage($"Player {player.displayName} joined");
        }

        private void PlayerLeftReceived(NetBuffer message) {
            string username = message.ReadString();
            if(!_playerRegistry.TryGetValue(username, out Player player)) return;

            _logger.LogInfo($"Player {player.username} left");
            Chat.Chat.AddMessage($"Player {player.displayName} left");

            if(_client.spectating?.username == username) {
                _client.spectating = null;
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

        private void PlayerChangedWorldPackReceived(NetBuffer message) {
            string username = message.ReadString();
            if(!_playerRegistry.TryGetValue(username, out Player player)) return;

            string worldPackGuid = message.ReadString();
            string worldPackName = message.ReadString();

            if(player.LocationEqual(worldPackGuid, player.worldGuid, player.roomGuid)) return;

            LocationChanged(username);
            player.worldPackGuid = worldPackGuid;
            player.worldPackName = worldPackName;
        }

        private void PlayerChangedWorldReceived(NetBuffer message) {
            string username = message.ReadString();
            if(!_playerRegistry.TryGetValue(username, out Player player)) return;

            string worldGuid = message.ReadString();
            string worldName = message.ReadString();

            if(player.LocationEqual(player.worldPackGuid, worldGuid, player.roomGuid)) return;

            LocationChanged(username);
            player.worldGuid = worldGuid;
            player.worldName = worldName;
        }

        private void PlayerChangedRoomReceived(NetBuffer message) {
            string username = message.ReadString();
            if(!_playerRegistry.TryGetValue(username, out Player player)) return;

            string roomGuid = message.ReadString();
            string roomName = message.ReadString();

            if(player.LocationEqual(player.worldPackGuid, player.worldGuid, roomGuid)) return;

            LocationChanged(username);
            player.roomGuid = roomGuid;
            player.roomName = roomName;
        }

        private void LocationChanged(string username) {
            if(_client.spectating?.username == username) {
                _client.spectating = null;
                Chat.Chat.AddMessage($"Stopped spectating <b>{username}</b> (player changed location)");
            }

            List<Guid> toRemove = (from syncedObject in _syncedObjectRegistry
                                   where syncedObject.Value.owner.username == username
                                   select syncedObject.Key).ToList();
            foreach(Guid id in toRemove) {
                _syncedObjectRegistry[id].Remove();
                _syncedObjectRegistry.Remove(id);
            }
        }

        private void PlayerChangedControllingObjectReceived(NetBuffer message) {
            string username = message.ReadString();
            if(!_playerRegistry.TryGetValue(username, out Player player)) return;

            player.controlling = Guid.Parse(message.ReadString());

            if(_client.spectating != player) return;
            FollowPlayer.customFollowTarget = _syncedObjectRegistry[player.controlling].transform;
        }

        private void SyncedObjectAddedReceived(NetBuffer message) {
            string username = message.ReadString();
            if(!_playerRegistry.TryGetValue(username, out Player player)) return;

            SyncedObjectType type = (SyncedObjectType)message.ReadByte();
            Guid id = Guid.Parse(message.ReadString());

            if(_syncedObjectRegistry.ContainsKey(id)) return;

            SyncedObject syncedObject = SyncedObject.Create(_client, type, id, player, message);
            _syncedObjectRegistry.Add(id, syncedObject);

            _client.CheckForWaitingObject(id);
        }

        private void SyncedObjectRemovedReceived(NetBuffer message) {
            Guid id = Guid.Parse(message.ReadString());
            _syncedObjectRegistry[id].Remove();
            _syncedObjectRegistry.Remove(id);
        }

        private void SyncedObjectChangedStateReceived(NetIncomingMessage message) {
            Guid id = Guid.Parse(message.ReadString());
            if(!_syncedObjectRegistry.TryGetValue(id, out SyncedObject syncedObject)) return;

            syncedObject.ReadStateDelta(message);
        }

        private void ChatMessageReceived(NetBuffer message) {
            string username = message.ReadString();
            Player player = new("SERVER", "<color=blue>SERVER</color>");
            if(!string.IsNullOrEmpty(username) && !_playerRegistry.TryGetValue(username, out player)) return;

            string text = message.ReadString();

            _logger.LogInfo($"[{player.username}] {text}");
            Chat.Chat.AddMessage($"[{player.displayName}] {text}");
        }
    }
}
