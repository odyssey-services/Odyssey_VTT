# ODY-S00-001 — Align and Close the Private Repository Foundation

**Status:** In Review  
**Roadmap stage / slice:** SLICE-00  
**Owner:** Codex  
**Requested by:** Product owner  
**Branch:** `chore/ody-s00-001-foundation-closeout`  
**Pull request:** Not opened  
**ExecPlan:** Not required; coordinated by `docs/plans/active/ODY-S00-000_SLICE_00_Technical_Skeleton.md`  
**Created:** 2026-07-28  
**Last updated:** 2026-08-01

## 1. Goal

Align the single Private authoritative Odyssey VTT GitHub repository `odyssey-services/Odyssey_VTT` with the owner decision, preserve All Rights Reserved and Git/LFS/editor policy, and close the foundation through a reviewed branch without rewriting the owner bootstrap history. The resulting repository must be safe for the Unity and Core scaffolding tasks without exposing private product documentation or secrets.

## 2. Why this task exists

- Problem or dependency being addressed: The authoritative repository exists, but its task contract and evidence still describe the earlier pre-bootstrap assumptions, so later `SLICE-00` tasks cannot safely proceed until the authority and closeout evidence are aligned.
- Value or risk reduction: Establishes ownership, license, history, repository-safe/private-product boundary, review flow, large-file policy, and predictable text formatting before generated project content arrives.
- Blocking or enabling relationship: Blocks `ODY-S00-002` and every later implementation task.

## 3. Authorities and requirement references

### Required authorities

- `ACTIVE_DOCUMENTATION_BASELINE_Odyssey_VTT_v1.7.md`, sections 1–7
- `AGENTS.md`, sections 1–4, 12–17
- `PLANS.md`, sections 1–4, 8–11
- `docs/tasks/TASK_TEMPLATE.md`
- `docs/tasks/SLICE-00_BACKLOG.md`
- `TECHNICAL_DEVELOPMENT_BASELINE_Odyssey_VTT_v0.2.md`:
  - section 4.1 Milestone M0;
  - section 8 Repository, rights and openness;
  - section 9 Hybrid documentation;
  - section 13 Physical repository structure;
  - section 14 Git and branch policy;
  - section 15 CI baseline where repository policy is relevant;
  - section 30 `PR-000 — Repository Policy and Documentation`;
  - section 31 Definition of Done;
  - section 32 Repository review checklist.
- `docs/adr/ADR-001_Module_Boundaries_and_Dependency_Direction_v1.0.md` for future directory ownership only; no assemblies are created in this task.
- `docs/adr/ADR-006_Test_Project_Structure_and_Dual_Unity_DotNet_Compilation_v1.0.md` for future test-directory names only.
- `docs/adr/ADR-007_Versioning_and_Build_Identity_v1.0.md` for repository/tag policy; no build identity is generated in this task.
- `docs/adr/ADR-009_Unity_Project_and_Build_Baseline_v1.0.md` for the exact future Unity project location; the Unity project is out of scope here.
- `docs/adr/ADR-010_Logging_Diagnostics_and_Redaction_v1.0.md` for forbidden secret/private path content in repository evidence.

### Requirement and test IDs

- Requirement IDs: `SLICE-00`, Milestone `M0`, Technical Baseline `PR-000`, `TDB-DEC-008–TDB-DEC-016`
- Existing test IDs: None; the repository does not yet contain executable tests.
- New test IDs to introduce:
  - `REPO-POLICY-001` required repository files exist;
  - `REPO-POLICY-002` forbidden private files and archive patterns are absent from tracked files;
  - `REPO-POLICY-003` Git LFS patterns are active for approved binary candidates;
  - `REPO-POLICY-004` text/meta/Unity YAML extensions are not globally forced into LFS;
  - `REPO-POLICY-005` branch and merge settings match the supported GitHub protection baseline;
  - `REPO-POLICY-006` approved Private visibility, authoritative identity `odyssey-services/Odyssey_VTT`, and All Rights Reserved notice are verified.

### Task-safe private context

- Approved summary / references: `odyssey-services/Odyssey_VTT` is the single authoritative code repository and remains Private until a separate owner decision. Full Product Vision, Product Requirements, MVP Scope, Domain Model, subsystem contracts, internal Roadmap, private decision logs, current-context archives, handoff files, and local backup paths remain outside authoritative Git history.

## 4. Verified current state

