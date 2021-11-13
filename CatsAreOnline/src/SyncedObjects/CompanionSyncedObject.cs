using System;
using System.Collections.Generic;

using CalApi.API;

using Lidgren.Network;

using UnityEngine;

namespace CatsAreOnline.SyncedObjects;

public class CompanionSyncedObject : SyncedObject {
    protected override SyncedObjectState state { get; } = new CompanionSyncedObjectState();
    public BoxCollider2D? collider { get; set; }

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
        if(collider)
            collider!.enabled = owner is not null && owner.username != state.client?.ownPlayer.username &&
                                (state.client?.playerCollisions ?? false);
        if(renderer) renderer!.sprite = MultiplayerPlugin.capturedData.companionSprite;
    }

    protected override void ReadDelta(NetBuffer buffer) {
        CompanionSyncedObjectStateDelta useDelta = _pendingDeltas.Count > 0 ? _pendingDeltas[_pendingDeltas.Count - 1] :
            new CompanionSyncedObjectStateDelta((CompanionSyncedObjectState)state);
        _pendingDeltas.Add(new CompanionSyncedObjectStateDelta(useDelta, buffer));
    }

    protected override void RemovePreLatestDelta() => _pendingDeltas.RemoveAt(_pendingDeltas.Count - 2);

    public static CompanionSyncedObject Create(Client client, Guid id, Player owner, GameObject obj,
        SpriteRenderer renderer, Rigidbody2D rigidbody,
        Vector2 position, Color color, float scale, float rotation) {
        BoxCollider2D companionCollider = obj.AddComponent<BoxCollider2D>();
        companionCollider.size = Vector2.one;

        CompanionSyncedObject companion = obj.AddComponent<CompanionSyncedObject>();
        companion.state.client = client;
        companion.id = id;
        companion.owner = owner;
        if(client.nameTags && UI.font)
            companion.nameTag = CreatePlayerNameTag(owner.username!, owner.displayName ?? owner.username!,
                client.nameTags!, UI.font!);
        companion.renderer = renderer;
        companion.rigidbody = rigidbody;
        companion.collider = companionCollider;

        companion.SetPosition(position, position);
        companion.SetColor(color);
        companion.SetScale(scale);
        companion.SetRotation(rotation, rotation);

        companion.UpdateLocation();

        return companion;
    }
}
