using System;

namespace SiliconSandbox.Authoring
{
    public enum GridDirection { North, South, East, West, Up, Down }

    // Version 1 forward/up record. The right basis is up x forward, so every
    // valid pair is one of the 24 proper grid-aligned orientations.
    public readonly struct GridOrientation : IEquatable<GridOrientation>
    {
        public GridDirection Forward { get; }
        public GridDirection Up { get; }
        public bool IsValid => Valid(Forward) && Valid(Up) &&
            Dot(Vector(Forward), Vector(Up)) == 0;
        public static readonly GridOrientation Default =
            new GridOrientation(GridDirection.North, GridDirection.Up);

        public GridOrientation(GridDirection forward, GridDirection up)
        {
            if (!Valid(forward) || !Valid(up) || Dot(Vector(forward), Vector(up)) != 0)
                throw new ArgumentException("Forward and up must be perpendicular grid directions.");
            Forward = forward;
            Up = up;
        }

        public GridCell TransformCellOffset(GridCell local)
        {
            if (!IsValid) throw new InvalidOperationException("Invalid grid orientation.");
            var right = Cross(Vector(Up), Vector(Forward));
            var up = Vector(Up);
            var forward = Vector(Forward);
            return new GridCell(right.X * local.X + up.X * local.Y + forward.X * local.Z,
                right.Y * local.X + up.Y * local.Y + forward.Y * local.Z,
                right.Z * local.X + up.Z * local.Y + forward.Z * local.Z);
        }

        public QuarterPoint TransformPoint(QuarterPoint local)
        {
            var rotated = TransformCellOffset(new GridCell(local.X - 2,
                local.Y - 2, local.Z - 2));
            return new QuarterPoint(rotated.X + 2, rotated.Y + 2, rotated.Z + 2);
        }

        public GridOrientation ClockwiseYaw() => new GridOrientation(
            RotateClockwise(Forward), RotateClockwise(Up));
        public GridOrientation CounterclockwiseYaw() => new GridOrientation(
            RotateCounterclockwise(Forward), RotateCounterclockwise(Up));

        public bool Equals(GridOrientation other) => Forward == other.Forward && Up == other.Up;
        public override bool Equals(object obj) => obj is GridOrientation other && Equals(other);
        public override int GetHashCode() => (int)Forward * 397 ^ (int)Up;

        public static GridDirection ParseFace(string name)
        {
            switch (name)
            {
                case "north": return GridDirection.North;
                case "south": return GridDirection.South;
                case "east": return GridDirection.East;
                case "west": return GridDirection.West;
                case "up": return GridDirection.Up;
                case "down": return GridDirection.Down;
                default: throw new ArgumentException("Unknown orientation face.");
            }
        }

        public static string FaceName(GridDirection direction)
        {
            switch (direction)
            {
                case GridDirection.North: return "north";
                case GridDirection.South: return "south";
                case GridDirection.East: return "east";
                case GridDirection.West: return "west";
                case GridDirection.Up: return "up";
                case GridDirection.Down: return "down";
                default: throw new ArgumentOutOfRangeException(nameof(direction));
            }
        }

        private static bool Valid(GridDirection direction) =>
            direction >= GridDirection.North && direction <= GridDirection.Down;

        private static GridCell Vector(GridDirection direction)
        {
            switch (direction)
            {
                case GridDirection.North: return new GridCell(0, 0, 1);
                case GridDirection.South: return new GridCell(0, 0, -1);
                case GridDirection.East: return new GridCell(1, 0, 0);
                case GridDirection.West: return new GridCell(-1, 0, 0);
                case GridDirection.Up: return new GridCell(0, 1, 0);
                case GridDirection.Down: return new GridCell(0, -1, 0);
                default: throw new ArgumentOutOfRangeException(nameof(direction));
            }
        }

        private static GridDirection RotateClockwise(GridDirection direction)
        {
            var vector = Vector(direction);
            return Direction(new GridCell(vector.Z, vector.Y, -vector.X));
        }

        private static GridDirection RotateCounterclockwise(GridDirection direction)
        {
            var vector = Vector(direction);
            return Direction(new GridCell(-vector.Z, vector.Y, vector.X));
        }

        private static GridDirection Direction(GridCell vector)
        {
            foreach (GridDirection candidate in Enum.GetValues(typeof(GridDirection)))
                if (Vector(candidate).Equals(vector)) return candidate;
            throw new ArgumentException("Vector is not a grid direction.");
        }

        private static int Dot(GridCell a, GridCell b) =>
            a.X * b.X + a.Y * b.Y + a.Z * b.Z;

        private static GridCell Cross(GridCell a, GridCell b) =>
            new GridCell(a.Y * b.Z - a.Z * b.Y,
                a.Z * b.X - a.X * b.Z,
                a.X * b.Y - a.Y * b.X);
    }
}