### Verified facts

- A current local documentation bundle exists with accepted technical authorities and private product documents.
- Technical Development Baseline v0.2 requires one Private authoritative GitHub repository `odyssey-services/Odyssey_VTT`, short-lived branches, owner-reviewed merges, All Rights Reserved, Git LFS, and hybrid documentation.
- The authoritative repository may contain the technical baseline, ADRs, AGENTS, PLANS, build/testing/task documentation and repository-safe architecture material.
- Repository `odyssey-services/Odyssey_VTT` is Private with default branch `main`; owner-controlled bootstrap commit `82de52e9cb47bd7a1fa8952ac5cba2b9c88456f5` contains the foundation. Branch protection has not been verified.

### Assumptions

- Repository identity and visibility are owner decisions: `odyssey-services/Odyssey_VTT`, Private.
- Branch protection is not claimed until directly inspected; any unavailable option is recorded rather than silently treated as passed.
- Git and Git LFS are available on the workstation; versions are recorded during execution.

## 5. Scope

### In scope

- Record the real owner-controlled foundation bootstrap commit `82de52e9cb47bd7a1fa8952ac5cba2b9c88456f5` on `main` as a one-time deviation; do not rewrite history or create a retroactive PR.
- Use `chore/ody-s00-001-foundation-closeout` for this closeout and retain branch → PR → owner review → owner merge for all subsequent substantive changes.
- Add root policy files:
  - `README.md`;
  - `LICENSE` with the approved All Rights Reserved wording and copyright notice;
  - `CONTRIBUTING.md`;
  - `SECURITY.md`;
  - `THIRD_PARTY_NOTICES.md`;
  - `AGENTS.md`;
  - `PLANS.md`;
  - `TECHNICAL_DEVELOPMENT_BASELINE_Odyssey_VTT_v0.2.md`;
  - the current technical authority register needed by Codex, without committing the private source documents it references.
- Add repository-safe technical documentation:
  - `docs/adr/ADR-001–ADR-010`;
  - `docs/adr/README.md` or index;
  - `docs/tasks/TASK_TEMPLATE.md`;
  - `docs/tasks/README.md`;
  - `docs/tasks/SLICE-00_BACKLOG.md`;
  - active parent/first task contracts and the parent ExecPlan;
  - `docs/plans/README.md` and active/completed directories.
- Add repository configuration:
  - `.gitignore` for Unity, .NET, IDE, build, logs, secrets, local/private docs and temporary artifacts;
  - `.gitattributes` with text normalization and approved Git LFS candidates;
  - `.editorconfig` baseline;
  - `.github/PULL_REQUEST_TEMPLATE.md`;
  - optional `CODEOWNERS` owned by the product owner/maintainer when a valid GitHub identity is known.
- Add a minimal `scripts/check-repository-policy.ps1` and corresponding documentation-only/shell validation that can run before Unity/.NET projects exist.
- Enable supported `main` protection: block force push/deletion, require pull request, require owner review, dismiss stale approvals where available, and later allow required checks to be added by `ODY-S00-008`.
- Open a pull request containing the substantive foundation and record real evidence.

### Out of scope

- Unity `Assets`, `Packages`, `ProjectSettings`, `UserSettings`, `Library`, or generated project content.
- `.asmdef`, `.csproj`, production/test C# code, Unity scenes, settings, packages or locks.
- GitHub Actions workflows beyond an optional non-blocking documentation/policy check; required CI belongs to `ODY-S00-008`.
- `version.json`, `config/compatibility.json`, BuildIdentity generation or release tags unless needed only as empty directories/placeholders; preferred implementation is deferred.
- Public product requirements, full internal Roadmap, current-context ZIP archives, changelogs, handoff documents, LegacyReference content, personal/local paths, secrets or campaign fixtures.
- Issue backlog migration, project board setup, wiki, Discussions, Pages, package publishing, releases, installer or distribution.
- Accepting external contributions or granting open-source rights.

### Allowed paths

```text
/
.github/PULL_REQUEST_TEMPLATE.md
.github/CODEOWNERS                 # only after valid owner identity is known
scripts/check-repository-policy.ps1
docs/adr/**
docs/plans/**
docs/tasks/**
```

Root changes are limited to the explicitly listed policy/configuration files. No Unity or production-code directories are created except optional empty directory placeholders explicitly justified in the pull request; empty placeholders are not required.

