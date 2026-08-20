# ADR-011 — Local Campaign Format

**Документ:** `docs/adr/ADR-011_Local_Campaign_Format_v1.0.md`  
**ADR:** ADR-011  
**Версия:** 1.0  
**Дата:** 20 августа 2026 года  
**Статус:** Proposed  
**Область:** физическая структура папки кампании, `.odcamp` container, `manifest.json`, независимые version dimensions кампании (`CampaignFormatVersion`, `DatabaseSchemaVersion`, `RulesetVersion` и рекомендуемые дополнительные), SQLite runtime profile, принцип построения базовой схемы данных, доменные идентификаторы  
**Связанные этапы:** Roadmap Этап 2 (`SLICE-01`), Milestone `M2`, backlog `ODY-S01-001`  
**Базовые документы:** `05_Persistence_Odyssey_VTT_v0.8.md` (разделы 3–9), `ADR-001_Module_Boundaries_and_Dependency_Direction_v1.0.md`, `ADR-003_Serialization_Strategy_v1.1.md`, `ADR-007_Versioning_and_Build_Identity_v1.0.md`, `docs/tasks/SLICE-01_BACKLOG.md`

---

# 1. Решение

Odyssey VTT хранит каждую локальную кампанию как самодостаточную рабочую папку с нормативной физической структурой, единственной авторитетной базой SQLite (`campaign.db`) и компактным bootstrap-манифестом (`manifest.json`), в соответствии с инвариантами `05_Persistence` раздела 3.

Обязательные решения:

1. Кампания физически представлена рабочей папкой с нормативным деревом каталогов (раздел 4 этого ADR). Пользователь сам выбирает расположение папки при создании или импорте; приложение не навязывает обязательный каталог по умолчанию.
2. Единственная авторитетная база данных кампании — `campaign.db` в корне рабочей папки (`PE-INV-001`).
3. Все пути, сохраняемые в авторитетных таблицах или в `manifest.json`, относительны корню кампании. Абсолютный путь пользователя, drive letter как часть asset identity, UNC path исходного файла и временный путь импорта не сохраняются как авторитетные данные (`05_Persistence` §4.2).
4. Перемещение всей папки кампании на другое место в файловой системе не ломает внутренние ссылки; после перемещения допускается изменение только локального клиентского списка Recent Campaigns.
5. `manifest.json` — обязательный, самостоятельно читаемый bootstrap-манифест, позволяющий определить идентичность кампании и совместимость версий до полного открытия SQLite. Манифест не является источником current game state.
6. Поля `CampaignId`, version fields (`CampaignFormatVersion`, `DatabaseSchemaVersion`, `RulesetId`/`RulesetVersion`) дублируются между `manifest.json` и `campaign.db`. При их расхождении запись блокируется, выполняется integrity diagnostic, и автоматическое молчаливое исправление не допускается (`05_Persistence` §5.3).
7. `manifest.json` обновляется только атомарной заменой (`.tmp` → flush → rename/replace). Прямая перезапись существующего файла запрещена.
8. Кампания использует минимум три независимых version dimension: `CampaignFormatVersion` (внешний container/layout), `DatabaseSchemaVersion` (таблицы/индексы/constraints), `RulesetVersion` (игровые формулы и content/rules definitions). Изменение одного dimension не повышает автоматически остальные.
9. Рабочая SQLite база использует WAL journal mode с обязательным профилем PRAGMA, определённым в разделе 8 этого ADR. Логическая запись сериализуется единственным writer per кампания даже при нескольких открытых connections.
10. Базовая схема данных использует гибридный подход: нормализованные current-state tables, append-only `DomainEvents`, отдельные `GameLogEntries`, и JSON columns только там, где структура действительно полиморфна (принцип из `05_Persistence` §8.1). Точная DDL не фиксируется этим ADR — только принцип и минимальный обязательный набор системных таблиц (раздел 9).
11. Доменные идентификаторы генерируются приложением до записи как time-sortable 128-bit значения (рекомендуется UUIDv7), не зависят от SQLite `AUTOINCREMENT`, и могут создаваться на клиенте для `CommandId`.
12. Каждое `DomainEvent` дополнительно получает монотонный `EventSequence` внутри кампании, назначаемый только host в транзакции.
13. Секреты (owner key, OAuth/refresh токены, пароли) никогда не входят в `campaign.db`, `manifest.json`, `.odcamp` или backup (`PE-INV-010`). Конкретный механизм их хранения — предмет отдельной ADR (`ODY-S01-004`); этот ADR только подтверждает границу и не определяет механизм.
14. Этот ADR не определяет: snapshot/backup contract (`ODY-S01-002`), append-only journal detail поверх `DomainEvents` (`ODY-S01-002`), migration runner (`ODY-S01-003`), owner key storage mechanism (`ODY-S01-004`), конкретную .NET SQLite provider-библиотеку (раздел 12, открытый вопрос).

