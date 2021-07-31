using System;
using System.Net;

using Lidgren.Network;

namespace CatsAreOnlineServer {
    public class Player {
        public enum StateType : byte {
            Position,
            Room,
            Color,
            Scale,
            Ice,
            IceRotation
        }
        
        public NetConnection connection { get; init; }
        public IPEndPoint ip => connection?.RemoteEndPoint;
        public Guid id { get; init; }
        public string username { get; init; }
        public string displayName { get; init; }
        public float posX { get; set; }
        public float posY { get; set; }
        public string room { get; set; }
        public float colorR { get; set; }
        public float colorG { get; set; }
        public float colorB { get; set; }
        public float colorA { get; set; }
        public float scale { get; set; }
        public bool ice { get; set; }
        public float iceRotation { get; set; }
    }
}
