# ODY-S00-001 — Create the Public Repository Foundation

**Status:** Blocked  
**Roadmap stage / slice:** SLICE-00  
**Owner:** Codex  
**Requested by:** Product owner  
**Branch:** `chore/ody-s00-001-repository-foundation` local unborn branch  
**Pull request:** Not opened  
**ExecPlan:** Not required; coordinated by `docs/plans/active/ODY-S00-000_SLICE_00_Technical_Skeleton.md`  
**Created:** 2026-07-28  
**Last updated:** 2026-07-29 08:15 UTC

## 1. Goal

Create the single public authoritative Odyssey VTT GitHub repository with a protected `main`, All Rights Reserved policy, Git/LFS/editor configuration, public-safe technical documentation, and repository contribution/security rules. The resulting repository must be safe for the Unity and Core scaffolding tasks without exposing private product documentation or secrets.

## 2. Why this task exists

- Problem or dependency being addressed: There is no evidenced code repository in which later `SLICE-00` tasks can create the Unity project, modules, tests, scripts, or CI.
- Value or risk reduction: Establishes ownership, license, history, private/public boundary, review flow, large-file policy, and predictable text formatting before generated project content arrives.
- Blocking or enabling relationship: Blocks `ODY-S00-002` and every later implementation task.

## 3. Authorities and requirement references

### Required authorities

- `ACTIVE_DOCUMENTATION_BASELINE_Odyssey_VTT_v1.6.md`, sections 1–7
- `AGENTS.md`, sections 1–4, 12–17
- `PLANS.md`, sections 1–4, 8–11
- `docs/tasks/TASK_TEMPLATE.md`
- `docs/tasks/SLICE-00_BACKLOG.md`
- `TECHNICAL_DEVELOPMENT_BASELINE_Odyssey_VTT_v0.1.md`:
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
- `docs/adr/ADR-010_Logging_Diagnostics_and_Redaction_v1.0.md` for forbidden secret/private path content in public evidence.

### Requirement and test IDs

- Requirement IDs: `SLICE-00`, Milestone `M0`, Technical Baseline `PR-000`, `TDB-DEC-008–TDB-DEC-016`
- Existing test IDs: None; the repository does not yet contain executable tests.
- New test IDs to introduce:
  - `REPO-POLICY-001` required public files exist;
  - `REPO-POLICY-002` forbidden private files and archive patterns are absent from tracked files;
  - `REPO-POLICY-003` Git LFS patterns are active for approved binary candidates;
  - `REPO-POLICY-004` text/meta/Unity YAML extensions are not globally forced into LFS;
  - `REPO-POLICY-005` branch and merge settings match the supported GitHub protection baseline;
  - `REPO-POLICY-006` repository is public and the license notice is visible.

### Task-safe private context

- Approved summary / references: The code repository is public. Full Product Vision, Product Requirements, MVP Scope, Domain Model, subsystem contracts, internal Roadmap, private decision logs, current-context archives, handoff files, and local backup paths remain outside public Git history.

## 4. Verified current state

### Verified facts

- A current local documentation bundle exists with accepted technical authorities and private product documents.
- Technical Development Baseline requires one public GitHub repository, protected `main`, short-lived branches, owner-reviewed merges, All Rights Reserved, Git LFS, and hybrid documentation.
- The public repository may contain technical baseline, ADRs, AGENTS, PLANS, build/testing/task documentation and public-safe architecture material.
- No repository URL, Git commit, branch protection screenshot/export, LFS pointer, CI run, or repository-policy script result is present in the current planning evidence.

### Assumptions

- The owner-selected repository slug will be `Odyssey-VTT` unless unavailable; a different slug does not change architecture and is recorded in completion evidence.
- The owner can create a public repository and configure branch protection. If a GitHub plan does not expose an exact setting, the strongest available equivalent is used and the limitation is recorded rather than silently ignored.
- Git and Git LFS are available on the workstation; versions are recorded during execution.

## 5. Scope

### In scope

- Owner creates the public GitHub repository and a one-time minimal initial commit on `main` solely to establish the base branch.
- Create a short-lived branch such as `chore/ody-s00-001-repository-foundation` for all substantive task content.
- Add root policy files:
  - `README.md`;
  - `LICENSE` with the approved All Rights Reserved wording and copyright notice;
  - `CONTRIBUTING.md`;
  - `SECURITY.md`;
  - `THIRD_PARTY_NOTICES.md`;
  - `AGENTS.md`;
  - `PLANS.md`;
  - `TECHNICAL_DEVELOPMENT_BASELINE_Odyssey_VTT_v0.1.md`;
  - the current technical authority register needed by Codex, without committing the private source documents it references.
