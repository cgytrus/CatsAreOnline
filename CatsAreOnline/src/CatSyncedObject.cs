using CatsAreOnline.Shared.StateTypes;

using Lidgren.Network;

using UnityEngine;

namespace CatsAreOnline {
    public class CatSyncedObject : SyncedObject {
        public CircleCollider2D catCollider { get; set; }
        public BoxCollider2D iceCollider { get; set; }
        public override SyncedObjectState state { get; } = new CatSyncedObjectState();

        public override void SetRotation(float rotation) {
            if(!((CatSyncedObjectState)state).ice) return;
            base.SetRotation(rotation);
        }

        public void SetIce(bool ice) {
            ((CatSyncedObjectState)state).ice = ice;
            renderer.sprite = ice ? state.client.iceSprite : state.client.catSprite;
            if(!ice) transform.eulerAngles = Vector3.zero;
        }

        public override void UpdateRoom() {
            base.UpdateRoom();
            bool enableAnyCollider = owner.username != state.client.ownPlayer.username && state.client.playerCollisions;
            catCollider.enabled = enableAnyCollider && !((CatSyncedObjectState)state).ice;
            iceCollider.enabled = enableAnyCollider && ((CatSyncedObjectState)state).ice;
        }

        protected override void ReadCustomChangedState(NetBuffer message, byte stateTypeByte) {
            CatStateType stateType = (CatStateType)stateTypeByte;
            switch(stateType) {
                case CatStateType.Ice:
                    SetIce(message.ReadBoolean());
                    break;
            }
        }
    }
}
