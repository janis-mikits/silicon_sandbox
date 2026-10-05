using System;
using System.Collections.Generic;

namespace SiliconSandbox.Authoring
{
    public readonly struct FixturePinRef : IEquatable<FixturePinRef>
    {
        public Guid ObjectId { get; }
        public Guid PinId { get; }

        public FixturePinRef(Guid objectId, Guid pinId)
        {
            ObjectId = objectId;
            PinId = pinId;
        }

        public bool Equals(FixturePinRef other) => ObjectId == other.ObjectId && PinId == other.PinId;
        public override bool Equals(object obj) => obj is FixturePinRef other && Equals(other);
        public override int GetHashCode() => ObjectId.GetHashCode() ^ PinId.GetHashCode();
    }

    public sealed class FixtureComponent
    {
        private readonly Dictionary<string, FixturePinRef> pins;

        public Guid Id { get; }
        public string TypeId { get; }
        public int TypeVersion => BuiltInPinCatalog.TypeVersion;

        public FixtureComponent(Guid id, string typeId, IReadOnlyDictionary<string, Guid> pinIds)
        {
            if (id == Guid.Empty) throw new ArgumentException("Object ID is empty.");
            Id = id;
            TypeId = typeId;
            pins = new Dictionary<string, FixturePinRef>();
            foreach (var geometry in BuiltInPinCatalog.Pins(typeId, TypeVersion))
            {
                if (!pinIds.TryGetValue(geometry.Key, out var pinId) || pinId == Guid.Empty)
                    throw new ArgumentException("Missing pin ID: " + geometry.Key);
                pins.Add(geometry.Key, new FixturePinRef(id, pinId));
            }
            if (pinIds.Count != pins.Count) throw new ArgumentException("Unexpected pin ID.");
        }

        public FixturePinRef Pin(string key) => pins[key];
    }

    // A small authored fixture, not a V1 save record. Each connector's pin attachments
    // are explicit; shape/proximity alone never creates an electrical connection.
    public sealed class FixtureConnector
    {
        public Guid Id { get; }
        public IReadOnlyList<FixturePinRef> AttachedPins { get; }

        public FixtureConnector(Guid id, params FixturePinRef[] attachedPins)
        {
            if (id == Guid.Empty || attachedPins == null || attachedPins.Length < 1 || attachedPins.Length > 2)
                throw new ArgumentException("Fixture connector needs one or two pin attachments.");
            Id = id;
            AttachedPins = Array.AsReadOnly((FixturePinRef[])attachedPins.Clone());
        }
    }

    public sealed class AndFixtureDesign
    {
        public FixtureComponent SourceA { get; }
        public FixtureComponent SourceB { get; }
        public FixtureComponent Gate { get; }
        public IReadOnlyList<FixtureConnector> Connectors { get; }

        public AndFixtureDesign(FixtureComponent sourceA, FixtureComponent sourceB,
            FixtureComponent gate, params FixtureConnector[] connectors)
        {
            SourceA = sourceA ?? throw new ArgumentNullException(nameof(sourceA));
            SourceB = sourceB ?? throw new ArgumentNullException(nameof(sourceB));
            Gate = gate ?? throw new ArgumentNullException(nameof(gate));
            Connectors = Array.AsReadOnly((FixtureConnector[])connectors.Clone());
        }

        public static AndFixtureDesign Create()
        {
            var a = new FixtureComponent(Guid.Parse("01e9c881-e60c-493c-8d35-36f33896bf7a"), BuiltInPinCatalog.Source,
                new Dictionary<string, Guid> { { "OUT", Guid.Parse("7c896e87-e68e-434a-881f-c237efc4621d") } });
            var b = new FixtureComponent(Guid.Parse("afc0f818-03a6-4de8-9f20-d17c9d6c2884"), BuiltInPinCatalog.Source,
                new Dictionary<string, Guid> { { "OUT", Guid.Parse("5be0f5b1-528b-4ebf-bd9b-eb7c7561093f") } });
            var gate = new FixtureComponent(Guid.Parse("52c56ef7-e921-4169-8da9-fba5ab641537"), BuiltInPinCatalog.And,
                new Dictionary<string, Guid>
                {
                    { "A", Guid.Parse("035768e1-a891-4b5c-aaef-a576e23b7ac5") },
                    { "B", Guid.Parse("6be774cf-7c5e-446e-a3c8-4e9382c4d3d7") },
                    { "Y", Guid.Parse("6aa1c7df-3848-4d09-9b9e-1017de1e79da") }
                });
            return new AndFixtureDesign(a, b, gate,
                new FixtureConnector(Guid.Parse("b75874ec-02b3-4d8e-904d-f59758a130b8"), a.Pin("OUT"), gate.Pin("A")),
                new FixtureConnector(Guid.Parse("58ae64a4-adbb-4b31-8915-6a124f18e5f0"), b.Pin("OUT"), gate.Pin("B")),
                new FixtureConnector(Guid.Parse("67bdb599-5fd0-4ce0-9914-f5d5e8954e24"), gate.Pin("Y")));
        }
    }
}
