using System;

using Lidgren.Network;

namespace CatsAreOnline {
    public class Player {
        public string username { get; private set; }
        public string displayName { get; private set; }
        public string room { get; set; }
        public Guid controlling { get; set; }

        public Player(string username, string displayName, string room, Guid controlling) {
            this.username = username;
            this.displayName = displayName;
            this.room = room;
            this.controlling = controlling;
        }

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
