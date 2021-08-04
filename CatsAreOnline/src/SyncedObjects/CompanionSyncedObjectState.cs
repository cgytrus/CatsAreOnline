namespace CatsAreOnline.SyncedObjects {
    public class CompanionSyncedObjectState : SyncedObjectState {
        public override void Update() {
            if(!CapturedData.companionTransform) return;
            position = CapturedData.companionTransform.position;
            scale = CapturedData.companionTransform.localScale.x;
            color = CapturedData.companionColor;
            rotation = CapturedData.companionTransform.eulerAngles.z;
        }
    }
}
