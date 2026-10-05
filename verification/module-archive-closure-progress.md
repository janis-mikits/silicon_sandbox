# Exact module archive closure progress

The repository now selects fixed module versions for a healthy V1 world archive from placed instances and module items in the 36-slot inventory. It includes exact child dependencies, sorts by version ID, and rejects missing exact versions, a mismatched family ID, or a recursive dependency. A version merely available in the wider library stays out of this world's archive.

Independent fixture expectations: one placed version plus a distinct inventory version produce exactly those two entries; a third unused version is excluded. A missing placed version or a mismatched inventory family fails before any archive can be published. The offline repository-local Edit Mode suite passed **168 tests, zero failed, zero ignored** with the command in [V1 manifest writer progress](v1-manifest-writer-progress.md). This is not a Unity Test Runner result.

This selector is for healthy saves. Missing or damaged module recovery on load still needs the strict V1 JSON reader, exact global-copy comparison, and placeholder handling. The currently supported first-playable module model has no nested child instances, so the recursive closure path is future-facing and not yet exercised by a real nested design.
