# Odyssey VTT — Execution Plan Storage

`PLANS.md` in the repository root defines the mandatory planning contract.

- `active/` contains living ExecPlans for work that is approved, in progress, or blocked.
- `completed/` contains completed or superseded plans without rewriting their execution history.
- Every ExecPlan uses the same Task ID as its governing task contract.
- Plans do not grant permission to change product scope, architecture, versions, dependencies, or security boundaries.
- Private product text, local private paths, secrets, personal data, and hidden campaign content must never be copied into these files.
