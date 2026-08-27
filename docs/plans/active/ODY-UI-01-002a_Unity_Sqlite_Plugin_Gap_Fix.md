# ExecPlan — ODY-UI-01-002a: Unity/SQLite Plugin Gap Fix

**Governing task contract:** `docs/tasks/active/ODY-UI-01-002a_Unity_Sqlite_Plugin_Gap_Fix.md`
**Status:** Complete (deliverable produced and verified; PR pending CI/review)
**Created:** 2026-08-27
**Last updated:** 2026-08-27 UTC

## Authorities

- `docs/tasks/active/ODY-UI-01-002_Board_Screen.md` §4, §18 — the original discovery, read in full.
- `docs/adr/ADR-011_*` v1.1 §1 — accepted SQLite provider/version, not reopened.
- `DotNet/Projects/Odyssey.Persistence.csproj` — authoritative source of exact pinned package versions.
- `Packages/com.odyssey.persistence/Runtime/Odyssey.Persistence.asmdef`.
- `scripts/verify-test-structure.ps1` / `DotNet/Tests/Odyssey.Tests.Architecture/RepositoryArchitectureTests.cs` — the architecture guard this task had to satisfy.
- `docs/tasks/SLICE-UI-01_BACKLOG.md` §3.4–3.5 — Editor/desktop-only scope, real-SQLite decision.

## Investigation performed

1. Read `Odyssey.Persistence.csproj` in full: `Microsoft.Data.Sqlite 9.0.10`, `SQLitePCLRaw.bundle_e_sqlite3 3.0.3`.
2. Read `Odyssey.Persistence.asmdef`: `precompiledReferences: []`, confirming the original gap.
3. Investigated the local NuGet package cache (`~/.nuget/packages/`) to resolve the exact dependency chain. Discovered `sqlitepclraw.bundle_e_sqlite3 3.0.3`'s `.nuspec` depends on `SQLitePCLRaw.config.e_sqlite3` (provides `SQLitePCLRaw.batteries_v2.dll`) and `SourceGear.sqlite3` (native binaries) — a different, newer chain than the older `SQLitePCLRaw.lib.e_sqlite3` package.
4. Cross-verified this against the real `dotnet build` output at `artifacts/bin/Odyssey.Tests.Persistence/debug/`: confirmed the exact same managed-DLL set plus a `runtimes/<RID>/native/` tree.
5. Checked for any explicit `SQLitePCLRaw.batteries_v2.Init()` call in `Odyssey.Persistence`'s own code — found none (only a comment). Concluded the .NET path relies on `Microsoft.Data.Sqlite`'s own automatic provider initialization, which would need to be empirically verified to also work under Unity's Mono Editor hosting rather than assumed.
6. Copied the four managed DLLs into `Packages/com.odyssey.persistence/Runtime/Plugins/Managed/` and the win-x64 native `e_sqlite3.dll` into `Plugins/x86_64/`.
7. First attempt: set `Odyssey.Persistence.asmdef`'s `overrideReferences: true` with an explicit `precompiledReferences` list. Ran Unity Editor batchmode compile directly (`Unity.exe -batchmode -quit -nographics -projectPath . -logFile ...`) three iterations:
   - First run: 0 SQLite errors, but a **new** error surfaced — `SqliteGameLogRepository.cs` uses `Newtonsoft.Json.Linq`, unresolved.
   - Investigated: `Odyssey.Application.asmdef` lists `"Unity.Newtonsoft.Json"` in `references`, but `com.unity.nuget.newtonsoft-json` ships no asmdef at all — that reference entry is inert, and `Odyssey.Application` actually compiles via Unity's default auto-referencing of loose, unrestricted plugin DLLs. Since `overrideReferences: true` on Persistence disables that auto-referencing, `Newtonsoft.Json.dll` had to be added to `precompiledReferences` explicitly too.
   - Second run (with `Newtonsoft.Json.dll` added): 0 compile errors. Confirmed via `unity-compile3.log`.
