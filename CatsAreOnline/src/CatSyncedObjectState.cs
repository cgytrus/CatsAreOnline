using System.Diagnostics.CodeAnalysis;

using CatsAreOnline.Shared.StateTypes;

using Lidgren.Network;

namespace CatsAreOnline {
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
