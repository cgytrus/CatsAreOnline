using System;
using System.Collections.Generic;

using CatsAreOnline.Shared;

using Lidgren.Network;

using UnityEngine;
using UnityEngine.UI;

namespace CatsAreOnline.SyncedObjects;

public abstract class SyncedObject : MonoBehaviour {
    public class InterpolationSettings {
        public float delay { get; set; }
        public float extrapolationTime { get; set; }
        public float maxTimeDelta { get; set; }
        public float maxTimeDeltaAccelerationThreshold { get; set; }
    }

    public static bool debugMode { get; set; }

    public Guid id { get; protected set; }
    public Player owner { get; protected set; } = null!;
    public Text? nameTag { get; set; }
    public SpriteRenderer? renderer { get; set; }
    public Rigidbody2D? rigidbody { get; set; }
    protected abstract SyncedObjectState state { get; }

    public static InterpolationSettings interpolationSettings { get; } = new();

    private readonly Vector2 _nameTagOffset = Vector2.up;

    private readonly List<float> _pendingTimes = new(8);
    private float _interpolationTime = -1f;
    private bool _pauseInterpolation;

    // https://developer.valvesoftware.com/wiki/Source_Multiplayer_Networking#Entity_interpolation
    private void Update() {
        float actualTime = (float)NetTime.Now - interpolationSettings.delay;
        if(!_pauseInterpolation) {
            float delta = actualTime - _interpolationTime;
            if(delta > interpolationSettings.maxTimeDelta && _interpolationTime >= 0f) {
                float accelerationThreshold = interpolationSettings.maxTimeDeltaAccelerationThreshold;
                float deltaAcceleration = Mathf.Max(delta - accelerationThreshold + 1f, 1f);
                delta = interpolationSettings.maxTimeDelta * deltaAcceleration;
                _interpolationTime += delta;
            }
            else _interpolationTime = actualTime;
        }

        float time = _pauseInterpolation ? actualTime : _interpolationTime;

        int index = Math.Min(GetPendingTimeIndexAt(time), _pendingTimes.Count - 2);
        if(index < 0) return;

        float min = _pendingTimes[index];
        float max = _pendingTimes[index + 1];

        float duration = max - min;
        float t = duration == 0f ? 1f : (time - min) / duration;
        if(t > 1f) _pauseInterpolation = true;
        // extrapolate only for `extrapolationTime`
        if(time - max > interpolationSettings.extrapolationTime)
            t = (duration + interpolationSettings.extrapolationTime) / duration;

        Interpolate(index, t);
        RemoveOldStates(index - 3);

        if(debugMode) DrawInterpolationDebug(index, index + 1);
    }

    private int GetPendingTimeIndexAt(float time) {
        int index = _pendingTimes.BinarySearch(time);
        if(index < 0) index = ~index;
        return index - 1;
    }

    protected abstract void Interpolate(int index, float t);

    protected virtual void DrawInterpolationDebug(int startCurrent, int endCurrent) { }

    protected virtual void RemoveOldStates(int removeCount) {
        if(removeCount <= 0) return;
        _pendingTimes.RemoveRange(0, removeCount);
    }

    protected virtual void SetPosition(Vector2 position, Vector2 interpolatedPosition) {
        state.position = interpolatedPosition;
        transform.position = position;
        if(renderer) renderer!.transform.position = interpolatedPosition;
    }

    protected virtual void SetColor(Color color) {
        state.color = color;
        if(renderer) renderer!.color = color;
    }

    protected virtual void SetScale(float scale) {
        state.scale = scale;
        transform.localScale = Vector3.one * scale;
        if(renderer) renderer!.transform.localScale = Vector3.one * scale;
    }

    protected virtual void SetRotation(float rotation, float interpolatedRotation) {
        state.rotation = interpolatedRotation;
        if(rigidbody) rigidbody!.MoveRotation(rotation);
        if(!renderer) return;
        Transform transform = renderer!.transform;
        Vector3 currentRot = transform.eulerAngles;
        currentRot.z = interpolatedRotation;
        transform.eulerAngles = currentRot;
    }