8. Ran the full `scripts/test-unity.ps1`. To get real evidence that the *native* binary loads (not just that the managed DLLs resolve at compile time — no existing Unity-side test exercises `Odyssey.Persistence`'s SQLite path on this branch, since `ODY-UI-01-002`'s `BoardScreenPresenterTests.cs` lives only on the separate, not-yet-merged PR #67 branch), added a temporary, uncommitted EditMode test (`TempSqlitePluginRuntimeProbeTests.cs`) that constructs a real `SqliteCampaignRepository` and calls `Create(...)` against a real temp directory. Iterated through several small bugs in this throwaway test itself (missing `IWallClock` constructor argument; a `Microsoft.Data.Sqlite`-namespaced call requiring an assembly reference the test project didn't have; a Windows file-locking race in the test's own cleanup against SQLite's connection pool) — none of these were product defects, all were fixed by adjusting the temporary test file, and each fix was re-verified with a fresh full `scripts/test-unity.ps1` run. Final run before the architecture-policy issue: `editmode-results.xml total=37 passed=37 failed=0`, including the probe test passing — direct proof the native `e_sqlite3.dll` genuinely loads and performs a real SQLite database write under Unity's own Mono Editor runtime.
9. Ran `dotnet test DotNet/Odyssey.Core.sln` to check for regressions in the pure `.NET` path. `Odyssey.Tests.Architecture.RepositoryArchitectureTests.RepositoryStructurePassesArchitectureGuard` **failed**: `"Production asmdef must set overrideReferences=false: Packages/com.odyssey.persistence/Runtime/Odyssey.Persistence.asmdef."` — a real, enforced repository policy this task's first approach violated.
10. Course-corrected: reverted `Odyssey.Persistence.asmdef` to exactly its `main` state (`overrideReferences: false`, `precompiledReferences: []`) and re-ran the Unity batchmode compile with no asmdef change at all, relying purely on the new `Plugins/` folder's DLLs being present on disk. Result: 0 compile errors (`unity-compile-noasmdef.log`) — confirming the hypothesis that loose plugin DLLs are auto-referenced by Unity to every script assembly by default, the same mechanism already (accidentally) relied on for `Newtonsoft.Json.dll`. This is the simpler, policy-compliant fix, and became the final one.
11. Re-ran the full `scripts/test-unity.ps1` one more time with the asmdef fully reverted (temporary probe test already removed at this point, since its purpose was served): `editmode-results.xml total=36 passed=36 failed=0`, `playmode-results.xml total=2 passed=2 failed=0`.
12. Re-ran `dotnet build`/`dotnet test`: 0 errors, all 262 tests passing including the previously-failing architecture guard test.
13. Ran `scripts/verify-format.ps1` and `scripts/check-repository-policy.ps1`: both passed, including `TC-CI-006` reconfirming CI's Unity check remains static-only.
14. Cleaned the git diff: discarded `ProjectSettings/ProjectSettings.asset`'s incidental trailing-whitespace churn (Unity's own YAML writer touching it on every Editor open) and the incidental stray `.meta` files Unity auto-generates for unrelated, pre-existing `Packages/**.cs` files across `com.odyssey.application`, `com.odyssey.networking`, and `com.odyssey.persistence/Runtime/Sqlite/` — same precedent `ODY-UI-01-002` established. Final diff scope: only the new `Plugins/` folder.

## Intended change

- New: `Packages/com.odyssey.persistence/Runtime/Plugins/Managed/{Microsoft.Data.Sqlite.dll, SQLitePCLRaw.core.dll, SQLitePCLRaw.provider.e_sqlite3.dll, SQLitePCLRaw.batteries_v2.dll}` (+ `.meta` each).
- New: `Packages/com.odyssey.persistence/Runtime/Plugins/x86_64/e_sqlite3.dll` (+ `.meta`).
- New: `Packages/com.odyssey.persistence/Runtime/Plugins.meta`, `Plugins/Managed.meta`, `Plugins/x86_64.meta`.
- Unchanged: `Packages/com.odyssey.persistence/Runtime/Odyssey.Persistence.asmdef` (zero diff from `main`).
- Changed: `docs/tasks/SLICE-UI-01_IMPLEMENTATION_BACKLOG.md` (row 1 blocker note).
- New: this task's contract, this ExecPlan.

## Tests or validation commands

```powershell
.\scripts\test-unity.ps1
.\scripts\verify-format.ps1
.\scripts\check-repository-policy.ps1
```

```bash
cd DotNet
dotnet build Odyssey.Core.sln
dotnet test Odyssey.Core.sln
```

## Explicit non-goals

- Swapping the SQLite provider or touching any `Sqlite*Repository`/`SqliteSavingPipeline` code.
- Fixing `Odyssey.Application.asmdef`'s inert `"Unity.Newtonsoft.Json"` reference entry (documented, not touched).
- Committing the broader set of pre-existing stray `.meta` files unrelated to this fix.
- Adding a real Unity Editor compile step to CI.
- Non-Windows native SQLite binaries.
- Any `ODY-UI-01-003`+ work.
