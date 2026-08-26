# ADR-021 — Extended Audience and Selected-Participant Visibility

**Документ:** `docs/adr/ADR-021_Extended_Audience_And_Selected_Participant_Visibility_v1.0.md`
**ADR:** ADR-021
**Версия:** 1.0
**Дата:** 26 августа 2026 года
**Статус:** Accepted
**Область:** интеграция расширенной аудиторной модели — `SelectedParticipants` (стабильный список пользователей) и `CampaignUserGroup` (групповое членство) — с уже принятым `ADR-019` §7's механизмом "единое авторитетное состояние + per-connection фильтр"; композиция постфактум-раскрытия/отзыва видимости уже созданного артефакта (бросок, запись журнала, зона тумана) с уже принятыми `ADR-017`'s delta-операциями; распространение `PERM-INV-012`/`ADR-019`'s safe denial принципа на новую аудиторную модель — техническая расширяющая ADR для `ODY-S03-002`, не переоткрывающая три baseline-роли `ADR-019`
**Связанные этапы:** Roadmap Этап 4 (`SLICE-03`), Milestone `M4`, backlog `ODY-S03-002`
**Базовые документы:** `07_Permissions_Odyssey_VTT_v0.7.md` §16 (`CampaignUserGroup` — полный aggregate), §30 (Private events and audiences — шесть игровых аудиторий), `09_Dice_And_Game_Log_Odyssey_VTT_v0.3.md` §16 (Видимость — четыре audience kinds для броска), §27 (полнотекстовый поиск — security invariant), §28 (изменение аудитории постфактум — раскрытие/отзыв), §36.5 (revocation networking contract), `08_Scenes_And_Board_Odyssey_VTT_v0.5.md` §16.3 (fog `AudienceKey`), §19.4 (projection redaction — второй потребитель этой ADR помимо броска/журнала), `docs/adr/ADR-019_Permissions_Baseline_v1.0.md` §7 (single-authoritative-state-plus-per-connection-filter pipeline — расширяется, не переоткрывается), §10 (явно отложенные `CampaignUserGroup`, произвольные `PermissionKey`/`Scope` — именно этот отложенный объём закрывает эта ADR), `docs/adr/ADR-017_Snapshot_Delta_Reconnect_Model_v1.0.md` §5 (`Operations[]` — переиспользуется, не расширяется новым типом операции), `docs/adr/ADR-020_Board_Geometry_And_Movement_Determinism_v1.0.md` (структурный/стилистический образец той же волны задач), `docs/tasks/SLICE-03_BACKLOG.md`

---

# 1. Решение

Odyssey VTT расширяет уже принятый `ADR-019` §7's механизм redaction (единое авторитативное состояние плюс per-connection `VisibilityPolicy`-фильтр) новым классом аудиторных входных данных — явным списком выбранных участников (`SelectedParticipants`) и групповым членством (`CampaignUserGroup`) — используя исключительно уже принятые механизмы (`ADR-019`'s pipeline, `ADR-017`'s `Operations[]`, `ADR-004`'s `SafeReasonCode`), не изобретая параллельный механизм.

Обязательные решения:

1. **`CampaignUserGroup` — узкое read-model представление для этой baseline-задачи, не полный lifecycle-контракт**: для целей audience-резолюции воспроизводится ровно то подмножество полей `07_Permissions` §16.1's уже документированного aggregate, которое нужно `VisibilityPolicy`-вычислению — `CampaignUserGroupId`, `CampaignId`, `MemberUserIds`, `Status` (`Active`/`Archived`), `Revision`. Команды жизненного цикла группы (создание/переименование/архивация/изменение состава) — обычные `ADR-002`-командные обработчики, не архитектурно новый вопрос, поэтому не фиксируются этой ADR как отдельное нормативное решение (раздел 4).
2. **Интеграция с `ADR-019` §7's pipeline — дополнительный вход `VisibilityPolicy`, не параллельный механизм**: `SelectedParticipants` (стабильный, зафиксированный при создании артефакта список `UserId`/`CampaignUserGroupId`, раздел 5) и `CampaignUserGroup`-членство подключаются как ещё один источник данных для уже существующей функции `VisibilityPolicy` (`ADR-019` §3.4), наравне с уже принятым `BaseRoleKind`/character-assignment — не отдельная, независимо вычисляемая проверка.
3. **Evaluation-time правило**: принадлежность к аудитории всегда вычисляется по **текущим** permissions/membership, не по составу, зафиксированному на момент создания артефакта (`09_Dice_And_Game_Log` §16.4: "Audience хранит стабильные ссылки на users/groups, а projection вычисляется по текущим permissions и membership") — прямое обобщение того же принципа, который `ADR-019` §1 п.8 уже зафиксировала для reconnect-redaction (раздел 6).
4. **Постфактум-раскрытие/отзыв — прямое применение уже принятых `ADR-017` `Operations[]`, без новой операции**: раскрытие (расширение аудитории уже созданного артефакта) — `AddJournalEntry`/`AddEntity` для newly-included `AudienceUserId`; отзыв (сужение аудитории) — `RemoveFromProjection` для excluded `AudienceUserId`, то же самое применение, которое `ADR-019` §8 уже зафиксировала для permission-revocation, здесь распространяется на artifact-level audience change (`09_Dice_And_Game_Log` §28.1's `LogEntryDisclosureChanged`) (раздел 7).
5. **`PERM-INV-012`/`ADR-019`'s safe denial принцип распространяется без изменений на новую аудиторную модель**, включая полнотекстовый поиск по журналу (`09_Dice_And_Game_Log` §27.2's собственный security invariant — уже сформулирован в продуктовом документе как прямое приложение того же принципа, не новый) — никакой новый `SafeReasonCode` не требуется (раздел 8).
6. Этот ADR **не переоткрывает** три baseline-роли `ADR-019` §5 (MainGM/Player/Observer) — расширяет исключительно §10's явно отложенный объём (`CampaignUserGroup`, часть произвольного `PermissionKey`/`Scope`-пространства, относящаяся к аудитории). Не проектирует полнотекстовый поиск целиком (`09_Dice_And_Game_Log` §27.3's searchable fields, индексация) — только подтверждает применимость safe denial принципа (раздел 8).

Этот ADR является нормативным authority по интеграции расширенной аудиторной модели с уже принятым redaction-механизмом — не полным контрактом `07_Permissions` §16/§30 или `09_Dice_And_Game_Log` §16/§27/§28, которые остаются источником полной продуктовой модели за пределами того, что этот ADR фиксирует как техническую baseline-интеграцию.

---

# 2. Контекст и проблема

`ADR-019` (Permissions Baseline), уже принятый в рамках `SLICE-02`, фиксирует три baseline-роли (MainGM/Player/Observer) и единый redaction-механизм (единое состояние + per-connection `VisibilityPolicy`-фильтр), но **явно откладывает** (§10): "Произвольные `PermissionKey`/`Scope` за пределами трёх baseline-ролей... `CampaignUserGroup` (`07_Permissions` §16) — групповые assignments." Roadmap `17_Roadmap_Odyssey_VTT_v0.11.md` §12.2's четвёртый prerequisite-пункт прямо называет "правила visibility броска, включая selected users/groups" как требование для `SLICE-03` — тот самый отложенный `ADR-019` §10's объём, не закрытый ни одной существующей ADR.

`07_Permissions` §30.1 документирует шесть игровых аудиторий (`Public`, `PlayerAndGM`, `GMOnly`, `SelectedParticipants`, `CampaignUserGroup`, `SceneParticipants`); `09_Dice_And_Game_Log` §16.1 для броска — подмножество из четырёх (`Public`, `PlayerAndGM`, `GMOnly`, `SelectedParticipants`); `08_Scenes_And_Board` §16.3 для fog — свой `AudienceKey`-словарь (`User:<id>`, `Group:<id>`, `CharacterOwners:<characterId>`, `CharacterControllers:<characterId>`, `SceneParticipants`), структурно тот же класс концепции (стабильная ссылка на пользователя/группу/производный набор), но с собственным именованием. Эта ADR фиксирует единый интеграционный принцип, применимый ко всем трём потребителям (бросок, журнал, fog), не три независимых механизма.

