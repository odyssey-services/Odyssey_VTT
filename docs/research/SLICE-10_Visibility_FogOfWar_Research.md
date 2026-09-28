# SLICE-10 — Visibility / Fog of War / Obstacle Geometry: Research Report

**Type:** research only. No production code, no ADR, no decomposition into tasks, no estimates (per the governing ТЗ §2).
**Baseline:** `main` at `727d51c` (after PR #186). Every path below is relative to the repository root unless stated otherwise; `file:line` references are to that commit.
**Precedent for format:** `docs/research/Unity_Rules_Asmdef_Break_Investigation.md` (PR #173) — a single research document, no `docs/tasks/active`/`docs/plans/active` entry (PR #173 added none, so none is added here).

## 0. Summary of findings

1. **The preliminary assumption "there is nothing at all about geometry/visibility in the code" is refuted in part, confirmed in the rest.** A small, real geometry module exists and is used in production paths: `Packages/com.odyssey.domain/Runtime/Geometry/BoardGeometry.cs` (69 lines) — `GeometryEpsilonV1`, `IsFinite`, `EuclideanDistance`, `AlmostEqual`, `SamePosition` — with 5 tests. It is **only the `GridType=None` subset** of `ADR-020` and contains **no** segment/polygon/intersection/orientation primitive, none of the seven services `ADR-020` names, no `SpatialIndexV1`, and nothing about walls, doors, windows, line of sight, cover or fog. See §1.2 for the exhaustive search.
2. **No scene object other than "token" and "scene background" exists** anywhere in code (§1.3). `SceneObject`, layers, components, `GridSettings`, board bounds: absent. Walls/doors/windows/vision sources/fog regions would be entirely new data types, not extensions.
3. **"Per-player" state has no first-class home in the domain today** (§1.4). `UserId` is a value type; there is no persisted user/member/participant table; membership exists only as an in-memory session model; the desktop client's "who am I" is a three-user development stub. Token control (`TokenRecord.ControllerUserId`) and character ownership (`CharacterOwnership`) are two separate, unlinked sources of "who controls".
4. **Two disjoint scene models exist** (§1.3/§1.4): the SQLite-persisted `SceneRecord`/`TokenRecord` (what the board client uses) and a separate in-memory networking projection model (`Scene`/`SceneEntity`, per-user redaction). Nothing bridges them in production code.
5. **The normative product specification for all of this already exists** (`08_Scenes_And_Board_Odyssey_VTT_v0.5.md`, §§13–16, 19, 21–22, 25, slices `BOARD-04`/`BOARD-05`) — **but it lives in a git-ignored local folder (`Documentation/`) and is not in the repository** (§1.1a). This report cites it by section/line from the local copy at `D:\Documents\Odyssey_VTT\Documentation\`.
6. **The attack pipeline already has the seams cover would plug into** (§1.5): a `Topology` input already carrying a per-target distance, an `AttackModifierEntry` list that the evaluator currently always returns empty, and `AttackRangeResult`/`AttackHitResult`. There is no cover concept in code.
7. The board UI has coordinate conversion, rectangle-style overlays and a button-aware gesture state machine, but **no oriented-line drawing path** (§1.6).
8. Command/toggle precedents with `Result` + `expectedRevision` + `commandId` exist in several shapes (§1.7); the closest structural template for a door toggle is the `SetTokenPortrait`/`SetTokenZOrder`/`SetTokenScale` family plus `BoardMovementService`'s validate-then-commit layering.
9. Test infrastructure is ready for a new geometry test file but has **no** determinism-specific tooling (§1.8).

## 1.1 `ADR-020` — line-by-line reading

Source: `docs/adr/ADR-020_Board_Geometry_And_Movement_Determinism_v1.0.md` (280 lines; status `Accepted`, dated 2026-08-26).

### 1.1a Where the product document is (and is not)

`ADR-020`'s §2 cites `08_Scenes_And_Board_Odyssey_VTT_v0.5.md` §13.4 (epsilon) and §25.1 (spatial index) as the two places the product document itself defers to "implementation ADR". That file is **not tracked by git**: `.gitignore:2` lists `Documentation/` (`git ls-files | grep -c '^Documentation/'` → `0`), and `docs/adr/` contains only ADRs. A local copy exists at `D:\Documents\Odyssey_VTT\Documentation\08_Scenes_And_Board_Odyssey_VTT_v0.5.md` (3069 lines) and was used for this report. Anyone reviewing this report from a clean clone cannot verify the product-document citations. (Also noteworthy: `BoardGeometry.cs:30` cites it as `06_Scenes_And_Board`, the ADRs as `08_Scenes_And_Board` — a naming inconsistency in a code comment only.)

### 1.1b Decisions the ADR fixes (quoted)

`ADR-020` §1 "Обязательные решения" (lines 18–25):

1. **Cross-platform arithmetic:** "вся авторитетная geometry-математика выполняется исключительно операциями IEEE-754 `System.Double` через `System.Math`, без `MathF`/`float`, без platform-specific fast-math/FMA-переупорядочивания, в фиксированном, документированном порядке операций для каждой формулы".
2. **Distance formulas** (§5): three Square metrics — `Euclidean` (default; `sqrt(dx² + dy²)`, no grid conversion), `ChebyshevDiagonalEqualsOne` (`max(abs(dCellX), abs(dCellY)) × c` per segment, accumulated), `AlternatingOneTwo` (diagonal steps alternate cost 1,2,1,2 counted along the whole `MovementPath`, not reset per `Segment`); hex distance via cube coordinates (`(abs(qA-qB)+abs(rA-rB)+abs(sA-sB))/2 × WorldUnitsPerCell`); `GridType=None` = Euclidean world distance.
3. **Epsilon:** `GeometryEpsilonV1 = 1e-6` world units (§6.1), versioned by the `ADR-008` `…V1` naming style; one epsilon-tolerant orientation primitive, `orientation(P,Q,R) = (Q.X-P.X)(R.Y-P.Y) - (Q.Y-P.Y)(R.X-P.X)`, classified `Left`/`Right`/`Collinear` by `abs(orientation) < GeometryEpsilonV1 × scaleFactor` (§6.2), used "во всех geometry-сервисах — не отдельная epsilon-логика на сервис"; a boundary/`Collinear`/touching case in LOS/cover/movement-obstacle intersection is **blocking** (fail-closed, §6.3).
4. **Spatial index:** `SpatialIndexV1` = uniform spatial hash — key is the grid cell for `Square`/`Hex` (footprint cells all indexed), a fixed-size world-unit bucket for `GridType=None` (bucket size left as a tuning parameter, §7.1); not R-tree/quadtree (§7.2); one index for occupancy, obstacle intersection, visible-object, area-target and cover-candidate queries (§7.3); cache invalidation tied to `SceneObjectRevision`/`BoardRevision`.
5. **Rounding:** `WorldPosition → GridCoordinate` by **floor** relative to `GridSettings.Origin` (§4.3), explicitly not round-half-away / banker's.
6. **Forbidden sources of non-determinism** (§4.2): `float`/`MathF`/`Unity.Mathematics`/`UnityEngine.Vector2/3` as an authoritative source; order-dependent reductions (`AggressiveOptimization`/vectorized, `Parallel.For`, PLINQ); clock/culture inputs.

### 1.1c Services the ADR names

§4.2 (line 79) lists: `BoardGeometryService`, `GridCoordinateService`, `MovementPathValidator`, `ObstacleIntersectionService`, `LineOfSightService`, `CoverSuggestionService`, `AreaIntersectionService` (attributed to `08_Scenes_And_Board` §4.4). §7 additionally names `SpatialIndexV1`; §3.5 `GridCoordinate`; §3.1 `WorldPosition`/`WorldVector`/`WorldRect` ("уже принято, не переопределяется" — value types from the product document §6.1).

The product document's own §4.4 list is longer (it adds `MapCalibrationService`, `TokenPlacementService`, `FogProjectionService`, `VisionResolver`, `DrawingService`, `SceneReadinessService`): see `08_Scenes_And_Board…v0.5.md:442-462`.

### 1.1d What the ADR explicitly excludes (§8, verbatim, lines 174–181)

- "**Полная схема `Scene`/`Board`/`SceneObject`/`Token`** (`08_Scenes_And_Board` §4) — implementation-задача, использующая эту ADR как математическую основу, не содержание самой ADR."
- "**Unity-рендеринг-оптимизация** (… §25.4 …) — явно presentation-layer tuning".
- "**Сетевая доставка board-дельт** — уже `ADR-017`, не переоткрывается."
- "**Командная модель движения токена** … — уже `ADR-002`, не переоткрывается; этот ADR фиксирует только геометрический шаг (§12.4 пункты 7–9)".
- "**Circular footprint rasterization rule** (`OPEN-BOARD-005`)".
- "**Fog physical representation** (polygon vs tile/mask, `OPEN-BOARD-004`) — отдельный non-blocking open item, не геометрия movement/intersection."
- "**Технический спайк**" and "**Production-реализация** … — future implementation-задача."

Confirmed still true at `727d51c`: the ADR is a mathematical specification; the domain schema for walls/doors/fog is not in it. Also §11 (Definition of Done for the future implementation) requires six proofs, incl. bit-identical results on pure .NET and Unity Mono/IL2CPP (item 1), golden vectors for every formula (item 2), the four §13.4 boundary scenarios each tested as blocking (item 3), brute-force cross-check of the spatial index (item 4), restart-restore identity via the `ADR-012` snapshot mechanism (item 5), and a compile of the Core geometry assembly without `UnityEngine` (item 6). None of items 1, 3, 4, 5 has a counterpart today (§1.8).

### 1.1e Product-document requirements that *define the target* (not in the ADR)

For orientation only (from the local copy of `08_Scenes_And_Board_Odyssey_VTT_v0.5.md`; this report does not propose an architecture):

- Obstacle model: walls are polylines split into per-segment `SceneObject`s each with its own id and revision (§13.1, lines 1114–1124); geometry uses the centre segment, `VisualThickness` is render-only (§13.2); six independent flags `BlocksMovement/BlocksVision/BlocksProjectiles/BlocksEffects/BlocksSound/ProvidesCover` (§13.3); editing a shared vertex updates several segments in one atomic command (§13.5).
- Doors: independent flags `IsOpen/IsLocked/IsHidden/IsDestroyed`, "player movement does not auto-open a door", `InteractWithDoorCommand` pipeline ending in "fog/LOS invalidation" (§14.1–14.2, `BOARD-INV-013`). Windows: `State = Intact|Open|Broken|Boarded` (§14.3). Hidden doors block on the host but are excluded from projection (§14.4).
- LOS `ViewerCenter → SamplePoint`; line of attack centre-to-centre for any footprint size (`BOARD-INV-014`, §15.1–15.2); cover is a *suggestion* the GM may override (`BOARD-INV-015`, §15.4–15.8).
- Fog: per-audience `FogState` (`BOARD-INV-017`), three states `Unexplored/Explored/Visible` (`BOARD-INV-018`), `AudienceKey` vocabulary `User:<id>|Group:<id>|CharacterOwners:<id>|CharacterControllers:<id>|SceneParticipants` (§16.3), `VisionSourceComponent` Circle/Cone (§16.4), explored-region projection rules and last-known door state per audience (§16.7), manual reveal grants with expiry (§16.8), a recalculation-trigger list (§16.10).
- Physical fog representation (polygon vs mask) is left to the implementer (§16.1, §25.3, `OPEN-BOARD-004`).
- The product document's own slices are `BOARD-04 Structures and cover` and `BOARD-05 Fog and vision` (lines 2783–2802); command names for both are already listed (§22.4–22.5, lines 2017–2045); persistence "logical storage baseline" names `SceneObjects`, `SceneObjectComponents`, `FogStates`, `FogRegions/MaskBlobs`, `ManualRevealGrants` (§21.1).
- `BOARD-INV-028`: "Скрытые данные не вычисляются клиентом" (line 342) — relevant to *where* per-player visibility may be computed (host).

## 1.2 Is there any geometry code? Exact search

### Searches run (on `727d51c`)

1. Names of every service/type `ADR-020` lists — `BoardGeometryService|GridCoordinateService|MovementPathValidator|ObstacleIntersectionService|LineOfSightService|SpatialIndexV1|CoverSuggestionService|AreaIntersectionService|GeometryEpsilon` across `*.cs`, `*.json`, `*.ps1`, `*.csproj`, `*.asmdef`: hits in only three files —
   - `Packages/com.odyssey.domain/Runtime/Geometry/BoardGeometry.cs` (only `GeometryEpsilonV1`),
   - `DotNet/Tests/Odyssey.Tests.Domain/Geometry/BoardGeometryTests.cs`,
   - `Tests/Metadata/test-catalog.json` (the test catalogue entries for those tests).
   In markdown the same names appear only in `ADR-020` and completed task/plan documents of `ODY-S03-001`, `ODY-S03-004`, `ODY-S06-104` and the `SLICE-03` backlogs.
2. Keywords `fog|FogState|VisionSource|WallSegment|DoorComponent|IsOpen|ProvidesCover|BlocksVision|CoverProposal|LineOfAttack|AttackLine` across `Packages`, `Assets`, `DotNet`, `Tools`: **one** hit — a comment at `Packages/com.odyssey.application/Runtime/Board/BoardContracts.cs:68` ("this task has no hidden-token/fog model yet"). Same keywords in json/ps1/yml/asmdef: none.
3. Broader keyword sweep (`line of sight|LOS|visibility|Wall|Door|Window|Obstacle|Polygon|Segment|Raycast|Cover`) across `*.cs`: all remaining hits are unrelated (string "segment" in error-code/path parsing, "concurrency window", roll-panel modifier label `"Cover"`, `BackgroundSizeType.Cover`, and the networking visibility policy of §1.4). No geometry primitive.
4. `Physics|Collider|Raycast|Mesh|generateVisualContent|MeshGenerationContext|Painter2D|Vertex` under `Assets/Odyssey` (excluding third-party): **no hits**.
5. `GridSettings|GridType|HexFlat|WorldUnitsPerCell` in code: only inside `BoardGeometry.cs`/its test *comments*.

### What exists, in full

`Packages/com.odyssey.domain/Runtime/Geometry/BoardGeometry.cs` (created by `ODY-S03-004`, commit `f51aaa7`), a `public static class BoardGeometry` in `Odyssey.Domain.Geometry` (Domain has no dependency on other modules; compiled into the .NET bridge via `DotNet/Projects/Odyssey.Domain.csproj:11` glob `Packages\com.odyssey.domain\Runtime\**\*.cs`, and into Unity via `Odyssey.Domain.asmdef`):

```csharp
public const double GeometryEpsilonV1 = 1e-6;                                             // :27
public static bool IsFinite(double x, double y) => double.IsFinite(x) && double.IsFinite(y); // :33
public static double EuclideanDistance(double ax, double ay, double bx, double by)         // :43
{ double dx = bx - ax; double dy = by - ay; return Math.Sqrt((dx * dx) + (dy * dy)); }
public static bool AlmostEqual(double a, double b, double epsilon = GeometryEpsilonV1) => Math.Abs(a - b) < epsilon;   // :56
public static bool SamePosition(double ax, double ay, double bx, double by, double epsilon = GeometryEpsilonV1) =>
    AlmostEqual(ax, bx, epsilon) && AlmostEqual(ay, by, epsilon);                          // :66
```

Its own doc comment (lines 5–19) states the scoping: "scoped to the `GridType=None` case … Square/Hex distance metrics and grid-coordinate conversion (ADR-020 sections 4.3, 5.1-5.2) remain for a future task that actually needs a grid."

Compared with `ADR-020`: implemented — `GeometryEpsilonV1` (§6.1), Euclidean/None distance (§5.1/5.3), finite-only check (§4.2 via product §6.1), scalar epsilon comparison. **Not** implemented — the orientation primitive and `Left/Right/Collinear` classification (§6.2), fail-closed boundary semantics (§6.3), floor rounding / `GridCoordinate` (§4.3), Chebyshev/Alternating/hex distance (§5.1–5.2), `SpatialIndexV1` (§7), every named service. There is no segment, polygon, rectangle or vector type (`TokenPosition` is a `(double X, double Y)` struct in the *Application* layer, `SceneRepositoryContracts.cs:144`, not a Domain geometry type; `BoardGeometry` takes raw doubles).

### Tests

`DotNet/Tests/Odyssey.Tests.Domain/Geometry/BoardGeometryTests.cs` (63 lines, 5 `[Test]` methods, catalogue ids `TC-BOARD-001..003`, "golden-vector style: expected results are computed by hand"): 3-4-5 triangle and same-point distance; `IsFinite` on NaN/±Infinity; `AlmostEqual` just inside/outside `GeometryEpsilonV1`; `SamePosition` on both axes. No test about line of sight, visibility, fog, intersection, cover.

### Production call sites of `BoardGeometry`

- `Packages/com.odyssey.application/Runtime/Board/BoardMovementService.cs:39` (`IsFinite` on the destination) and `:120` (`SamePosition` for occupancy — `BOARD-INV-009` interpreted as epsilon-equal coordinates because "no footprint/grid-cell model yet", `BoardGeometry.cs:59-65`).
- `Packages/com.odyssey.persistence/Runtime/Sqlite/SqliteAttackStateReader.cs:103` (`EuclideanDistance` actor→target; see §1.5).
- Movement has **no obstacle check at all**: `BoardMovementService.MoveToken` validates finite destination → authorization → occupancy → re-authorization → `ISceneRepository.MoveToken`; there is no path, no wall test, no door test, no cost/budget.

### Verdict on the preliminary assumption

- "Nothing about geometry" — **partly wrong**: a 69-line `GridType=None` primitive set exists, is tested, and is used by movement occupancy and attack distance.
- "Nothing about intersection / visibility / walls / fog / cover / grid / spatial index" — **confirmed** by the searches above.
- `docs/tasks/SLICE-08_IMPLEMENTATION_BACKLOG.md:51` describes `ADR-020` as "a separate, not-yet-started track"; this is stale in that the `None`-grid subset has been implemented since `ODY-S03-004`.

## 1.3 Current `Scene`/`Board` domain schema

### `SceneRecord` (`Packages/com.odyssey.application/Runtime/Persistence/SceneRepositoryContracts.cs:159-190`)

`SceneId`, `CampaignId`, `Name` (≤128), `Status` (free `string`), `Revision`, `CreatedAt`, `UpdatedAt`, `BackgroundAssetId?` (from `ODY-S07-105`). SQLite DDL (`Packages/com.odyssey.persistence/Runtime/Sqlite/SqliteSceneRepository.cs:1048-1058`): `SceneId, CampaignId, Name, Status, Revision, CreatedAt, UpdatedAt, LastCommandId, BackgroundAssetId`. **No** board bounds, grid settings, world-unit definition, background transform/calibration, layers, default vision range.

### `TokenRecord` (`SceneRepositoryContracts.cs:192-…`, DDL directly after `Scene` in the same file)

`TokenId`, `SceneId`, `CampaignId`, `Position` (`TokenPosition`, doubles), `ControllerUserId`, `Revision`, `CreatedAt`, `UpdatedAt`, `CharacterId?`, `PortraitAssetId?`, `ZOrder` (`long`), `Scale` (`double`, `[0.5, 3.0]`, `TokenRecord.MinScale/MaxScale`; both from `ODY-S08-105`). No footprint cells, no facing, no presence kind (token vs marker), no vision source, no visibility flag.

### `ISceneRepository` surface (`SceneRepositoryContracts.cs:40-141`)

`CreateScene`, `CreateToken`, `GetToken`, `MoveToken`, `ListTokens`, `ListTokensByCharacter`, `RegisterAsset`, `SetSceneBackground`, `SetTokenPortrait`, `SetTokenZOrder`, `SetTokenScale`, `GetScene`, `ReadAssetContent`, `ListAssets`. Every mutating method is revision-gated and command-id idempotent through `SqliteSavingPipeline`.

### Is there any "object on a scene that is not a token or a background"?

No. `SceneObject`, `SceneObjectComponent`, layers, props, decorations, zones, obstacles do not exist in any `.cs` file (`grep SceneObject` in `*.cs` → 0 hits; the term appears only in ADRs and completed-task markdown). SQLite tables in the campaign database (`CREATE TABLE IF NOT EXISTS` sweep over `Packages`): `Scene`, `Token`, `AssetManifestEntries`, `AssetReferences`, character/inventory/combat/effect/log tables, `AppliedCommands`, `DomainEvents`, `AggregateRevisions`, `NetworkOutbox`, `SessionArchiveIndex`, etc. — nothing resembling structures, fog or vision. Walls/doors/windows/vision sources/fog regions/reveal grants would be **new aggregates and new tables**, not extensions of `Token`.

The schema is created with `CREATE TABLE IF NOT EXISTS` and is unversioned (see `ODY-S08-105` task contract §18): adding a *column* to an existing table is not applied to pre-existing campaign files; adding a *new table* is applied automatically by the same `EnsureSceneTokenTables`-style call. (Stated as a fact about the current mechanism, not a recommendation.)

### The second, separate scene model (networking)

`Packages/com.odyssey.application/Runtime/Networking/Projection/SceneProjectionContracts.cs`: `Scene` (`:60`, `SceneId` string + `List<SceneEntity>`), `SceneEntity` (`:31`, `EntityId`, `DisplayName`, `SceneEntityVisibility {Public=1, HiddenGameplay=2}`, `AssignedToUserId?`), `VisibilityPolicy.ComputeVisibleEntities` (`:114`), `ProjectionSnapshot` (per `AudienceUserId`); the token-move network path `TokenMoveContracts.cs` (`SceneMutableState :30`, `MoveTokenService :208`, `DeltaBroadcastPlanner :333`). These types are used only by networking/projection/reconnect code and tests; **`ISceneRepository`/`BoardMovementService`/`BoardGeometry` are not referenced from `Packages/com.odyssey.networking` or `Runtime/Networking/**`** (grep → 0 hits). So the persisted board and the projected/redacted scene are two unconnected models, and the networked move path does no geometry at all (no occupancy, no distance).

## 1.4 Player / user model

- **`UserId`**: `Odyssey.Domain.Identity.UserId` (`Packages/com.odyssey.domain/Runtime/Identity/DomainIdentity.cs:82`), a `user_<32 hex>` value type. It is a value everywhere (`TokenRecord.ControllerUserId`, `SessionMember.UserId`, `CharacterOwnership.*`, `MoveTokenRequest.ActorUserId`) — but there is **no user/member/participant aggregate or table**: the campaign SQLite schema has no such table (see the table sweep in §1.3), and no `IUserDirectory`/`ICampaignMember*` port exists (grep → 0 hits).
- **`CampaignHandle`** (`CampaignRepositoryContracts.cs`): a handle to an opened campaign's root path + `CampaignId`; it carries no user or membership data.
- **Session membership** is in-memory only: `SessionMember(UserId, MemberAdmissionState, BaselineRole)` in `Packages/com.odyssey.application/Runtime/Networking/Session/SessionAdmissionContracts.cs:173`; `BaselineRole { MainGM = 1, Player = 2, Observer = 3 }` (`:15`). Its own doc says "no AssistantGM … `SLICE-02_IMPLEMENTATION_BACKLOG` §4's explicit narrowing".
- **The Unity client's "who am I"** is `Assets/Odyssey/Client/Runtime/RoleSelection.cs`: three hard-coded default ids (`user_…0001` MainGM, `…0002` Player, `…0003` Observer) switched by a role selector — a development stub, explicitly the mechanism behind `BoardScreenPresenter.LocalActorUserId/LocalActorIsMainGm`. There is exactly one client process acting as any of three users; no real multi-client session exists to receive a per-player view.
- **Character vs player.** A *character* is a `CharacterRecord` whose `Ownership` (`Packages/com.odyssey.domain/Runtime/Character/CharacterOwnership.cs:16`) has `PrimaryOwnerUserId?`, `CoOwnerUserIds`, `PermanentControllerUserIds`, `TemporaryControlGrants` (with expiry evaluated lazily by `CharacterOwnershipAssignment.IsAssignedCharacter(ownership, actorUserId, now)`, `:81`). A *token* is a `TokenRecord` with its **own** `ControllerUserId` (a single user) and an optional `CharacterId`. `BoardMovementService.CheckAuthorization` (`BoardMovementService.cs:~96-103`) decides move rights from `token.ControllerUserId` alone; it does **not** consult `CharacterOwnership`. So "which user(s) may see through this token" has two candidate sources (token controller, character owners/controllers) that are not linked in code today. The product document's fog `AudienceKey` vocabulary explicitly has both `CharacterOwners:<id>` and `CharacterControllers:<id>` (§16.3).
- **Audience resolution precedent.** `Packages/com.odyssey.application/Runtime/Audience/AudienceContracts.cs` holds the read-model `CampaignUserGroup` and `ICampaignUserGroupDirectory` (only an in-memory fixture implementation `InMemoryCampaignUserGroupDirectory`); `DiceRollAudienceKind {Public, PlayerAndGM, GMOnly, SelectedParticipants}` in `DiceContracts.cs:95`. `ADR-021` §3.3 records that fog has "свой `AudienceKey`-словарь" and that artifacts like a "fog-регион" have an audience that can change after creation.
- **Where "what this player currently sees" could physically live today.** Nowhere: no persisted per-user state exists in the scene domain; the only per-user computed state is the *ephemeral* `ProjectionSnapshot` per `AudienceUserId` on the networking side (§1.3). Persisted per-audience state would need a new table keyed by audience; the product document sketches `FogStates` keyed by `AudienceKey` (§16.2, §21.1).
- **Highlighted finding:** because a user is not a first-class persisted entity and a real multi-client session is not wired (no working client-to-client transport; `ADR-016` bars the chosen relay from production before a pilot), "visibility per player" currently has neither a persisted subject nor a delivery channel; the closest existing pieces are the in-memory `VisibilityPolicy` + `ProjectionSnapshot` (server-side per-user redaction, `BOARD-INV-028`-style) and the ADR-021 audience vocabulary.

## 1.5 Attack pipeline — where cover would attach

Governing spec: `docs/adr/ADR-029_Full_Attack_Pipeline_Specification_v1.0.md` (stage table around line 95–104: stage 3 "range — Rules evaluates topology/distance/line constraints from the authoritative scene/encounter snapshot"; stage 4 "modifiers — Rules collects applicable snapshot-based modifiers in stable Rules-defined order, including active effects and equipment"; stage 6 "hit — from range, modifiers, and roll"). Product side: `12_Combat_And_Actions_Odyssey_VTT_v0.1.md` §20 "Cover" (line 643: "Линия атаки — центр токена атакующего к центру токена цели. Геометрия создаёт `CoverProposal`… Cover получает весь `DamagePacket`; переполнение … не переходит к цели"), and the attack record field `CoverProposals[]` (line 520); none of `CoverProposal`/`CoverHit`/`CoverComponent` exists in code.

### Files and the data flow (task ids `ODY-S06-102/103/104/105`, `ODY-S05-6xx`)

1. **Read side (persistence):** `Packages/com.odyssey.persistence/Runtime/Sqlite/SqliteAttackStateReader.cs` builds `AttackEvaluationSnapshot`. `ReadTopology` (`:~84-113`) resolves the actor's token via `ListTokensByCharacter`, and for each target whose token is on the **same scene** computes `BoardGeometry.EuclideanDistance` (`:103`) and returns `AttackTopologyInput.Available(entries)` or `.Unavailable("No resolvable actor/target token position binding.")`; unresolvable targets are simply absent, "not a hard-fail". `ReadArmor` (`:~115-…`) aggregates each target's equipped armor from `ListEquippedEntriesByCharacter`.
2. **Snapshot types (domain):** `Packages/com.odyssey.domain/Runtime/Combat/AttackPipelineContracts.cs` — `AttackTargetDistanceEntry(CharacterId targetId, double distance)` (`:~137`), `AttackTopologyInput` (`:156`, `Available`/`Unavailable`), `AttackArmorInput` (`:94`), `AttackEvaluationSnapshot` (`:291`, fields `Topology`, `ArmorAndEffects`, `ActionWeapon`, …), and the output `ProposedAttackResolution` (`:333`: `Range`, **`Modifiers` (`IReadOnlyList<AttackModifierEntry>`)**, `Hit`, `BodyPart`, `Armor`, `DamageDeltas`). `AttackModifierEntry(string source, int value)` exists as a type.
3. **Decision point:** `Packages/com.odyssey.rules/Runtime/Combat/CoreAttackRulesEvaluator.cs`:
   - per target `IsInRange(snapshot, targetId, weapon.Range)` (`:60`, body `:112-120`): `Topology` unavailable ⇒ "in range"; otherwise `entry.Distance <= weaponRange` — **this is where the geometry result is consumed today**;
   - `AggregateProtection(snapshot, targetId, out armorRef)` (`:68`, body `:~130-145`): sum of `ArmorDefinition.Protection` over the target's equipped armor — "no selection by `CoveredBodyPartIds`, since no hit-location model exists yet" (`:125`);
   - `damage = max(0, formulaValue - protection)` (`:80-81`);
   - `Range` and `Hit` are single aggregates and `Hit` **mirrors** `Range` ("no separate to-hit mechanic exists yet", `:84-88`);
   - the returned `ProposedAttackResolution` is constructed with `Array.Empty<AttackModifierEntry>()` for `Modifiers` (`:96`).
4. **Application orchestration:** `Packages/com.odyssey.application/Runtime/Combat/AttackEvaluationService.cs` (decodes the weapon before the snapshot reaches `Odyssey.Rules`, because Rules cannot reference Application per `ADR-001` §6.2 — see the doc comment at `AttackPipelineContracts.cs` `AttackEvaluationSnapshot` ctor) and `AttackApplyService.cs`/`Domain/Combat/AttackApplyContracts.cs` for applying deltas.

### How armor is wired (the "precedent")

Armor is a snapshot **input** assembled by the persistence reader (data), and a Rules-side **aggregation** (`AggregateProtection`) that produces both a numeric effect on damage and a reference (`AttackArmorProposal(armorRef, absorbed)`). Content-side, `ArmorDefinition` (`Packages/com.odyssey.domain/Runtime/Content/TypedDefinitions.cs:~170-207`) carries `EquipmentSlotKey`, `CoveredBodyPartIds` and `Protection`.

### Structure a further "modifier" would have to fit (facts only)

- geometry-derived facts already have a designated carrier: the `Topology` input of the snapshot (today: one distance per target);
- the evaluator already has an empty, typed, ordered modifier output list (`AttackModifierEntry`) that stage 4 of `ADR-029` says is "collected in stable Rules-defined order";
- the read-side/Rules-side split (persistence assembles facts, Rules decides consequences) and the Rules→Domain-only dependency direction constrain where geometry code may sit: a cover computation needing wall data would be a *read-side* concern (persistence/application) whose result travels in the snapshot, unless the geometry library is placed in `Odyssey.Domain` (where `BoardGeometry` already lives and which `Odyssey.Rules` may reference). Not evaluated further here.
- the product spec makes cover a *proposal* the GM confirms or overrides, recorded with reason/actor (`08_Scenes…:1331-1339`, `12_Combat…` §20), and separates the projectile obstacle flag from vision (§15.3) — neither has a counterpart in the current pipeline.

## 1.6 Board UI — what is reusable

`Assets/Odyssey/Client/Runtime/BoardScreenPresenter.cs` (1416 lines; a plain C# class over UI Toolkit, code-built `VisualElement`s), `BoardCamera.cs`, `BoardPointerGesture.cs`, `BoardBoxSelectGesture.cs`.

- **World↔pixel conversion:** `BoardCamera` (pure C#): `ToPixelsX/Y`, `FromPixelsX/Y` (`BoardCamera.cs:65-71`; `pixel = (world − offset) × scale`), `Pan`, `Zoom` (anchor-preserving), scale clamped `[5, 400]` px/unit. Presenter helpers `ToWorldPosition(px,py)` and the per-element `PositionTokenElement`/`RepositionTokens` (re-anchor everything to the camera after pan/zoom without a DOM rebuild).
- **Overlay elements already implemented, all absolutely-positioned rectangles:** the selection box (`UpdateBoxElement`, `BoardScreenPresenter.cs`, `PickingMode.Ignore`, re-added after `RenderTokens` clears the board), the player marker ring (`PlacePlayerMarker`, `border-radius` on a square element, world-anchored, repositioned via `RepositionTokens`, auto-expiring via `element.schedule`), token squares/portraits, the scene background.
- **`BoardBoxSelectGesture`** (`BoardBoxSelectGesture.cs`): pure C# rectangle-from-two-corners with a drag threshold (`Begin/Move/TryGetBox/End/Cancel`); it stores start and current point and normalises to min/max. It is rectangle-specific (returns an axis-aligned box); the two stored points are exactly what a straight segment needs, but as written it exposes only the normalised box, not the ordered pair. Whether to generalise it is a design question not decided here.
- **Gesture infrastructure:** `BoardPointerGesture` (5 px click/drag disambiguation; deltas), the board button state machine `HandleBoardButtonDown/Up` with a single owner `_activeBoardButton` (left = click/box, middle = pan, right = marker, `ODY-S08-107`), the token gesture (`BeginTokenDrag/MoveTokenDrag/EndTokenDrag`, left-button only), wheel handling (`HandleBoardWheel`: Shift+wheel scales a token, plain wheel zooms). Everything is exposed through public number-driven methods for tests (project convention); real UI Toolkit event wiring is not simulated in tests.
- **Selection / hit-testing:** selection is a session-only set of token ids; `HitTestToken` is pure math over `_tokenPositionsByTokenId` and scales; there is **no hit test for anything but tokens** and no generic "scene object" selection.
- **What is absent:** any oriented-line/polygon drawing path in the client (no `generateVisualContent`, `MeshGenerationContext`, `Painter2D`, textures made procedurally — search in §1.2 item 4). The only rendering primitives used are rectangle `VisualElement`s (fills, borders, radius) and `backgroundImage` textures (`AssetTextureCache`). Any other drawing technique in UI Toolkit is outside what this repository demonstrates and was not investigated here.
- No grid is drawn (no grid model exists, §1.3); no layers; the presenter renders exactly one background plus tokens.
- Size note: `BoardScreenPresenter.cs` already hosts token rendering, selection, drag, z-order, scale, camera gestures, box selection, marker, asset drop (1416 lines).

## 1.7 Toggle-state precedents

Structure of the existing revision-gated write path: interface method on a repository → `SqliteSavingPipeline.Execute` (`Packages/com.odyssey.persistence/Runtime/Sqlite/SqliteSavingPipeline.cs`; idempotent replay by `CommandId`, journal/event/`AggregateRevisions` writes, revision conflict → typed error), with an Application-layer static service in front when there are legality rules.

Closest precedents found:

1. **Scene/token setters** (`SceneRepositoryContracts.cs:99,104,109`): `SetTokenPortrait`, `SetTokenZOrder`, `SetTokenScale` — `(campaign, id, newValue, expectedRevision, commandId, correlationId) → Result<TokenRecord>`; read current row in-transaction, compare revision (`TokenRevisionConflict`, `ErrorCodes.cs:29`), `UPDATE … Revision+1`, emit a `PipelineWrite` event with `aggregateRevision`. A boolean/state toggle is structurally the same call with a different column.
2. **`BoardMovementService.MoveToken`** (`Packages/com.odyssey.application/Runtime/Board/BoardMovementService.cs`): the layering "validate request → load current → submission-time authorization → domain rule (occupancy) → pre-commit authorization again → repository write with the atomic revision guard" — the doc comment cites `ADR-002` §11 ordering and `ADR-019` §6.1's two-point authorization. This is the template for "a command with legality checks before the write".
3. **Equip/Unequip** (`Packages/com.odyssey.application/Runtime/Inventory/EquipmentService.cs:25` `Equip`, `:87` `Unequip`; `IInventoryRepository.EquipItem`/`UnequipItem`, `InventoryRepositoryContracts.cs:129,142`): an explicit two-state transition (equipped / not), MainGM-only, with `EquipTransition`/`UnequipTransition` request records carrying the expected revision and command id.
4. **`IActiveEffectRepository.SetItemEffectEquipped`** (`ActiveEffectRepositoryContracts.cs:74`): explicit direction toggle `Active↔Suspended` with revision CAS, replay semantics ("changing the effect, actor, revision or direction is a replay conflict"), and "authorization is inherited from the successful host equipment operation".
5. Lifecycle-state transitions on characters/encounters (`CharacterRepositoryContracts.cs`, `CombatEncounterRepository`) follow the same revision-gated pattern with state-machine legality in Application code.

Notably, in the product spec a door toggle is **not** just a boolean: `InteractWithDoorCommand` has discovery (`IsHidden`), lock, optional action-cost and post-commit "fog/LOS invalidation" steps (§14.2), and window state is a 4-value state (§14.3). No existing precedent contains a *post-commit invalidation of derived caches* step (nothing derived exists yet).

## 1.8 Consistency / determinism verification infrastructure

- **`scripts/verify-test-structure.ps1`** (1570 lines): validates project/asmdef structure and the test catalogue. The catalogue check `requiredTestCaseIds` (`:61`) is a fixed list of foundational ids that must exist; other ids (e.g. `TC-BOARD-*`) are unconstrained by prefix, so a new geometry test family needs only catalogue entries. It enforces `noEngineReferences=true` on production asmdefs (`:373`), the module dependency matrix and, for the six production `.csproj`, `DisableTransitiveProjectReferences` (`checkedProjectModules`, `:40`). There is **no geometry-specific category** and nothing reserved for geometry.
- **Where geometry tests live today:** `DotNet/Tests/Odyssey.Tests.Domain/Geometry/` (a folder under the Domain test project; catalogue ids `TC-BOARD-001..003`). `DotNet/Tests/Odyssey.Tests.Architecture/` holds scope-guard tests (`RulesScopeTests`, `AttackScopeTests`, …) that enforce which files/namespaces certain modules may touch; none mention geometry.
- **Determinism-specific tooling against `ADR-020` §11:** (1) *bit-identical results on .NET vs Unity Mono/IL2CPP* — no such test. The nearest precedents are `scripts/test-serialization-aot.ps1` and `scripts/test-player-smoke.ps1`, which run Unity batchmode smokes for serialization/Player builds (Unity 6000.4.0f1 at `C:\Program Files\Unity\Hub\Editor\…`), i.e. a mechanism to run code under Unity exists but not a golden-vector comparison harness. CI runs only static Unity project validation (`.github/workflows` step "Static Unity project validation"); Unity EditMode tests are run locally, not in CI. (2) golden vectors for all metrics — 3 of them exist for `None`-grid only. (3) the four `§13.4` boundary scenarios — no. (4) spatial-index vs brute-force — no. (5) restart-restore identity through `ADR-012` — the persistence test suite (`DotNet/Tests/Odyssey.Tests.Persistence`, 849 tests) has reload-after-restart patterns for other aggregates, none for geometry. (6) "Core geometry assembly compiles without `UnityEngine`" — already structurally guaranteed for `Odyssey.Domain` by `noEngineReferences`, and the `.csproj` bridge builds it without Unity.
- The forbidden-API scanner `Test-ForbiddenGlobalApis` in the same script (wall-clock APIs) is the existing pattern for banning constructs in production code; it has no rule for `float`/`MathF`/`UnityEngine.Vector*` (which `ADR-020` §4.2/§10 forbid for authoritative geometry).

## 2. Verdict on the two questions the ТЗ asks to settle

1. *"Geometry/visibility in code today: none at all"* — **Refuted in the small, confirmed in the large.** Present: `BoardGeometry` (`GridType=None` subset, 5 tests, 2 production call sites). Absent: every intersection/visibility/wall/door/window/cover/fog/vision/spatial-index/grid element, plus any scene object other than a token.
2. *"Schema to hang geometry on"* — `SceneRecord` (9 columns) and `TokenRecord` (12 fields) only; walls/doors/windows/vision/fog need new types and tables.

## 3. Findings worth the product owner's attention (facts, no proposals)

1. **The normative product spec is not versioned in the repository** (`Documentation/` is git-ignored). Reviews/CI cannot see it; `ADR-020` and the completed tasks quote it.
2. **Two unconnected scene models** (persisted `SceneRecord/TokenRecord` vs in-memory networking `Scene/SceneEntity`); the networked token-move path performs no geometry.
3. **No persisted user/participant entity, no real multi-client channel** — the subject and the delivery path of "per-player visibility" are both missing today; the client's identity is a 3-user stub.
4. **Token control and character ownership are separate, unlinked sources** of "who controls this", while the fog audience vocabulary distinguishes them.
5. **Movement has no obstacle logic and occupancy is epsilon point-equality** (no footprint/cell), by design of `ODY-S03-004`; any wall/door work changes what `BoardMovementService` means.
6. **`ADR-020` is only partly implemented** (see §1.2); its `Square/Hex` metrics, floor rounding, orientation primitive, fail-closed semantics and spatial index have no code.
7. **Cover has seams but no concept:** `Topology` carrying distances, an always-empty `Modifiers` list, `Hit` mirroring `Range`, and armor aggregation without body-part selection.
8. **The UI has no oriented-line drawing path**; the overlay/gesture infrastructure is otherwise substantial.
9. **No determinism harness**: `ADR-020` §11 items 1/3/4/5 have no existing test mechanism; Unity EditMode tests are not run in CI.
10. **`docs/tasks/SLICE-08_IMPLEMENTATION_BACKLOG.md:51` wording is stale** regarding `ADR-020` being "not-yet-started" (a `None`-grid subset exists).

## 4. Method and limits

- Read: `ADR-020` in full; `ADR-021`, `ADR-029` and `ADR-019` passages cited; the local `08_Scenes_And_Board_Odyssey_VTT_v0.5.md` (headings, §§4.3–4.4, 13–16, 21.1, 22.4–22.5, 25.3, 28, 29.6–29.7, `BOARD-INV-013…020`) and `12_Combat_And_Actions…` §§19–20; the code files cited above.
- Not done, by scope: no code written, no ADR created, no decomposition, no estimates. Nothing was executed against Unity; the `.NET` build/tests were not run for this report (no code changed).
- Not investigated: UI Toolkit drawing techniques beyond what the repo already uses; performance characteristics; the product documents other than those quoted (`07_Permissions`, `06_Networking`) beyond what the ADRs cite.
