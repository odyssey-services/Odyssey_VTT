# Odyssey VTT - Active Documentation Baseline

**Document:** `ACTIVE_DOCUMENTATION_BASELINE_Odyssey_VTT_v2.0.md`  
**Version:** 2.0  
**Date:** 12 August 2026  
**Status:** Active authority register

**Material change v2.0:** Accepted `TECHNICAL_DEVELOPMENT_BASELINE_Odyssey_VTT_v0.5.md`. Under the current owner decision, Unity Personal is the available Unity license, a dedicated isolated self-hosted runner is unavailable, and a paid serial or Unity Licensing Server is unavailable. GitHub Actions remain the CI provider for no-secret automated gates, but Unity Editor execution is not approved for GitHub Actions. Unity validation is a mandatory local merge gate using Unity `6000.4.0f1 (8cf496087c8f)` and `scripts/test-unity.ps1`.

---

# 1. Purpose

This file is the single current active documentation authority register for development tasks. Historical handoff and changelog files are not requirements sources unless a task explicitly names them.

# 2. Priority Order

When sources conflict, apply this order:

1. Latest explicit product owner decision in the current task.
2. This Active Documentation Baseline.
3. Accepted ADR for the technical question.
4. Technical Development Baseline for repository and Stage 0-1 organization.
5. `AGENTS.md` as operational summary.
6. `PLANS.md` as planning and ExecPlan operating contract.
7. `docs/tasks/TASK_TEMPLATE.md` and the active task contract.
8. Specialized subsystem contract.
9. Product Requirements.
10. MVP Scope.
11. Domain Model.
12. Project Vision.
13. Roadmap.
14. Test Strategy.
15. Changelog, handoff, and LegacyReference only as history or technical evidence.

# 3. Active Normative Documents

```text
00_Project_Vision_Odyssey_VTT_v0.11.md
01_Product_Requirements_Odyssey_VTT_v0.14.md
02_MVP_Scope_Odyssey_VTT_v0.10.md
03_Domain_Model_Odyssey_VTT_v0.25.md
04_Odyssey_Rules_Engine_Odyssey_VTT_v0.6.md
05_Persistence_Odyssey_VTT_v0.8.md
06_Networking_and_Session_Sync_Odyssey_VTT_v0.8.md
07_Permissions_Odyssey_VTT_v0.7.md
08_Scenes_And_Board_Odyssey_VTT_v0.5.md
09_Dice_And_Game_Log_Odyssey_VTT_v0.3.md
10_Characters_And_Progression_Odyssey_VTT_v0.2.md
11_Content_Block_System_Odyssey_VTT_v0.1.md
12_Combat_And_Actions_Odyssey_VTT_v0.1.md
13_Audio_System_Odyssey_VTT_v0.3.md
15_Legacy_Prototype_Reference_Odyssey_VTT_v0.1.md
16_Test_Strategy_Odyssey_VTT_v0.1.md
17_Roadmap_Odyssey_VTT_v0.11.md
TECHNICAL_DEVELOPMENT_BASELINE_Odyssey_VTT_v0.5.md
AGENTS.md
PLANS.md
docs/tasks/TASK_TEMPLATE.md
docs/tasks/SLICE-00_BACKLOG.md
docs/tasks/active/ODY-S00-000_SLICE_00_Technical_Skeleton.md
docs/tasks/active/ODY-S00-009_Windows_Development_Build_and_Player_Smoke.md
docs/plans/active/ODY-S00-000_SLICE_00_Technical_Skeleton.md
docs/adr/ADR-001_Module_Boundaries_and_Dependency_Direction_v1.0.md
docs/adr/ADR-002_Command_and_Domain_Event_Model_v1.0.md
docs/adr/ADR-003_Serialization_Strategy_v1.1.md
docs/adr/ADR-004_Result_and_Error_Model_v1.0.md
docs/adr/ADR-005_Dependency_Composition_v1.0.md
docs/adr/ADR-006_Test_Project_Structure_and_Dual_Unity_DotNet_Compilation_v1.0.md
docs/adr/ADR-007_Versioning_and_Build_Identity_v1.0.md
docs/adr/ADR-008_Deterministic_Clock_and_RNG_v1.0.md
docs/adr/ADR-009_Unity_Project_and_Build_Baseline_v1.1.md
docs/adr/ADR-010_Logging_Diagnostics_and_Redaction_v1.1.md
```

