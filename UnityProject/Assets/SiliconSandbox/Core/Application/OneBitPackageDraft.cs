using System;
using System.Collections.Generic;
using SiliconSandbox.Authoring;

namespace SiliconSandbox.Application
{
    // Editable, unpublished package proposal. Confirmation must still write
    // the global version and world inventory as one durable operation.
    public sealed class OneBitPackageDraft
    {
        private readonly List<OneBitPortChoice> ports;

        public CellRegion Region { get; }
        public ulong BaseRevision { get; }
        public OneBitModuleSnapshot Snapshot { get; }
        public string Name { get; set; }
        public IReadOnlyList<OneBitPortChoice> Ports => ports.AsReadOnly();

        public OneBitPackageDraft(OneBitWorldDesign world, CellRegion region,
            string proposedName, ulong baseRevision = 0)
        {
            Region = region;
            BaseRevision = baseRevision;
            Snapshot = OneBitModuleSnapshotBuilder.Preview(world, region);
            ports = new List<OneBitPortChoice>(
                OneBitPackageDefaults.ForSelection(Snapshot));
            Name = proposedName;
        }

        public void ReplacePort(int index, OneBitPortChoice choice)
        {
            if (choice == null) throw new ArgumentNullException(nameof(choice));
            ports[index] = choice;
        }

        public void RemovePort(int index) => ports.RemoveAt(index);

        public OneBitModuleVersion BuildCandidate(Guid familyId)
        {
            // A caller may validate freely. This does not publish to a world,
            // library index, inventory, or live simulator.
            return OneBitModuleVersionFactory.Create(Snapshot, familyId,
                Name, ports);
        }
    }
}
