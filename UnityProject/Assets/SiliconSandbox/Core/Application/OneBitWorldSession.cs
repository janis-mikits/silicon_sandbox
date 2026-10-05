using System;
using System.Collections.Generic;
using SiliconSandbox.Authoring;
using SiliconSandbox.Contracts;
using SiliconSandbox.Graph;
using SiliconSandbox.Simulation;

namespace SiliconSandbox.Application
{
    // Coherent authored revision + derived graph + live simulator. Unity views
    // subscribe to successful revision changes; they never own the circuit.
    public sealed class OneBitWorldSession
    {
        private sealed class HistoryEntry
        {
            public OneBitWorldDesign Design { get; }
            public DateTimeOffset RecordedAt { get; }
            public HistoryEntry(OneBitWorldDesign design, DateTimeOffset recordedAt)
            { Design = design; RecordedAt = recordedAt; }
        }

        private readonly Func<DateTimeOffset> wallClock;
        private readonly List<HistoryEntry> undo = new List<HistoryEntry>();
        private readonly List<HistoryEntry> redo = new List<HistoryEntry>();
        private static readonly TimeSpan HistoryWindow = TimeSpan.FromMinutes(5);
        private Dictionary<Guid, OneBitModuleVersion> versions;
        public OneBitWorldDesign Design { get; private set; }
        public BuiltOneBitCircuitPlan Built { get; private set; }
        public GraphDrivenOneBitCircuit Circuit { get; }
        public OneBitCircuitInspection Inspector { get; private set; }
        public WorldSimulationScheduler Scheduler { get; }
        public ulong Revision { get; private set; }
        public bool HasModuleVersion(Guid versionId) => versions.ContainsKey(versionId);
        public IReadOnlyDictionary<Guid, OneBitModuleVersion> ModuleVersions =>
            new System.Collections.ObjectModel.ReadOnlyDictionary<Guid,
                OneBitModuleVersion>(versions);

        public OneBitWorldSession(OneBitWorldDesign design, string frequencyHz = "10",
            IReadOnlyDictionary<Guid, OneBitModuleVersion> moduleVersions = null,
            Func<DateTimeOffset> utcNow = null)
        {
            Design = design ?? throw new ArgumentNullException(nameof(design));
            wallClock = utcNow ?? (() => DateTimeOffset.UtcNow);
            versions = moduleVersions == null
                ? new Dictionary<Guid, OneBitModuleVersion>()
                : new Dictionary<Guid, OneBitModuleVersion>(moduleVersions);
            Built = Build(design, versions);
            Circuit = new GraphDrivenOneBitCircuit(Built.Plan);
            Scheduler = new WorldSimulationScheduler(Circuit, frequencyHz);
            Inspector = MakeInspector(design, Built, Circuit);
        }

        public void PlaceComponent(string typeId, GridCell anchorCell,
            GridOrientation orientation, LogicBit sourceOnValue = LogicBit.One,
            bool sourceInitialOn = false, LogicBit? srInitialQ = null,
            string tag = "")
        {
            SafePause();
            Publish(OneBitWorldEdits.PlaceComponent(Design, typeId, anchorCell,
                orientation, sourceOnValue, sourceInitialOn, srInitialQ, tag));
        }

        public void PlaceModule(OneBitModuleVersion version, string instanceName,
            GridCell anchorCell, GridOrientation orientation)
        {
            if (version == null) throw new ArgumentNullException(nameof(version));
            SafePause();
            Publish(OneBitWorldEdits.PlaceModule(Design, version, instanceName,
                anchorCell, orientation), version);
        }

        public void BreakSpan(Guid connectorId, Guid spanId)
        {
            SafePause();
            var edit = OneBitTopologyEdits.BreakSpan(Design.Topology,
                connectorId, spanId);
            Publish(new OneBitWorldDesign(Design.Bounds, Design.Components,
                edit.Design, Design.Modules));
        }

        public void PlaceConnector(ConnectorRoute connector,
            IEnumerable<ElectricalJoin> joins)
        {
            SafePause();
            Publish(OneBitWorldEdits.PlaceConnector(Design, connector, joins));
        }

        public void ConnectPins(JoinMember first, JoinMember second)
        {
            SafePause();
            var proposal = OneBitPinRoutePlanner.Plan(Design, first, second);
            Publish(OneBitWorldEdits.PlaceConnector(Design,
                proposal.Route, proposal.Joins));
        }

        public void AttachWorldClockPin(Guid srObjectId)
        {
            SafePause();
            Publish(OneBitWorldEdits.AttachWorldClockPin(Design, srObjectId));
        }

        public void AttachWorldClockPort(Guid moduleObjectId, Guid portId)
        {
            SafePause();
            Publish(OneBitWorldEdits.AttachWorldClockPort(Design,
                moduleObjectId, portId));
        }

