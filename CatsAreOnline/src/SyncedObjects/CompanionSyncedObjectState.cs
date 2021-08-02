using Cat;

using UnityEngine;

namespace CatsAreOnline.SyncedObjects {
    public class CompanionSyncedObjectState : SyncedObjectState {
        public static Companion companion {
            get => _companion;
            set {
                _companion = value;
                if(value == null) return;
                companionRenderer = value.transform.Find("Companion Sprite").GetComponent<SpriteRenderer>();
                CompanionSyncedObject.sprite = companionRenderer.sprite;
            }
        }

        public static SpriteRenderer companionRenderer { get; set; }
        
        private static Companion _companion;

        public override void Update() {
            Transform transform = companion.transform;
            position = transform.position;
            scale = transform.localScale.x;
            color = companionRenderer.color;
            rotation = transform.eulerAngles.z;
        }
    }
}
