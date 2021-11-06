using System.Collections.Generic;

using Lidgren.Network;

using UnityEngine;

namespace CatsAreOnline.SyncedObjects {
    public class CompanionSyncedObject : SyncedObject {
        protected override SyncedObjectState state { get; } = new CompanionSyncedObjectState();
        public BoxCollider2D collider { get; set; }

        private readonly List<CompanionSyncedObjectStateDelta> _pendingDeltas = new(8);

        protected override void Interpolate(int index, int removeCount, float t) {
            CompanionSyncedObjectStateDelta min = _pendingDeltas[index];
            CompanionSyncedObjectStateDelta max = _pendingDeltas[index + 1];
            CompanionSyncedObjectStateDelta latest = _pendingDeltas[_pendingDeltas.Count - 1];

            SetPosition(latest.position, Vector2.LerpUnclamped(min.position, max.position, t));
            if(max.color != state.color) SetColor(max.color);
            if(max.scale != state.scale) SetScale(max.scale);
            SetRotation(latest.rotation, Mathf.LerpUnclamped(min.rotation, max.rotation, t));

            if(removeCount > 0) _pendingDeltas.RemoveRange(0, removeCount);
        }

        public override void UpdateLocation() {
            base.UpdateLocation();
            collider.enabled = (owner.username != state.client.ownPlayer.username) && state.client.playerCollisions;
            renderer.sprite = MultiplayerPlugin.capturedData.companionSprite;
        }

        protected override void ReadDelta(NetBuffer buffer) {
            CompanionSyncedObjectStateDelta useDelta = _pendingDeltas.Count > 0 ? _pendingDeltas[_pendingDeltas.Count - 1] :
                new CompanionSyncedObjectStateDelta((CompanionSyncedObjectState)state);
            _pendingDeltas.Add(new CompanionSyncedObjectStateDelta(useDelta, buffer));
        }

        protected override void RemovePreLatestDelta() => _pendingDeltas.RemoveAt(_pendingDeltas.Count - 2);
    }
}