### Paths requiring explicit approval before editing

```text
Any private product document
Any accepted ADR content beyond copying the approved file unchanged
Application/schema/format/contract/protocol/ruleset version sources
.github/workflows/**
Assets/**
Packages/**
ProjectSettings/**
DotNet/**
Tests/**
```

## 6. Technical constraints

- Module ownership and dependency direction: No production modules are created. Future directory examples must match ADR-001 and may not introduce `Common`, `Shared`, `Utils`, or service-locator modules.
- Authoritative-state and transaction boundary: Not applicable.
- Serialization / compatibility boundary: Do not create or claim serialization contracts in this task.
- Time / RNG rule: Not applicable.
- Unity / thread / lifetime rule: Do not create a Unity project or runtime bootstrap.
- Dependency / licensing rule: No new runtime dependency. Git LFS and GitHub-native repository features are approved tooling. Any new GitHub Action or downloaded executable requires separate approval and license/provenance review.
- Security / privacy / redaction rule: Authoritative Git history must contain no private source document, secret, token, credential, absolute private path, personal data, hidden campaign content, or full context bundle.
- Performance or platform constraint: Repository text/config files must work on Windows with LF normalization policy explicitly defined; no platform build is required yet.
- Other: Codex cannot merge to `main`. The owner controls the one-time bootstrap and final merge.

## 7. Expected behavior

### Scenario 1 — Safe authorized clone

**Given** an authorized contributor clones the Private authoritative repository after this task  
**When** they inspect tracked files and Git history  
**Then** they see the technical policies/authorities needed for future work and no private product documents, secrets, local backup paths, generated Unity cache, or current-context archive.

### Scenario 2 — Binary asset candidate

**Given** a future contributor adds an approved binary candidate such as a PSD or WAV  
**When** Git attributes are evaluated  
**Then** the file is routed through Git LFS, while `.cs`, `.md`, `.json`, `.meta`, `.asset`, `.prefab`, `.unity`, `.uxml`, and `.uss` remain normal text/diffable files unless an explicit later rule says otherwise.

### Scenario 3 — Protected main

**Given** substantive repository foundation changes are ready  
**When** Codex or a developer attempts to deliver them  
**Then** they use a branch and pull request, required owner review applies, and Codex cannot directly merge or force-push `main`.

### Required invariants

- There is one Private authoritative code repository: `odyssey-services/Odyssey_VTT`.
- `main` is the only release-bearing branch and is protected to the strongest supported baseline.
- Private visibility does not grant reuse rights; the repository remains All Rights Reserved.
- The full private documentation bundle is never committed, even temporarily and later deleted.
- No task completion claim depends on a check that was not run.

## 8. Deliverables

- Production code: None.
- Tests: Repository-policy script checks `REPO-POLICY-001–004`; owner/settings evidence covers 005–006.
- Scripts / CI: `scripts/check-repository-policy.ps1`; no required GitHub Actions workflow.
- Configuration: `.gitignore`, `.gitattributes`, `.editorconfig`, PR template, supported branch protection settings.
- Documentation: Root policy files, repository-safe technical authorities, task/plan workflow and ADR index.
- Generated evidence or build artifacts: Repository URL, branch/PR reference, policy-script output, tracked-file inventory/hash, Git LFS attribute evidence, protection checklist.
- Migration / recovery material: Not applicable.

## 9. Acceptance criteria

1. Private GitHub repository `odyssey-services/Odyssey_VTT` is recorded as the single authoritative code repository and remains Private until a separate owner decision.
2. `main` contains owner-controlled foundation bootstrap `82de52e9cb47bd7a1fa8952ac5cba2b9c88456f5`; this one-time direct commit is documented without history rewrite or retroactive PR, and all subsequent substantive changes use a short-lived branch and pull request.
3. `LICENSE` and `README.md` clearly state All Rights Reserved and do not grant open-source reuse rights.
4. `CONTRIBUTING.md` requires prior written agreement for external contributions, and `SECURITY.md` defines a private reporting path without publishing a secret address that was not provided.
5. All required root operational/technical files and ADR-001–ADR-010 are present in their canonical paths and unchanged except for repository-safe path/reference alignment explicitly recorded in the PR.
6. Full private product documents, current-context archives, historical handoffs/changelogs, local backup paths, secrets, credentials, generated Unity/.NET cache and user data are absent from tracked files and the substantive PR diff.
7. `.gitattributes` routes the approved large binary candidates through Git LFS and preserves normal text handling for source, Markdown, JSON, Unity YAML/meta, UXML and USS.
8. `.gitignore` covers Unity cache/build/user directories, .NET outputs, IDE files, logs/diagnostics, secrets, local environment files, private documentation staging and generated artifacts without ignoring required source/meta files.
9. `.editorconfig` establishes UTF-8, final newline, whitespace, C# indentation/line-ending baseline and does not conflict with Unity serialization text assets.
10. `scripts/check-repository-policy.ps1` exits 0 on the repository and exits non-zero against a controlled fixture or temporary tracked-file list containing a forbidden private/archive/secret pattern.
11. Supported `main` protection blocks force push/deletion and requires pull-request review/owner approval; unavailable GitHub settings are recorded explicitly.
12. The pull request includes real validation output, known limitations, no invented CI/build claims, and owner review before merge.

