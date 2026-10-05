using System;
using System.Collections.Generic;

namespace SiliconSandbox.Authoring
{
    // The saved interface snapshot does not include a target inside the
    // definition; it remains usable when that exact definition is unavailable.
    public sealed class OneBitPortInterface
    {
        public Guid Id { get; }
        public string Name { get; }
        public OneBitPortDirection Direction { get; }
        public GridCell LocalCell { get; }
        public QuarterPoint PointQ { get; }

        public OneBitPortInterface(Guid id, string name,
            OneBitPortDirection direction, GridCell localCell, QuarterPoint pointQ)
        {
            Id = id; Name = name; Direction = direction;
            LocalCell = localCell; PointQ = pointQ;
        }
    }

    public sealed class PlacedOneBitModuleInstance
    {
        public Guid Id { get; }
        public Guid InstanceId { get; }
        public Guid FamilyId { get; }
        public Guid VersionId { get; }
        public string InstanceName { get; }
        public GridCell AnchorCell { get; }
        public GridOrientation Orientation { get; }
        public GridCell SizeCells { get; }
        public IReadOnlyList<OneBitPortInterface> InterfacePorts { get; }
        public string Tag { get; }

        public PlacedOneBitModuleInstance(Guid id, Guid instanceId,
            Guid familyId, Guid versionId, string instanceName,
            GridCell anchorCell, GridOrientation orientation, GridCell sizeCells,
            IEnumerable<OneBitPortInterface> interfacePorts, string tag = "")
        {
            if (id == Guid.Empty || instanceId == Guid.Empty ||
                familyId == Guid.Empty || versionId == Guid.Empty ||
                string.IsNullOrWhiteSpace(instanceName) || !orientation.IsValid ||
                sizeCells.X < 1 || sizeCells.Y < 1 || sizeCells.Z < 1 ||
                interfacePorts == null || tag == null)
                throw new ArgumentException("Invalid placed module interface.");
            var ports = new List<OneBitPortInterface>(interfacePorts);
            var ids = new HashSet<Guid>();
            var names = new HashSet<string>(StringComparer.Ordinal);
            var positions = new HashSet<(GridCell, QuarterPoint)>();
            foreach (var port in ports)
                if (port == null || port.Id == Guid.Empty ||
                    string.IsNullOrWhiteSpace(port.Name) ||
                    !Enum.IsDefined(typeof(OneBitPortDirection), port.Direction) ||
                    !ids.Add(port.Id) || !names.Add(port.Name) ||
                    !Exterior(sizeCells, port.LocalCell, port.PointQ) ||
                    !positions.Add((port.LocalCell, port.PointQ)))
                    throw new ArgumentException("Invalid duplicate module interface port.");
            Id = id; InstanceId = instanceId; FamilyId = familyId;
            VersionId = versionId; InstanceName = instanceName;
            AnchorCell = anchorCell; Orientation = orientation;
            SizeCells = sizeCells; InterfacePorts = ports.AsReadOnly(); Tag = tag;
        }

        public IReadOnlyList<GridCell> OccupiedCells()
        {
            var result = new List<GridCell>();
            for (var x = 0; x < SizeCells.X; x++)
                for (var y = 0; y < SizeCells.Y; y++)
                    for (var z = 0; z < SizeCells.Z; z++)
                        result.Add(WorldCell(new GridCell(x, y, z)));
            return result.AsReadOnly();
        }

        public IReadOnlyList<AuthoredModulePortBit> BuildPortBits()
        {
            var result = new List<AuthoredModulePortBit>();
            foreach (var port in InterfacePorts)
                result.Add(new AuthoredModulePortBit(Id, port.Id, 0,
                    WorldCell(port.LocalCell), Orientation.TransformPoint(port.PointQ)));
            return result.AsReadOnly();
        }

        private GridCell WorldCell(GridCell local)
        {
            var minX = int.MaxValue; var minY = int.MaxValue; var minZ = int.MaxValue;
            for (var x = 0; x <= SizeCells.X - 1; x += Math.Max(1, SizeCells.X - 1))
                for (var y = 0; y <= SizeCells.Y - 1; y += Math.Max(1, SizeCells.Y - 1))
                    for (var z = 0; z <= SizeCells.Z - 1; z += Math.Max(1, SizeCells.Z - 1))
                    {
                        var corner = Orientation.TransformCellOffset(new GridCell(x, y, z));
                        minX = Math.Min(minX, corner.X);
                        minY = Math.Min(minY, corner.Y);
                        minZ = Math.Min(minZ, corner.Z);
                    }
            var transformed = Orientation.TransformCellOffset(local);
            return new GridCell(checked(AnchorCell.X + transformed.X - minX),
                checked(AnchorCell.Y + transformed.Y - minY),
                checked(AnchorCell.Z + transformed.Z - minZ));
        }

        private static bool Exterior(GridCell size, GridCell cell,
            QuarterPoint point) => point.IsFacePoint &&
            cell.X >= 0 && cell.Y >= 0 && cell.Z >= 0 &&
            cell.X < size.X && cell.Y < size.Y && cell.Z < size.Z &&
            (cell.X == 0 && point.X == 0 ||
             cell.X == size.X - 1 && point.X == 4 ||
             cell.Y == 0 && point.Y == 0 ||
             cell.Y == size.Y - 1 && point.Y == 4 ||
             cell.Z == 0 && point.Z == 0 ||
             cell.Z == size.Z - 1 && point.Z == 4);
    }
}
