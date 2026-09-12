# ODY-S05-402 — Report supported migration incompatibilities

**Status:** Active
**Owner:** Codex
**Branch:** codex/ody-s05-402-migration-blocking
**Pull request:** Not opened
**Last updated:** 2026-09-12 UTC

## 1. Purpose and user-visible outcome

Identify occupied equipment slots lost by a target definition and stacks exceeding its capacity before future confirmation.

## 2. Task contract

Contract: docs/tasks/active/ODY-S05-402_Migration_Blocking_Incompatibility_Rules.md. Authorities: Active Baseline v2.2, Technical Baseline v0.5, ADR-001/003/004/006, ADR-027 sections 10/12/14, backlog section 13. Goal, acceptance criteria, allowed paths and required commands are listed in contract sections 5, 9 and 10. No apply, persistence, preview changes or missing runtime mechanics.

## 3. Current state

Origin/main 4ecf6a6 includes 401. Maximum inventory test ID is 168. Existing codecs and occupied slot records support two checks only. Null capacity is valid only for non-stackable definitions. No loaded-ammo/damage/custom/hidden runtime representation was found in Packages.

## 4. Proposed approach

Add typed issue code, issue with InventoryId and InventoryItemRef, and immutable report to the existing file. Decode target once using its actual type. Enforce matching preview target and supplied equipment membership. Compare equipment slots ordinally; evaluate capacity per group member, never group total. Caller fetches GetEquippedEntry for each affected instance before calling; no I/O in calculation. No transaction or persisted compatibility change. Fixed descriptions are host-side findings, not transport DTOs.

## 5. Milestones

- [x] M1: verify baseline/runtime gaps and record decisions before coding.
- [x] M2: implement two checks and registered tests 169-178, preserve preview and guard forbidden arrays.
- [ ] M3: run available required checks, review complete diff, publish Draft PR and update backlog.

## 6. Progress log

- 2026-09-12 UTC — fetched main and created isolated task worktree; inspected preview, typed definitions/codecs, repository queries and runtime fields.

## 7. Decisions

- 2026-09-12 — Separate result and pure caller-supplied equipment list, per owner scope. No repository query added.
- 2026-09-12 — Loaded ammo, armor damage, custom state and hidden mechanics explicitly remain unsupported until runtime representations exist; extend this method in future attack/state tasks. 403/404 consume only documented partial coverage.
- 2026-09-12 — Invalid target decoding is a precondition exception, not an incompatibility. Null capacity does not block. Match exact target ref to prevent accidental checks against another version.

## 8. Discoveries and deviations

Initial sandbox checks failed because MSBuild could not access the installed SDK and Git rejected worktree ownership; authorized execution outside the sandbox resolved those environment failures. First compilation found ambiguous NUnit Assert.Throws overloads; explicit Action delegates resolve them. Codec error construction requires a valid CorrelationId; use a local deterministic sentinel whose errors are discarded and translated to targetDefinition preconditions, tested by ParamName. CatalogValidationIssue uses a localization key, not Description; this task's explicit API calls for Description, supplied as fixed text. verify-docs.ps1 and test-all.ps1 do not exist. No scope expansion.

## 9. Validation and acceptance evidence

verify-format and verify-repository passed. Full solution test-fast passed: 806 tests, zero failures/skips; build zero warnings/errors. Persistence 520, Unit 136, Architecture 2, Domain 80, Networking 67, Contracts 1. TRX evidence under Logs/ODY-S00-008/dotnet/. verify-docs and test-all scripts are absent and not run. Exact preview class comparison against origin/main passed; guard forbidden arrays and SQLite/interfaces unchanged. No Unity/IL2CPP execution claimed; existing local Unity merge gate remains with owner review.

## 10. Recovery and rollback

Revert task commit. No authoritative state or persisted format changes. Original checkout preserved.

## 11. Open questions and blockers

None for implementation. Missing documentation script is an environment/repository limitation to report.

## 12. Outcome and follow-up

Pending. Future state representations must extend the rule method; 403 owns confirm/apply, 404 owns integration.