- Add public-safe technical documentation:
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
- Security / privacy / redaction rule: Public Git history must contain no private source document, secret, token, credential, absolute private path, personal data, hidden campaign content, or full context bundle.
- Performance or platform constraint: Repository text/config files must work on Windows with LF normalization policy explicitly defined; no platform build is required yet.
- Other: Codex cannot merge to `main`. The owner controls the one-time bootstrap and final merge.

## 7. Expected behavior

### Scenario 1 — Safe public clone

**Given** a person clones the public repository after this task  
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

- There is one authoritative public code repository.
- `main` is the only release-bearing branch and is protected to the strongest supported baseline.
- The public repository is not licensed as open source.
- The full private documentation bundle is never committed, even temporarily and later deleted.
- No task completion claim depends on a check that was not run.

## 8. Deliverables

- Production code: None.
- Tests: Repository-policy script checks `REPO-POLICY-001–004`; owner/settings evidence covers 005–006.
- Scripts / CI: `scripts/check-repository-policy.ps1`; no required GitHub Actions workflow.
- Configuration: `.gitignore`, `.gitattributes`, `.editorconfig`, PR template, supported branch protection settings.
- Documentation: Root policy files, public-safe technical authorities, task/plan workflow and ADR index.
- Generated evidence or build artifacts: Repository URL, branch/PR reference, policy-script output, tracked-file inventory/hash, Git LFS attribute evidence, protection checklist.
- Migration / recovery material: Not applicable.

## 9. Acceptance criteria

1. A public GitHub repository exists under the owner-selected namespace and is recorded as the single authoritative code repository.
2. `main` exists from an owner-controlled minimal bootstrap; all substantive task changes are delivered through a short-lived branch and pull request.
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
| `REPO-POLICY-001` | PowerShell script | Required public files and canonical directories exist | Pass |
| `REPO-POLICY-002` | PowerShell script | Forbidden private/archive/secret/generated patterns are not tracked | Pass |
| `REPO-POLICY-003` | Git attributes/LFS check | Approved binary candidates use LFS | Pass |
| `REPO-POLICY-004` | Git attributes check | Source/Markdown/JSON/Unity YAML/meta/UI text are not globally put in LFS | Pass |
| `REPO-POLICY-005` | Owner settings inspection | Main protection matches supported baseline | Pass or documented unavailable option with owner acceptance |
| `REPO-POLICY-006` | GitHub repository inspection | Repository visibility and license notice are correct | Pass |

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

- Inspect the public repository while logged out/incognito to confirm visibility and license notice.
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
- Data-loss risk and protection: The main risk is accidental public disclosure or polluted Git history. Inspect before push; if a secret/private file reaches a remote, stop work, rotate affected secret, remove it with an approved history-rewrite incident procedure, and document the incident. Deleting a later commit is not sufficient.
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

- Data classes handled: Public technical documentation/configuration; repository metadata; owner GitHub identity; no campaign/user data.
- Trust boundaries: Local documentation bundle → selected public-safe files → Git staging → public GitHub repository.
- Authorization / audience checks: Owner controls repository creation, visibility, protection and merge. Codex can prepare changes but cannot merge.
- Redaction requirements: Remove private document bodies, personal/local paths, tokens, credentials, email addresses not explicitly approved for publication, diagnostic dumps, RNG secrets and hidden campaign data.
- Log-safe fields: Repository path relative to root, task ID, Git commit/branch, file category and rule result. Do not log file contents from suspected secret/private files.
- Abuse / malformed input limits: Policy script operates on tracked paths and bounded text configuration; it must not recursively print contents of large/binary/private files.
- Security tests: Forbidden-pattern negative fixture, secret-pattern/path checks, manual public diff review.

## 14. Planning and execution mode

- Planning mode: `Brief plan`
- Reason for selected mode: One documentation/policy pull request with no production module, persisted format, protocol, Unity version, build pipeline or runtime behavior change. The parent slice ExecPlan coordinates dependencies.
- ExecPlan path: Not required for this child task; parent coordination plan is `docs/plans/active/ODY-S00-000_SLICE_00_Technical_Skeleton.md`.
- Expected pull request count: 1 substantive pull request after the owner-only base-branch bootstrap.
- Milestone or sequencing constraints: Must complete before `ODY-S00-002`. Branch protection required checks are expanded later by `ODY-S00-008` after workflows exist.

## 15. Documentation and versioning impact

- Documents that must change: Repository copies/paths of the approved public-safe technical authorities; ADR index; task completion evidence; parent ExecPlan/backlog status.
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

