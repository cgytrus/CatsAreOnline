using Lidgren.Network;

namespace CatsAreOnline.Shared {
    public static class DeliveryMethods {
        public const NetDeliveryMethod Global = NetDeliveryMethod.UnreliableSequenced;
        public const NetDeliveryMethod LessReliable = NetDeliveryMethod.ReliableSequenced;
        public const NetDeliveryMethod Reliable = NetDeliveryMethod.ReliableOrdered;
    }
}
