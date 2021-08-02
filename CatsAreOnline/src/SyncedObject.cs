using System;
using System.Diagnostics;

using CatsAreOnline.Shared;
using CatsAreOnline.Shared.StateTypes;

using Lidgren.Network;

using UnityEngine;
using UnityEngine.UI;

namespace CatsAreOnline {
    public abstract class SyncedObject : MonoBehaviour {
        public readonly struct InterpolationSettings {
            public enum InterpolationMode { Lerp, LerpUnclamped, Velocity }
            
            public InterpolationMode mode { get; }
            public double time { get; }
            public int packetsToAverage { get; }
            public double maxTime { get; }

            public InterpolationSettings(InterpolationMode mode, double time, int packetsToAverage, double maxTime) {
                this.mode = mode;
                this.time = time;
                this.packetsToAverage = packetsToAverage;
                this.maxTime = maxTime;
            }
        }
        
        public Guid id { get; private set; }
        public Player owner { get; private set; }
        public Text nameTag { get; set; }
        public SpriteRenderer renderer { get; set; }
        public Rigidbody2D rigidbody { get; set; }
        public abstract SyncedObjectState state { get; }
        
        public bool restoreFollowPlayerHead { get; set; }
        public Transform restoreFollowTarget { get; set; }

        public static InterpolationSettings interpolationSettings { get; set; }

        private double _setPositionTime;
        private double _toAverage;
        private int _toAverageCount;
        private Stopwatch _interpolateStopwatch;
        private Vector3 _fromPosition;

        private void Awake() => _setPositionTime = Time.fixedDeltaTime;

        private void FixedUpdate() {
            switch(interpolationSettings.mode) {
                case InterpolationSettings.InterpolationMode.Lerp:
                    rigidbody.MovePosition(Vector3.Lerp(_fromPosition, state.position,
                        (float)(_interpolateStopwatch.Elapsed.TotalSeconds /
                                (_setPositionTime * interpolationSettings.time))));
                    break;
                case InterpolationSettings.InterpolationMode.LerpUnclamped:
                    rigidbody.MovePosition(Vector3.LerpUnclamped(_fromPosition, state.position,
                        (float)(_interpolateStopwatch.Elapsed.TotalSeconds /
                                (_setPositionTime * interpolationSettings.time))));
                    break;
                case InterpolationSettings.InterpolationMode.Velocity:
                    rigidbody.velocity = (state.position - (Vector2)_fromPosition) /
                                         (float)(_setPositionTime * interpolationSettings.time);
                    break;
            }
        }

        public virtual void SetPosition(Vector2 position) {
            state.position = position;
            _fromPosition = transform.position;
            _toAverage += Math.Min(_interpolateStopwatch?.Elapsed.TotalSeconds ?? Time.fixedDeltaTime,
                Time.fixedDeltaTime * interpolationSettings.maxTime);
            _toAverageCount++;
            if(_toAverageCount >= interpolationSettings.packetsToAverage) {
                _setPositionTime = _toAverage / _toAverageCount;
                _toAverage = 0d;
                _toAverageCount = 0;
            }
            if(_interpolateStopwatch == null) _interpolateStopwatch = Stopwatch.StartNew();
            else _interpolateStopwatch.Restart();
        }

        /*public virtual void SetRoom(string room, string currentClientRoom) {
            bool sameRoom = currentClientRoom == room && !string.IsNullOrEmpty(currentClientRoom);
            bool own = username == state.client.username;
            state.room = room;
            gameObject.SetActive(sameRoom);
            nameTag.gameObject.SetActive(sameRoom);
            renderer.enabled = !own || state.client.displayOwnCat;
            collider.enabled = username != state.client.username && state.client.playerCollisions;
            
            if(sameRoom || FollowPlayer.customFollowTarget != transform) return;
            FollowPlayer.followPlayerHead = restoreFollowPlayerHead;
            FollowPlayer.customFollowTarget = restoreFollowTarget;
            state.client.spectating = null;
            Chat.Chat.AddMessage($"Stopped spectating <b>{username}</b> (room changed)");
        }*/

        public virtual void SetColor(Color color) {
            state.color = color;
            renderer.color = color;
        }

        public virtual void SetScale(float scale) {
            state.scale = scale;
            transform.localScale = Vector3.one * scale;
        }

        public virtual void SetRotation(float rotation) {
            state.rotation = rotation;
            rigidbody.MoveRotation(rotation);
        }