Fill this section with real results before moving the task to `Done`.

### Changed files / areas

- Root repository policy/configuration scaffold: `README.md`, `LICENSE`, `CONTRIBUTING.md`, `SECURITY.md`, `THIRD_PARTY_NOTICES.md`, `.gitignore`, `.gitattributes`, `.editorconfig`, `.github/PULL_REQUEST_TEMPLATE.md`.
- Public-safe technical authorities copied into canonical root paths: `AGENTS.md`, `PLANS.md`, `TECHNICAL_DEVELOPMENT_BASELINE_Odyssey_VTT_v0.1.md`, `ACTIVE_DOCUMENTATION_BASELINE_Odyssey_VTT_v1.6.md`, `docs/adr/**`, `docs/tasks/**`, `docs/plans/**`.
- Repository policy script added: `scripts/check-repository-policy.ps1`.
- Local Git repository initialized on `chore/ody-s00-001-repository-foundation` and files staged for validation only; no commit, remote push, pull request, merge, Unity project, C# code, or GitHub workflow was created.

### Validation results

| Command / check | Result | Evidence / notes |
|---|---|---|
| Root authority review | Passed | Read `Documentation/AGENTS.md`, `Documentation/PLANS.md`, `docs/tasks/active/ODY-S00-001_Repository_Foundation.md`, parent ExecPlan, Technical Development Baseline, and ADR-001-ADR-010 before implementation. |
| `git --version` | Passed | `git version 2.54.0.windows.1`. |
| `git lfs version` | Passed | `git-lfs/3.7.1 (GitHub; windows amd64; go 1.25.1; git b84b3384)`. |
| `git init -b chore/ody-s00-001-repository-foundation` | Passed | Initialized local Git repository at `D:/Documents/Odyssey_VTT/.git/`; no commit created. |
| `git add .` | Passed with warning | Staged the public-safe scaffold. Warning: unable to access `C:\Users\alexx/.config/git/ignore`: Permission denied. |
| `pwsh -NoProfile -File ./scripts/check-repository-policy.ps1` | Failed / not available | `pwsh` is not present in PATH in this environment. |
| `powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\check-repository-policy.ps1` | Passed | `REPO-POLICY-001` through `REPO-POLICY-004` all PASS; repository policy check passed. |
| Negative fixture policy run | Passed as rejection test | `powershell ... -TrackedFileList .\repo-policy-negative-fixture.txt` exited 1 and rejected `Documentation/01_Product_Requirements_Odyssey_VTT_v0.14.md` and `context.zip`; fixture file removed after the run. |
| `git status --short` | Passed | Shows 36 staged public-safe files. Warning: unable to access global ignore due permission denied. |
| `git ls-files` | Passed | Listed 36 staged files; `Documentation/`, private product docs, changelogs, handoffs, archives, Unity/.NET/generated paths absent. |
| `git lfs env` | Passed | Git LFS 3.7.1 configured; local media dir `.git/lfs/objects`; no remote access configured. |
| `git lfs track` | Passed | Listed LFS patterns from `.gitattributes`: PSD/PSB/TIF/TIFF/PNG/JPG/JPEG/WAV/MP3/OGG/FLAC/FBX/BLEND/EXR. |
| `git check-attr filter diff merge text -- sample.psd sample.wav sample.cs sample.md sample.json sample.meta sample.asset sample.prefab sample.unity sample.uxml sample.uss` | Passed | `sample.psd` and `sample.wav` use `filter/diff/merge: lfs` and `text: unset`; `.cs`, `.md`, `.json`, `.meta`, `.asset`, `.prefab`, `.unity`, `.uxml`, `.uss` are not LFS and have `text: set`. |
| `git diff --check` | Passed | No output; exit 0. |
| `git diff --cached --check` | Initially failed, then passed | Initial failure was trailing whitespace in copied Markdown authorities. `.gitattributes` now sets `*.md whitespace=-blank-at-eol` to preserve accepted Markdown content; rerun exit 0. |
| `git log --oneline --decorate --all` | Passed | No output; no local commits exist yet. |
| GitHub connector installed account check | Passed | Connector sees account `limonety995-maker`. |
| GitHub connector repository search | Not found | Search for `Odyssey-VTT` returned no accessible repositories. |
| `gh --version` | Not available | `gh` is not installed in PATH. |
| `git remote -v` | Passed | No output; no remote configured. |
| GitHub repository visibility inspection | Not run | No owner-selected GitHub namespace/repository URL available and connector cannot create a new repository. |
| GitHub branch protection/ruleset inspection | Not run | Public remote repository and owner settings are not available in this environment. |
| Pull request creation | Not run | No remote repository/owner bootstrap exists; Codex did not push or open a PR. |

