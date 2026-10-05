using System;
using System.Collections.Generic;
using System.IO;
using SiliconSandbox.Authoring;

namespace SiliconSandbox.Persistence
{
    // Select the exact fixed definitions a healthy V1 world archive must
    // embed. A damaged/missing definition is handled by the separate load
    // recovery path, never by silently substituting a newer library version.
    public static class WorldModuleClosure
    {
        public static IReadOnlyList<OneBitModuleVersion> RequiredFor(
            WorldSaveSnapshot snapshot)
        {
            if (snapshot == null) throw new ArgumentNullException(nameof(snapshot));
            var required = new Dictionary<Guid, OneBitModuleVersion>();
            var visiting = new HashSet<Guid>();
            foreach (var instance in snapshot.Design.Modules)
                Visit(instance.VersionId, instance.FamilyId);
            foreach (var slot in snapshot.InventorySlots)
                if (slot != null && slot.Kind == SavedInventoryKind.ModuleVersion)
                    Visit(slot.VersionId, slot.FamilyId);
            var result = new List<OneBitModuleVersion>(required.Values);
            result.Sort((first, second) =>
                first.VersionId.CompareTo(second.VersionId));
            return result.AsReadOnly();

            void Visit(Guid versionId, Guid expectedFamilyId)
            {
                if (required.TryGetValue(versionId, out var already))
                {
                    if (already.FamilyId != expectedFamilyId)
                        throw new InvalidDataException(
                            "Module reference disagrees with exact version family.");
                    return;
                }
                if (!snapshot.ModuleVersions.TryGetValue(versionId, out var version))
                    throw new InvalidDataException(
                        "Exact referenced module version is unavailable for saving.");
                if (version.FamilyId != expectedFamilyId ||
                    !visiting.Add(versionId))
                    throw new InvalidDataException(
                        "Module family mismatch or recursive version dependency.");
                foreach (var child in version.ChildVersionIds)
                {
                    if (!snapshot.ModuleVersions.TryGetValue(child,
                            out var childVersion))
                        throw new InvalidDataException(
                            "Exact child module version is unavailable for saving.");
                    Visit(child, childVersion.FamilyId);
                }
                visiting.Remove(versionId);
                required.Add(versionId, version);
            }
        }
    }
}
