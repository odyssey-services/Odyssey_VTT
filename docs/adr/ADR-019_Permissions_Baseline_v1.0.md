# ADR-019 — Permissions Baseline

**Документ:** `docs/adr/ADR-019_Permissions_Baseline_v1.0.md`
**ADR:** ADR-019
**Версия:** 1.0
**Дата:** 25 августа 2026 года
**Статус:** Accepted
**Область:** ролевая модель Main GM/Player/Observer, host-side read/action permission check, redacted scene projection per connection, механизм "отзыв права удаляет данные из текущего клиентского состояния" через уже принятый `ADR-017` snapshot/delta протокол — техническое baseline-подмножество `07_Permissions_Odyssey_VTT_v0.7.md`, не его полная общность
**Связанные этапы:** Roadmap Этап 3 (`SLICE-02`), Milestone `M3`, backlog `ODY-S02-006`
**Базовые документы:** `07_Permissions_Odyssey_VTT_v0.7.md` (целиком, `PERM-INV-001`–`012`, разделы 3, 6–10, 33–37), `17_Roadmap_Odyssey_VTT_v0.11.md` §11.3 (Permissions baseline — точный список, ограничивающий эту ADR), `docs/adr/ADR-017_Snapshot_Delta_Reconnect_Model_v1.0.md` §1 п.8, §11, §12 (visibility/redaction — явно оставлено этому ADR), `docs/adr/ADR-018_Identity_Baseline_v1.0.md` §4 (`UserId` — актор), `docs/adr/ADR-004_Result_and_Error_Model_v1.0.md` (`SafeReasonCode` — уже покрывает нужный словарь), `docs/adr/ADR-002_Command_and_Domain_Event_Model_v1.0.md` (permissions там — только общая концепция, не ролевая модель), `docs/tasks/SLICE-02_BACKLOG.md`

---

# 1. Решение

