using System;
using System.Collections.Generic;

namespace SiliconSandbox.Authoring
{
    public readonly struct CellRegion
    {
        public GridCell Min { get; }
        public GridCell Max { get; }

        public CellRegion(GridCell first, GridCell second)
        {
            Min = new GridCell(Math.Min(first.X, second.X), Math.Min(first.Y, second.Y),
                Math.Min(first.Z, second.Z));
            Max = new GridCell(Math.Max(first.X, second.X), Math.Max(first.Y, second.Y),
                Math.Max(first.Z, second.Z));
        }

        public bool Contains(GridCell cell) => cell.X >= Min.X && cell.X <= Max.X &&
            cell.Y >= Min.Y && cell.Y <= Max.Y && cell.Z >= Min.Z && cell.Z <= Max.Z;
        public GridCell Local(GridCell cell) =>
            new GridCell(cell.X - Min.X, cell.Y - Min.Y, cell.Z - Min.Z);
        public GridCell SizeCells => new GridCell(Max.X - Min.X + 1,
            Max.Y - Min.Y + 1, Max.Z - Min.Z + 1);
    }

    // The exact local endpoint is retained so later port configuration can map
    // a stable external port to authored topology rather than a derived net index.
    public sealed class OneBitPortCandidate
    {
        public JoinMember InternalEndpoint { get; }
        public GridCell LocalCell { get; }
        public QuarterPoint PointQ { get; }

        public OneBitPortCandidate(JoinMember internalEndpoint, GridCell localCell,
            QuarterPoint pointQ)
        {
            InternalEndpoint = internalEndpoint;
            LocalCell = localCell;
            PointQ = pointQ;
        }
    }

    public sealed class OneBitModuleSnapshot
    {
        public GridCell SizeCells { get; }
        public IReadOnlyList<PlacedOneBitComponent> Components { get; }
        public OneBitAuthoredTopology Topology { get; }
        public IReadOnlyList<OneBitPortCandidate> PortCandidates { get; }

        public OneBitModuleSnapshot(GridCell sizeCells,
            List<PlacedOneBitComponent> components, OneBitAuthoredTopology topology,
            List<OneBitPortCandidate> candidates)
        {
            SizeCells = sizeCells;
            Components = components.AsReadOnly();
            Topology = topology;
            PortCandidates = candidates.AsReadOnly();
        }
    }

}
