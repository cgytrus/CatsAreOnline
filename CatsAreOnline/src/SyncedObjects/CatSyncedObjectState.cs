using System.Diagnostics.CodeAnalysis;

using Cat;

using CatsAreOnline.Patches;
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
            if(Pipe.catInPipe) scale = Client.GetScaleFromCatState(State.Liquid);
            else {
                scale = MultiplayerPlugin.catScale;
                color = MultiplayerPlugin.catColor;
            }
            movementCatState = MultiplayerPlugin.catState;
            position = client.currentCatPosition;
            if(!client.playerControls) return;
            ice = CurrentIceUpdates.currentIce;
            if(!ice) return;
            color = client.iceColor;
            scale = CurrentIceUpdates.currentIce.Size.y * 3.5f;
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
                deliveryMethod = Client.LessReliableDeliveryMethod;
            }
        }
    }
}