Этот ADR является нормативным authority по физическому формату локальной кампании. Он реализует и уточняет `05_Persistence_Odyssey_VTT_v0.8.md` разделы 3–9 применительно к `SLICE-01` без изменения продуктового поведения, описанного там.

---

# 2. Контекст и проблема

`05_Persistence_Odyssey_VTT_v0.8.md` описывает инварианты и намерение формата кампании, но остаётся product-документом, а не ADR: он не является нормативным authority для реализации и не проходит тот же процесс принятия, что и ADR-001–010. `SLICE-01` не может начать implementation-работу (создание кампании, импорт карты, сцена, токены) без принятого технического контракта, определяющего:

1. Какое именно дерево каталогов нормативно, а что — только пример.
2. Какие поля `manifest.json` обязательны, а какие — рекомендованы.
3. Как разрешается конфликт между `manifest.json` и `campaign.db`.
4. Какой именно PRAGMA-профиль SQLite обязателен, а какой — предмет дальнейшего durability-тестирования (`SP-02`).
5. Где проходит граница между этим ADR и тремя последующими (`ODY-S01-002` snapshot/journal, `ODY-S01-003` migration runner, `ODY-S01-004` owner key storage) — без этой границы работа над ними не может начаться параллельно и независимо.
6. Как формат кампании согласуется с уже принятыми `ADR-001` (module boundaries), `ADR-003` (serialization), `ADR-007` (versioning) без противоречий.

Без этого ADR implementation-задачи `SLICE-01` были бы вынуждены изобретать формат по ходу кода, что `SLICE-00` уже явно запрещает как процесс (см. `PLANS.md` и прецедент `ADR-002`–`ADR-010`).

---

# 3. Термины

## 3.1 Campaign working folder

Рабочая папка кампании на диске пользователя — не архив, а её текущее "открытое" представление, используемое приложением при работе с кампанией.

## 3.2 `.odcamp`

Экспортный/переносимый container кампании (архив), создаваемый из консистентного backup рабочей папки. Runtime SQLite-файлы (`campaign.db-wal`, `campaign.db-shm`) не включаются напрямую; перед экспортом создаётся checkpoint и консистентная копия.

## 3.3 CampaignId / CampaignPublicId

`CampaignId` — внутренний доменный идентификатор кампании (раздел 10). `CampaignPublicId` упомянут в roadmap `SLICE-01` §10.3 как отдельная публично-адресуемая идентичность кампании; его точный контракт (формат, назначение, отношение к сетевой identity) не определяется этим ADR и остаётся открытым для реализации, реализующей эту роадмап-строку, поскольку не входит в объём `05_Persistence` §4–9.

## 3.4 CampaignFormatVersion / DatabaseSchemaVersion / RulesetVersion

Три независимых integer/SemVer version dimension кампании, определённые в разделе 7.

## 3.5 System table / Domain table

System table — таблица, обслуживающая persistence-инфраструктуру (например, `SchemaHistory`, `AppliedCommands`). Domain table — таблица, представляющая доменный aggregate (Campaign, Scene, Character и т.д.), точный состав которых определяется Domain Model, а не этим ADR.

---

# 4. Физическая структура кампании

## 4.1 Нормативное дерево каталогов

