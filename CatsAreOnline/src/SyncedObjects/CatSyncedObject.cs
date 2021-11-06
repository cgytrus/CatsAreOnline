using System.Collections.Generic;

using Lidgren.Network;

using UnityEngine;

namespace CatsAreOnline.SyncedObjects {
    public class CatSyncedObject : SyncedObject {
        public CircleCollider2D catCollider { get; set; }
        public BoxCollider2D iceCollider { get; set; }
        protected override SyncedObjectState state { get; } = new CatSyncedObjectState();

        private readonly List<CatSyncedObjectStateDelta> _pendingDeltas = new(8);

        protected override void Interpolate(int index, int removeCount, float t) {
            CatSyncedObjectStateDelta min = _pendingDeltas[index];
            CatSyncedObjectStateDelta max = _pendingDeltas[index + 1];
            CatSyncedObjectStateDelta latest = _pendingDeltas[_pendingDeltas.Count - 1];

            SetPosition(latest.position, Vector2.LerpUnclamped(min.position, max.position, t));
            if(max.color != state.color) SetColor(max.color);
            if(max.scale != state.scale) SetScale(max.scale);
            SetRotation(latest.rotation, Mathf.LerpUnclamped(min.rotation, max.rotation, t));
            if(max.ice != ((CatSyncedObjectState)state).ice) SetIce(max.ice);

            if(removeCount > 0) _pendingDeltas.RemoveRange(0, removeCount);
        }

        protected override void SetRotation(float rotation, float interpolatedRotation) {
            if(!((CatSyncedObjectState)state).ice) return;
            base.SetRotation(rotation, interpolatedRotation);
        }

        public void SetIce(bool ice) {
            ((CatSyncedObjectState)state).ice = ice;
            renderer.sprite = ice ? MultiplayerPlugin.capturedData.iceSprite : MultiplayerPlugin.capturedData.catSprite;
            UpdateColliders();
            if(!ice) transform.eulerAngles = Vector3.zero;
        }

        public override void UpdateLocation() {
            base.UpdateLocation();
            UpdateColliders();
        }

        protected override void ReadDelta(NetBuffer buffer) {
            CatSyncedObjectStateDelta useDelta = _pendingDeltas.Count > 0 ? _pendingDeltas[_pendingDeltas.Count - 1] :
                new CatSyncedObjectStateDelta((CatSyncedObjectState)state);
            _pendingDeltas.Add(new CatSyncedObjectStateDelta(useDelta, buffer));
        }

        protected override void RemovePreLatestDelta() => _pendingDeltas.RemoveAt(_pendingDeltas.Count - 2);

        private void UpdateColliders() {
            bool enableAnyCollider = (owner.username != state.client.ownPlayer.username) && state.client.playerCollisions;
            catCollider.enabled = enableAnyCollider && !((CatSyncedObjectState)state).ice;
            iceCollider.enabled = enableAnyCollider && ((CatSyncedObjectState)state).ice;
        }
    }
}
