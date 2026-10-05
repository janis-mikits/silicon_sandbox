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
        public OneBitWorldDesign Design { get; private set; }
        public BuiltOneBitCircuitPlan Built { get; private set; }
        public GraphDrivenOneBitCircuit Circuit { get; }
        public WorldSimulationScheduler Scheduler { get; }
        public ulong Revision { get; private set; }

        public OneBitWorldSession(OneBitWorldDesign design, string frequencyHz = "10")
        {
            Design = design ?? throw new ArgumentNullException(nameof(design));
            Built = Build(design);
            Circuit = new GraphDrivenOneBitCircuit(Built.Plan);
            Scheduler = new WorldSimulationScheduler(Circuit, frequencyHz);
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

        public void BreakSpan(Guid connectorId, Guid spanId)
        {
            SafePause();
            var edit = OneBitTopologyEdits.BreakSpan(Design.Topology,
                connectorId, spanId);
            Publish(new OneBitWorldDesign(Design.Bounds, Design.Components, edit.Design));
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

        public void AddJoin(ElectricalJoin join, string chosenTag = null)
        {
            SafePause();
            var edit = OneBitTopologyEdits.AddJoin(Design.Topology, join, chosenTag);
            Publish(new OneBitWorldDesign(Design.Bounds, Design.Components, edit.Design));
        }

        public void ConfigureSource(Guid sourceId, LogicBit onValue, bool initialOn)
        {
            SafePause();
            var candidate = OneBitWorldEdits.ConfigureSource(Design,
                sourceId, onValue, initialOn);
            var nextRevision = checked(Revision + 1);
            var built = Build(candidate);
            Circuit.ReplacePlanWithAuthoredSourceConfiguration(built.Plan);
            Design = candidate;
            Built = built;
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

        private void SafePause()
        {
            if (!Scheduler.IsPaused) Scheduler.AdvanceUntil(Scheduler.Now);
            Scheduler.PauseSimulation();
        }

        private void Publish(OneBitWorldDesign candidate)
        {
            var nextRevision = checked(Revision + 1);
            var built = Build(candidate);
            Circuit.ReplacePlan(built.Plan);
            Design = candidate;
            Built = built;
            Revision = nextRevision;
        }

        private static BuiltOneBitCircuitPlan Build(OneBitWorldDesign design)
        {
            var components = new List<OneBitComponent>();
            foreach (var placed in design.Components)
                components.Add(placed.RuntimeDescriptor());
            return OneBitCircuitPlanBuilder.Build(design.Topology, components);
        }
    }
}