```text
CampaignName/
├── campaign.db
├── campaign.db-wal                 # runtime, присутствует пока активен WAL
├── campaign.db-shm                 # runtime, присутствует пока активен WAL
├── manifest.json
├── campaign.lock
├── Assets/
│   ├── Objects/
│   ├── Staging/
│   ├── Trash/
│   └── Quarantine/
├── Backups/
│   ├── Fast/
│   ├── Daily/
│   ├── Weekly/
│   ├── Full/
│   └── Emergency/
├── Logs/
│   ├── Archive/
│   ├── Diagnostics/
│   └── Migration/
└── Temp/
```

Правила:

- `campaign.db`, `manifest.json` и `campaign.lock` обязаны находиться непосредственно в корне рабочей папки.
- `campaign.db-wal`/`campaign.db-shm` — временные файлы SQLite WAL-режима; их присутствие/отсутствие не является ошибкой и не входит в `.odcamp` как отдельно значимые файлы.
- Поддеревья `Assets/`, `Backups/`, `Logs/`, `Temp/` обязательны как минимальный набор; дополнительные подкаталоги внутри них (например, `Backups/Fast/`) допускаются точно с этими именами, если соответствующая функция (fast backup, daily backup и т.д.) реализована; отсутствие ещё не реализованной функции не требует пустого каталога заранее.
- `Temp/` не является авторитетным хранилищем; его содержимое может быть безопасно удалено между сессиями приложения без потери подтверждённого состояния кампании.
- Содержимое `Assets/Trash/` и `Assets/Quarantine/` не входит в обычный `.odcamp` export без явного пользовательского решения (детали экспортного поведения — предмет реализации, не этого ADR).

## 4.2 Относительные пути

Все пути, сохраняемые в `campaign.db` или `manifest.json`, обязаны быть относительны корню рабочей папки кампании. Запрещено сохранять в авторитетных таблицах или в манифесте:

- абсолютный путь файловой системы пользователя;
- drive letter как часть asset identity;
- UNC path исходного файла;
- временный путь импорта.

Исходный (импортный) путь может кратковременно существовать в локальном UI-командном контексте на момент импорта, но не обязан и не должен попадать в доменный журнал или в `manifest.json`.

## 4.3 Перемещение папки кампании

Перемещение всей рабочей папки кампании на другое место в файловой системе (тот же диск, другой диск, другой компьютер через `.odcamp`) не должно ломать ни одну внутреннюю ссылку внутри кампании, поскольку все пути относительны (раздел 4.2). После перемещения единственное допустимое клиентское последствие — обновление локального, не входящего в кампанию списка Recent Campaigns.

---

# 5. `manifest.json`

## 5.1 Назначение

`manifest.json` — компактный bootstrap-манифест, позволяющий приложению определить до полного открытия SQLite: идентичность кампании, совместимость версий, отображаемое имя для UI, необходимость migration или read-only mode. Манифест **не** является источником current game state; при конфликте с `campaign.db` в отношении версий и идентичности применяется правило раздела 5.3, а не приоритет одного файла над другим по умолчанию.

## 5.2 Обязательные поля

```text
CampaignId
CampaignName
CampaignFormatVersion
DatabaseSchemaVersion
RulesetId
RulesetVersion
CreatedAt
LastModifiedAt
ApplicationVersionLastOpened
AssetManifestVersion
IsTemplate
CloneSourceCampaignId?
LastSuccessfulBackupAt?
```

## 5.3 Рекомендуемые дополнительные поля

```text
ManifestSchemaVersion
DatabaseFile = "campaign.db"
AssetsDirectory = "Assets"
CreatedByApplicationVersion
LastCleanShutdownAt?
LastOpenedMode
ContentPackageRefs[]
RequiredFeatureFlags[]
```

Реализующая задача обязана включить `ManifestSchemaVersion` как минимум одним из рекомендуемых полей, поскольку без него сам манифест не может версионироваться независимо от `CampaignFormatVersion` (см. раздел 7.2). Остальные рекомендуемые поля могут быть отложены до момента, когда соответствующая функция (feature flags, content package refs) реализуется.

## 5.4 Авторитетность полей

`CampaignId`, все version fields (`CampaignFormatVersion`, `DatabaseSchemaVersion`, `RulesetId`/`RulesetVersion`) дублируются между `manifest.json` и `campaign.db`. При расхождении между манифестом и базой:

