using System;
using System.Collections.Generic;
using SiliconSandbox.Contracts;

namespace SiliconSandbox.Authoring
{
    // Component identity and versioned pin identities from an authored design.
    // Placement, orientation, and configuration remain in the version 1 object record.
    public sealed class OneBitComponent
    {
        public Guid Id { get; }
        public string TypeId { get; }
        public int TypeVersion { get; }
        public IReadOnlyDictionary<string, Guid> PinIds { get; }
        public LogicBit? InitialQ { get; }

        public OneBitComponent(Guid id, string typeId, int typeVersion,
            IReadOnlyDictionary<string, Guid> pinIds, LogicBit? initialQ = null)
        {
            if (id == Guid.Empty || typeId == null || pinIds == null)
                throw new ArgumentException("Invalid component identity or pins.");
            Id = id;
            TypeId = typeId;
            TypeVersion = typeVersion;
            PinIds = new Dictionary<string, Guid>(pinIds);
            if (initialQ.HasValue && (typeId != BuiltInPinCatalog.SrFlipFlop ||
                initialQ.Value > LogicBit.Z))
                throw new ArgumentException("Only an SR flip-flop may configure initial Q.");
            InitialQ = initialQ;
        }

        public Guid Pin(string key) => PinIds[key];
    }
}
