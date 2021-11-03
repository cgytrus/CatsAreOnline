using System;

using Lidgren.Network;

namespace CatsAreOnlineServer {
    public class Player {
        public NetConnection connection { get; init; }
        public float latestPing { get; set; }
        public string username { get; init; }
        public string displayName { get; init; }
        public string worldPackGuid { get; set; }
        public string worldGuid { get; set; }
        public string roomGuid { get; set; }
        public string worldPackName { get; set; }
        public string worldName { get; set; }
        public string roomName { get; set; }
        public Guid controlling { get; set; }

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

        public bool IsPlaying() => !string.IsNullOrEmpty(worldPackGuid) && !string.IsNullOrEmpty(worldGuid) &&
                                  !string.IsNullOrEmpty(roomGuid);

        public bool LocationEqual(string worldPackGuid, string worldGuid, string roomGuid) => IsPlaying() &&
            worldPackGuid == this.worldPackGuid && worldGuid == this.worldGuid && roomGuid == this.roomGuid;

        public bool LocationEqual(Player player) => LocationEqual(player.worldPackGuid, player.worldGuid, player.roomGuid);
    }
}