Дополнительная сложность, которую предыдущие ADR не решали: артефакты этой предметной области (`DiceRoll`, `GameLogEntry`, fog-регион) **создаются один раз, но их аудитория может измениться постфактум** — `09_Dice_And_Game_Log` §28 документирует явный `LogEntryDisclosureChanged`-механизм (раскрытие) и отзыв с явным предупреждением ("Участники уже могли увидеть или сохранить эту информацию"). `ADR-019` §8 уже зафиксировала механизм "отзыв права удаляет данные из клиента" для permission-revocation в целом (через `ADR-017`'s `RemoveFromProjection`/`RemoveCapability`), но не для audience-change конкретно созданного игрового артефакта — эта ADR закрывает именно эту интеграционную точку, не изобретая параллельный механизм.

---

# 3. Термины

## 3.1 `SelectedParticipants` (baseline)

Стабильный, зафиксированный на момент создания артефакта список `UserId`/`CampaignUserGroupId`-ссылок (`09_Dice_And_Game_Log` §16.4). Ссылки стабильны, но **резолюция** аудитории (кто сейчас имеет доступ) всегда пересчитывается по текущему состоянию (раздел 6) — сам список участников не редактируется постфактум без явной `LogEntryDisclosureChanged`-подобной команды (раздел 7).

## 3.2 `CampaignUserGroup` (baseline read-model)

Узкое подмножество `07_Permissions` §16.1's полного aggregate, нужное для audience-резолюции: `CampaignUserGroupId`, `CampaignId`, `MemberUserIds`, `Status`, `Revision`. Не включает в объём этой ADR полный lifecycle-контракт (создание/переименование/архивация как отдельно спроектированные команды) — раздел 4.

## 3.3 `AudienceKind` (расширенный список)

Union уже принятых/документированных значений по потребителю: бросок/журнал — `Public`/`PlayerAndGM`/`GMOnly`/`SelectedParticipants` (`09_Dice_And_Game_Log` §16.1, §30.1's дополнительные `CampaignUserGroup`/`SceneParticipants` для private events в целом); fog — `User:<id>`/`Group:<id>`/`CharacterOwners:<characterId>`/`CharacterControllers:<characterId>`/`SceneParticipants` (`08_Scenes_And_Board` §16.3). Эта ADR не унифицирует именование между потребителями (не требуется — каждый потребитель уже сам зафиксировал свой словарь в продуктовом документе), а фиксирует **единый интеграционный принцип** (раздел 5) для всех них.

## 3.4 Раскрытие (`Disclosure`) и отзыв (`Revocation`) постфактум

Раскрытие — расширение аудитории уже созданного артефакта (`09_Dice_And_Game_Log` §28.1). Отзыв — сужение аудитории уже созданного артефакта (§28.2). Оба — новые `DomainEvent`, не редактирование существующей записи на месте (`ADR-012`'s append-only принцип, не переопределяется).

---

# 4. `CampaignUserGroup` — узкое представление, обоснование

**Явное решение (отвечает на явный вопрос задачи)**: узкое read-model представление, не полный lifecycle-контракт.

`07_Permissions` §16.1 документирует полный aggregate (`CampaignUserGroupId`, `CampaignId`, `Name`, `Description?`, `MemberUserIds`, `Status`, `CreatedByUserId`, `CreatedAt`, `UpdatedAt`, `Revision`) и полный lifecycle: создание, переименование, изменение состава, архивация (§16.4/§16.5), каждое из которых увеличивает revision, инициирует пересчёт `ClientProjection` (§16.4 — прямое применение того же принципа, что `ADR-019` §8 уже фиксирует для permission-revocation в целом), и создаёт audit event.

Для целей **этой** ADR — интеграции группового членства с уже принятым `ADR-019` §7's pipeline — не требуется фиксировать сами команды жизненного цикла группы как архитектурно новый вопрос: `CreateCampaignUserGroup`/`RenameCampaignUserGroup`/`ArchiveCampaignUserGroup`/`UpdateGroupMembership` — обычные авторитетные команды, идентичные по структуре любой другой `ADR-002`-команде (submission → validation → persist → `DomainEvent`), не вводящие новый паттерн. Что архитектурно ново и что эта ADR обязана зафиксировать — это **как** membership-данные текущей группы (уже существующей, независимо от того, как она была создана) используются `VisibilityPolicy`-вычислением (раздел 5) и **как** изменение состава триггерит revocation/disclosure-дельты (раздел 7). Поэтому нормативный объём этой ADR ограничен узким read-model подмножеством полей (`CampaignUserGroupId`, `CampaignId`, `MemberUserIds`, `Status`, `Revision`) — ровно то, что нужно для читающей стороны (audience-резолюция), не пишущей стороны (lifecycle-команды), которая остаётся обычной implementation-задачей без отдельного архитектурного решения.

Это прямая параллель тому, как `ADR-019` §3.1 уже поступила с `RolePreset` ("упрощённое, ограниченное baseline-подмножество... этот ADR фиксирует только `BaseRoleKind`... не полную структуру") — тот же паттерн: воспроизвести из продуктового документа ровно то подмножество, которое нужно для решаемого архитектурного вопроса, не всю структуру целиком.

---

# 5. Интеграция с `ADR-019` §7's pipeline

**Явное решение (отвечает на явный вопрос задачи)**: дополнительный вход существующей `VisibilityPolicy`-функции, не параллельный механизм.

`ADR-019` §7 уже фиксирует нормативный pipeline:

```text
Membership
→ PermissionDecision inputs
→ VisibilityPolicy
→ (ADR-019 baseline: character-assignment)
→ Scene assignments
→ ClientProjection
```

Эта ADR расширяет шаг `VisibilityPolicy` третьим классом входных данных, наравне с уже принятым `BaseRoleKind`/character-assignment:

```text
VisibilityPolicy(AudienceUserId, artifact)
= f(
    BaseRoleKind,                      -- ADR-019 §3.4, не изменяется
    character-assignment,              -- ADR-019 §5.2, не изменяется
    artifact.AudienceKind,             -- эта ADR: Public/PlayerAndGM/GMOnly/
                                        --   SelectedParticipants/CampaignUserGroup/...
    artifact.SelectedParticipants?,    -- эта ADR: если AudienceKind = SelectedParticipants
    CurrentGroupMembership(UserId)     -- эта ADR: если применимо CampaignUserGroup
  )
```

Ни один из трёх уже принятых потребителей (`ADR-019` §6.2's read/visibility check в Application-слое, `ADR-017`'s `ProjectionSnapshot`/`ProjectionDeltaBatch` construction, `Odyssey.Networking` never делает permission-решений) не изменяется — `VisibilityPolicy` остаётся единственной точкой вычисления, вызываемой в том же месте (`ADR-019` §6.2), просто с расширенным набором входных параметров для тех артефактов (`DiceRoll`, `GameLogEntry`, fog-регион), которые несут `AudienceKind`/`SelectedParticipants`/group-ссылку. Для артефактов, не несущих эти поля (обычная board-геометрия, уже покрытая `ADR-020`), функция ведёт себя как раньше — расширение аддитивно, не меняет поведение существующих потребителей.

---

# 6. Evaluation-time правило — текущее, не сохранённое состояние

**Явное решение**: принадлежность к аудитории всегда пересчитывается по текущим данным на момент построения проекции, не фиксируется на момент создания артефакта.

`09_Dice_And_Game_Log` §16.4 фиксирует это прямо для броска: "Audience хранит стабильные ссылки на users/groups, а projection вычисляется по текущим permissions и membership." Эта ADR фиксирует это как **общий принцип** для всех трёх потребителей (бросок, журнал, fog), не только для броска — прямое обобщение того же evaluation-time правила, которое `ADR-019` §1 п.8 уже установила для reconnect-redaction ("redaction при reconnect всегда по текущим (не сохранённым) permissions"). Практическое следствие: если `CampaignUserGroup`'s состав меняется после того, как `DiceRoll` с `AudienceKind = CampaignUserGroup` уже создан, видимость этого броска для конкретного пользователя пересчитывается по **новому** составу группы, не по составу на момент броска — согласуется с `07_Permissions` §16.4's уже принятым правилом ("изменение состава... инициирует пересчёт `ClientProjection`").

---

# 7. Постфактум-раскрытие/отзыв — применение `ADR-017`'s `Operations[]`

**Явное решение (отвечает на явный вопрос задачи)**: не новая операция — прямое применение уже принятых `AddJournalEntry`/`AddEntity`/`RemoveFromProjection`.

`ADR-017` §5's `Operations[]` уже включает ровно нужный набор: `AddEntity`, `AddJournalEntry`, `RemoveFromProjection` (наряду с `ReplaceEntity`, `PatchFields`, `RemoveCapability`, `AddCapability`, `CreatePendingInteraction`, `ResolvePendingInteraction`, `SetSceneActive`, `SetParticipantState`). Композиция:

1. **Раскрытие** (`09_Dice_And_Game_Log` §28.1's `LogEntryDisclosureChanged`, `NewAudience` шире `PreviousAudience`): для каждого `AudienceUserId`, получившего доступ по новой аудитории и не имевшего его по старой — host формирует `ProjectionDeltaBatch`, содержащий `AddJournalEntry` (для `GameLogEntry`/`DiceRoll` — уже существующая операция, спроектированная именно для журнальных записей) или `AddEntity` (для fog-региона/иного board-артефакта) — та же операция, что уже используется для любого другого случая "сущность стала видимой", не отдельный "disclosure"-тип операции.
2. **Отзыв** (§28.2, `NewAudience` уже `PreviousAudience`): для каждого `AudienceUserId`, потерявшего доступ — `RemoveFromProjection`, то же самое применение, которое `ADR-019` §8 уже зафиксировала для permission-revocation в целом. Эта ADR не вводит новый случай — только подтверждает, что artifact-level audience-change (не только role-level permission-change) — тот же самый триггер того же самого механизма.
3. **Групповое изменение состава** (`07_Permissions` §16.4): та же композиция — пользователь, исключённый из группы, чья видимость зависела от членства, получает `RemoveFromProjection` для затронутых артефактов; добавленный в группу пользователь — `AddJournalEntry`/`AddEntity` для артефактов, ставших видимыми по новому членству. Не отдельный "group sync"-механизм — то же самое revocation/disclosure-применение раздела 7, инициированное другим триггером (изменение группы вместо изменения самого артефакта).
4. **Оригинальная запись не редактируется на месте** (`09_Dice_And_Game_Log` §28.1: "Исходная запись не редактируется на месте") — согласуется с `ADR-012`'s append-only принципом, не переопределяется; `LogEntryDisclosureChanged`/аналогичное board/roll-событие — новый `DomainEvent`, дельта — следствие этого события, не патч существующей записи.
5. **Сетевой контракт revocation** (`09_Dice_And_Game_Log` §36.5: "Revocation event удаляет запись из active projection/cache index приложения, но не обещает стирание ранее увиденного контента") — уже согласуется с `ADR-017`'s собственной семантикой `RemoveFromProjection` (убирает из будущих snapshot/delta, не претендует на стирание того, что клиент уже мог сохранить локально) — не требует нового уточнения.

Никакой новый тип `Operation` не вводится этой ADR.

---

# 8. Safe denial — распространение `PERM-INV-012` на расширенную аудиторную модель

**Явное решение**: принцип распространяется без изменений и без нового `SafeReasonCode`.

`ADR-019` §9 уже подтвердила, что `PERM-INV-012`'s требуемый словарь (`PermissionDenied`, `ActionNotAllowed`, `TargetUnavailable`, `StateChanged`, `InteractionExpired`) полностью существует в `ADR-004`'s `SafeReasonCode` enum. Эта ADR подтверждает, что тот же словарь достаточен для отказов, связанных с `SelectedParticipants`/`CampaignUserGroup`-аудиторией — пользователь, не входящий в аудиторию броска/записи журнала/fog-региона, получает `TargetUnavailable`/`PermissionDenied` (в зависимости от контекста запроса), не новый специализированный код.

`09_Dice_And_Game_Log` §27.2 уже формулирует security invariant для полнотекстового поиска как прямое приложение того же принципа: запрос не должен возвращать count скрытых совпадений, snippets скрытых записей, timing difference, раскрывающую наличие secret entry, или `EntityId` недоступной сущности. Эта ADR подтверждает: это **тот же** `PERM-INV-012`-принцип ("safe denial никогда не подтверждает существование скрытой сущности"), применённый к новой поверхности (полнотекстовый индекс), не отдельное новое требование, изобретённое для search. Полное проектирование search-реализации (индексация, `09_Dice_And_Game_Log` §27.3's searchable fields, конкретный движок) — вне объёма этой ADR (раздел 9), это подтверждение — единственное, что эта ADR фиксирует по поводу search.

---

# 9. Не входит в ADR-021

Явно исключено из объёма этого ADR:

- **Переоткрытие трёх baseline-ролей `ADR-019`** (`MainGM`/`Player`/`Observer`) — не затрагивается; эта ADR расширяет исключительно `ADR-019` §10's явно отложенный объём.
- **Полный lifecycle-контракт `CampaignUserGroup`** (создание/переименование/архивация как отдельно спроектированные команды, `07_Permissions` §16.4/§16.5's полная детализация) — обычные `ADR-002`-команды, implementation-задача без отдельного архитектурного решения (раздел 4).
- **Произвольный `PermissionKey`/`Scope` за пределами `CampaignUserGroup`-аудитории** (`ADR-019` §10's остальной отложенный список — делегирование, `AssistantGM`, ownership/control-transfer, временные permissions) — не затрагивается этой ADR, остаётся отложенным.
- **Полное проектирование permission-aware full-text search** (`09_Dice_And_Game_Log` §27.3's searchable fields, конкретный индекс/движок) — только подтверждение (раздел 8), что safe denial принцип естественно распространяется, не более.
- **Production-реализация** (реальный код `VisibilityPolicy`-расширения, `CampaignUserGroup`-репозиторий, disclosure/revocation-обработчики) — future implementation-задача.
- **Технический спайк** — `SLICE-03_BACKLOG.md` §3 уже обосновала: это расширение уже эмпирически проверенного (`SP-04`/`ODY-S02-007`) механизма новым входным параметром, не требующее отдельной эмпирической проверки.

---

# 10. Соответствие module boundaries (`ADR-001`) и уже принятым `ADR-002`/`ADR-004`/`ADR-017`/`ADR-019`

Этот ADR не вводит новый код и не переопределяет ни одну уже принятую границу:

- `VisibilityPolicy`-вычисление остаётся в Application-слое, до передачи payload в `Odyssey.Networking` (`ADR-019` §6.2, не изменяется) — расширенные входные данные (раздел 5) вычисляются в той же точке, тем же слоем.
- Disclosure/revocation-дельты используют исключительно уже существующие `ADR-017` `Operations[]` (`AddJournalEntry`/`AddEntity`/`RemoveFromProjection`) — не вводит новый тип операции или отдельный "audience sync channel" (раздел 7), той же дисциплины, которую `ADR-019` §14.3 уже отвергла для аналогичного случая ("новый 'permission sync'/'revocation channel' отдельно от `ADR-017`'s delta-протокола").
- `CampaignUserGroup`-lifecycle команды (раздел 4) — обычные `ADR-002`-команды, не параллельный командный pipeline.
- `SafeReasonCode`-словарь — исключительно уже существующие значения `ADR-004` (раздел 8) — не вводит новый enum.
- `08_Scenes_And_Board`'s fog `AudienceKey` (`Group:<id>`) — не переопределяется этой ADR; интеграционный принцип раздела 5 применим к нему без изменения его собственного именования.

---

# 11. Правила для Codex

Codex обязан:

1. Реализовывать `CampaignUserGroup` как узкое read-model подмножество раздела 4 для целей audience-резолюции — не откладывать реализацию полного `07_Permissions` §16.1's aggregate под этой ADR, но и не проектировать его lifecycle-команды как архитектурно новый вопрос (обычные `ADR-002`-команды).
2. Расширять `VisibilityPolicy`-функцию (`ADR-019` §3.4) дополнительными входными параметрами раздела 5 — не вводить вторую, параллельную функцию для `SelectedParticipants`/`CampaignUserGroup`-случая.
3. Всегда пересчитывать audience-принадлежность по текущему состоянию на момент построения проекции (раздел 6) — не кэшировать/фиксировать состав на момент создания артефакта как источник истины для будущих проверок.
4. Реализовывать раскрытие/отзыв постфактум исключительно через `ADR-017`'s `AddJournalEntry`/`AddEntity`/`RemoveFromProjection` (раздел 7) — не вводить новый тип операции, отдельный "disclosure channel" или "group sync channel".
5. Не редактировать исходную запись артефакта на месте при audience-change — создавать новый `DomainEvent` (`LogEntryDisclosureChanged`-подобный), согласуясь с `ADR-012`'s append-only принципом.
6. Переиспользовать существующие `SafeReasonCode` значения (`ADR-004`) для audience-related отказов, включая search — не вводить новые коды без явного обоснования.
7. Не реализовывать под этой ADR: делегирование, `AssistantGM`, ownership/control-transfer, временные permissions, полное проектирование full-text search — все явно отложены (раздел 9).

---

# 12. Definition of Done для будущей implementation-задачи

Implementation-задача, реализующая эту модель, обязана — до открытия своего Draft PR с production-кодом — доказать (тестами):

1. `VisibilityPolicy`, расширенная разделом 5's дополнительными входами, корректно определяет доступ для `SelectedParticipants`/`CampaignUserGroup`-аудитории для всех трёх потребителей (бросок, журнал, fog) на одной и той же тестовой сцене/кампании.
2. Изменение состава `CampaignUserGroup` после создания артефакта с `AudienceKind = CampaignUserGroup` корректно меняет видимость этого артефакта для затронутых пользователей без изменения самого артефакта (evaluation-time правило, раздел 6).
3. Раскрытие постфактум (`LogEntryDisclosureChanged`-подобное событие) порождает `AddJournalEntry`/`AddEntity`-дельту, доставленную только newly-included `AudienceUserId`, не всем соединениям.
4. Отзыв постфактум порождает `RemoveFromProjection`-дельту, доставленную только excluded `AudienceUserId`; клиент, ранее видевший запись, перестаёт получать её в будущих snapshot/delta (не заявляется стирание уже увиденного, согласно `09_Dice_And_Game_Log` §36.5).
5. Ни один audience-related отказ (включая полнотекстовый поиск) не раскрывает существование скрытой сущности — доказано тестом, воспроизводящим `09_Dice_And_Game_Log` §27.2's security invariant буквально (нет утечки count/snippet/timing/EntityId для недоступной записи).
6. Ни одна реализация не вводит новый `SafeReasonCode` или новый тип `ADR-017` `Operation` без явного обоснования в PR.

---

# 13. Рассмотренные альтернативы

## 13.1 Полная реализация `CampaignUserGroup`'s lifecycle (создание/переименование/архивация) как часть этой ADR

Отклонено: эти команды — обычные `ADR-002`-команды без архитектурно нового вопроса; фиксация их как отдельного нормативного решения этой ADR добавила бы объём без добавления архитектурной ясности, нарушая тот же принцип "не решать вопрос, не требующий архитектурного решения", которым руководствовалась `ADR-019` при сужении `RolePreset` (раздел 4).

## 13.2 Отдельная, параллельная `VisibilityPolicy`-функция для `SelectedParticipants`/`CampaignUserGroup`-случая

Отклонено: расходится с `ADR-019` §7's уже принятым единым pipeline; две независимые функции создали бы риск рассинхронизации между "какой пользователь видит артефакт по роли" и "какой пользователь видит артефакт по аудитории" для одного и того же артефакта — тот же класс риска, который `ADR-019` §14.2 уже отвергла для N-копий-состояния случая.

## 13.3 Новая `Operation` типа `ChangeAudience`/`Disclose`/`Revoke` в `ADR-017`'s `Operations[]`

Отклонено: `AddJournalEntry`/`AddEntity`/`RemoveFromProjection` уже точно покрывают оба случая (видимость появилась/исчезла) без потери информации — введение специализированной операции продублировало бы уже существующую семантику и нарушило бы `ADR-017` §15.3's уже принятое решение не вводить обходные пути вокруг delta-протокола.

## 13.4 Кэшировать audience-принадлежность на момент создания артефакта вместо пересчёта по текущему состоянию

Отклонено: прямо противоречит `09_Dice_And_Game_Log` §16.4's явному тексту ("projection вычисляется по текущим permissions и membership") и `07_Permissions` §16.4's требованию, что изменение состава группы "инициирует пересчёт `ClientProjection`" — кэширование сделало бы это правило невыполнимым.

## 13.5 Новый `SafeReasonCode`, специфичный для audience-отказов (например, `AudienceDenied`)

Отклонено: пять уже существующих значений `ADR-004` (`PermissionDenied`, `ActionNotAllowed`, `TargetUnavailable`, `StateChanged`, `InteractionExpired`) точно покрывают необходимый словарь для audience-отказов — тот же вывод, что `ADR-019` §14.4 уже сделала для permission-отказов в целом; вводить дублирующий код для узкого подкласса того же явления нарушило бы `ADR-004`'s собственный принцип единого нормативного словаря.

## 13.6 Технический спайк для эмпирической проверки group-membership-change-triggers-delta механизма

Отклонено, обоснование уже дано `SLICE-03_BACKLOG.md` §3: это расширение уже эмпирически проверенного `SP-04`/`ODY-S02-007`'s механизма ("скрытая сущность реально никогда не достигает клиента") новым входным параметром (групповое членство вместо ролевого решения) — тот же класс проверки, не новый эмпирический вопрос; будущая implementation-задача докажет корректность собственными тестами (раздел 12), тем же путём, которым `ODY-S02-010`–`013` уже доказывали свойства redaction без отдельного prerequisite-спайка.

---

# 14. Трассировка

ADR реализует и уточняет:

- `07_Permissions_Odyssey_VTT_v0.7.md` §16 (`CampaignUserGroup` — узкое подмножество, раздел 4), §30 (Private events and audiences — шесть аудиторий, интеграционный принцип применим ко всем);
- `09_Dice_And_Game_Log_Odyssey_VTT_v0.3.md` §16 (Видимость — четыре audience kinds для броска, evaluation-time правило §16.4), §27 (полнотекстовый поиск — security invariant подтверждена, не расширена), §28 (изменение аудитории — раскрытие/отзыв, раздел 7), §36.5 (revocation networking contract);
- `08_Scenes_And_Board_Odyssey_VTT_v0.5.md` §16.3 (fog `AudienceKey` — второй потребитель того же интеграционного принципа), §19.4 (projection redaction);
- `17_Roadmap_Odyssey_VTT_v0.11.md` §12.2 (четвёртый prerequisite-пункт — "selected users/groups", валидирующий необходимость этой ADR);
- `docs/adr/ADR-019_Permissions_Baseline_v1.0.md` §7 (pipeline, расширяется разделом 5), §10 (явно отложенный объём, закрываемый этой ADR);
- `docs/adr/ADR-017_Snapshot_Delta_Reconnect_Model_v1.0.md` §5 (`Operations[]`, переиспользуется без изменений, раздел 7);
- `docs/adr/ADR-004_Result_and_Error_Model_v1.0.md` (`SafeReasonCode`, переиспользован без изменений, раздел 8);
- `docs/adr/ADR-012_Snapshot_And_Append_Only_Journal_v1.0.md` (append-only принцип — исходная запись не редактируется на месте, раздел 7 п.4).

Связанные будущие задачи (`docs/tasks/SLICE-03_BACKLOG.md`):

```text
ODY-S03-001  ADR: Board Geometry and Movement Determinism (независимая от этой задачи, уже Accepted)
(будущая, не зарезервированная в этой ревизии backlog) SLICE-03 vertical slice implementation backlog —
  использует эту ADR как основу для roll/log/fog visibility, CampaignUserGroup-репозиторий,
  disclosure/revocation-обработчики и их тесты по Definition of Done (раздел 12)
```

---

# 15. Нормативное действие

Принято как ADR этой задачи (`ODY-S03-002`) без ожидания технического спайка — обоснование: этот ADR расширяет уже принятый и уже эмпирически проверенный (`SP-04`/`ODY-S02-007`) redaction-механизм одним новым классом входных данных, полностью выводимым из уже документированных продуктовых концепций (`07_Permissions` §16/§30, `09_Dice_And_Game_Log` §16/§27/§28) без изобретения новой архитектуры — то же обоснование, которым уже руководствовалась `ADR-019` при принятии до `SP-04`, и `ADR-020` при принятии без ожидания спайка для геометрии.

С даты принятия (`Accepted`):

- ни одна implementation-задача `SLICE-03`/будущих слайсов не вводит альтернативный audience-механизм, новый тип `ADR-017` `Operation`, или новый `SafeReasonCode` для audience-отказов под этой ADR без amendment/superseding ADR;
- будущая implementation-задача обязана переиспользовать `ADR-019`'s `VisibilityPolicy`-функцию (расширенную разделом 5) и `ADR-017`'s `AddJournalEntry`/`AddEntity`/`RemoveFromProjection` — не вводить параллельные механизмы;
- `SLICE-03_BACKLOG.md`'s прескурсор-ревизия закрывается принятием этой ADR — обе зафиксированные задачи (`ODY-S03-001`/`ODY-S03-002`) выполнены; implementation-ревизия `SLICE-03` может начинаться отдельной будущей задачей, не частью этой ревизии backlog;
- изменение принятого узкого `CampaignUserGroup`-представления (например, расширение до полного lifecycle-контракта как архитектурного вопроса) требует amendment этого ADR или нового superseding ADR, не молчаливого расширения в реализации.

---

**Конец документа**