        public void ConnectPinToNode(JoinMember pin, JoinMember targetNode)
        {
            SafePause();
            var proposal = OneBitPinRoutePlanner.PlanToConnectorNode(
                Design, pin, targetNode);
            Publish(OneBitWorldEdits.PlaceConnector(Design,
                proposal.Route, proposal.Joins));
        }

        public void AddJoin(ElectricalJoin join, string chosenTag = null)
        {
            SafePause();
            var edit = OneBitTopologyEdits.AddJoin(Design.Topology, join, chosenTag);
            Publish(new OneBitWorldDesign(Design.Bounds, Design.Components,
                edit.Design, Design.Modules));
        }

        public void ConfigureSource(Guid sourceId, LogicBit onValue, bool initialOn)
        {
            SafePause();
            var candidate = OneBitWorldEdits.ConfigureSource(Design,
                sourceId, onValue, initialOn);
            var nextRevision = checked(Revision + 1);
            var built = Build(candidate, versions);
            Circuit.ReplacePlanWithAuthoredSourceConfiguration(built.Plan);
            RecordEdit();
            Design = candidate;
            Built = built;
            Inspector = MakeInspector(candidate, built, Circuit);
            Revision = nextRevision;
        }

        public void ToggleSource(Guid sourceId)
        {
            var nextOn = !Circuit.Source(sourceId).IsOn;
            if (Scheduler.IsPaused)
            {
                Circuit.SetSourceOn(sourceId, nextOn);
                Circuit.AdvanceToSettled();
            }
            else
            {
                Scheduler.QueueSourceChange(sourceId, nextOn, Scheduler.Now);
                Scheduler.AdvanceUntil(Scheduler.Now);
            }
        }

        public bool TryUndo()
        {
            PruneHistory();
            if (undo.Count == 0) return false;
            SafePause();
            var previous = undo[undo.Count - 1];
            var built = Build(previous.Design, versions);
            var nextRevision = checked(Revision + 1);
            Circuit.ReplacePlanWithAuthoredSourceConfiguration(built.Plan);
            undo.RemoveAt(undo.Count - 1);
            redo.Add(new HistoryEntry(Design, wallClock()));
            Design = previous.Design;
            Built = built;
            Inspector = MakeInspector(Design, built, Circuit);
            Revision = nextRevision;
            return true;
        }

        public bool TryRedo()
        {
            PruneHistory();
            if (redo.Count == 0) return false;
            SafePause();
            var next = redo[redo.Count - 1];
            var built = Build(next.Design, versions);
            var nextRevision = checked(Revision + 1);
            Circuit.ReplacePlanWithAuthoredSourceConfiguration(built.Plan);
            redo.RemoveAt(redo.Count - 1);
            undo.Add(new HistoryEntry(Design, wallClock()));
            Design = next.Design;
            Built = built;
            Inspector = MakeInspector(Design, built, Circuit);
            Revision = nextRevision;
            return true;
        }

        private void SafePause()
        {
            if (!Scheduler.IsPaused) Scheduler.AdvanceUntil(Scheduler.Now);
            Scheduler.PauseSimulation();
        }

        private void Publish(OneBitWorldDesign candidate,
            OneBitModuleVersion addedVersion = null)
        {
            var nextRevision = checked(Revision + 1);
            var nextVersions = new Dictionary<Guid, OneBitModuleVersion>(versions);
            if (addedVersion != null)
            {
                if (nextVersions.TryGetValue(addedVersion.VersionId, out var existing) &&
                    !ReferenceEquals(existing, addedVersion))
                    throw new ArgumentException("Conflicting exact module version identity.");
                nextVersions[addedVersion.VersionId] = addedVersion;
            }
            var built = Build(candidate, nextVersions);
            Circuit.ReplacePlan(built.Plan);
            RecordEdit();
            versions = nextVersions;
            Design = candidate;
            Built = built;
            Inspector = MakeInspector(candidate, built, Circuit);
            Revision = nextRevision;
        }

        private void RecordEdit()
        {
            PruneHistory();
            undo.Add(new HistoryEntry(Design, wallClock()));
            redo.Clear();
        }

        private void PruneHistory()
        {
            var now = wallClock();
            undo.RemoveAll(item => now - item.RecordedAt > HistoryWindow);
            redo.RemoveAll(item => now - item.RecordedAt > HistoryWindow);
        }

        private static BuiltOneBitCircuitPlan Build(OneBitWorldDesign design,
            IReadOnlyDictionary<Guid, OneBitModuleVersion> moduleVersions)
        {
            return OneBitHierarchicalCircuitPlanBuilder.Build(design,
                moduleVersions);
        }

        private static OneBitCircuitInspection MakeInspector(OneBitWorldDesign design,
            BuiltOneBitCircuitPlan built, GraphDrivenOneBitCircuit circuit)
        {
            var components = new List<OneBitComponent>();
            foreach (var placed in design.Components)
                components.Add(placed.RuntimeDescriptor());
            return new OneBitCircuitInspection(design.Topology, components, built, circuit);
        }
    }
}
