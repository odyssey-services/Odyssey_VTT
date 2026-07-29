# Odyssey VTT

Odyssey VTT is a Windows 10/11 x64 virtual tabletop application being built from a new technical foundation with Unity 6000.3 LTS, HDRP, UI Toolkit, and a pure Core architecture.

This public repository is the single authoritative code repository for Odyssey VTT implementation work. It intentionally contains only public-safe technical documentation, repository policy, task contracts, and future source/build artifacts. Private product documentation, campaign content, local handoffs, context archives, secrets, and personal paths do not belong in this repository.

## Status

Current stage: `SLICE-00 — Technical Skeleton`.

The first repository foundation task is `docs/tasks/active/ODY-S00-001_Repository_Foundation.md`. Unity project files, .NET projects, C# production code, CI workflows, and build artifacts are intentionally out of scope for this foundation step.

## Rights

Odyssey VTT is public source, not open source. All rights are reserved. See `LICENSE`.

Viewing or forking through GitHub is permitted only as allowed by GitHub Terms of Service. No license is granted to use, copy, modify, distribute, sublicense, sell, host, train on, or create derivative works from this project without prior written permission from the rights holder.

## Repository Entry Points

- `AGENTS.md` — operating rules for Codex and contributors.
- `PLANS.md` — execution plan rules.
- `TECHNICAL_DEVELOPMENT_BASELINE_Odyssey_VTT_v0.1.md` — approved technical baseline.
- `ACTIVE_DOCUMENTATION_BASELINE_Odyssey_VTT_v1.6.md` — active authority register.
- `docs/adr/` — accepted architecture decisions.
- `docs/tasks/` — task contracts and execution backlog.
- `scripts/check-repository-policy.ps1` — repository foundation policy check.

## Local Validation

```powershell
pwsh -NoProfile -File ./scripts/check-repository-policy.ps1
```

GitHub repository visibility, branch protection, owner review, and pull request evidence are owner-controlled checks and must be recorded separately.
