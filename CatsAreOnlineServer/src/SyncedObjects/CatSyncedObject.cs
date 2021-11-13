using CatsAreOnline.Shared;
using CatsAreOnline.Shared.StateTypes;

using Lidgren.Network;

namespace CatsAreOnlineServer.SyncedObjects;

public class CatSyncedObject : SyncedObject {
    public override SyncedObjectType enumType => SyncedObjectType.Cat;

    public bool ice { get; set; }

    public override void Write(NetBuffer message) {
        base.Write(message);
        message.Write(ice);
    }

    protected override void ReadCustomChangedState(NetBuffer message, NetBuffer notifyMessage,
        byte stateTypeByte, ref NetDeliveryMethod deliveryMethod) {
        CatStateType stateType = (CatStateType)stateTypeByte;
        switch(stateType) {
            case CatStateType.Ice:
                ice = message.ReadBoolean();
                notifyMessage.Write(stateTypeByte);
                notifyMessage.Write(ice);
                SetDeliveryMethod(DeliveryMethods.LessReliable, ref deliveryMethod);
                break;
        }
    }
}