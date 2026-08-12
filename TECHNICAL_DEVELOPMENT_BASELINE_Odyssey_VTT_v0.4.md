# Odyssey VTT - Technical Development Baseline

**Document:** `TECHNICAL_DEVELOPMENT_BASELINE_Odyssey_VTT_v0.4.md`  
**Version:** 0.4  
**Date:** 12 August 2026  
**Status:** Approved baseline for M0 / M1  
**Supersedes:** `TECHNICAL_DEVELOPMENT_BASELINE_Odyssey_VTT_v0.3.md` for active technical authority. v0.3 remains historical context.

---

# 1. Purpose

This baseline preserves the repository, Unity, module, testing, CI, licensing, Windows target, HDRP, UI Toolkit, Input System, MVP, and Codex operating rules from Technical Development Baseline v0.3.

The only material technical decision changed in v0.4 is the active production JSON serializer mechanism for release-critical contracts.

# 2. Active Authority

Use this baseline together with:

- `ACTIVE_DOCUMENTATION_BASELINE_Odyssey_VTT_v1.9.md`;
- ADR-001 through ADR-010 as listed in the active baseline;
- `AGENTS.md`;
- `PLANS.md`;
- the active task contract.

# 3. Owner Decisions

All TDB-DEC decisions from v0.3 remain approved except TDB-DEC-025, which is replaced by this decision:

| ID | Decision | Status |
|---|---|---|
| TDB-DEC-025 | Production JSON contracts use explicit deterministic codecs backed by pinned Newtonsoft.Json 13.0.2 low-level streaming primitives. Automatic reflection/object serialization is prohibited. Canonical parity must pass pure .NET, Unity Mono, and Windows x64 IL2CPP. See ADR-003 v1.1. | Approved |

Approved serializer dependency baseline:

```text
Unity package: com.unity.nuget.newtonsoft-json@3.2.2
Underlying Newtonsoft product version: 13.0.2
AssemblyVersion: 13.0.0.0
Pure .NET package: Newtonsoft.Json 13.0.2
```

# 4. Unchanged Baselines

The following v0.3 decisions are unchanged:

- Unity baseline remains Unity `6000.4.0f1 (8cf496087c8f)`.
- Unity release line remains Unity 6.4 Update release / Supported release.
- HDRP remains `com.unity.render-pipelines.high-definition` `17.4.0`.
- Runtime UI remains UI Toolkit.
- Input remains the Input System package.
- Target remains Windows 10/11 x64 desktop standalone.
- Repository remains the private authoritative `odyssey-services/Odyssey_VTT`.
- Module graph remains ADR-001.
- Domain remains free of serializer, persistence, networking, Unity, and logging dependencies.
- SQLite remains the future authoritative persistence direction where approved by Persistence ADR/task scope.
- GitHub Actions/CI model, branch/PR review workflow, and no-merge-by-Codex rules remain unchanged.
- No Unity 6.5 baseline is adopted.

# 5. PR-004 Result Wording

The active PR-004 / ODY-S00-007 result is no longer System.Text.Json source-generation implementation. It is:

```text
Explicit canonical JSON codecs, invalid-input tests, and .NET / Unity Mono / Windows x64 IL2CPP parity evidence using the ADR-003 v1.1 Newtonsoft streaming implementation strategy.
```

# 6. Normative Effect

From activation in `ACTIVE_DOCUMENTATION_BASELINE`, active work must use Technical Development Baseline v0.4. v0.3 remains historical and must not be cited as current authority when it conflicts with ADR-003 v1.1 or TDB-DEC-025 above.