Odyssey VTT вводит первую конкретную ролевую модель прав доступа — Main GM / Player / Observer — и фиксирует, как host вычисляет read/action-права и redacted-проекцию для каждого соединения, используя исключительно уже принятые механизмы (`ADR-002`'s командный pipeline, `ADR-004`'s `SafeReasonCode`, `ADR-017`'s `ProjectionSnapshot`/`ProjectionDeltaBatch`), не изобретая параллельный механизм.

Обязательные решения:

1. **Три baseline-роли**: `MainGM`, `Player`, `Observer` — точно тот список, что фиксирует roadmap §11.3 ("Main GM; Player; Observer"), **не** четыре роли `07_Permissions` §6.1's `BaseRoleKind` (тот список включает `AssistantGM`). `AssistantGM` явно исключён из этого ADR (раздел 10) — не назван ни roadmap §11.3, ни `SLICE-02_BACKLOG.md` §4's собственной границей задачи.
2. **Принятое подмножество `PERM-INV-001`–`012`**: восемь из двенадцати инвариантов (`001`, `002`, `003`-принцип, `005`, `006`, `010`, `011`, `012`) — раздел 4 даёт точный состав и обоснование для каждого включённого и каждого отложенного пункта.
3. **Read/action check выполняется host-side**, в двух разных точках для двух разных решений (раздел 6): **action check** — в уже принятом `ADR-002`'s командном pipeline (submission + непосредственно перед persistence commit, тот же pipeline, не параллельный); **read/visibility check** — в Application-слое при построении `ProjectionSnapshot`/`ProjectionDeltaBatch` payload, до передачи в `Odyssey.Networking` (точка, которую `ADR-017` §11 уже предполагала, но не определяла — определяется здесь).
4. **Redacted scene projection**: единое авторитативное игровое состояние **плюс** VisibilityPolicy-фильтр, применяемый per-connection при построении `ClientProjection` — не N независимо поддерживаемых авторитативных копий состояния (раздел 7). Соответствует `06_Networking...` §37.2's собственному вычислительному pipeline (`Membership → PermissionDecision → VisibilityPolicy → ClientProjection`) и §19.1's "host строит отдельную projection с учётом membership/roles/permissions" — оба уже фиксируют этот же принцип, этот ADR его формализует.
5. **Отзыв права удаляет данные из клиента — через `ADR-017`'s уже принятый delta-механизм**, не новый: host, обнаружив уменьшение видимости/прав конкретного `AudienceUserId`, формирует `ProjectionDeltaBatch`, содержащий `RemoveFromProjection` (для сущностей/полей, потерявших видимость) и/или `RemoveCapability` (для потерянных action-прав) — оба типа операций уже существуют в `ADR-017` §5's `Operations[]` списке, ни одна новая операция не вводится (раздел 8).
6. **Безопасный отказ**: `PERM-INV-012`'s требуемый словарь `SafeReasonCode` (`PermissionDenied`, `ActionNotAllowed`, `TargetUnavailable`, `StateChanged`, `InteractionExpired`) **уже полностью существует** в `ADR-004`'s принятом `SafeReasonCode` enum (`Odyssey.Application.Results.ErrorCodes`) — ни один новый код не требуется для baseline (раздел 9).
7. Этот ADR **не определяет**: делегирование прав, произвольные `PermissionKey`/`Scope` за пределами трёх baseline-ролей, `AssistantGM`, ownership/control-модель персонажей (`PERM-INV-007`/`008`), Allow/Deny-override resolution algorithm поверх roles (`PERM-INV-004`) — все явно отложены (раздел 10).

Этот ADR является нормативным authority по baseline-ролевой модели и её интеграции с `ADR-002`/`ADR-004`/`ADR-017` — не полным контрактом `07_Permissions_Odyssey_VTT_v0.7.md`, который остаётся источником будущей, более полной permissions-модели за пределами `SLICE-02`.

---

# 2. Контекст и проблема

`07_Permissions_Odyssey_VTT_v0.7.md` — обширный (3101 строка), детально проработанный продуктовый документ, описывающий полную permissions-модель: `PermissionKey`/`Scope`-систему произвольной гранулярности (разделы 11–14), делегирование (`PERM-INV-009`, раздел 12.4), ownership/control-модель персонажей с transfer-workflow (разделы 17–21), field-level visibility с шестью audience-уровнями (раздел 26), временные permissions с политиками истечения (раздел 31), permission audit (раздел 35) и т.д. Roadmap `17_Roadmap_Odyssey_VTT_v0.11.md` §11.3 явно ограничивает то, что требуется на этом этапе (`SLICE-02` — prototype prerequisites), шестью пунктами: Main GM, Player, Observer, read/action check, redacted scene projection, revoked-permission-removes-data. Ни делегирование, ни произвольные `PermissionKey`/`Scope`, ни ownership/control-transfer-workflow, ни временные permissions не входят в этот список.

`ADR-002` (Command and Domain Event Model), уже принятый ранее (`SLICE-00`), упоминает "permissions" только как общий шаг командного pipeline (шаг 6: "Check authoritative permissions and control grants") — не определяет конкретную ролевую модель. Подтверждено `ODY-S02-000`'s более ранним чтением `ADR-002` целиком: эта ADR — **первая**, вводящая конкретную ролевую модель, не расширение существующей.

`ADR-017` (Snapshot/Delta/Reconnect Model) явно оставила visibility/redaction-механику этому ADR: §12 ("Visibility/redaction-правила... не входит в объём этого ADR — `ODY-S02-006`"), §1 п.8 ("redaction при reconnect всегда по текущим permissions, не сохранённым" — уже зафиксировано, не переоткрывается здесь), §11 (предполагает, что payload, передаваемый в `Odyssey.Networking`, "уже отредактирован по permissions до передачи в транспортный слой" — не определяя, где именно это "уже" происходит). Эта ADR — точка, где эта интеграция должна закрыться: не изобретая параллельный redaction-механизм, а определяя точную точку и способ применения уже принятого `ADR-017`'s `Operations[]` словаря.

---

# 3. Термины

## 3.1 `RolePreset` (baseline)

Упрощённое, ограниченное baseline-подмножество `07_Permissions` §6's полной `RolePreset`-структуры: этот ADR фиксирует только `BaseRoleKind ∈ {MainGM, Player, Observer}` как значимое поле для permission-решений на этом этапе — не полную структуру с `PermissionAssignments`/`IsSystemProtected`/клонированием presets (раздел 10).

## 3.2 Actor

Пользователь (`UserId`, `ADR-018` §4), от имени которого выполняется permission-решение — тот же термин, что `07_Permissions` §4.1 уже определяет, идентичность которого фиксирована `ADR-018`, не переопределяется здесь.

## 3.3 `PermissionDecision` (baseline)

Результат host-side проверки: может ли `Actor` с данной ролью выполнить конкретное действие. В baseline — прямая функция от `BaseRoleKind` и, где применимо, character-assignment (раздел 5) — не полный `07_Permissions` §4.6's `PermissionDecision` с учётом произвольных overrides/scope-chain/групп (раздел 10).

## 3.4 `VisibilityPolicy` (baseline)

Отдельное от `PermissionDecision` решение (`PERM-INV-006`, принято без изменений): какие сущности/поля разрешено включить в `ClientProjection` данного соединения. В baseline — производная от `BaseRoleKind` (Observer видит меньше, чем Player; Player видит меньше скрытых GM-данных, чем MainGM) плюс character-assignment (Player видит полные данные назначенных ему персонажей).

## 3.5 `ClientProjection` (baseline)

Per-connection выход VisibilityPolicy-фильтра — то же понятие, что `ADR-017`'s `ProjectionSnapshot`/`ProjectionDeltaBatch` уже несут как payload; этот ADR не вводит отдельный тип, а определяет, как этот payload редактируется перед отправкой (раздел 7).

---

# 4. Принятое подмножество `PERM-INV-001`–`012` — состав и обоснование

**Явное решение (отвечает на явный вопрос задачи)**: приняты **восемь** из двенадцати инвариантов, полностью или как принцип; **четыре** явно отложены.

## 4.1 Принято

| Инвариант | Статус в этом ADR | Обоснование |
|---|---|---|
| `PERM-INV-001` — MainGM защищённый владелец | Принят полностью | Прямо соответствует roadmap §11.3's "Main GM" — MainGM не может быть заблокирован/лишён критических прав, остаётся authority host (раздел 5). |
| `PERM-INV-002` — одна основная роль на участника | Принят полностью | Простое структурное правило, необходимое для самого определения `RolePreset`-присвоения; не требует scope/override-механизма. |
| `PERM-INV-003` — роль это preset, не окончательное решение | Принят как **принцип**, не как override-механизм | Архитектурный принцип ("роль не хардкодит финальное решение навсегда") принимается, чтобы не заблокировать будущее расширение; но сам override-механизм (индивидуальные `PermissionKey` overrides) явно отложен (раздел 10) — baseline PermissionDecision (раздел 3.3) не читает overrides, поскольку их ещё не существует как принятой концепции здесь. |
| `PERM-INV-005` — проверка на хосте | Принят полностью | Прямо отвечает на "read/action check" пункт roadmap §11.3; раздел 6 даёт точные точки. |
| `PERM-INV-006` — Permission ≠ Visibility | Принят полностью | Необходимое концептуальное разграничение для самой структуры этого ADR (разделы 6–7 — два разных решения, не одно). |
| `PERM-INV-010` — изменения прав журналируются | Принят как **прямое следствие уже принятой архитектуры** | Не новое решение: `ADR-012`'s event-sourcing принцип уже требует, чтобы любое изменение авторитативного состояния было `DomainEvent`; изменение роли/permission — не исключение. Ничего нового не изобретается. |
| `PERM-INV-011` — отзыв блокирует незавершённую операцию | Принят полностью | Прямо соответствует roadmap §11.3's "revoked permission removes data from current client state"; раздел 8 даёт механизм через `ADR-017`. |
| `PERM-INV-012` — причина отказа не раскрывает секрет | Принят полностью | Раздел 9 показывает, что весь требуемый `SafeReasonCode`-словарь уже существует в `ADR-004` — ничего нового не изобретается, только подтверждается применимость. |

## 4.2 Явно отложено

| Инвариант | Причина отсрочки |
|---|---|
| `PERM-INV-004` — явный Deny приоритетнее Allow того же уровня | Требует полной Allow/Deny/Inherit override-модели (`07_Permissions` §12–14's scope-chain и conflict-resolution algorithm) — за пределами трёх baseline-ролей; roadmap §11.3 не упоминает индивидуальные overrides. |
| `PERM-INV-007` — Ownership отделён от Control | Требует полной ownership/control-модели персонажей (`07_Permissions` §17–21: co-owners, temporary control grant, transfer workflow) — не названо roadmap §11.3; baseline использует упрощённое прямое character-assignment (раздел 5.3). |
| `PERM-INV-008` — несколько контроллеров одного персонажа | Прямое следствие отложенной control-модели (раздел 7's отсрочка) — не может быть принято без неё. |
| `PERM-INV-009` — делегирование не повышает полномочия | Явно исключено самой задачей ("никакого делегирования прав") — неприменимо, пока делегирование вообще не введено. |

---

# 5. Роли: MainGM, Player, Observer (baseline-подмножество)

Приняты **сокращённые** baseline-версии `07_Permissions` §7/§9/§10 — только то, что напрямую нужно для read/action check и redaction, не полный список полномочий каждой роли:

## 5.1 MainGM

Полный read/action доступ ко всем синхронизируемым игровым данным кампании (`07_Permissions` §7.1, принято без изменений в части read/action, не в части управления membership/backup/export — та функциональность вне объёма этого ADR). Защитные ограничения `PERM-INV-001`/§7.2 принимаются полностью: ни одно baseline permission-решение не может заблокировать MainGM доступ к кампании или позволить его исключение.

## 5.2 Player

Read-доступ к разрешённым сценам/объектам и назначенным персонажам; action-доступ к игровым действиям назначенных персонажей и ответам на `PendingInteraction` (`07_Permissions` §9.1's baseline подмножество). Не видит скрытые GM-поля (`VisibilityPolicy`, раздел 3.4). "Назначенный персонаж" в baseline — прямая, не отдельно управляемая ownership/control-моделью связь (раздел 4.2's отсрочка `PERM-INV-007`/`008`).

## 5.3 Observer

Read-доступ только к публичным, явно назначенным сценам/данным; никакого action-доступа к игровым командам (`07_Permissions` §10.1/§10.2's baseline подмножество). Не видит скрытые данные ни при каких обстоятельствах в baseline (§10.3's расширение через отдельные permission grants явно отложено — требует `PermissionKey`-системы, раздел 10).

---

# 6. Read/action check — точки выполнения (host-side)

**Явное решение (отвечает на явный вопрос задачи)**: два разных решения, в двух разных точках, не одна проверка на всё.

## 6.1 Action check — в уже принятом `ADR-002` командном pipeline

`ADR-002`'s командный pipeline уже включает шаг "Check authoritative permissions and control grants" (существующий шаг 6, до этого ADR — общая концепция без конкретной модели). Этот ADR конкретизирует его: `PermissionDecision` (раздел 3.3) вычисляется **дважды** для команды — при получении команды host'ом, и непосредственно перед persistence commit (тот же паттерн, что `07_Permissions` §33.1's `Recheck points` описывает, и то же двух-точечное правило, что `ADR-012`'s транзакционная граница journal↔projection уже требует для revision-конфликтов). Отказ на любой из двух точек — типизированная `Result`-ошибка через уже существующий `SafeReasonCode` (раздел 9), не raw exception.

## 6.2 Read/visibility check — в Application-слое, до передачи в `Odyssey.Networking`

`VisibilityPolicy`-фильтр (раздел 3.4) применяется **в Application-слое**, при построении `ProjectionSnapshot`/`ProjectionDeltaBatch` для конкретного `AudienceUserId` (`ADR-017` §4/§5) — **до** того, как payload передаётся `Odyssey.Networking` для транспортировки. Это прямо отвечает на то, что `ADR-017` §11 предполагала ("payload уже отредактирован по permissions до передачи в транспортный слой"), но не определяла: **эта** точка — построение `ProjectionSnapshot`/`ProjectionDeltaBatch`, не позже. `Odyssey.Networking` никогда не принимает permission-решений самостоятельно (`ADR-001` §6.6, `ADR-017` §11 — уже установленное правило, не переопределяется).

---

# 7. Redacted scene projection — механизм

**Явное решение (отвечает на явный вопрос задачи)**: **единое авторитативное состояние плюс per-connection redaction-фильтр**, не N независимо поддерживаемых авторитативных копий.

`06_Networking_and_Session_Sync` §37.2's уже документированный pipeline принимается как нормативный механизм этого ADR:

```text
Membership
→ PermissionDecision inputs
→ VisibilityPolicy
→ (baseline: character-assignment, не полная Ownership/Control-модель)
→ Scene assignments
→ ClientProjection
```

Для каждого соединения host применяет этот pipeline к единому авторитативному игровому состоянию (не читает `campaign.db` напрямую из `Odyssey.Networking` — `ADR-001` §6.6), производя отдельный `ClientProjection` (реализуемый как `ProjectionSnapshot`/`ProjectionDeltaBatch`, `ADR-017`). Два разных `AudienceUserId` с разными ролями получают структурно разные `ClientProjection`, вычисленные из одного и того же авторитативного источника, не два расходящихся "мира".

---

# 8. Отзыв права удаляет данные из клиента — механизм через `ADR-017`

**Явное решение (отвечает на явный вопрос задачи)**: **не новый механизм** — применение уже принятого `ADR-017` delta-протокола.

Когда изменение permission-состояния (смена роли, потеря character-assignment) уменьшает `VisibilityPolicy`/`PermissionDecision` для конкретного `AudienceUserId`:

1. Host пересчитывает `VisibilityPolicy` для затронутого соединения (тот же принцип, что `07_Permissions` §37.4/§34.1's `PermissionStateRevision` уже описывает: изменение прав инвалидирует существующую projection).
2. Host формирует `ProjectionDeltaBatch` (`ADR-017` §5), нацеленный на этот `AudienceUserId`, содержащий:
   - `RemoveFromProjection` — для сущностей/полей, потерявших видимость (уже существующая операция, `ADR-017` §5's `Operations[]`);
   - `RemoveCapability` — для потерянных action-прав, отражённых в `AllowedCommands` клиента (уже существующая операция, там же).
3. Этот батч доставляется через уже принятый reliable-канал (`ADR-015` §5.1), проходит через уже принятый gap-detection/dedup-протокол (`ADR-017` §6/§7) — никакого параллельного "revocation channel" не вводится.

**Незавершённая операция** (`PERM-INV-011`): если право отозвано до commit команды — раздел 6.1's двух-точечная action-check уже это покрывает (отказ на второй проверке, перед commit); локальный client-side preview удаляется как побочный эффект получения `PermissionDenied`-ответа (уже существующее клиентское поведение, не новый механизм этого ADR).

**Явное соответствие `ADR-017` §1 п.8**: redaction при reconnect всегда по текущим (не сохранённым) permissions — уже зафиксировано `ADR-017`, не переоткрывается здесь; этот ADR лишь подтверждает, что механизм раздела 7 (единое состояние + per-connection фильтр) естественным образом даёт это свойство: reconnect просто повторно применяет тот же pipeline с текущим permission-состоянием, не читает сохранённую старую проекцию.

---

# 9. Безопасный отказ — `SafeReasonCode`, уже существующий

**Явное решение**: `PERM-INV-012`'s требуемый словарь (`PermissionDenied`, `ActionNotAllowed`, `TargetUnavailable`, `StateChanged`, `InteractionExpired`) — **все пять значений уже существуют** в `ADR-004`'s принятом `SafeReasonCode` enum (`Odyssey.Application.Results.ErrorCodes`, подтверждено по памяти уже принятой кодовой базы: `SafeReasonCode` включает `InvalidRequest, PermissionDenied, ActionNotAllowed, TargetUnavailable, StateChanged, ResourceUnavailable, CapacityReached, ApprovalRequired, InteractionExpired, VersionUnsupported, UpdateRequired, DataCorrupted, ServiceUnavailable, OperationTimedOut, OperationCancelled, ManualRecoveryRequired, UnexpectedError`). **Ни один новый `SafeReasonCode` не требуется** для baseline permission-отказов. Future implementation task обязана переиспользовать существующие значения, не вводить дублирующие.

`07_Permissions` §36.3's `PermissionDenialInternal` (детальный internal-лог с `FullDecisionTrace`) — принцип "детальная причина только в GM technical log, не клиенту" принимается как согласующийся с уже принятым `ADR-004` разделением `Error` (safe, клиенту) от internal diagnostic-логирования (`ADR-010`) — не вводит новый механизм, использует уже существующие.

---

# 10. Не входит в ADR-019

Явно исключено из объёма этого ADR (либо принадлежит другим задачам backlog, либо полной общности `07_Permissions`, не требуемой roadmap §11.3):

- **`AssistantGM`** — не названа ни roadmap §11.3, ни `SLICE-02_BACKLOG.md` §4's границей этой задачи; `07_Permissions` §8 документирует её полностью, но она остаётся вне baseline до отдельного будущего расширения.
- **Делегирование прав** (`PERM-INV-009`) — явно исключено самой задачей.
- **Произвольные `PermissionKey`/`Scope`** за пределами трёх baseline-ролей (`07_Permissions` §11–14) — Allow/Deny/Inherit override-модель, scope-chain, conflict-resolution algorithm.
- **Ownership/Control-модель персонажей** (`PERM-INV-007`/`008`, `07_Permissions` §17–21) — co-owners, temporary control grant, transfer workflow.
- **Временные permissions** (`07_Permissions` §31) — expiration policies, temporary Allow/Deny.
- **`CampaignUserGroup`** (`07_Permissions` §16) — групповые assignments.
- **Field-level visibility с шестью audience-уровнями** (`07_Permissions` §26) — baseline использует только role-level redaction (раздел 7), не field-granular audience-модель.
- **Permission audit UI/query** (`07_Permissions` §35.3's "кто видит журнал") — журналирование как `DomainEvent` принято (раздел 4.1, `PERM-INV-010`), но запрос/просмотр audit-журнала — не входит.
- **`SP-04` (`ODY-S02-007`)** — эмпирическая проверка, что скрытая сущность реально никогда не достигает клиента — future spike, использующий контракт, который этот ADR фиксирует, не выполняемая этим ADR.
- **Production-реализация** (реальный код permission-check, redaction-фильтра, revocation-delta построения) — future implementation task.

---

# 11. Соответствие module boundaries (`ADR-001`) и уже принятым `ADR-002`/`ADR-004`/`ADR-015`/`ADR-017`

Этот ADR не вводит новый код и не переопределяет ни одну уже принятую границу:

- `Odyssey.Networking` не принимает permission-решений (`ADR-001` §6.6, `ADR-017` §11) — `VisibilityPolicy`/`PermissionDecision` вычисляются в Application-слое до передачи payload в Networking (раздел 6.2).
- Action check интегрируется в уже существующий шаг `ADR-002`'s командного pipeline, не добавляет параллельный pipeline.
- Revocation-delta использует исключительно уже существующие `ADR-017` `Operations[]` (`RemoveFromProjection`, `RemoveCapability`) — не вводит новый тип операции или отдельный "permission channel".
- `SafeReasonCode`-словарь — исключительно уже существующие значения `ADR-004` (раздел 9) — не вводит новый enum.
- `UserId` (`ADR-018` §4) — актор, против которого проверяется право — не переопределяется.

---

# 12. Правила для Codex

Codex обязан:

1. Реализовывать только три baseline-роли (`MainGM`, `Player`, `Observer`) под этим ADR — не `AssistantGM`, не произвольные `PermissionKey`/`Scope`.
2. Выполнять action check дважды (submission + перед commit), как уже определяет `ADR-002`'s pipeline, не изобретать отдельный permission-pipeline.
3. Выполнять read/visibility check в Application-слое при построении `ProjectionSnapshot`/`ProjectionDeltaBatch`, никогда в `Odyssey.Networking`.
4. Реализовывать "отзыв права удаляет данные" исключительно через `ADR-017`'s `RemoveFromProjection`/`RemoveCapability` delta-операции — не через отдельный "permission sync" механизм.
5. Переиспользовать существующие `SafeReasonCode` значения (`ADR-004`) для permission-отказов — не вводить новые коды без явного обоснования, почему существующих пяти недостаточно.
6. Не реализовывать делегирование, ownership/control-transfer, временные permissions, `CampaignUserGroup` или field-level audience-модель под этим ADR — все явно отложены (раздел 10).
7. Не давать MainGM-защитным ограничениям (раздел 5.1, `PERM-INV-001`) быть обходимыми ни одним baseline permission-решением.

---

# 13. Definition of Done для будущей implementation-задачи

Implementation-задача, реализующая эту модель, обязана — до открытия своего Draft PR с production-кодом — доказать (тестами):

1. Три роли (`MainGM`/`Player`/`Observer`) корректно определяют read/action-доступ per раздел 5.
2. Action check выполняется дважды (submission + pre-commit) и корректно отклоняет отозванное право до commit, не после (`PERM-INV-011`).
3. Read/visibility check происходит до передачи payload в `Odyssey.Networking`, доказано тестом, что `Odyssey.Networking` никогда не видит нередактированную сущность.
4. Redacted scene projection даёт структурно разный `ClientProjection` для `MainGM` vs `Player` vs `Observer` из одного и того же авторитативного состояния (тест на все три роли одновременно против одной и той же сцены).
5. Уменьшение permission-состояния порождает корректный `ProjectionDeltaBatch` с `RemoveFromProjection`/`RemoveCapability`, доставленный только затронутому `AudienceUserId`, не всем соединениям.
6. Ни один permission-отказ не вводит новый `SafeReasonCode` без явного обоснования в PR — все baseline-случаи используют пять уже существующих значений (раздел 9).

---

# 14. Рассмотренные альтернативы

## 14.1 Включить `AssistantGM` как четвёртую baseline-роль

Отклонено: ни roadmap §11.3, ни `SLICE-02_BACKLOG.md` §4's зафиксированная граница этой задачи не называют её; включение непрошенной четвёртой роли — unapproved scope expansion.

## 14.2 N независимо поддерживаемых авторитативных projection-копий вместо единого состояния + фильтра

Отклонено: расходится с `06_Networking...` §37.2's уже документированным единым pipeline (`Membership → ... → ClientProjection`); дублирование авторитативного состояния создало бы риск рассинхронизации между копиями, которого единый источник + read-time фильтр избегает по конструкции.

## 14.3 Новый "permission sync"/"revocation channel" отдельно от `ADR-017`'s delta-протокола

Отклонено: `ADR-017` §5's `Operations[]` уже включает `RemoveFromProjection`/`RemoveCapability` — оба operation type спроектированы именно для этого случая; введение параллельного механизма нарушило бы `ADR-017` §15.3's уже принятое решение не вводить обходные пути вокруг delta-протокола и создало бы два источника истины для "что клиент должен забыть."

## 14.4 Новые `SafeReasonCode` значения специально для permissions

Отклонено: пять уже существующих значений `ADR-004` (`PermissionDenied`, `ActionNotAllowed`, `TargetUnavailable`, `StateChanged`, `InteractionExpired`) точно покрывают `PERM-INV-012`'s требуемый словарь — введение дублирующих кодов нарушило бы `ADR-004`'s собственный принцип единого нормативного словаря без расползания.

## 14.5 Принять полный `07_Permissions` документ целиком сейчас, не откладывать делегирование/scope/ownership

Отклонено: прямо противоречит зафиксированной `SLICE-02_BACKLOG.md` §4 границе этой задачи ("baseline-подмножество... не полная общность документа"); реализация полной модели без реального использования (произвольный `PermissionKey`-namespace, delegation) на этом этапе прототипа — преждевременная сложность, которую сама эта репозиторий-практика (`AGENTS.md`, `PLANS.md`) последовательно избегает.

---

# 15. Трассировка

ADR реализует и уточняет:

- `07_Permissions_Odyssey_VTT_v0.7.md` §3 (`PERM-INV-001`–`012`, восемь принято), §6–10 (`RolePreset`, `MainGM`, `Player`, `Observer` — baseline-подмножество), §33–37 (recheck points, permission state revision, safe denial model, networking integration);
- `17_Roadmap_Odyssey_VTT_v0.11.md` §11.3 (Permissions baseline, точный список, ограничивающий эту ADR);
- `docs/adr/ADR-002_Command_and_Domain_Event_Model_v1.0.md` (командный pipeline, action check интегрирован в уже существующий шаг);
- `docs/adr/ADR-004_Result_and_Error_Model_v1.0.md` (`SafeReasonCode`, полностью переиспользован, не расширен);
- `docs/adr/ADR-015_Transport_Abstraction_v1.0.md` §5.1 (reliable-канал, на котором доставляется revocation-delta);
- `docs/adr/ADR-017_Snapshot_Delta_Reconnect_Model_v1.0.md` §1 п.8, §5, §11, §12 (delta-механизм, точка redaction, явно оставленная этой ADR);
- `docs/adr/ADR-018_Identity_Baseline_v1.0.md` §4 (`UserId`, актор).

Связанные будущие задачи (`docs/tasks/SLICE-02_BACKLOG.md`):

```text
ODY-S02-007  Technical Spike SP-04: Hidden Data Boundary (эмпирическая проверка этого контракта)
(будущая, не зарезервированная в этой ревизии backlog) полная permissions-модель — AssistantGM, delegation, PermissionKey/Scope, ownership/control
(будущая, не зарезервированная в этой ревизии backlog) production-реализация этого ADR поверх ISessionTransport/ADR-017
```

---

# 16. Нормативное действие

Принято как ADR этой задачи (`ODY-S02-006`) без ожидания `SP-04` — обоснование: этот ADR фиксирует контракт (роли, точки проверки, redaction-механизм), который `SP-04` затем эмпирически проверит; сама формулировка контракта не требует эмпирических данных, поскольку целиком строится из уже принятых, не требующих измерения решений (`ADR-002`, `ADR-004`, `ADR-017`) и уже документированного baseline-подмножества `07_Permissions`.

С даты принятия (`Accepted`):

- ни одна implementation-задача `SLICE-02`/будущих слайсов не вводит `AssistantGM`, делегирование, или произвольные `PermissionKey`/`Scope` под этим ADR — они остаются вне объёма до отдельного будущего расширения;
- `ODY-S02-007` (`SP-04`) авторизована опираться на этот ADR как на контракт, который её эмпирический тест проверяет — не обязана заново решать вопросы разделов 4–9;
- будущая implementation-задача обязана переиспользовать `ADR-017`'s `RemoveFromProjection`/`RemoveCapability` и `ADR-004`'s существующий `SafeReasonCode`-словарь, не вводить параллельные механизмы;
- изменение принятого baseline-подмножества (например, включение `PERM-INV-004`/`007`/`008` в будущем) требует amendment этого ADR или нового superseding ADR, не молчаливого расширения в реализации.

---

**Конец документа**
