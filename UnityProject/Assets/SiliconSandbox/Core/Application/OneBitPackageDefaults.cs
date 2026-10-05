using System;
using System.Collections.Generic;
using SiliconSandbox.Authoring;
using SiliconSandbox.Graph;

namespace SiliconSandbox.Application
{
    // Editable starting layout for the first-playable one-cell package screen.
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
            var graph = OneBitTopologyGraphBuilder.Build(snapshot.Topology);
            var selected = new List<OneBitPortCandidate>();
            var representedNets = new HashSet<DerivedOneBitNet>();
            foreach (var candidate in snapshot.PortCandidates)
                if (candidate.InternalEndpoint.Kind == JoinTargetKind.ComponentPin)
                {
                    selected.Add(candidate);
                    representedNets.Add(graph.NetFor(candidate.InternalEndpoint));
                }
            foreach (var candidate in snapshot.PortCandidates)
                if (candidate.InternalEndpoint.Kind == JoinTargetKind.ConnectorNode &&
                    representedNets.Add(graph.NetFor(candidate.InternalEndpoint)))
                    selected.Add(candidate);

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
                if (slot >= 4)
                    throw new NotSupportedException(
                        "This selection needs a larger configured module face.");
                var point = direction == OneBitPortDirection.Output
                    ? new QuarterPoint(4, slot % 2 == 0 ? 1 : 3,
                        slot < 2 ? 1 : 3)
                    : new QuarterPoint(0, slot % 2 == 0 ? 1 : 3,
                        slot < 2 ? 1 : 3);
                output.Add(new OneBitPortChoice(name, direction,
                    new GridCell(0, 0, 0), point,
                    candidate.InternalEndpoint));
            }
            return output.AsReadOnly();
        }
    }
}
