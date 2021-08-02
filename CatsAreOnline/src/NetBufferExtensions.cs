﻿using Lidgren.Network;

using UnityEngine;

namespace CatsAreOnline {
    public static class NetBufferExtensions {
        public static Vector2 ReadVector2(this NetBuffer buffer) => new Vector2(buffer.ReadFloat(), buffer.ReadFloat());
        public static Color ReadColor(this NetBuffer buffer) =>
            new Color(buffer.ReadFloat(), buffer.ReadFloat(), buffer.ReadFloat(), buffer.ReadFloat());
        
        public static void Write(this NetBuffer buffer, Vector2 source) {
            buffer.Write(source.x);
            buffer.Write(source.y);
        }
        
        public static void Write(this NetBuffer buffer, Color source) {
            buffer.Write(source.r);
            buffer.Write(source.g);
            buffer.Write(source.b);
            buffer.Write(source.a);
        }
        
        public static void Write(this NetBuffer buffer, CatSyncedObjectState state) {
            buffer.Write(state.position);
            buffer.Write(state.color);
            buffer.Write(state.scale);
            buffer.Write(state.rotation);
            buffer.Write(state.ice);
        }
    }
}