1. запись в базу блокируется;
2. выполняется integrity diagnostic;
3. автоматическое молчаливое исправление одного значения на основе другого не допускается;
4. восстановление возможно только из подтверждённого источника (явное пользовательское/GM решение) или из backup.

Ни манифест, ни база не объявляются этим ADR "более авторитетными" по умолчанию — расхождение всегда является диагностируемой ошибкой, а не поводом для тихого выбора одной стороны.

## 5.5 Запись манифеста

Манифест обновляется только атомарной заменой:

```text
manifest.json.tmp
→ flush
→ rename/replace
→ manifest.json
```

Прямая перезапись существующего `manifest.json` без atomic-replace запрещена. Поле `LastModifiedAt` не обязано обновляться после каждой отдельной команды; оно обновляется после значимых checkpoint-операций, чистого закрытия (clean close) и backup.

---

# 6. Версии кампании

## 6.1 Независимые dimensions

Кампания использует минимум три независимых version dimension:

| Dimension | Формат | Описывает |
|---|---|---|
| `CampaignFormatVersion` | monotonic integer, начиная с `1` | внешний container/layout: папки, `manifest.json`, `.odcamp`, asset layout, archive layout |
| `DatabaseSchemaVersion` | monotonic integer, начиная с `1` | таблицы, индексы, constraints и persistence contracts внутри `campaign.db` |
| `RulesetVersion` | SemVer | игровые формулы и content/rules definitions |

Изменение одного dimension не повышает автоматически остальные — правило идентично `ADR-007` §5 "Правило независимости", применённому здесь к кампании, а не к приложению.

## 6.2 Рекомендуемые дополнительные версии

```text
EventContractVersion
CalculationTraceSchemaVersion
AssetManifestVersion
SessionArchiveSchemaVersion
```

Они позволяют читать исторические события без переписывания payload задним числом (`PE-INV-004`). Их точные контракты не определяются этим ADR: `EventContractVersion` относится к предмету `ODY-S01-002` (append-only journal), остальные — к соответствующим будущим ADR/задачам по мере реализации содержащих их функций.

## 6.3 Хранение версий в базе

```text
PersistenceMetadata
├── Key
├── Value
└── UpdatedAt
```

Критические version fields дополнительно имеют типизированные columns в `CampaignRecord` и `SchemaHistory` (минимальный набор системных таблиц — раздел 9.1). `SchemaHistory` как таблица минимально обязательна этим ADR; её полная схема (какие именно колонки, ссылка на migration registry) — предмет `ODY-S01-003` (Migration Runner), поскольку `SchemaHistory` — прежде всего журнал миграций, не журнал формата.

## 6.4 Отношение к `ApplicationVersion` и `BuildIdentity`

Per `ADR-007`, `ApplicationVersion` и `BuildIdentity` — dimensions приложения, не кампании. `CampaignFormatVersion`, `DatabaseSchemaVersion` и `RulesetVersion` не выводятся из `ApplicationVersion` и не повышаются автоматически при её изменении (`ADR-007` §5, §11.1, "Правило независимости"). `manifest.json` поле `ApplicationVersionLastOpened`/`CreatedByApplicationVersion` — диагностическая информация о том, каким билдом приложения кампания была создана/последний раз открыта; она **не** является compatibility gate. Совместимость определяется исключительно `CampaignFormatVersion`, `DatabaseSchemaVersion`, `ManifestSchemaVersion` и `RulesetVersion` в сравнении с `config/compatibility.json` (`ADR-007` §7.2, §13.1) — не сравнением `ApplicationVersion`.

---

# 7. SQLite runtime profile

## 7.1 Режим журналирования

Рабочая база кампании обязана использовать следующий PRAGMA-профиль:

```sql
PRAGMA journal_mode = WAL;
PRAGMA foreign_keys = ON;
PRAGMA synchronous = FULL;
PRAGMA busy_timeout = 5000;
```

Обоснование: crash-safe завершённые транзакции, чтение projections без блокировки writer на обычных операциях, безопасная работа SQLite Backup API, отсутствие необходимости вручную копировать открытый DB-файл.

Эквивалентная (отличающаяся) конфигурация допускается только после durability-тестов — то есть только по результатам `SP-02` (`ODY-S01-005`) и только через явное amendment этого ADR, не молчаливым изменением реализации.

