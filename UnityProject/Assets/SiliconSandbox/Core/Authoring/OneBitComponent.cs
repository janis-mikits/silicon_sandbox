using System;
using System.Collections.Generic;

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

        public OneBitComponent(Guid id, string typeId, int typeVersion,
            IReadOnlyDictionary<string, Guid> pinIds)
        {
            if (id == Guid.Empty || typeId == null || pinIds == null)
                throw new ArgumentException("Invalid component identity or pins.");
            Id = id;
            TypeId = typeId;
            TypeVersion = typeVersion;
            PinIds = new Dictionary<string, Guid>(pinIds);
        }

        public Guid Pin(string key) => PinIds[key];
    }
}
