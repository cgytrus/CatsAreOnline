using System;

using CatsAreOnline.Shared;
using CatsAreOnline.Shared.StateTypes;

using Lidgren.Network;

namespace CatsAreOnlineServer.SyncedObjects;

public abstract class SyncedObject {
    public abstract SyncedObjectType enumType { get; }

    public Guid id { get; init; }
    public Player owner { get; init; }
    public float posX { get; set; }
    public float posY { get; set; }
    public float colorR { get; set; }
    public float colorG { get; set; }
    public float colorB { get; set; }
    public float colorA { get; set; }
    public float scale { get; set; }
    public float rotation { get; set; }

    public virtual void Write(NetBuffer message) {
        message.Write(owner.username);
        message.Write((byte)enumType);
        message.Write(id.ToString());
        message.Write(posX);
        message.Write(posY);
        message.Write(colorR);
        message.Write(colorG);
        message.Write(colorB);
        message.Write(colorA);
        message.Write(scale);
        message.Write(rotation);
    }

    public void ReadChangedState(NetBuffer message, NetBuffer notifyMessage,
        byte stateTypeByte, ref NetDeliveryMethod deliveryMethod) {
        SyncedObjectStateType stateType = (SyncedObjectStateType)stateTypeByte;
        switch(stateType) {
            case SyncedObjectStateType.Position:
                posX = message.ReadFloat();
                posY = message.ReadFloat();
                notifyMessage.Write(stateTypeByte);
                notifyMessage.Write(posX);
                notifyMessage.Write(posY);
                SetDeliveryMethod(DeliveryMethods.Global, ref deliveryMethod);
                break;
            case SyncedObjectStateType.Color:
                colorR = message.ReadFloat();
                colorG = message.ReadFloat();
                colorB = message.ReadFloat();
                colorA = message.ReadFloat();
                notifyMessage.Write(stateTypeByte);
                notifyMessage.Write(colorR);
                notifyMessage.Write(colorG);
                notifyMessage.Write(colorB);
                notifyMessage.Write(colorA);
                SetDeliveryMethod(DeliveryMethods.LessReliable, ref deliveryMethod);
                break;
            case SyncedObjectStateType.Scale:
                scale = message.ReadFloat();
                notifyMessage.Write(stateTypeByte);
                notifyMessage.Write(scale);
                SetDeliveryMethod(DeliveryMethods.LessReliable, ref deliveryMethod);
                break;
            case SyncedObjectStateType.Rotation:
                rotation = message.ReadFloat();
                notifyMessage.Write(stateTypeByte);
                notifyMessage.Write(rotation);
                SetDeliveryMethod(DeliveryMethods.Global, ref deliveryMethod);
                break;
            default:
                ReadCustomChangedState(message, notifyMessage, stateTypeByte, ref deliveryMethod);
                break;
        }
    }

    protected virtual void ReadCustomChangedState(NetBuffer message, NetBuffer notifyMessage,
        byte stateTypeByte, ref NetDeliveryMethod deliveryMethod) { }

    protected static void SetDeliveryMethod(NetDeliveryMethod method, ref NetDeliveryMethod deliveryMethod) {
        if(method > deliveryMethod) deliveryMethod = method;
    }

    public static SyncedObject Create(SyncedObjectType type, Guid id, Player owner, NetBuffer message) =>
        type switch {
            SyncedObjectType.Cat => new CatSyncedObject {
                id = id,
                owner = owner,
                posX = message.ReadFloat(),
                posY = message.ReadFloat(),
                colorR = message.ReadFloat(),
                colorG = message.ReadFloat(),
                colorB = message.ReadFloat(),
                colorA = message.ReadFloat(),
                scale = message.ReadFloat(),
                rotation = message.ReadFloat(),
                ice = message.ReadBoolean()
            },
            SyncedObjectType.Companion => new CompanionSyncedObject {
                id = id,
                owner = owner,
                posX = message.ReadFloat(),
                posY = message.ReadFloat(),
                colorR = message.ReadFloat(),
                colorG = message.ReadFloat(),
                colorB = message.ReadFloat(),
                colorA = message.ReadFloat(),
                scale = message.ReadFloat(),
                rotation = message.ReadFloat()
            },
            _ => throw new ArgumentOutOfRangeException(nameof(type), type,
                "Possibly, someone connected with an old version or uses a modified client")
        };
}