using UnityEngine;
using UnityEngine.UI;

namespace CatsAreOnline {
    public class Player : MonoBehaviour {
        public string username { get; set; }
        public string displayName { get; set; }
        public Text nameTag { get; set; }
        public SpriteRenderer renderer { get; set; }
        public Rigidbody2D rigidbody { get; set; }
        public CircleCollider2D collider { get; set; }
        public PlayerState state { get; } = new PlayerState();
        
        public bool restoreFollowPlayerHead { get; set; }
        public Transform restoreFollowTarget { get; set; }
        public void SetPosition(Vector2 position) {
            state.position = position;
            if(isActiveAndEnabled) rigidbody.MovePosition(position);
            else transform.position = position;
        }

        public void SetRoom(string room, string currentClientRoom) {
            bool sameRoom = currentClientRoom == room && !string.IsNullOrEmpty(currentClientRoom);
            bool own = !state.client.displayOwnCat && username == state.client.username;
            state.room = room;
            gameObject.SetActive(!own && sameRoom);
            nameTag.gameObject.SetActive(sameRoom);
            collider.enabled = username != state.client.username && state.client.playerCollisions;
            if(sameRoom || FollowPlayer.customFollowTarget != transform) return;
            FollowPlayer.followPlayerHead = restoreFollowPlayerHead;
            FollowPlayer.customFollowTarget = restoreFollowTarget;
            state.client.spectating = null;
            Chat.Chat.AddMessage($"Stopped spectating <b>{username}</b> (room changed)");
        }

        public void SetColor(Color color) {
            state.color = color;
            renderer.color = color;
        }

        public void SetScale(float scale) {
            state.scale = scale;
            transform.localScale = Vector3.one * scale;
        }

        public void SetIce(bool ice) {
            state.ice = ice;
            renderer.sprite = ice ? state.client.iceSprite : state.client.catSprite;
            if(!ice) transform.eulerAngles = Vector3.zero;
        }

        public void SetIceRotation(float iceRotation) {
            if(!state.ice) return;

            state.iceRotation = iceRotation;
            if(isActiveAndEnabled) rigidbody.MoveRotation(iceRotation);
            else {
                Transform transform = this.transform;
                Vector3 currentRot = transform.eulerAngles;
                currentRot.z = iceRotation;
                transform.eulerAngles = currentRot;
            }
        }
    }
}
