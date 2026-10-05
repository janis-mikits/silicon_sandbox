# Windows wrapper parity

The Windows PowerShell verification wrapper now expresses the same built-player persistence smoke gate as the Mac shell wrapper: author one AND component, save and reopen the actual V1 world file in repository-local verification storage, quit normally, then require a separate exit autosave. It already pinned Unity 6000.3.24f1, ran Edit/Play tests, checked XML, built the native player and checked startup. Both wrappers now use their local project path and repository-local test storage.

Changed files: `scripts/verify-unity.ps1`, the current-status paragraph of `first-playable-implementation-plan.md`, and this report. `git diff --check` was run. The PowerShell wrapper and Windows Unity player **have not been executed** because no Windows machine or licensed runner is available; the user temporarily waived Windows verification. CI remains manual-dispatch only and has not run. This change is design parity, not a Windows pass.
