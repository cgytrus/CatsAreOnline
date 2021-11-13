namespace CatsAreOnline.SyncedObjects;

public class CompanionSyncedObjectState : SyncedObjectState {
    public override void Update() {
        if(!MultiplayerPlugin.capturedData.companionTransform) return;
        position = MultiplayerPlugin.capturedData.companionTransform!.position;
        scale = MultiplayerPlugin.capturedData.companionTransform.localScale.x;
        color = MultiplayerPlugin.capturedData.companionColor;
        rotation = MultiplayerPlugin.capturedData.companionTransform.eulerAngles.z;
    }
}
