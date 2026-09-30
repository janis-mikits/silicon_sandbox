# SiliconSandbox canonical design knowledge base

This directory is the durable specification for the SiliconSandbox game. The [INDEX](INDEX.md) routes tasks to the relevant subsystem files. The [conversion manifest](conversion-manifest.md) records how the authoritative revised DOCX was migrated; it is a migration audit, not an additional source of game rules.

## Source of truth

1. The user's newest explicit instruction.
2. The canonical Markdown specification in `docs/`.
3. Other project files and durable records.
4. Retrieved information from previous chats.

Previous-chat retrieval may provide context but must not silently override the canonical specification. If two canonical files appear inconsistent, flag the inconsistency instead of choosing one. The original DOCX remains the migration source; the Markdown files are the working source of truth after conversion. Within the migrated content, preserve its status convention: unqualified operative rules, proposed items, open decisions, and future directions have different statuses. The [current research backlog](docs/research-backlog.md) lists unanswered research only; the [original backlog archive](docs/original-research-backlog.md) and revision history preserve provenance. None of these reference records supersedes the operative subsystem files.

## At the beginning of a task

1. Read [INDEX.md](INDEX.md) first.
2. Determine which files are relevant to the current task.
3. Load only those files plus dependencies identified by the index.
4. Do not assume chat history contains the authoritative project state.
5. Treat the canonical Markdown files as the source of truth.

## During a task

1. Consult additional files when the task crosses system boundaries.
2. Check cross-references before making assumptions about another subsystem.
3. Identify conflicts between the requested change and existing specifications.
4. Do not silently change established design decisions.
5. If the user's new instruction intentionally changes the design, treat it as superseding the old specification for that decision.

## After a design decision changes

Update the appropriate canonical Markdown file so the decision persists outside the conversation. Narrow or remove the corresponding question in the [current research backlog](docs/research-backlog.md) when research or a decision answers it. Update [INDEX.md](INDEX.md) when the change affects file responsibilities, topic routing, dependencies, terminology, or document structure. Keep the durable project state in files rather than relying on retrieval from old chats.