        public virtual void UpdateRoom() {
            bool sameRoom = state.client.ownPlayer.room == owner.room &&
                            !string.IsNullOrEmpty(state.client.ownPlayer.room);
            bool own = owner.username == state.client.ownPlayer.username;
            gameObject.SetActive(sameRoom);
            nameTag.gameObject.SetActive(sameRoom);
            renderer.enabled = !own || state.client.displayOwnCat;
            
            if(sameRoom || FollowPlayer.customFollowTarget != transform) return;
            FollowPlayer.followPlayerHead = restoreFollowPlayerHead;
            FollowPlayer.customFollowTarget = restoreFollowTarget;
            state.client.spectating = null;
            Chat.Chat.AddMessage($"Stopped spectating <b>{owner.username}</b> (room changed)");
        }

        public void ReadChangedState(NetBuffer message, byte stateTypeByte) {
            SyncedObjectStateType stateType;
            try {
                stateType = (SyncedObjectStateType)stateTypeByte;
            }
            catch(Exception) {
                ReadCustomChangedState(message, stateTypeByte);
                return;
            }
            
            switch(stateType) {
                case SyncedObjectStateType.Position:
                    SetPosition(message.ReadVector2());
                    break;
                case SyncedObjectStateType.Color:
                    SetColor(message.ReadColor());
                    break;
                case SyncedObjectStateType.Scale:
                    SetScale(message.ReadFloat());
                    break;
                case SyncedObjectStateType.Rotation:
                    SetRotation(message.ReadFloat());
                    break;
            }
        }
        
        protected virtual void ReadCustomChangedState(NetBuffer message, byte stateTypeByte) { }

        public static SyncedObject Create(Client client, SyncedObjectType type, Guid id, Player owner,
            NetBuffer message) {
            switch(type) {
                case SyncedObjectType.Cat:
                    Vector2 position = message.ReadVector2();
                    Color color = message.ReadColor();
                    float scale = message.ReadFloat();
                    float rotation = message.ReadFloat();
                    bool ice = message.ReadBoolean();
                    
                    GameObject obj = new GameObject($"OnlinePlayer_{owner.username}") { layer = 0 };
                    DontDestroyOnLoad(obj);

                    SpriteRenderer renderer = obj.AddComponent<SpriteRenderer>();
                    renderer.sortingOrder = -50;

                    Rigidbody2D rigidbody = obj.AddComponent<Rigidbody2D>();
                    rigidbody.bodyType = RigidbodyType2D.Kinematic;
                    rigidbody.interpolation = RigidbodyInterpolation2D.Extrapolate;
                    rigidbody.useFullKinematicContacts = true;

                    CircleCollider2D catCollider = obj.AddComponent<CircleCollider2D>();
                    catCollider.radius = 0.4f;
                    
                    BoxCollider2D iceCollider = obj.AddComponent<BoxCollider2D>();
            
                    CatSyncedObject cat = obj.AddComponent<CatSyncedObject>();
                    cat.state.client = client;
                    cat.id = id;
                    cat.owner = owner;
                    cat.nameTag = CreatePlayerNameTag(owner.username, owner.displayName, client.nameTags, client.nameTagFont);
                    cat.renderer = renderer;
                    cat.rigidbody = rigidbody;
                    cat.catCollider = catCollider;
                    cat.iceCollider = iceCollider;
            
                    cat.SetPosition(position);
                    cat.SetColor(color);
                    cat.SetScale(scale);
                    cat.SetRotation(rotation);
                    cat.SetIce(ice);
                    
                    cat.UpdateRoom();
            
                    return cat;
            }

            return null;
        }
        
        private static Text CreatePlayerNameTag(string username, string displayName, Transform parent, Font font) {
            GameObject nameTag = new GameObject($"OnlinePlayerNameTag_{username}") {
                layer = LayerMask.NameToLayer("UI")
            };
            DontDestroyOnLoad(nameTag);

            RectTransform nameTagTransform = nameTag.AddComponent<RectTransform>();
            nameTagTransform.SetParent(parent);
            nameTagTransform.sizeDelta = new Vector2(200f, 30f);
            nameTagTransform.pivot = new Vector2(0.5f, 0f);
            nameTagTransform.localScale = Vector3.one;
            
            Text nameTagText = nameTag.AddComponent<Text>();
            nameTagText.font = font;
            nameTagText.fontSize = 28;
            nameTagText.alignment = TextAnchor.LowerCenter;
            nameTagText.horizontalOverflow = HorizontalWrapMode.Overflow;
            nameTagText.verticalOverflow = VerticalWrapMode.Overflow;
            nameTagText.supportRichText = true;
            nameTagText.text = displayName;

            return nameTagText;
        }

        public void Remove() {
            Destroy(nameTag.gameObject);
            Destroy(gameObject);
        }
    }
}
