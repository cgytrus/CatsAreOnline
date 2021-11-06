using CatsAreOnline.Shared.StateTypes;

using Lidgren.Network;

using UnityEngine;

namespace CatsAreOnline.SyncedObjects {
    public readonly struct CompanionSyncedObjectStateDelta {
        public Vector2 position { get; }
        public Color color { get; }
        public float scale { get; }
        public float rotation { get; }

        // ReSharper disable once SuggestBaseTypeForParameterInConstructor
        public CompanionSyncedObjectStateDelta(CompanionSyncedObjectState original) {
            position = original.position;
            color = original.color;
            scale = original.scale;
            rotation = original.rotation;
        }

        public CompanionSyncedObjectStateDelta(CompanionSyncedObjectStateDelta original, NetBuffer buffer) {
            position = original.position;
            color = original.color;
            scale = original.scale;
            rotation = original.rotation;

            while(buffer.ReadByte(out byte stateTypeByte)) {
                SyncedObjectStateType stateType = (SyncedObjectStateType)stateTypeByte;
                switch(stateType) {
                    case SyncedObjectStateType.Position:
                        position = buffer.ReadVector2();
                        break;
                    case SyncedObjectStateType.Color:
                        color = buffer.ReadColor();
                        break;
                    case SyncedObjectStateType.Scale:
                        scale = buffer.ReadFloat();
                        break;
                    case SyncedObjectStateType.Rotation:
                        rotation = buffer.ReadFloat();
                        break;
                }
            }
        }
    }
}
