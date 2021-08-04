using UnityEngine;

namespace CatsAreOnline.SyncedObjects {
    public class CompanionSyncedObject : SyncedObject {
        protected override SyncedObjectState state { get; } = new CompanionSyncedObjectState();
        public BoxCollider2D collider { get; set; }

        public override void UpdateRoom() {
            base.UpdateRoom();
            collider.enabled = owner.username != state.client.ownPlayer.username && state.client.playerCollisions;
            renderer.sprite = CapturedData.companionSprite;
        }
    }
}
