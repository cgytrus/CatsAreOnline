using System;
using System.Collections.Generic;

using CalApi.API;

using Lidgren.Network;

using UnityEngine;

namespace CatsAreOnline.SyncedObjects;

public class CatSyncedObject : SyncedObject {
    public CircleCollider2D? catCollider { get; set; }
    public BoxCollider2D? iceCollider { get; set; }
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
        if(renderer)
            renderer!.sprite = ice ? MultiplayerPlugin.capturedData.iceSprite :
                MultiplayerPlugin.capturedData.catSprite;
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
        bool enableAnyCollider = owner.username != state.client.ownPlayer.username && state.client.interactions;
        if(catCollider) catCollider!.enabled = enableAnyCollider && !((CatSyncedObjectState)state).ice;
        if(iceCollider) iceCollider!.enabled = enableAnyCollider && ((CatSyncedObjectState)state).ice;
    }

    public static CatSyncedObject Create(Client client, Guid id, Player owner, NetBuffer message, GameObject obj,
        SpriteRenderer renderer, Rigidbody2D rigidbody,
        Vector2 position, Color color, float scale, float rotation) {
        bool ice = message.ReadBoolean();

        CircleCollider2D catCollider = obj.AddComponent<CircleCollider2D>();
        catCollider.radius = 0.4f;

        BoxCollider2D iceCollider = obj.AddComponent<BoxCollider2D>();
        iceCollider.size = Vector2.one;

        CatSyncedObject cat = obj.AddComponent<CatSyncedObject>();
        cat.state.client = client;
        cat.id = id;
        cat.owner = owner;
        if(client.nameTags && UI.font)
            cat.nameTag = CreatePlayerNameTag(owner.username!, owner.displayName ?? owner.username!, client.nameTags!,
                UI.font!);
        cat.renderer = renderer;
        cat.rigidbody = rigidbody;
        cat.catCollider = catCollider;
        cat.iceCollider = iceCollider;

        cat.SetPosition(position, position);
        cat.SetColor(color);
        cat.SetScale(scale);
        cat.SetRotation(rotation, rotation);
        cat.SetIce(ice);

        cat.UpdateLocation();
        return cat;
    }

    public override void UpdateNameTagPosition(Camera camera) {
        if(!state.client.attachOwnNameTag || owner.username != state.client.ownPlayer.username) {
            base.UpdateNameTagPosition(camera);
            return;
        }
        Vector2 position = state.client.catState.position;
        SetNameTagPosition(camera, position);
    }
}
