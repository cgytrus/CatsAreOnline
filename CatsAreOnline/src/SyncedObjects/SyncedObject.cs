using System;
using System.Diagnostics;

using CatsAreOnline.Shared;
using CatsAreOnline.Shared.StateTypes;

using Lidgren.Network;

using UnityEngine;
using UnityEngine.UI;

namespace CatsAreOnline.SyncedObjects {
    public abstract class SyncedObject : MonoBehaviour {
        public readonly struct InterpolationSettings {
            public enum InterpolationMode { Lerp, LerpUnclamped }
            
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
                    renderer.transform.position = Vector3.Lerp(_fromPosition, state.position,
                        (float)(_interpolateStopwatch.Elapsed.TotalSeconds /
                                (_setPositionTime * interpolationSettings.time)));
                    break;
                case InterpolationSettings.InterpolationMode.LerpUnclamped:
                    renderer.transform.position = Vector3.LerpUnclamped(_fromPosition, state.position,
                        (float)(_interpolateStopwatch.Elapsed.TotalSeconds /
                                (_setPositionTime * interpolationSettings.time)));
                    break;
            }
        }

        public virtual void SetPosition(Vector2 position) {
            state.position = position;
            _fromPosition = renderer.transform.position;
            transform.position = position;
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

        public virtual void SetColor(Color color) {
            state.color = color;
            renderer.color = color;
        }

        public virtual void SetScale(float scale) {
            state.scale = scale;
            transform.localScale = Vector3.one * scale;
            renderer.transform.localScale = Vector3.one * scale;
        }

        public virtual void SetRotation(float rotation) {
            state.rotation = rotation;
            rigidbody.MoveRotation(rotation);
            Transform transform = renderer.transform;
            Vector3 currentRot = transform.eulerAngles;
            currentRot.z = rotation;
            transform.eulerAngles = currentRot;
        }

        public virtual void UpdateRoom() {
            bool sameRoom = state.client.ownPlayer.room == owner.room &&
                            !string.IsNullOrEmpty(state.client.ownPlayer.room);
            bool own = owner.username == state.client.ownPlayer.username;
            gameObject.SetActive(sameRoom);
            nameTag.gameObject.SetActive(sameRoom);
            renderer.gameObject.SetActive(sameRoom);
            renderer.enabled = !own || state.client.displayOwnCat;
        }

        public void ReadChangedState(NetBuffer message, byte stateTypeByte) {
            SyncedObjectStateType stateType = (SyncedObjectStateType)stateTypeByte;
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
                default:
                    ReadCustomChangedState(message, stateTypeByte);
                    break;
            }
        }
        
        protected virtual void ReadCustomChangedState(NetBuffer message, byte stateTypeByte) { }

        public static SyncedObject Create(Client client, SyncedObjectType type, Guid id, Player owner,
            NetBuffer message) {
            Vector2 position = message.ReadVector2();
            Color color = message.ReadColor();
            float scale = message.ReadFloat();
            float rotation = message.ReadFloat();
                    
            GameObject obj = new GameObject($"OnlinePlayer_{owner.username}_{type.ToString()}") { layer = 0 };
            DontDestroyOnLoad(obj);

            GameObject rendererObject = new GameObject($"{obj.name}_Renderer") { layer = 0 };
            DontDestroyOnLoad(rendererObject);
            SpriteRenderer renderer = rendererObject.AddComponent<SpriteRenderer>();
            renderer.sortingOrder = -50;

            Rigidbody2D rigidbody = obj.AddComponent<Rigidbody2D>();
            rigidbody.bodyType = RigidbodyType2D.Kinematic;
            rigidbody.interpolation = RigidbodyInterpolation2D.Extrapolate;
            rigidbody.useFullKinematicContacts = true;
            
            switch(type) {
                case SyncedObjectType.Cat:
                    bool ice = message.ReadBoolean();

                    CircleCollider2D catCollider = obj.AddComponent<CircleCollider2D>();
                    catCollider.radius = 0.4f;
                    
                    BoxCollider2D iceCollider = obj.AddComponent<BoxCollider2D>();
                    iceCollider.size = Vector2.one;
            
                    CatSyncedObject cat = obj.AddComponent<CatSyncedObject>();
                    cat.state.client = client;
                    cat.id = id;
                    cat.owner = owner;
                    cat.nameTag = CreatePlayerNameTag(owner.username, owner.displayName, client.nameTags,
                        client.nameTagFont);
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
                case SyncedObjectType.Companion:
                    BoxCollider2D companionCollider = obj.AddComponent<BoxCollider2D>();
                    companionCollider.size = Vector2.one;
            
                    CompanionSyncedObject companion = obj.AddComponent<CompanionSyncedObject>();
                    companion.state.client = client;
                    companion.id = id;
                    companion.owner = owner;
                    companion.nameTag = CreatePlayerNameTag(owner.username, owner.displayName, client.nameTags,
                        client.nameTagFont);
                    companion.renderer = renderer;
                    companion.rigidbody = rigidbody;
                    companion.collider = companionCollider;
            
                    companion.SetPosition(position);
                    companion.SetColor(color);
                    companion.SetScale(scale);
                    companion.SetRotation(rotation);
                    
                    companion.UpdateRoom();
            
                    return companion;
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
            Destroy(renderer.gameObject);
            Destroy(nameTag.gameObject);
            Destroy(gameObject);
        }
    }
}