### Acceptance result

| Criterion | Status | Evidence |
|---|---|---|
| AC-1 | Blocked | Public GitHub repository URL/namespace was not provided or created by owner. |
| AC-2 | Blocked | Owner-controlled minimal `main` bootstrap and remote PR workflow not available; local work is staged on `chore/ody-s00-001-repository-foundation` only. |
| AC-3 | Passed locally | `README.md` and `LICENSE` state All Rights Reserved and no open-source license grant. |
| AC-4 | Passed locally | `CONTRIBUTING.md` requires prior written agreement; `SECURITY.md` requires private reporting without inventing an address. |
| AC-5 | Passed locally | Required root files, ADR-001-ADR-010, task docs, plan docs, and authority register exist in canonical paths. |
| AC-6 | Passed locally | `git ls-files` and policy script show private docs, changelogs, handoffs, archives, Unity/.NET generated paths, and secrets absent from staged public file set. |
| AC-7 | Passed locally | `.gitattributes` routes approved binary candidates through LFS and keeps source/Markdown/JSON/Unity YAML/meta/UI text out of LFS. |
| AC-8 | Passed locally | `.gitignore` covers private docs, Unity generated/local paths, .NET outputs, IDE files, logs, secrets, env files, archives, and artifacts. |
| AC-9 | Passed locally | `.editorconfig` sets UTF-8, LF, final newline, whitespace, and C#/Unity text indentation baseline; Markdown trailing spaces are allowed to preserve copied authorities. |
| AC-10 | Passed locally | Policy script exits 0 on staged repository and exits non-zero against negative tracked-file fixture. |
| AC-11 | Blocked | Remote `main` protection requires owner GitHub repository/settings access. |
| AC-12 | Blocked | Pull request cannot be opened without owner remote repository/bootstrap. |

### Build and artifact evidence

- Build identity: Not applicable
- Artifact path / name: Local staged repository scaffold only; no build artifact.
- Checksums: Not created; no binary artifact.
- Test or quality report: Policy command output recorded above.

### Known limitations

- Exact GitHub namespace, repository URL, public visibility, branch protection, owner review requirement, and pull request evidence remain blocked on owner-controlled GitHub setup.
- Local branch is an unborn branch with staged files and no commits; this avoids pretending that the owner-controlled `main` bootstrap or reviewed PR exists.
- `pwsh` is not installed in PATH; Windows PowerShell compatibility was verified instead.

### Follow-up tasks

- Complete owner-controlled GitHub setup for `ODY-S00-001`: create public repository, bootstrap `main`, push this staged scaffold through a short-lived branch, configure branch protection, open PR, record visibility/protection evidence, and obtain owner review.
- `ODY-S00-002 — Unity Project Foundation` only after `ODY-S00-001` reaches Done.

### Self-review summary

- Scope review: Repository policy/documentation only; no Unity project, C# code, .NET projects, CI workflows, or product feature work.
- Architecture review: No module edges, assemblies, runtime contracts, persistence, networking, logging runtime, clock/RNG, or Unity settings were created.
- Test review: `REPO-POLICY-001` through `REPO-POLICY-004` pass locally; GitHub-only checks remain not run.
- Security/privacy review: Public/private boundary is enforced by `.gitignore`, staged inventory, and negative fixture; source private bundle remains outside staged public files.
- Documentation/version review: Accepted ADR content copied unchanged; no product/ADR/application/schema/format/contract/protocol/ruleset version change.

## 18. Blockers, decisions, and change control

### Blockers

- Owner-controlled GitHub setup is still required: select/create the public repository namespace, create the minimal `main` bootstrap, configure visibility and branch protection, and provide remote access for branch/PR evidence.

### Decisions made during execution

- 2026-07-28 — One-time owner-created minimal commit may establish `main`; all substantive foundation content must use branch/PR workflow — Authority / approval: Parent ExecPlan operational decision consistent with owner-only merge policy.
- 2026-07-28 — Commit only a public-safe technical subset, not the full current context bundle — Authority / approval: Technical Baseline hybrid documentation policy.
- 2026-07-29 — Preserve copied accepted Markdown authorities and configure Git whitespace checking with `*.md whitespace=-blank-at-eol` rather than editing accepted ADR/baseline line endings or hard-break spacing — Authority / approval: ODY-S00-001 acceptance criterion 5 and repository `git diff --check` validation.

### Approved task changes

- None.