# 4. Active Technical Authorities

`TECHNICAL_DEVELOPMENT_BASELINE_Odyssey_VTT_v0.5.md` is the active technical baseline for the private authoritative repository, Unity 6.4 Update release HDRP/UI Toolkit/Input System project, CI model, Unity Personal licensing constraint, and Codex workflow. It preserves v0.4 except for the CI/Unity licensing decision recorded in v0.5.

`docs/adr/ADR-003_Serialization_Strategy_v1.1.md` is the active authority for JSON profiles, explicit versioned DTOs, canonical serialization, command fingerprints, event payload hashes/upcasting, parser limits, AOT/IL2CPP compatibility, and the approved release-critical JSON codec mechanism.

`docs/adr/ADR-010_Logging_Diagnostics_and_Redaction_v1.1.md` is the active authority for logging, diagnostics, and redaction. It preserves ADR-010 v1.0 semantics while routing `LogEventV1` JSON through ADR-003 v1.1 explicit DiagnosticJson codecs.

`docs/tasks/completed/ODY-S00-007_Serialization_and_AOT_Compatibility_Spike.md` is complete through owner-merged PR #11, merged head `555c7adbead725cf84658588d3777a3827f39dd6`, merge commit `88382217a1053fbe5eb631024063800f45e69926`.

`docs/tasks/completed/ODY-S00-008_Fast_CI_and_Build_Identity.md` is complete through owner-merged PR #12 and corrective PR #13. Corrective feature HEAD `43225c9f753903c7678704891c22d5e98676fb3e` entered `main` as merge commit `1e6483aee42c53595bbc4758dff0a9a696345661`; main push CI run `31799960601` passed all four required no-secret checks and Development provenance checksum validation passed.

`docs/tasks/active/ODY-S00-009_Windows_Development_Build_and_Player_Smoke.md` is the current child task. It is `Ready` after owner-approved `TC-PLAYER-001` through `TC-PLAYER-010` catalog mapping for the mandatory ADR-009 Windows build and Player smoke scenarios. Implementation has not started.

# 5. CI and Unity Validation Authority

Automated GitHub Actions gates for ODY-S00-008 are limited to no-secret checks:

- repository policy;
- formatting;
- test structure and architecture;
- source, toolchain, and package validation;
- .NET restore, build, and tests;
- BuildIdentity and provenance validation.

Mandatory local Unity merge validation remains separate from GitHub Actions:

- exact Unity `6000.4.0f1 (8cf496087c8f)`;
- `scripts/test-unity.ps1`;
- Unity compile;
- EditMode;
- PlayMode when invoked by the repository entry-point script;
- clean worktree after generated drift is removed;
- evidence recorded in the task before Draft PR readiness and independently reviewed before owner merge.

GitHub Actions must not claim that Unity CI exists or that Unity compiled unless an owner-approved future amendment provides a secure automated Unity path. Static Unity project validation is allowed, but it is not Unity compile evidence.

# 6. Non-Normative Files

The following are not requirements sources unless explicitly named by a task:

- `DOCUMENTATION_ALIGNMENT_CHANGELOG_*`;
- handoff files;
- `LegacyReference/**`;
- templates or placeholder evidence;
- private product documentation not included in the task bundle.

# 7. Historical Baselines

`ACTIVE_DOCUMENTATION_BASELINE_Odyssey_VTT_v1.9.md`, `TECHNICAL_DEVELOPMENT_BASELINE_Odyssey_VTT_v0.4.md`, earlier active baselines, earlier technical baselines, `docs/adr/ADR-003_Serialization_Strategy_v1.0.md`, and `docs/adr/ADR-010_Logging_Diagnostics_and_Redaction_v1.0.md` remain historical context. They must not be treated as current authority where they conflict with this v2.0 baseline.
