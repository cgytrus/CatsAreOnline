using UnityEngine;
using UnityEngine.UI;

namespace CatsAreOnline {
    public class Player : MonoBehaviour {
        public string username;
        public string displayName;
        public Text nameTag;
        public SpriteRenderer renderer;
        public PlayerState state { get; } = new PlayerState();
        public bool restoreFollowPlayerHead { get; set; }

        public Transform restoreFollowTarget { get; set; }

        public void SetPosition(Vector2 position) {
            state.position = position;
            transform.position = position;
        }

        public void SetRoom(string room, string currentClientRoom) {
            bool sameRoom = currentClientRoom == room && !string.IsNullOrEmpty(currentClientRoom);
            bool own = !Client.displayOwnCat && username == Client.username;
            state.room = room;
            gameObject.SetActive(!own && sameRoom);
            nameTag.gameObject.SetActive(sameRoom);
            if(sameRoom || FollowPlayer.customFollowTarget != transform) return;
            FollowPlayer.followPlayerHead = restoreFollowPlayerHead;
            FollowPlayer.customFollowTarget = restoreFollowTarget;
            Client.spectating = null;
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
            renderer.sprite = ice ? Client.iceSprite : Client.catSprite;
            transform.localScale = ice ? new Vector3(3.5f, 3.5f, 1f) : Vector3.one * state.scale;
            if(!ice) transform.eulerAngles = Vector3.zero;
        }

        public void SetIceRotation(float iceRotation) {
            if(!state.ice) return;

            state.iceRotation = iceRotation;
            Transform transform = this.transform;
            Vector3 currentRot = transform.eulerAngles;
            currentRot.z = iceRotation;
            transform.eulerAngles = currentRot;
        }
    }
}