## 10. Tests and validation

### Required automated tests

| Test ID | Layer / runner | Behavior or contract proven | Required result |
|---|---|---|---|
| `REPO-POLICY-001` | PowerShell script | Required repository files and canonical directories exist | Pass |
| `REPO-POLICY-002` | PowerShell script | Forbidden private/archive/secret/generated patterns are not tracked | Pass |
| `REPO-POLICY-003` | Git attributes/LFS check | Approved binary candidates use LFS | Pass |
| `REPO-POLICY-004` | Git attributes check | Source/Markdown/JSON/Unity YAML/meta/UI text are not globally put in LFS | Pass |
| `REPO-POLICY-005` | Owner settings inspection | Main protection matches supported baseline | Pass or documented unavailable option with owner acceptance |
| `REPO-POLICY-006` | GitHub repository inspection | Private visibility, authoritative repository identity, and All Rights Reserved notice are correct | Pass |

### Required commands

Run the exact available equivalents and record outputs. Example entry points after files exist:

```powershell
pwsh -NoProfile -File ./scripts/check-repository-policy.ps1
git status --short
git ls-files
git lfs env
git lfs track
git check-attr filter diff merge text -- sample.psd sample.wav sample.cs sample.md sample.json sample.meta sample.asset sample.prefab sample.unity sample.uxml sample.uss
git diff --check
git log --oneline --decorate --all
```

Also run the policy script against an isolated negative fixture or injected file list; do not commit a real secret/private file merely to test rejection.

### Manual validation

- Inspect repository metadata through the authenticated GitHub connector to confirm Private visibility, authoritative identity, default branch, and All Rights Reserved notice.
- Review GitHub branch protection/ruleset settings against the task checklist.
- Inspect the pull-request file list for accidental private documents, ZIP archives, generated caches, absolute paths or credentials.
- Confirm the substantive change was not pushed directly to `main`.

### Required environments / profiles

- OS / architecture: Windows 10/11 x64 preferred; policy script must use PowerShell 7 syntax or document Windows PowerShell compatibility.
- Unity editor or Player profile: Not required.
- Scripting backend: Not required.
- Network topology or database fixture: Not required.
- Other: Git, Git LFS, GitHub account with repository/ruleset permissions.

### Validation not required by this task

- Unity open/import/compile, because the Unity project is created by `ODY-S00-002`.
- .NET restore/build/test, because the solution is created by `ODY-S00-003`.
- GitHub Actions required checks, because workflows are created by `ODY-S00-008`.
- Windows Player or IL2CPP build, because later tasks create them.
- Runtime performance, persistence, networking, security penetration testing or release publication.

## 11. Compatibility, migration, and rollback

- Compatibility impact: Establishes initial repository policy; no previous release or user data exists.
- Version fields affected: No application/schema/format/contract/protocol/ruleset version change.
- Migration or upcaster: Not applicable.
- Forward / backward behavior: Future commits must follow the repository policy; no runtime behavior exists.
- Rollback method: Close the unmerged PR or revert the foundation PR. Repository visibility/deletion is owner-only and must not be used as a casual rollback method.
- Data-loss risk and protection: The main risk is unauthorized disclosure or polluted authoritative Git history. Inspect before push; if a secret/private file reaches a remote, stop work, rotate affected secret, remove it with an approved history-rewrite incident procedure, and document the incident. Deleting a later commit is not sufficient.
- Recovery rehearsal required: Negative policy fixture; no production-data rehearsal.

