using System.Diagnostics.CodeAnalysis;

using Cat;

using CatsAreOnline.Shared;
using CatsAreOnline.Shared.StateTypes;

using Lidgren.Network;

using PipeSystem;

namespace CatsAreOnline.SyncedObjects {
    public class CatSyncedObjectState : SyncedObjectState {
        public override float rotation {
            get => base.rotation;
            set {
                if(!_ice) return;
                base.rotation = value;
            }
        }

        public bool ice {
            get => _ice;
            set {
                if(_ice != value) {
                    _iceChanged = true;
                    anythingChanged = true;
                }
                _ice = value;
            }
        }

        private bool _ice;
        private bool _iceChanged;

        public override void Update() {
            if(Pipe.catInPipe) {
                scale = State.Liquid.GetScale();
                color = MultiplayerPlugin.capturedData.catPipeColor;
            }
            else {
                scale = MultiplayerPlugin.capturedData.catScale;
                color = MultiplayerPlugin.capturedData.catColor;
            }
            movementCatState = MultiplayerPlugin.capturedData.catState;
            position = client.currentCatPosition;
            if(!MultiplayerPlugin.capturedData.catControls) return;
            ice = MultiplayerPlugin.capturedData.inIce;
            if(!ice) return;
            color = MultiplayerPlugin.capturedData.iceColor;
            scale = MultiplayerPlugin.capturedData.iceBlock.Size.y * 3.5f;
            rotation = MultiplayerPlugin.capturedData.iceRotation;
        }

        public override void Write(NetBuffer message) {
            base.Write(message);
            message.Write(ice);
        }

        [SuppressMessage("ReSharper", "InvertIf")]
        public override void WriteDeltaToMessage(NetOutgoingMessage message) {
            base.WriteDeltaToMessage(message);
            if(_iceChanged) {
                message.Write((byte)CatStateType.Ice);
                message.Write(ice);
                _iceChanged = false;
                anythingChanged = false;
                deliveryMethod = DeliveryMethods.LessReliable;
            }
        }
    }
}
