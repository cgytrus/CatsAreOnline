using System;

using Lidgren.Network;

namespace CatsAreOnlineServer {
    public class Player {
        public NetConnection connection { get; init; }
        public Guid id { get; init; }
        public string username { get; init; }
        public string displayName { get; init; }
        public string room { get; set; }
        public Guid controlling { get; set; }

        public void Write(NetBuffer message) {
            message.Write(username);
            message.Write(displayName);
            message.Write(room);
            message.Write(controlling.ToString());
        }

        public bool RoomEqual(string room) => room == this.room;
        public bool RoomEqual(Player player) => RoomEqual(player.room);
    }
}
