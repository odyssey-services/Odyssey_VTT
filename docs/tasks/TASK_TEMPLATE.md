# Odyssey VTT — Codex Task Template

Use this template for every implementation, infrastructure, migration, documentation, or investigation task that is intended to produce a repository change. Keep the task proportional to the work: concise for a small change, explicit for high-risk or cross-module work.

A filled task is an execution contract. It does not override the Active Documentation Baseline, an accepted ADR, the Technical Development Baseline, `AGENTS.md`, or `PLANS.md`.

---

# <Task ID> — <Outcome-oriented title>

**Status:** Draft | Ready | In Progress | Blocked | In Review | Done | Cancelled  
**Roadmap stage / slice:** <for example SLICE-00>  
**Owner:** <developer/agent or Unassigned>  
**Requested by:** <product owner / maintainer>  
**Branch:** <branch name or Not created>  
**Pull request:** <URL/number or Not opened>  
**ExecPlan:** <path or Not required>  
**Created:** <YYYY-MM-DD>  
**Last updated:** <YYYY-MM-DD HH:MM UTC>

## 1. Goal

State the single observable result of this task in one or two sentences.

Good:

> Create a compilable Domain and Rules module skeleton that is shared by Unity and the pure .NET build and whose dependency direction is automatically enforced.

Avoid:

> Work on architecture, add folders, improve tests, and clean up related code.

## 2. Why this task exists

Explain the immediate reason and what later work this task enables or protects. Do not repeat the goal. Keep this section short.

- Problem or dependency being addressed:
- Value or risk reduction:
- Blocking or enabling relationship:

## 3. Authorities and requirement references

List only the sources needed to execute and review this task.

### Required authorities

- `ACTIVE_DOCUMENTATION_BASELINE_Odyssey_VTT_*.md`
- `AGENTS.md`
- `PLANS.md`
- <applicable ADR path and exact sections>
- <Technical Development Baseline sections>
- <public subsystem document and exact sections>

### Requirement and test IDs

- Requirement IDs: <IDs or None>
- Existing test IDs: <IDs or None>
- New test IDs to introduce: <IDs or None>

### Task-safe private context

Use only sanitized summaries or approved IDs. Never paste private documentation, local private paths, secrets, personal data, or hidden campaign content into this file.

- Approved summary / references: <summary or None>

## 4. Verified current state

Record facts observed in the repository before implementation. Name relevant files, projects, assemblies, scripts, behavior, or missing components. Distinguish verified facts from assumptions.

### Verified facts

- <fact and evidence>

### Assumptions

- <assumption, why it is safe, and how it will be verified>

Use `None` when there are no assumptions. Do not present an assumption as an existing repository fact.

## 5. Scope

### In scope

- <specific behavior, module, path, contract, migration, test, or artifact>

### Out of scope

- <related work that must not be implemented in this task>

### Allowed paths

Prefer explicit paths or path groups when the repository already exists.

```text
<path>
<path>
```

### Paths requiring explicit approval before editing

```text
<path or None>
```

Unplanned adjacent cleanup is out of scope unless it is required to satisfy an acceptance criterion. Record necessary scope changes before implementing them.

## 6. Technical constraints

Copy only constraints that are especially relevant to this task and link them to their authority. Do not paraphrase an ADR into a conflicting rule.

- Module ownership and dependency direction:
- Authoritative-state and transaction boundary:
- Serialization / compatibility boundary:
- Time / RNG rule:
- Unity / thread / lifetime rule:
- Dependency / licensing rule:
- Security / privacy / redaction rule:
- Performance or platform constraint:
- Other:

Use `Not applicable` where appropriate.

## 7. Expected behavior

Describe observable behavior. Use scenarios when the task changes behavior; use invariant checks for infrastructure tasks.

### Scenario 1 — <name>

**Given** <initial state>  
**When** <action>  
**Then** <observable outcome>

### Scenario 2 — <name or remove when not needed>

**Given** <initial state>  
**When** <action>  
**Then** <observable outcome>

### Required invariants

- <condition that must always remain true>

## 8. Deliverables

List concrete outputs expected from the task. Do not require speculative files whose need has not been established.

- Production code:
- Tests:
- Scripts / CI:
- Configuration:
- Documentation:
- Generated evidence or build artifacts:
- Migration / recovery material:

Use `None` for non-applicable categories.

## 9. Acceptance criteria

Every criterion must be objective and reviewable. Use numbered items. Avoid subjective wording such as “clean,” “good,” “proper,” or “production-ready” unless followed by measurable conditions.

1. <observable criterion>
2. <observable criterion>
3. <negative or boundary criterion>
4. <compatibility / persistence / security criterion when applicable>
5. <validation criterion>

The task is not complete while any mandatory criterion is unverified, failed, or silently deferred.

## 10. Tests and validation

### Required automated tests

| Test ID | Layer / runner | Behavior or contract proven | Required result |
|---|---|---|---|
| `<ID>` | `<.NET / EditMode / PlayMode / Player / script>` | `<what it proves>` | Pass |

Use `None` only when the task genuinely cannot be meaningfully automated, and explain the replacement evidence.

### Required commands

Commands must be repository entry points when available. Do not invent successful results for scripts that do not exist or were not run.

```powershell
<command>
<command>
```

### Manual validation

