using System;
using System.Collections.Generic;

namespace SiliconSandbox.Contracts
{
    // A local blueprint object is qualified by its exact immutable version and
    // outer-to-inner placed instance chain. Top-level world objects have neither.
    public readonly struct RuntimeObjectKey : IEquatable<RuntimeObjectKey>
    {
        private readonly Guid[] instancePath;
        public Guid VersionId { get; }
        public Guid LocalObjectId { get; }
        public IReadOnlyList<Guid> InstancePath =>
            Array.AsReadOnly(instancePath ?? Array.Empty<Guid>());

        private RuntimeObjectKey(Guid versionId, Guid localObjectId, Guid[] instancePath)
        {
            VersionId = versionId;
            LocalObjectId = localObjectId;
            this.instancePath = instancePath;
        }

        public static RuntimeObjectKey World(Guid objectId)
        {
            if (objectId == Guid.Empty) throw new ArgumentException("Empty world object ID.");
            return new RuntimeObjectKey(Guid.Empty, objectId, Array.Empty<Guid>());
        }

        public static RuntimeObjectKey Module(Guid versionId, Guid localObjectId,
            IEnumerable<Guid> outerToInnerInstanceIds)
        {
            if (versionId == Guid.Empty || localObjectId == Guid.Empty ||
                outerToInnerInstanceIds == null)
                throw new ArgumentException("Incomplete module runtime identity.");
            var path = new List<Guid>(outerToInnerInstanceIds);
            if (path.Count == 0) throw new ArgumentException("Module instance path is empty.");
            foreach (var id in path)
                if (id == Guid.Empty) throw new ArgumentException("Empty instance ID.");
            return new RuntimeObjectKey(versionId, localObjectId, path.ToArray());
        }

        public bool Equals(RuntimeObjectKey other)
        {
            if (VersionId != other.VersionId || LocalObjectId != other.LocalObjectId)
                return false;
            var first = instancePath ?? Array.Empty<Guid>();
            var second = other.instancePath ?? Array.Empty<Guid>();
            if (first.Length != second.Length) return false;
            for (var i = 0; i < first.Length; i++)
                if (first[i] != second[i]) return false;
            return true;
        }

        public override bool Equals(object obj) => obj is RuntimeObjectKey other && Equals(other);
        public override int GetHashCode()
        {
            var hash = VersionId.GetHashCode() * 397 ^ LocalObjectId.GetHashCode();
            foreach (var id in instancePath ?? Array.Empty<Guid>())
                hash = hash * 31 ^ id.GetHashCode();
            return hash;
        }
    }

    public readonly struct RuntimeDriverKey : IEquatable<RuntimeDriverKey>
    {
        public RuntimeObjectKey Object { get; }
        public Guid OutputPinId { get; }

        public RuntimeDriverKey(RuntimeObjectKey @object, Guid outputPinId)
        {
            if (@object.LocalObjectId == Guid.Empty)
                throw new ArgumentException("Empty runtime object identity.");
            Object = @object;
            OutputPinId = outputPinId;
        }

        public bool Equals(RuntimeDriverKey other) =>
            Object.Equals(other.Object) && OutputPinId == other.OutputPinId;
        public override bool Equals(object obj) => obj is RuntimeDriverKey other && Equals(other);
        public override int GetHashCode() => Object.GetHashCode() * 397 ^
            OutputPinId.GetHashCode();
    }
}
