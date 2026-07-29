# Odyssey VTT — Task Contracts

Root `AGENTS.md`, root `PLANS.md`, and `TASK_TEMPLATE.md` define the operational workflow.

- `TASK_TEMPLATE.md` is copied to create every repository-changing task.
- `SLICE-00_BACKLOG.md` is the approved ordered backlog for the technical skeleton.
- `active/` contains Ready, In Progress, Blocked, and In Review task contracts.
- `completed/` contains Done and Cancelled task contracts after review.
- Every task uses an ID such as `ODY-S00-001` and records real validation evidence before Done.
- A complex task references an ExecPlan under `docs/plans/active/` with the same Task ID.
- A parent slice task may coordinate multiple child tasks; child tasks still own their individual pull-request scope and acceptance evidence.
- Private product documents, local paths, secrets, personal data, and hidden campaign content never belong in task contracts.

Current execution package:

```text
docs/tasks/SLICE-00_BACKLOG.md
docs/tasks/active/ODY-S00-000_SLICE_00_Technical_Skeleton.md
docs/tasks/active/ODY-S00-001_Repository_Foundation.md
docs/plans/active/ODY-S00-000_SLICE_00_Technical_Skeleton.md
```
