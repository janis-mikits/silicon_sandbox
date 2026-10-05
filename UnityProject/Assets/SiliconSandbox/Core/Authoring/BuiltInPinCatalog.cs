using System;
using System.Collections.Generic;

namespace SiliconSandbox.Authoring
{
    public enum PinDirection { Input, Output }

    public sealed class PinGeometry
    {
        public string Key { get; }
        public PinDirection Direction { get; }
        public int Qx { get; }
        public int Qy { get; }
        public int Qz { get; }

        public PinGeometry(string key, PinDirection direction, int qx, int qy, int qz)
        {
            Key = key;
            Direction = direction;
            Qx = qx;
            Qy = qy;
            Qz = qz;
        }
    }

    // Versioned built-ins. A later pin move needs a new type version or migration.
    public static class BuiltInPinCatalog
    {
        public const int TypeVersion = 1;
        public const string Source = "builtin.constant_logic_source";
        public const string And = "builtin.and";
        public const string SrFlipFlop = "builtin.sr_flip_flop";

        private static readonly Dictionary<string, PinGeometry[]> Geometry = new Dictionary<string, PinGeometry[]>
        {
            { Source, new[] { new PinGeometry("OUT", PinDirection.Output, 4, 1, 1) } },
            { And, new[]
                {
                    new PinGeometry("A", PinDirection.Input, 0, 1, 1),
                    new PinGeometry("B", PinDirection.Input, 0, 3, 1),
                    new PinGeometry("Y", PinDirection.Output, 4, 1, 1)
                }
            },
            { SrFlipFlop, new[]
                {
                    new PinGeometry("S", PinDirection.Input, 0, 1, 1),
                    new PinGeometry("R", PinDirection.Input, 0, 3, 1),
                    new PinGeometry("CLK", PinDirection.Input, 1, 1, 0),
                    new PinGeometry("Q", PinDirection.Output, 4, 1, 1),
                    new PinGeometry("Q_bar", PinDirection.Output, 4, 3, 1)
                }
            }
        };

        public static IReadOnlyList<PinGeometry> Pins(string typeId, int typeVersion)
        {
            if (typeVersion != TypeVersion || !Geometry.TryGetValue(typeId, out var pins))
                throw new ArgumentException("Unsupported built-in type/version.");
            return Array.AsReadOnly(pins);
        }
    }
}