- <step and expected result, or None>

### Required environments / profiles

- OS / architecture:
- Unity editor or Player profile:
- Scripting backend:
- Network topology or database fixture:
- Other:

### Validation not required by this task

Explicitly list major checks that are intentionally outside scope, for example Windows IL2CPP, PlayMode, migration rehearsal, or performance profiling. This prevents an unrun check from being mistaken for a successful check.

- <check and reason>

## 11. Compatibility, migration, and rollback

Complete this section when the task changes persisted state, a public contract, protocol, package, Unity version, build identity, or deployable artifact. Otherwise write `Not applicable`.

- Compatibility impact:
- Version fields affected:
- Migration or upcaster:
- Forward / backward behavior:
- Rollback method:
- Data-loss risk and protection:
- Recovery rehearsal required:

A task must not silently change application, schema, contract, protocol, ruleset, package, or documentation versions.

## 12. Dependencies and licensing

### New or changed dependencies

| Dependency | Version / source | Purpose | License | Approved by |
|---|---|---|---|---|
| None | — | — | — | — |

For every new dependency, action, executable, or downloaded tool, record approval and license compatibility before use. GPL, AGPL, unclear, unverified, or unapproved dependencies are blocked.

## 13. Security, privacy, and hidden information

Complete when the task touches logs, diagnostics, permissions, networking, imports, files, secrets, user data, hidden GM information, or audience projections. Otherwise write `Not applicable`.

- Data classes handled:
- Trust boundaries:
- Authorization / audience checks:
- Redaction requirements:
- Log-safe fields:
- Abuse / malformed input limits:
- Security tests:

## 14. Planning and execution mode

- Planning mode: `Brief plan` | `ExecPlan`
- Reason for selected mode:
- ExecPlan path: <path or Not required>
- Expected pull request count: <number>
- Milestone or sequencing constraints:

If `PLANS.md` requires an ExecPlan, create or update it before production code changes. The task defines the contract; the ExecPlan defines how a complex contract will be executed.

## 15. Documentation and versioning impact

- Documents that must change:
- Documents that must not change:
- Application version change: Yes / No — <reason>
- Schema / format / contract / protocol / ruleset version change: <details or None>
- Documentation version changes: <details or None>
- Changelog or release-note requirement:

Changing only a link to the current authority, dependency reference, manifest hash, or next-document pointer does not by itself justify increasing that document's version.

## 16. Definition of Done

The task is done only when all applicable items are true:

- [ ] Goal is achieved without unapproved scope expansion.
- [ ] All acceptance criteria are satisfied.
- [ ] Required automated tests pass.
- [ ] Required manual checks are completed.
- [ ] Required commands and their real results are recorded.
- [ ] Architecture and dependency rules remain valid.
- [ ] Security, privacy, redaction, and audience rules are verified where applicable.
- [ ] Compatibility, migration, rollback, and versioning obligations are complete where applicable.
- [ ] No unapproved dependency, tool, GitHub Action, or license was introduced.
- [ ] Documentation is updated only where materially required.
- [ ] Codex/developer performed a self-review against this task and `AGENTS.md`.
- [ ] Pull request explains changes, evidence, limitations, and follow-up work.
- [ ] Product owner or authorized reviewer completes the required review; Codex does not merge into `main`.

## 17. Completion evidence

Fill this section with real results before moving the task to `Done`.

### Changed files / areas

- <path and purpose>

### Validation results

| Command / check | Result | Evidence / notes |
|---|---|---|
| `<command>` | Passed / Failed / Not run | `<real output summary or artifact path>` |

### Acceptance result

| Criterion | Status | Evidence |
|---|---|---|
| AC-1 | Passed / Failed / Deferred | <evidence> |

### Build and artifact evidence

- Build identity:
- Artifact path / name:
- Checksums:
- Test or quality report:

### Known limitations

- <limitation or None>

### Follow-up tasks

- <Task ID and reason, or None>

### Self-review summary

- Scope review:
- Architecture review:
- Test review:
- Security/privacy review:
- Documentation/version review:

## 18. Blockers, decisions, and change control

### Blockers

- <blocker, owner, and smallest safe next step, or None>

### Decisions made during execution

- <date> — <decision> — Authority / approval: <source>

### Approved task changes

- <date> — <change to scope or acceptance> — Approved by: <owner>

Do not erase rejected approaches, failed validation, or approved scope changes from the task history. Keep the final task understandable without the original chat.

---

## Template completion rules

1. Remove instructional examples that do not apply, but keep all numbered section headings.
2. Write `None` or `Not applicable` instead of leaving an ambiguous blank.
3. A task may be marked `Ready` only when goal, scope, authorities, acceptance criteria, validation, and required decisions are complete.
4. A task may be marked `In Progress` only after the working branch exists and the required ExecPlan is created when applicable.
5. A task may be marked `In Review` only after completion evidence is filled honestly.
6. A task may be marked `Done` only after required review and all non-deferred acceptance criteria pass.
7. Deferred work requires an explicit follow-up Task ID; it cannot disappear into prose.
8. Never mark an unrun validation command as passed.
9. Never update golden files, snapshots, manifests, or expected outputs only to make a failing test green without verifying the intended behavior.
10. Never broaden MVP scope or create a new architectural rule inside a task; request an owner decision or ADR instead.
