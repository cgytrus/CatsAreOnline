using System;

using Lidgren.Network;

namespace CatsAreOnlineServer {
    public static class NetBufferExtensions {
        public static void Write(this NetBuffer buffer, Player player) {
            buffer.Write(player.username);
            buffer.Write(player.displayName);
            buffer.Write(player.posX);
            buffer.Write(player.posY);
            buffer.Write(player.room);
            buffer.Write(player.colorR);
            buffer.Write(player.colorG);
            buffer.Write(player.colorB);
            buffer.Write(player.colorA);
            buffer.Write(player.scale);
            buffer.Write(player.ice);
            buffer.Write(player.iceRotation);
        }
    }
}