## 7.2 Единственный writer

Application Layer использует одну логическую write queue на кампанию. Даже если базовый SQLite provider допускает несколько одновременных connections, команды записи сериализуются на уровне host runtime, а не полагаются на файловую блокировку SQLite как единственный механизм.

## 7.3 Read connections

Read-only connections допускаются для: UI projections, reports, search, SessionArchive, diagnostics, backup validation. Read-only connection не должно наблюдать частично применённую (ещё не закоммиченную) команду.

## 7.4 Checkpoint

WAL checkpoint обязателен: при чистом закрытии (clean close); перед упаковкой `.odcamp`; по внутренней политике размера WAL-файла; после безопасного backup, если это не мешает активной сессии. Явная пользовательская команда "Сохранить" (если появится в UI) может инициировать checkpoint и snapshot, но не является условием сохранения уже подтверждённых команд (`PE-INV-007`).

---

# 8. Принцип построения базовой схемы данных

## 8.1 Гибридный подход

Схема данных кампании обязана следовать гибридному подходу:

1. нормализованные current-state tables для доменных aggregate;
2. append-only `DomainEvents` (полный контракт этой таблицы — предмет `ODY-S01-002`, здесь фиксируется только факт её обязательного присутствия как системной таблицы);
3. отдельные `GameLogEntries` для пользовательского журнала (отдельно от `DomainEvents`);
4. JSON payload только там, где структура действительно полиморфна;
5. типизированные columns для keys, revisions, status, sequence, visibility и полей, участвующих в поиске/фильтрации.

Перенос legacy-подхода "вся механика в одном JSON/RPC blob" не допускается.

## 8.2 Минимальный обязательный набор системных таблиц

```text
PersistenceMetadata
SchemaHistory
AppliedCommands
DomainEvents
AggregateRevisions
PendingInteractions
NetworkOutbox
AdministrativeAudit
DiagnosticRecords
BackupRecords
MigrationRecords
AssetManifestEntries
AssetReferences
SessionArchiveIndex
```

Этот ADR фиксирует обязательность существования этих системных таблиц как части формата; их полный DDL, точные колонки и индексы определяются реализующей задачей и, где применимо, последующими ADR (`ODY-S01-002` для `DomainEvents`/`AggregateRevisions` semantics, `ODY-S01-003` для `SchemaHistory`/`MigrationRecords` semantics).

## 8.3 Доменные таблицы

Точный набор доменных таблиц (Campaign, Scene, SceneObject/Token и т.д.) определяется Domain Model (`03_Domain_Model_Odyssey_VTT_v0.25.md`), не этим ADR. Aggregate root обычно имеет как минимум:

```text
Id
CampaignId
Revision
Status
CreatedAt
UpdatedAt
CreatedByUserId?
LastModifiedByUserId?
```

## 8.4 JSON columns

Допустимые применения JSON columns: immutable event payload, CalculationTrace tree, сложный EffectMechanicsSnapshot, migration step details, diagnostic technical payload, versioned content block payload. JSON не заменяет: foreign keys, aggregate revision, campaign ownership, sequence, статус lifecycle, доступные для фильтрации даты, integrity constraints.

Любой JSON payload, персистируемый по этому ADR или его дочерним ADR (`ODY-S01-002`), обязан использовать явные, версионированные DTO и канонические кодеки, определённые `ADR-003_Serialization_Strategy_v1.1.md` — не reflection-based или auto-mapping сериализацию (`ADR-003` §3, нормативное действие). Этот ADR не вводит отдельный, параллельный JSON-механизм для формата кампании.

---

# 9. Идентификаторы

## 9.1 Тип

Доменные ID генерируются приложением до записи в базу. Рекомендуется UUIDv7 либо эквивалентный time-sortable 128-bit identifier. Требования:

- глобальная уникальность;
- отсутствие зависимости от SQLite `AUTOINCREMENT` для domain identity;
- возможность создавать `CommandId` на клиенте (до отправки команды хосту);
- стабильное логирование и корреляция (совместимо с `CorrelationId`/`DiagnosticId` из `ADR-004`).

## 9.2 Локальная последовательность событий

