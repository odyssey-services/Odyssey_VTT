# Odyssey VTT

Odyssey VTT is a Windows 10/11 x64 virtual tabletop application being built from a new technical foundation with Unity 6000.4, HDRP, UI Toolkit, and a pure Core architecture.

The private GitHub repository `odyssey-services/Odyssey_VTT` is the single authoritative code repository for Odyssey VTT implementation work. It intentionally contains only repository-safe technical documentation, repository policy, task contracts, and future source/build artifacts. Private product documentation, campaign content, local handoffs, context archives, secrets, and personal paths do not belong in this repository.

## Status

Current stage: `SLICE-00 — Technical Skeleton`.

Repository Foundation, Unity Project Foundation, Module/Test Skeleton, and Identity/Version/Result Primitives are complete. The active task is `docs/tasks/active/ODY-S00-005_Command_Event_Clock_and_RNG_Contracts.md`, currently In Progress on `feat/ody-s00-005-command-event-clock-rng-primitives`. This repository state contains the Unity 6000.4/HDRP project foundation, accepted Core module/test skeleton, ODY-S00-004 primitive foundation, and ODY-S00-005 command/event/clock/RNG contract work in progress.

## Rights

Odyssey VTT is private source and not open source. All rights are reserved. See `LICENSE`.

Repository access through GitHub does not grant rights beyond those required to use GitHub as authorized. No license is granted to use, copy, modify, distribute, sublicense, sell, host, train on, or create derivative works from this project without prior written permission from the rights holder.

## Repository Entry Points

- `AGENTS.md` — operating rules for Codex and contributors.
- `PLANS.md` — execution plan rules.
- `TECHNICAL_DEVELOPMENT_BASELINE_Odyssey_VTT_v0.3.md` — approved technical baseline.
- `ACTIVE_DOCUMENTATION_BASELINE_Odyssey_VTT_v1.8.md` — active authority register.
- `docs/adr/` — accepted architecture decisions.
- `docs/tasks/` — task contracts and execution backlog.
- `scripts/check-repository-policy.ps1` — repository foundation policy check.

## Local Validation

```powershell
pwsh -NoProfile -File ./scripts/check-repository-policy.ps1
```

GitHub repository visibility, branch protection, owner review, and pull request evidence are owner-controlled checks and must be recorded separately.
