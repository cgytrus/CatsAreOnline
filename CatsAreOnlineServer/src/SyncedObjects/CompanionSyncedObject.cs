using CatsAreOnline.Shared;

namespace CatsAreOnlineServer.SyncedObjects;

public class CompanionSyncedObject : SyncedObject {
    public override SyncedObjectType enumType => SyncedObjectType.Companion;
}