    public virtual void UpdateLocation() {
        bool sameRoom = state.client.ownPlayer.LocationEqual(owner);
        bool own = owner.username == state.client.ownPlayer.username;
        gameObject.SetActive(sameRoom);
        if(nameTag) nameTag!.gameObject.SetActive(sameRoom);
        if(!renderer) return;
        renderer!.gameObject.SetActive(sameRoom);
        renderer.enabled = !own || state.client.displayOwnCat;
    }

    public void ReadStateDelta(NetIncomingMessage message) {
        float time = (float)message.ReadTime(true);

        int index = GetPendingTimeIndexAt(time) + 1;
        _pendingTimes.Insert(index, time);
        AddDelta(index, message);

        if(!_pauseInterpolation) return;
        _pauseInterpolation = false;
        time = _interpolationTime;

        index = GetPendingTimeIndexAt(time) + 1;
        _pendingTimes.Insert(index, time);
        AddCurrentStateAsDelta(index);
    }

    protected abstract void AddDelta(int index, NetBuffer buffer);
    protected abstract void AddCurrentStateAsDelta(int index);

    public static SyncedObject? Create(Client client, SyncedObjectType type, Guid id, Player owner,
        NetBuffer message) {
        Vector2 position = message.ReadVector2();
        Color color = message.ReadColor();
        float scale = message.ReadFloat();
        float rotation = message.ReadFloat();

        GameObject obj = new($"OnlinePlayer_{owner.username}_{type.ToString()}") { layer = 0 };
        DontDestroyOnLoad(obj);

        GameObject rendererObject = new($"{obj.name}_Renderer") { layer = 0 };
        DontDestroyOnLoad(rendererObject);
        SpriteRenderer renderer = rendererObject.AddComponent<SpriteRenderer>();
        renderer.sortingOrder = -50;

        Rigidbody2D rigidbody = obj.AddComponent<Rigidbody2D>();
        rigidbody.bodyType = RigidbodyType2D.Kinematic;
        rigidbody.interpolation = RigidbodyInterpolation2D.Extrapolate;
        rigidbody.useFullKinematicContacts = true;

        return type switch {
            SyncedObjectType.Cat => CatSyncedObject.Create(client, id, owner, message, obj, renderer, rigidbody,
                position, color, scale, rotation),
            SyncedObjectType.Companion => CompanionSyncedObject.Create(client, id, owner, obj, renderer, rigidbody,
                position, color, scale, rotation),
            _ => null
        };
    }

    protected static Text CreatePlayerNameTag(string username, string displayName, Transform parent, Font font) {
        GameObject nameTag = new($"OnlinePlayerNameTag_{username}") {
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

    public virtual void UpdateNameTagPosition(Camera camera) {
        Vector3 position = renderer ? renderer!.transform.position : transform.position;
        SetNameTagPosition(camera, position);
    }

    protected void SetNameTagPosition(Camera camera, Vector2 position) {
        if(!this.nameTag) return;
        Text nameTag = this.nameTag!;

        float horTextExtent = nameTag.preferredWidth * 0.5f;
        float vertTextExtent = nameTag.preferredHeight;

        Vector3 camPos = camera.transform.position;
        float vertExtent = camera.orthographicSize;
        float horExtent = vertExtent * Screen.width / Screen.height;
        float minX = camPos.x - horExtent + horTextExtent + 0.5f;
        float maxX = camPos.x + horExtent - horTextExtent - 0.5f;
        float minY = camPos.y - vertExtent + 0.5f;
        float maxY = camPos.y + vertExtent - vertTextExtent - 0.5f;

        float scale = state.scale;
        nameTag.rectTransform.anchoredPosition =
            new Vector2(Mathf.Clamp(position.x + _nameTagOffset.x * scale, minX, maxX),
                Mathf.Clamp(position.y + _nameTagOffset.y * scale, minY, maxY));
    }

    public void Remove() {
        if(renderer) Destroy(renderer!.gameObject);
        if(nameTag) Destroy(nameTag!.gameObject);
        Destroy(gameObject);
    }
}
