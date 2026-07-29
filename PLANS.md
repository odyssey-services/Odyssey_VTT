# Odyssey VTT — Execution Plans for Codex

This file defines how Codex plans and tracks implementation work in the Odyssey VTT repository. It applies to the entire repository together with `AGENTS.md` and `docs/tasks/TASK_TEMPLATE.md`.

A plan is an execution tool, not a substitute for the task contract, requirements, ADRs, code, or tests. The filled task defines what must be delivered; an ExecPlan defines how complex work will be executed. Keep planning proportional to risk.

## 1. Planning modes

Use one of two modes.

### 1.1 Brief plan

A brief plan is sufficient when all of the following are true:

- the change is contained in one production module or documentation area;
- it does not change a public contract, persisted format, protocol, permissions, redaction, dependency graph, Unity/package version, or build pipeline;
- it has one clear implementation path;
- it can be completed and validated in one focused pull request;
- no migration or recovery procedure is required.

A brief plan may live in the task response or pull request description. It should state:

1. files or areas to inspect;
2. intended change;
3. tests or validation commands;
4. any explicit non-goals.

### 1.2 ExecPlan

Create a repository plan when any trigger in `AGENTS.md` applies, or when the task:

- spans multiple milestones or pull requests;
- changes more than one production module;
- introduces or changes an Application port, public DTO, event, command, schema, protocol, manifest, package, build profile, or migration;
- affects authoritative state, persistence, networking, security, permissions, hidden information, redaction, diagnostics, time, or randomness;
- has meaningful data-loss, compatibility, rollback, release, or operational risk;
- requires investigation before the implementation path is known;
- is expected to be resumed by another agent or developer;
- cannot be safely understood from a short task description alone.

Store active plans under:

```text
docs/plans/active/<TaskId>_<Short_Name>.md
```

Move a completed plan without rewriting its history to:

```text
docs/plans/completed/<TaskId>_<Short_Name>.md
```

Do not create an ExecPlan for trivial edits merely to satisfy process.

## 2. Core properties of an ExecPlan

Every ExecPlan must be:

- **Self-contained:** a developer with the repository and named public authorities can continue the work without access to the original chat.
- **Living:** update it as facts, progress, decisions, and validation evidence change.
- **Outcome-oriented:** describe observable behavior and acceptance, not only files and classes.
- **Concrete:** name relevant modules, paths, commands, test IDs, contracts, and artifacts.
- **Honest:** distinguish completed work, assumptions, unresolved questions, failed attempts, and unrun checks.
- **Scope-controlled:** record non-goals and do not silently absorb unrelated cleanup.
- **Safe for the public repository:** never copy private product documentation, secrets, private paths, personal data, or hidden campaign content into a plan.

An ExecPlan must not rely on phrases such as “as discussed earlier,” “use the usual approach,” or “finish the remaining work” without defining the referenced work.

## 3. Source and privacy rules

Every ExecPlan must reference its filled task contract under `docs/tasks/active/`. If the task contract is incomplete or not Ready, do not use the plan to invent missing scope, acceptance, or product behavior.

At the top of the plan, list the authorities needed for execution:

- applicable ADRs;
- Technical Development Baseline;
- public subsystem documents;
- Requirement IDs and acceptance criteria supplied by the task.

When a task uses private documentation:

- cite only the task-safe Requirement IDs or a sanitized summary;
- do not paste private passages into the plan;
- do not mention local private repository paths;
- do not place private content in commits, test data, snapshots, logs, generated artifacts, or pull request text.

If the task bundle is insufficient to make a product decision, record the missing decision under **Open Questions** and stop before inventing behavior.

## 4. Required ExecPlan structure

Use the following sections. Sections may be concise, but they must remain present so the plan can be resumed reliably.

```markdown
# <Task ID> — <Outcome-oriented title>

**Status:** Draft | Active | Blocked | Completed | Superseded
**Owner:** <agent/developer or Unassigned>
**Branch:** <branch name or Not created>
**Pull request:** <URL/number or Not opened>
**Last updated:** <UTC date and time>

## 1. Purpose and user-visible outcome

Explain what becomes possible or correct when the plan is complete.

## 2. Task contract

- Goal:
- Acceptance criteria:
- Requirement IDs:
- In scope:
- Out of scope:
- Required authorities:
- Required validation commands:

## 3. Current state

Describe the relevant repository state, existing behavior, important paths,
and verified constraints. Distinguish observed facts from assumptions.

## 4. Proposed approach

Explain the implementation shape, module ownership, data flow, transaction
boundaries, compatibility implications, and why this approach follows the ADRs.

## 5. Milestones

### M1 — <verifiable milestone>

- [ ] <specific action or result>
- [ ] <specific test or evidence>

### M2 — <verifiable milestone>

- [ ] ...

## 6. Progress log

- <UTC timestamp> — <completed action and evidence>

## 7. Decisions

- <UTC date> — Decision: <decision>. Rationale: <why>. Authority: <ADR/task>.

## 8. Discoveries and deviations

Record unexpected repository facts, failed approaches, scope changes,
performance findings, compatibility issues, and why the plan changed.

## 9. Validation and acceptance evidence

List commands, results, test IDs, produced artifacts, and any checks not run.

## 10. Recovery and rollback

Explain safe retry, cleanup, rollback, migration recovery, or state restoration.
Write `Not applicable` only when that is genuinely true.

## 11. Open questions and blockers

List unresolved items, owner decisions, or external dependencies.

## 12. Outcome and follow-up

At completion, summarize delivered behavior, remaining limitations,
follow-up tasks, and the final pull request or patch.
```