Каждое `DomainEvent` дополнительно получает монотонный `EventSequence` внутри кампании:

```text
Campaign EventSequence: 1, 2, 3, ...
```

`EventSequence` назначается только host в рамках транзакции. Полная семантика упорядочивания и видимости событий — предмет `ODY-S01-002`; здесь фиксируется только факт существования и монотонности этого поля как части формата.

## 9.3 Клонирование

Полное клонирование кампании: `CampaignId` получает новое значение, внутренние `EntityId` сохраняются без изменений. Для трассировки допускаются поля `CloneSourceCampaignId`/`ClonedAt`. Сетевые directory entries и внешние приглашения не переносятся автоматически при клонировании.

## 9.4 Шаблон и выборочный импорт

При импорте отдельных данных из другой кампании/шаблона: `EntityId` получает новое значение, внутренние ссылки переназначаются по `ImportIdMap`. Импорт хранит исходную identity отдельно только для диагностики (`SourcePackageId`, `SourceEntityId`), не как активную ссылку доменной модели.

---

# 10. Соответствие module boundaries (ADR-001)

`Odyssey.Persistence` реализует persistence ports, объявленные `Odyssey.Application` (`ADR-001` §6.5). Формат, определённый этим ADR, обязан оставаться реализуемым в рамках уже принятых границ:

- `Persistence` не владеет игровыми инвариантами и не вызывает `Networking` напрямую (`ADR-001` §6.5, зависимость `Persistence → Networking` запрещена).
- `Persistence` не имеет права считать запись успешной до соблюдения Application transaction contract (`ADR-001` §6.5; согласуется с `PE-INV-003`, `PE-INV-005` этого документа-источника).
- Database row/record, реализация migration и repository остаются в модуле `Persistence`, не в `Domain`/`Application` (`ADR-001` таблица классификации, строка "Database row/record, migration, repository implementation").

Этот ADR не переопределяет и не ослабляет ни одно из этих правил; он их подтверждает применительно к конкретному физическому формату.

---

# 11. Не входит в ADR-011

Явно исключено из объёма этого ADR (владеют другие задачи backlog `SLICE-01`, см. `docs/tasks/SLICE-01_BACKLOG.md` §4):

- **Snapshot и append-only journal contract** (порядок событий сверх факта монотонности `EventSequence`, payload hash, event visibility, snapshot-триггеры и создание) — `ODY-S01-002`.
- **Migration runner** (registry миграций, порядок выполнения, транзакционность, откат, полная схема `SchemaHistory`/`MigrationRecords`) — `ODY-S01-003`.
- **Owner key storage mechanism** (конкретный OS API, формат, ротация, восстановление при потере) — `ODY-S01-004`. Этот ADR только подтверждает существующую границу `PE-INV-010` ("секреты не входят в кампанию"), не переопределяет и не предвосхищает её реализацию.
- Backup rotation policy, restore workflow, corruption test fixture — используют физическую структуру `Backups/` (раздел 4.1), но их поведенческий контракт не определяется здесь.
- `.odcamp` export/import workflow validation (manifest validation, отсутствие auto-merge) — использует формат, определённый здесь, но сам workflow — предмет реализации `SLICE-01` вертикального среза, не этого ADR.
- Выбор конкретной .NET SQLite provider-библиотеки — см. раздел 12 (открытый вопрос).

---

# 12. Открытые вопросы

## 12.1 Выбор конкретной SQLite provider-библиотеки — `[OPEN]`

`05_Persistence` §7 и раздел 7 этого ADR фиксируют обязательный PRAGMA-профиль (WAL, `foreign_keys = ON`, `synchronous = FULL`, `busy_timeout = 5000`) и поведенческие правила (single writer, read connections, checkpoint), но **не** фиксируют конкретную .NET-библиотеку доступа к SQLite (например, `Microsoft.Data.Sqlite` против `System.Data.SQLite` против прямого использования конкретного `SQLitePCLRaw` bundle).

Это намеренно оставлено открытым, потому что:

