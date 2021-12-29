namespace OpenMedStack.NEventStore.Serialization
{
    using NEventStore;

    public static class JsonSerializationWireupExtension
    {
        public static Wireup UsingCustomSerialization(this PersistenceWireup wireup, ISerialize serializer) =>
            wireup.With(serializer);

        public static Wireup UsingJsonSerialization(this PersistenceWireup wireup) =>
            wireup.With<ISerialize>(new NesJsonSerializer(wireup.Logger));
    }
}
