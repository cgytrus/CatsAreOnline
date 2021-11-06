using CatsAreOnline.Shared.StateTypes;

using Lidgren.Network;

using UnityEngine;

namespace CatsAreOnline.SyncedObjects {
    public readonly struct CatSyncedObjectStateDelta {
        public Vector2 position { get; }
        public Color color { get; }
        public float scale { get; }
        public float rotation { get; }
        public bool ice { get; }

        public CatSyncedObjectStateDelta(CatSyncedObjectState original) {
            position = original.position;
            color = original.color;
            scale = original.scale;
            rotation = original.rotation;
            ice = original.ice;
        }

        public CatSyncedObjectStateDelta(CatSyncedObjectStateDelta original, NetBuffer buffer) {
            position = original.position;
            color = original.color;
            scale = original.scale;
            rotation = original.rotation;
            ice = original.ice;

            while(buffer.ReadByte(out byte stateType)) {
                switch(stateType) {
                    case (byte)SyncedObjectStateType.Position:
                        position = buffer.ReadVector2();
                        break;
                    case (byte)SyncedObjectStateType.Color:
                        color = buffer.ReadColor();
                        break;
                    case (byte)SyncedObjectStateType.Scale:
                        scale = buffer.ReadFloat();
                        break;
                    case (byte)SyncedObjectStateType.Rotation:
                        rotation = buffer.ReadFloat();
                        break;
                    case (byte)CatStateType.Ice:
                        ice = buffer.ReadBoolean();
                        break;
                }
            }
        }
    }
}