- выбор конкретной библиотеки — implementation-деталь, а не формат данных на диске;
- `ODY-S01-005` (`SP-02 — Persistence Reliability`) явно проверяет WAL/transaction mode, crash-сценарии, interrupted backup, migration failure/rollback, snapshot size/speed и corrupted-db recovery — то есть именно те характеристики, от которых зависит пригодность конкретной provider-библиотеки;
- пин библиотеки без этих данных рискует зафиксировать выбор, который `SP-02` затем опровергнет, что потребовало бы либо игнорировать находки spike, либо amendment этого ADR.

Решение по конкретной библиотеке принимается либо как явный итог `SP-02` отчёта (`ODY-S01-005`), либо отдельным решением владельца продукта/amendment-ADR до начала кодирования migration runner (`ODY-S01-003`) или вертикального среза `SLICE-01`. Реализация не должна начинаться на непроверенной provider-библиотеке без owner approval.

## 12.2 `CampaignPublicId` — `[OPEN]`

Roadmap `SLICE-01` §10.3 упоминает `CampaignPublicId` наравне с `CampaignId`, но `05_Persistence` §4–9 не определяет его контракт. Раздел 3.3 этого ADR фиксирует термин, но не решение. Точный формат и назначение `CampaignPublicId` (публичная адресуемая идентичность для будущей сетевой функциональности?) остаётся открытым до соответствующей Networking-ADR или явного product owner решения; текущий ADR не блокирует `ODY-S01-001`-зависимые задачи этим вопросом, поскольку `CampaignPublicId` не требуется ни для одного обязательного решения разделов 4–10.

---

# 13. Правила для Codex

Codex обязан:

1. Использовать дерево каталогов раздела 4.1 как нормативное; не изобретать альтернативную структуру без amendment этого ADR.
2. Не сохранять абсолютные пути, drive letters или UNC paths в `campaign.db` или `manifest.json`.
3. Не считать `manifest.json` или `campaign.db` автоматически более авторитетным при их расхождении — только диагностировать и блокировать запись (раздел 5.4).
4. Не реализовывать snapshot/journal, migration runner или owner key storage mechanism под этим ADR — они принадлежат `ODY-S01-002`, `ODY-S01-003`, `ODY-S01-004` соответственно.
5. Не пинить конкретную SQLite provider-библиотеку без owner approval или явного итога `SP-02` (раздел 12.1).
6. Не отклоняться от обязательного PRAGMA-профиля раздела 7.1 без durability-теста и amendment этого ADR.
7. Не сериализовывать JSON columns через reflection-based/auto-mapping механизмы — только явные версионированные кодеки `ADR-003`.
8. Не вводить `CampaignFormatVersion`/`DatabaseSchemaVersion`/`RulesetVersion` bump без документированной причины (раздел 6.1, аналогично `ADR-007` §5).
9. Указывать в PR summary затронутые version dimensions кампании, если задача их меняет.

---

# 14. Definition of Done / критерии приёмки ADR-011 implementation

ADR считается реализованным (для той части, которую он определяет), когда:

1. Создание новой кампании производит дерево каталогов, соответствующее разделу 4.1.
2. `manifest.json` содержит все обязательные поля раздела 5.2 и валидируется строгим parser.
3. Запись манифеста выполняется только через atomic replace (раздел 5.5); прямая перезапись не встречается в кодовой базе.
4. Расхождение version fields между `manifest.json` и `campaign.db` детектируется и блокирует запись, не исправляется молчаливо (раздел 5.4).
5. Рабочая база открывается с PRAGMA-профилем раздела 7.1; отклонение детектируется тестом.
6. Все системные таблицы раздела 8.2 существуют в начальной схеме.
7. Доменные идентификаторы генерируются приложением как time-sortable 128-bit значения, не полагаются на `AUTOINCREMENT` для domain identity.
8. `EventSequence` монотонно возрастает внутри кампании и назначается только host-транзакцией.
9. Перемещение рабочей папки кампании (тест: скопировать/переместить и открыть заново) не ломает внутренние ссылки.
10. Ни один из compatibility dimensions (`CampaignFormatVersion`, `DatabaseSchemaVersion`, `RulesetVersion`) не выводится автоматически из другого или из `ApplicationVersion`.

---

# 15. Рассмотренные альтернативы

## 15.1 Единая версия формата вместо трёх независимых dimensions

