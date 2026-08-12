# Odyssey VTT - Technical Development Baseline

**Document:** `TECHNICAL_DEVELOPMENT_BASELINE_Odyssey_VTT_v0.5.md`  
**Version:** 0.5  
**Date:** 12 August 2026  
**Status:** Approved baseline for M0 / M1  
**Supersedes:** `TECHNICAL_DEVELOPMENT_BASELINE_Odyssey_VTT_v0.4.md` for active technical authority. v0.4 remains historical context.

---

# 1. Purpose

This baseline preserves the repository, Unity, module, testing, CI, licensing, Windows target, HDRP, UI Toolkit, Input System, MVP, and Codex operating rules from Technical Development Baseline v0.4.

The only material technical decision changed in v0.5 is the active CI/Unity licensing model for ODY-S00-008 under the current Unity Personal constraint.

# 2. Active Authority

Use this baseline together with:

- `ACTIVE_DOCUMENTATION_BASELINE_Odyssey_VTT_v2.0.md`;
- ADR-001 through ADR-010 as listed in the active baseline;
- `AGENTS.md`;
- `PLANS.md`;
- the active task contract.

# 3. Preserved Decisions

All Technical Development Baseline v0.4 decisions remain approved, including:

- ADR-003 v1.1 explicit deterministic Newtonsoft.Json streaming codecs for release-critical JSON contracts.
- Unity `6000.4.0f1 (8cf496087c8f)`.
- Unity 6.4 Update release / Supported release terminology.
- HDRP `com.unity.render-pipelines.high-definition` `17.4.0`.
- Runtime UI Toolkit and Input System.
- Windows 10/11 x64 desktop standalone target.
- Private authoritative repository `odyssey-services/Odyssey_VTT`.
- GitHub Actions as CI provider.
- Branch -> PR -> owner review -> owner merge workflow.
- Immutable full-length action SHA pinning for any approved action.
- Minimal workflow permissions.
- No secrets for fork pull requests.
- No false-green required checks.
- All existing architectural and product-scope constraints.

# 4. Owner Decision: Unity Personal CI Constraint

The product owner records the following current constraint:

```text
Unity license: Unity Personal
Dedicated isolated self-hosted runner: unavailable
Paid serial / Unity Licensing Server: unavailable
```

Accepted CI architecture under this constraint:

1. GitHub Actions run only no-secret automated gates:
   - repository policy;
   - formatting;
   - test structure and architecture;
   - source, toolchain, and package validation;
   - .NET restore, build, and tests;
   - BuildIdentity and provenance validation.
2. Unity Editor is not run in GitHub Actions under current conditions.
3. Unity validation remains a mandatory local merge gate:
   - exact Unity `6000.4.0f1 (8cf496087c8f)`;
   - `scripts/test-unity.ps1`;
   - Unity compile;
   - EditMode;
   - PlayMode when invoked by the existing repository entry-point script;
   - clean worktree after removing generated drift;
   - evidence recorded in the task and independently reviewed before Draft PR readiness.
4. If local Unity validation is absent, fails, or does not prove the exact Unity version, Draft PR readiness is not claimed, the task is not complete, and merge is not allowed.
5. The GameCI Personal `.ulf` workaround is not approved.
6. Unity credentials and license files are not added to GitHub Secrets.
7. A personal development PC is not registered as a self-hosted runner.

# 5. Normative Effect

Under Technical Development Baseline v0.5:

- The previous automated `UnityCompile` requirement on every pull request is replaced by mandatory local Unity merge evidence while the current Unity Personal constraint applies.
- GitHub Actions must not pretend Unity compiled when Unity did not actually run.
- Static Unity project validation may inspect `ProjectVersion.txt`, package locks, and Unity source/config inventory, but it must not be named or reported as `UnityCompile`.
- A GitHub check cannot substitute for the real `scripts/test-unity.ps1` local gate.
- Local Unity evidence is required for ODY-S00-008 PR readiness and owner merge.
- Returning to an automated Unity gate requires a separate owner-approved amendment when one of the following becomes available:
  - an officially supported Unity Personal CI path;
  - a paid Unity license;
  - a Unity Licensing Server;
  - a dedicated isolated runner.

# 6. Historical Baselines

Technical Development Baselines v0.2, v0.3, and v0.4 remain historical context. They must not be treated as current authority where they conflict with this v0.5 baseline.
