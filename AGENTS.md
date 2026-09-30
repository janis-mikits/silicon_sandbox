# SiliconSandbox agent instructions

## Scope and access

- Work only on the task the user has requested. Read only the repository files relevant to that task, starting with `INDEX.md` and its listed dependencies. Edit only files needed for the requested result.
- Treat this Git repository as the workspace boundary. Do not inspect, search, copy, or modify files in parent folders, other repositories, home directories, external drives, or application/account data unless the user has explicitly authorized that access for the current task. If it seems necessary, explain the exact path or service, purpose, and intended operation, then ask first.
- Do not access a network service, external account, or private repository unless the current request explicitly authorizes that target. Ask before adding a dependency, installing software, changing Unity versions, connecting another service, or sending project data outside this repository.
- Do not read or expose credentials, tokens, private keys, or unrelated personal files. If such material appears unexpectedly, stop handling it and tell the user what needs attention without reproducing its contents.
- Do not delete or replace project work, rewrite Git history, force-push, publish, or change repository visibility without explicit authorization. Ask before pushing or opening a pull request unless the current request already authorizes it.
- Do not delegate work to other agents unless the user has requested delegation for the task. When authorized, give agents bounded assignments and coordinate their file ownership.
- Agents may inspect and edit files under `UnityProject/` only when the user has requested Unity project work. Do not create game code as part of a documentation or planning task.

## Project authority and decisions

- `README.md` defines the source-of-truth hierarchy. The operative game specification is in `docs/`; use `INDEX.md` to find the relevant files. Historical research and the source DOCX do not override the canonical specification.
- Do not silently choose behavior when the specification is missing, ambiguous, or contradictory. Ask the user with the affected rule, concrete options, and a recommendation before proceeding with dependent work. The same applies to changes in scope, player-visible behavior, saved data, public interfaces, dependencies, and material performance or platform tradeoffs.
- Record an approved game-design decision in the relevant canonical document. Update `INDEX.md` if document responsibilities or routing change. Keep routine code-level choices small and explain consequential choices in the task report.
- Do not modify the original `SiliconSandbox_Design_Document_Revised.docx` unless the user specifically requests it.

## Implementation and verification

- Build toward the first playable defined in `docs/delivery-and-acceptance.md`. Keep authored data, the C# four-state simulator, persistence, and Unity presentation separated as required by the canonical documents.
- Before coding a feature, identify the specification rule and an independent expected-result test. Add or update appropriate unit, integration, and Play Mode tests with the feature. Use the first-playable internal correctness matrix as a coverage checklist; do not use the implementation itself as the expected-result oracle.
- Run the relevant tests and build checks before reporting a feature complete. Report the exact commands, results, files changed, assumptions, remaining risks, and any verification that could not run. Never claim a test passed if it was not executed.
- Once `UnityProject/` exists, maintain a repeatable local test command and a CI workflow that runs the same checks. Do not claim CI is active or passing until a real run confirms it.
- Keep changes small and reviewable. Coordinate agents around explicit file ownership and interfaces; do not let agents concurrently edit shared Unity scenes, Project Settings, save schemas, or common contracts. Integrate and rerun tests after combining work.
