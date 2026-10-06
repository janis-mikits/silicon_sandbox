using System;
using System.Collections.Generic;
using SiliconSandbox.Authoring;
using SiliconSandbox.Graph;

namespace SiliconSandbox.Application
{
    // Editable starting layout. Captured internal bounds do not determine the
    // placed exterior footprint; port capacity does.
    // The confirmed interface is still the player's final port choices.
    public static class OneBitPackageDefaults
    {
        public static IReadOnlyList<OneBitPortChoice> ForSingleCell(
            OneBitModuleSnapshot snapshot)
        {
            if (snapshot == null) throw new ArgumentNullException(nameof(snapshot));
            if (!snapshot.SizeCells.Equals(new GridCell(1, 1, 1)))
                throw new NotSupportedException(
                    "Automatic port layout currently requires one cell.");
            return ForSelection(snapshot);
        }

        public static IReadOnlyList<OneBitPortChoice> ForSelection(
            OneBitModuleSnapshot snapshot)
        {
            if (snapshot == null) throw new ArgumentNullException(nameof(snapshot));
            var graph = OneBitTopologyGraphBuilder.Build(snapshot.Topology);
            // Every connected boundary candidate receives an exterior port.
            // The snapshot already excludes pure pass-through routes; a
            // duplicate net is not a reason to silently omit a real port.
            var selected = new List<OneBitPortCandidate>(snapshot.PortCandidates);
            var exteriorSize = DefaultExteriorSize(snapshot);

            var output = new List<OneBitPortChoice>();
            var usedNames = new HashSet<string>(StringComparer.Ordinal);
            var west = 0; var east = 0;
            foreach (var candidate in selected)
            {
                var name = "PORT" + (output.Count + 1);
                var direction = OneBitPortDirection.Input;
                var pinRef = candidate.InternalEndpoint;
                if (pinRef.Kind != JoinTargetKind.ComponentPin)
                    foreach (var member in graph.NetFor(pinRef).Members)
                        if (member.Kind == JoinTargetKind.ComponentPin)
                        { pinRef = member; break; }
                if (pinRef.Kind == JoinTargetKind.ComponentPin)
                    foreach (var component in snapshot.Components)
                        if (component.Id == pinRef.OwnerId)
                            foreach (var pin in BuiltInPinCatalog.Pins(component.TypeId,
                                component.TypeVersion))
                                if (component.PinIds[pin.Key] == pinRef.PartId)
                                {
                                    name = pin.Key;
                                    direction = pin.Direction == PinDirection.Output
                                        ? OneBitPortDirection.Output
                                        : OneBitPortDirection.Input;
                                }
                var baseName = name;
                for (var suffix = 2; !usedNames.Add(name); suffix++)
                    name = baseName + "_" + suffix;
                var slot = direction == OneBitPortDirection.Output ? east++ : west++;
                var capacity = checked(4 * exteriorSize.Y * exteriorSize.Z);
                if (slot >= capacity)
                    throw new NotSupportedException(
                        "This selection needs a larger configured module face.");
                var faceCell = slot / 4;
                var localCell = new GridCell(
                    direction == OneBitPortDirection.Output
                        ? exteriorSize.X - 1 : 0,
                    faceCell / exteriorSize.Z,
                    faceCell % exteriorSize.Z);
                var point = direction == OneBitPortDirection.Output
                    ? new QuarterPoint(4, slot % 2 == 0 ? 1 : 3,
                        slot % 4 < 2 ? 1 : 3)
                    : new QuarterPoint(0, slot % 2 == 0 ? 1 : 3,
                        slot % 4 < 2 ? 1 : 3);
                output.Add(new OneBitPortChoice(name, direction,
                    localCell, point,
                    candidate.InternalEndpoint));
            }
            return output.AsReadOnly();
        }

        public static GridCell DefaultExteriorSize(OneBitModuleSnapshot snapshot)
        {
            if (snapshot == null) throw new ArgumentNullException(nameof(snapshot));
            var graph = OneBitTopologyGraphBuilder.Build(snapshot.Topology);
            var inputCount = 0;
            var outputCount = 0;
            foreach (var candidate in snapshot.PortCandidates)
            {
                var pinRef = candidate.InternalEndpoint;
                if (pinRef.Kind != JoinTargetKind.ComponentPin)
                    foreach (var member in graph.NetFor(pinRef).Members)
                        if (member.Kind == JoinTargetKind.ComponentPin)
                        { pinRef = member; break; }
                var output = false;
                if (pinRef.Kind == JoinTargetKind.ComponentPin)
                    foreach (var component in snapshot.Components)
                        if (component.Id == pinRef.OwnerId)
                            foreach (var pin in BuiltInPinCatalog.Pins(component.TypeId,
                                component.TypeVersion))
                                if (component.PinIds[pin.Key] == pinRef.PartId)
                                    output = pin.Direction == PinDirection.Output;
                if (output) outputCount++; else inputCount++;
            }
            var length = Math.Max(1,
                (Math.Max(inputCount, outputCount) + 3) / 4);
            return new GridCell(1, 1, length);
        }
    }
}
