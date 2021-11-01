using System;

using Lidgren.Network;

using PolyMap;

namespace CatsAreOnline {
    public class Player {
        public string username { get; }
        public string displayName { get; }
        public string worldPackGuid { get; set; }
        public string worldGuid { get; set; }
        public string roomGuid { get; set; }
        public string worldPackName { get; set; }
        public string worldName { get; set; }
        public string roomName { get; set; }
        public Guid controlling { get; set; }

        public Player(string username, string displayName) {
            this.username = username;
            this.displayName = displayName;
        }

        public void Write(NetBuffer message) {
            message.Write(username);
            message.Write(displayName);
            message.Write(worldPackGuid);
            message.Write(worldPackName);
            message.Write(worldGuid);
            message.Write(worldName);
            message.Write(roomGuid);
            message.Write(roomName);
            message.Write(controlling.ToString());
        }

        public bool UpdateWorldPack() => ItemManager.AllowEditing ? ResetLocation() : UpdateWorldPackDirect();
        public bool UpdateWorld() => ItemManager.AllowEditing ? ResetLocation() : UpdateWorldDirect();
        public bool UpdateRoom() => ItemManager.AllowEditing ? ResetLocation() : UpdateRoomDirect();

        private bool UpdateWorldPackDirect() {
            string oldWorldPackGuid = worldPackGuid;
            worldPackGuid = WorldPackSettings.CurrentWorldPackSettings.worldPackGUID;
            worldPackName = WorldPackSettings.CurrentWorldPackSettings.worldPackName;
            return oldWorldPackGuid != worldPackGuid;
        }

        private bool UpdateWorldDirect() {
            string oldWorldGuid = worldGuid;
            worldGuid = WorldSettings.CurrentWorldSettings.worldGUID;
            worldName = WorldSettings.CurrentWorldSettings.worldName;
            return oldWorldGuid != worldGuid;
        }

        private bool UpdateRoomDirect() {
            string oldRoomGuid = roomGuid;
            roomGuid = RSSystem.RoomSettings.GetCurrentRoomSettings.roomGUID;
            roomName = RSSystem.RoomSettings.GetCurrentRoomSettings.roomName;
            return oldRoomGuid != roomGuid;
        }

        public bool ResetLocation() {
            bool willChange = !string.IsNullOrEmpty(worldPackGuid) || !string.IsNullOrEmpty(worldGuid) ||
                              !string.IsNullOrEmpty(roomGuid);
            worldPackGuid = null;
            worldPackName = null;
            worldGuid = null;
            worldName = null;
            roomGuid = null;
            roomName = null;
            return willChange;
        }

        public bool IsPlaying() => !string.IsNullOrEmpty(worldPackGuid) && !string.IsNullOrEmpty(worldGuid) &&
                                  !string.IsNullOrEmpty(roomGuid);

        public bool LocationEqual(string worldPackGuid, string worldGuid, string roomGuid) => IsPlaying() &&
            worldPackGuid == this.worldPackGuid && worldGuid == this.worldGuid && roomGuid == this.roomGuid;

        public bool LocationEqual(Player player) =>
            LocationEqual(player.worldPackGuid, player.worldGuid, player.roomGuid);
    }
}