## 12. Dependencies and licensing

### New or changed dependencies

| Dependency | Version / source | Purpose | License | Approved by |
|---|---|---|---|---|
| Git | Installed toolchain | Version control | Tool license | Existing approved tooling |
| Git LFS | Installed toolchain | Large binary pointers | Tool license | Technical Baseline |
| GitHub repository features | GitHub | Hosting, PR and protection | Service terms | Product owner decision |

No GitHub Action, package manager dependency, executable download, or runtime library is added by this task without explicit task amendment and approval.

## 13. Security, privacy, and hidden information

- Data classes handled: Repository-safe technical documentation/configuration; repository metadata; owner GitHub identity; no campaign/user data.
- Trust boundaries: Local documentation bundle → selected repository-safe files → Git staging → Private authoritative GitHub repository.
- Authorization / audience checks: Owner controls repository creation, visibility, protection and merge. Codex can prepare changes but cannot merge.
- Redaction requirements: Remove private document bodies, personal/local paths, tokens, credentials, email addresses not explicitly approved for publication, diagnostic dumps, RNG secrets and hidden campaign data.
- Log-safe fields: Repository path relative to root, task ID, Git commit/branch, file category and rule result. Do not log file contents from suspected secret/private files.
- Abuse / malformed input limits: Policy script operates on tracked paths and bounded text configuration; it must not recursively print contents of large/binary/private files.
- Security tests: Forbidden-pattern negative fixture, secret-pattern/path checks, manual repository diff review.

## 14. Planning and execution mode

- Planning mode: `Brief plan`
- Reason for selected mode: One documentation/policy pull request with no production module, persisted format, protocol, Unity version, build pipeline or runtime behavior change. The parent slice ExecPlan coordinates dependencies.
- ExecPlan path: Not required for this child task; parent coordination plan is `docs/plans/active/ODY-S00-000_SLICE_00_Technical_Skeleton.md`.
- Expected pull request count: 1 substantive pull request after the owner-only base-branch bootstrap.
- Milestone or sequencing constraints: Must complete before `ODY-S00-002`. Branch protection required checks are expanded later by `ODY-S00-008` after workflows exist.

## 15. Documentation and versioning impact

- Documents that must change: Repository copies/paths of the approved repository-safe technical authorities; ADR index; task completion evidence; parent ExecPlan/backlog status.
- Documents that must not change: Product Vision, Product Requirements, MVP Scope, Domain Model, subsystem product documents, internal Roadmap, Test Strategy content, accepted ADR decisions, full current context archive.
- Application version change: No — no application exists.
- Schema / format / contract / protocol / ruleset version change: None.
- Documentation version changes: None merely for copying approved files into canonical repository paths. Material wording changes require a separately approved documentation task/version decision.
- Changelog or release-note requirement: Update task and parent ExecPlan progress. No end-user release note.

## 16. Definition of Done

- [ ] Goal is achieved without unapproved scope expansion.
- [ ] All acceptance criteria are satisfied.
- [ ] Required automated policy tests pass.
- [ ] Required manual repository/protection checks are completed.
- [ ] Required commands and their real results are recorded.
- [ ] Public/private documentation boundary is verified.
- [ ] No unapproved dependency, tool, GitHub Action or license was introduced.
- [ ] Git LFS and text normalization rules are verified.
- [ ] Documentation is copied/updated only where materially required.
- [ ] Codex/developer performs self-review against this task and `AGENTS.md`.
- [ ] Pull request explains changes, evidence, limitations and follow-up work.
- [ ] Product owner reviews and merges; Codex does not merge into `main`.

## 17. Completion evidence

This task remains `In Review` until owner review and merge.

### Changed files / areas

- Repository identity and visibility aligned to Private authoritative repository `odyssey-services/Odyssey_VTT`.
- Technical Development Baseline raised to v0.2 and Active Documentation Baseline raised to v1.7 for the material authority change.
- Root policy documents, Slice-00 backlog, parent task, child task, ExecPlan, and repository-policy required paths aligned.
- No Unity project, C#/.NET project, GitHub Actions workflow, runtime/gameplay code, `version.json`, BuildIdentity, or ODY-S00-002 artifact was created.

### Validation results