Отклонено: связывает несвязанные изменения (layout, схема БД, игровые правила), что уже отвергнуто на уровне приложения `ADR-007` §29.1 по той же причине; кампания не должна повторять эту ошибку на своём уровне.

## 15.2 Абсолютные пути внутри кампании для простоты реализации

Отклонено: делает `.odcamp` непереносимым между машинами/пользователями и напрямую противоречит `05_Persistence` §4.2 (`[DERIVED]` инвариант).

## 15.3 `manifest.json` как единственный source of truth (без дублирования в БД)

Отклонено: не позволяет открыть/диагностировать кампанию, если БД повреждена, но манифест цел, и наоборот; дублирование с явным правилом конфликта (раздел 5.4) безопаснее одного источника без перекрёстной проверки.

## 15.4 Прямая перезапись `manifest.json` без atomic replace

Отклонено: прерывание процесса или файловой системы во время записи может оставить повреждённый/частичный манифест, блокирующий открытие кампании; atomic replace устраняет этот класс сбоев.

## 15.5 SQLite `AUTOINCREMENT` как доменный идентификатор

Отклонено: делает невозможным создание `CommandId`/доменных ID на клиенте до подтверждения хостом, создаёт коллизии при клонировании/импорте и переносе между базами.

## 15.6 Зафиксировать конкретную SQLite provider-библиотеку сейчас

Отклонено на этом этапе: решение до данных `SP-02` рискует зафиксировать выбор, который спайк на надёжность (crash, corrupted db, migration rollback) может опровергнуть. Вынесено как открытый вопрос (раздел 12.1) вместо молчаливого решения.

## 15.7 "Вся механика в JSON/RPC blob" вместо гибридной схемы

Отклонено: явно запрещено `05_Persistence` §8.1 как legacy-подход; лишает возможности использовать foreign keys, индексы и типизированные constraints для целостности домена.

---

# 16. Трассировка

ADR реализует и уточняет:

- `05_Persistence_Odyssey_VTT_v0.8.md`, разделы 3 (инварианты `PE-INV-001`–`PE-INV-010`), 4 (физическая структура), 5 (`manifest.json`), 6 (версии кампании), 7 (SQLite runtime profile), 8 (базовая схема данных), 9 (идентификаторы);
- `17_Roadmap_Odyssey_VTT_v0.11.md` §10.2 (предварительные документы Этапа 2) и §10.3 (Campaign Storage);
- `ADR-001_Module_Boundaries_and_Dependency_Direction_v1.0.md` §6.5 (Persistence module ownership);
- `ADR-003_Serialization_Strategy_v1.1.md` (канонические JSON-кодеки для manifest/event payload);
- `ADR-007_Versioning_and_Build_Identity_v1.0.md` §5, §11.1, §13.1 (независимость version dimensions, отношение к `ApplicationVersion`/`BuildIdentity`).

Связанные будущие задачи (`docs/tasks/SLICE-01_BACKLOG.md`):

```text
ODY-S01-002  ADR: Snapshot and Append-Only Journal
ODY-S01-003  ADR: Migration Runner
ODY-S01-004  ADR: Owner Key Storage Baseline
ODY-S01-005  Technical Spike SP-02: Persistence Reliability
```

---

# 17. Нормативное действие

С даты принятия (`Accepted`):

- этот ADR имеет приоритет над `05_Persistence_Odyssey_VTT_v0.8.md` разделами 3–9 при возможных расхождениях в части нормативных решений (05_Persistence остаётся источником продуктового контекста и намерения, но реализация обязана следовать формулировкам этого ADR);
- ни одна implementation-задача `SLICE-01` не создаёт кампанию, `manifest.json` или SQLite-схему в противоречии с разделами 4–9;
- `ODY-S01-002` (Snapshot and Append-Only Journal) и `ODY-S01-003` (Migration Runner) авторизованы опираться на этот ADR как на принятую основу и не обязаны повторно решать вопросы разделов 4–10;
- открытые вопросы раздела 12 остаются открытыми до отдельного решения и не считаются молчаливо решёнными фактом принятия этого ADR;
- изменение принятого формата требует amendment этого ADR или нового superseding ADR, не молчаливого отклонения в реализации.

---

**Конец документа**