## 5. Writing good milestones

A milestone must leave the repository in a verifiable state. Prefer vertical, observable increments over layers of untestable scaffolding.

Good milestone:

```text
M2 — A duplicate MoveToken command returns the stored result without a second event.
Evidence: CMD-IDEMP-003 passes in pure .NET and the SQLite integration test.
```

Weak milestone:

```text
M2 — Implement services and helpers.
```

Each milestone should state:

- the behavior or artifact it delivers;
- the modules and paths affected;
- the validation that proves completion;
- any compatibility or migration boundary.

Do not mark a milestone complete because files exist. Mark it complete when its acceptance evidence exists.

## 6. Progress and history rules

The plan is a durable execution record.

- Update **Last updated** whenever meaningful work changes the plan.
- Add progress entries with UTC timestamps.
- Check an item only after its stated evidence is available.
- Do not delete failed approaches or discoveries that affect future decisions; summarize them under **Discoveries and deviations**.
- Do not rewrite earlier decisions to make the work appear linear.
- When a decision changes, add a new decision entry and identify what superseded the old one.
- If work stops, leave the plan in a state another developer can resume.
- If the task is abandoned or replaced, set status to `Superseded` and link the replacement plan.

## 7. Investigation and spikes

An investigation may be a milestone, but it must have a bounded question and a deliverable.

Examples:

- confirm `System.Text.Json` source generation under Windows IL2CPP;
- compare two SQLite providers against licensing and AOT requirements;
- measure HDRP board rendering at the target profile;
- verify a relay SDK contract without integrating it into production.

A spike must define:

- the question;
- the smallest experiment;
- the decision criteria;
- files that may be temporary;
- what will be removed or retained;
- the resulting ADR, task update, or implementation decision.

Do not merge exploratory production dependencies or architecture by accident. A successful spike is evidence, not automatic approval.

## 8. Validation planning

Plan validation before implementation, not after it.

For every acceptance criterion, identify at least one evidence source:

- pure .NET test;
- Unity EditMode test;
- Unity PlayMode test;
- Persistence or Networking integration test;
- architecture check;
- Windows Player smoke;
- migration rehearsal;
- checksum or artifact inspection;
- documented manual verification when automation is not yet reasonable.

Use canonical scripts from `AGENTS.md`. Record exact commands and results. If a script does not exist during repository scaffolding, the plan must identify the milestone that creates it and must not claim it was run.

A test that only asserts implementation details is not sufficient evidence for a user-visible acceptance criterion.

## 9. Change control

Update the plan before implementing a material change when work discovers:

- a new module dependency;
- a new third-party dependency or license obligation;
- a public contract or persisted-format change;
- a migration or destructive operation;
- a new security, privacy, permissions, or hidden-data concern;
- a Unity/package/editor version change;
- a scope increase beyond the task contract;
- a contradiction between active authorities.

An accepted plan does not authorize architecture that conflicts with an ADR. Create or amend an ADR first when required.

Stop and request an owner decision when:

- product behavior is ambiguous and affects users;
- two active authorities cannot be reconciled;
- acceptance requires expanding MVP;
- a paid, proprietary, copyleft, or unclear dependency appears necessary;
- data loss or irreversible migration cannot be ruled out;
- private material would need to enter the public repository.

## 10. Git and pull request use

- One ExecPlan may cover several pull requests only when it explicitly defines the sequence and each pull request leaves a safe state.
- Every pull request references the relevant plan and milestone.
- Update plan progress in the same branch as the related implementation when practical.
- Do not merge an incomplete milestone merely to hide broken intermediate state unless the plan explicitly defines a safe scaffold PR.
- The final plan state must list the exact pull request(s), validation evidence, migrations, and remaining limitations.
- Codex never merges the pull request.

## 11. Initial repository scaffolding exception

During `SLICE-00`, some canonical scripts, projects, scenes, and CI jobs do not exist yet. Plans may create them incrementally.

For each missing prerequisite, the plan must state:

- which milestone creates it;
- what temporary validation is possible before it exists;
- what final canonical command replaces the temporary check;
- when the temporary mechanism will be removed.

Do not fabricate green evidence for a command that has not been implemented.

## 12. Completion criteria for an ExecPlan

An ExecPlan may be marked `Completed` only when:

1. every acceptance criterion is satisfied or explicitly deferred by the owner;
2. all milestones have evidence;
3. required tests and repository commands are recorded honestly;
4. architecture, privacy, licensing, versioning, and compatibility impacts are documented;
5. recovery or rollback is proven where relevant;
6. the complete diff was reviewed for scope and accidental files;
7. the pull request or patch is ready for owner review;
8. remaining limitations and follow-up tasks are listed;
9. no private requirement text or secrets were added to the public repository.

A completed plan is evidence of how the result was produced. It is not evidence that the owner approved or merged it.