| Command / check | Result | Evidence / notes |
|---|---|---|
| GitHub connector repository inspection | Passed | Authenticated account `odyssey-services`; repository `odyssey-services/Odyssey_VTT`; visibility `private`; default branch `main`; connector reports push/admin access. |
| Owner bootstrap verification | Passed | Local `HEAD` and `origin/main` started at `82de52e9cb47bd7a1fa8952ac5cba2b9c88456f5`, `chore: initialize repository`. |
| Repository policy script, initial run | Environment-limited | Nested Git calls were rejected by Git dubious-ownership protection, so REPO-POLICY-003/004 had empty attribute output. This was not treated as a product failure. |
| Repository policy script with process-scoped `safe.directory` | Passed | `REPO-POLICY-001` through `REPO-POLICY-004` PASS; exit 0. No global Git config was changed. |
| Negative fixture | Passed as rejection test | Isolated tracked-file list containing private-document and ZIP paths was rejected; exit 1; temporary fixture removed. |
| `git diff --check` | Passed | No whitespace errors. |
| `git check-attr` | Passed | PSD/WAV use LFS; C#, Markdown, JSON, Unity YAML/meta, UXML and USS remain normal text. |
| Git LFS checks | Passed | Git LFS 3.7.1; remote endpoint resolves to `odyssey-services/Odyssey_VTT`; approved patterns listed from `.gitattributes`. |
| Tracked-file and history forbidden-path scan | Passed | No Unity/.NET/generated/private-document/archive/secret paths matched. |
| `gh --version` | Not available | GitHub CLI is not installed in PATH; authenticated GitHub connector is used for repository metadata and Draft PR creation. |
| Branch protection/ruleset inspection | Not run | No claim is made that branch protection is configured or passing. |

### Acceptance result

| Criterion | Status | Evidence |
|---|---|---|
| AC-1 | Passed | Private `odyssey-services/Odyssey_VTT` is verified as the authoritative repository. |
| AC-2 | Passed with documented deviation | Owner foundation bootstrap entered `main` directly as `82de52e9…`; no history rewrite or retroactive PR. Subsequent substantive work uses branch/PR. |
| AC-3–AC-10 | Passed | All Rights Reserved, contribution/security policy, authority files, forbidden-path checks, Git LFS/text attributes, ignores/editor policy, positive policy run and negative fixture verified. |
| AC-11 | Not verified | Branch protection/ruleset inspection was not performed and is not claimed. |
| AC-12 | In Review | Closeout is delivered through a Draft PR; owner review and merge are still required. |

### Build and artifact evidence

- Build identity: Not applicable; explicitly out of scope.
- Artifact path / name: Documentation/policy closeout only.
- Checksums: Not created; no binary artifact.
- Test or quality report: Validation evidence above.

### Known limitations

- Branch protection settings have not been inspected.
- `pwsh` and `gh` are not installed in PATH; Windows PowerShell and the authenticated GitHub connector were used.
- Task cannot move to `Done` until owner review and merge.

### Follow-up tasks

- Owner reviews and merges the Draft closeout PR; Codex does not merge.
- After merge and required owner evidence, move ODY-S00-001 to completed.
- Activate `ODY-S00-002 — Unity Project Foundation` only after ODY-S00-001 reaches Done.

### Self-review summary

- Scope: documentation and repository policy only; no ODY-S00-002 artifacts.
- Architecture: accepted ADR files were not modified.
- Security/privacy: Private visibility does not weaken tracked-file/history exclusions.
- Versioning: Technical Baseline v0.2 and Active Baseline v1.7 reflect the material owner decision; no application/schema/format/contract/protocol/ruleset version changed.

## 18. Blockers, decisions, and change control

### Blockers

- Owner review and merge are required before `Done`.
- Branch protection evidence remains not verified.

### Decisions made during execution

- 2026-08-01 — `odyssey-services/Odyssey_VTT` remains Private and is the single authoritative code repository; visibility must not be changed without a separate owner decision.
- 2026-08-01 — Owner-controlled foundation commit `82de52e9cb47bd7a1fa8952ac5cba2b9c88456f5` entered `main` directly. It is a recorded one-time bootstrap deviation, not the standard for future work.
- 2026-08-01 — No Git history rewrite and no fictitious retroactive PR. All subsequent substantive changes use branch → PR → owner review → owner merge.
- 2026-08-01 — Accepted ADR files remain unchanged; the owner decision is registered through Technical Baseline v0.2 and Active Baseline v1.7.

### Approved task changes

- Owner-approved controlled closeout and Private repository authority alignment described above.
