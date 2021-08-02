using CatsAreOnline.Shared;

namespace CatsAreOnlineServer {
    public class CompanionSyncedObject : SyncedObject {
        public override SyncedObjectType enumType => SyncedObjectType.Companion;
    }
}
