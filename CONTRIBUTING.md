# Contributing

Odyssey VTT is private source and All Rights Reserved. External contributions are not accepted unless the contributor has a prior written agreement with the project owner.

## Development Rules

- Work from an approved task contract in `docs/tasks/active/`.
- Use a short-lived branch and pull request for substantive work.
- Do not push directly to `main`.
- Do not merge pull requests unless you are the authorized owner/reviewer.
- Do not add product scope, dependencies, Unity/package changes, public contracts, persisted formats, workflows, or architecture changes outside the active task.
- Do not commit private product documents, local paths, handoffs, context archives, campaign data, credentials, tokens, keys, diagnostic dumps, generated caches, or build artifacts.
- Follow `AGENTS.md`, `PLANS.md`, the Technical Development Baseline, and accepted ADRs.

## Required Evidence

Every pull request must list:

- task ID and goal;
- in-scope and out-of-scope work;
- architecture/version/security impact;
- validation commands actually run;
- checks not run and why;
- dependency and license impact;
- known limitations and follow-up tasks.

False claims of test or policy success are blocking.
