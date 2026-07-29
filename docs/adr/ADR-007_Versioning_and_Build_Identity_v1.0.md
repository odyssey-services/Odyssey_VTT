# ADR-007 — Versioning and Build Identity

**Документ:** `docs/adr/ADR-007_Versioning_and_Build_Identity_v1.0.md`  
**ADR:** ADR-007  
**Версия:** 1.0  
**Дата:** 27 июля 2026 года  
**Статус:** Accepted  
**Область:** application version, build identity, release channels, Git tags, artifact identity, compatibility dimensions, schema/protocol support ranges, version source of truth, runtime display, CI generation, release provenance и `SLICE-00` version scaffold  
**Связанные этапы:** Roadmap Stage 1, `SLICE-00`, Milestone `M1`, все последующие Release Candidate и Release stages  
**Базовые документы:** `TECHNICAL_DEVELOPMENT_BASELINE_Odyssey_VTT_v0.1.md`, `05_Persistence_Odyssey_VTT_v0.8.md`, `06_Networking_and_Session_Sync_Odyssey_VTT_v0.8.md`, `16_Test_Strategy_Odyssey_VTT_v0.1.md`, `17_Roadmap_Odyssey_VTT_v0.11.md`, `ADR-002_Command_and_Domain_Event_Model_v1.0.md`, `ADR-003_Serialization_Strategy_v1.0.md`, `ADR-004_Result_and_Error_Model_v1.0.md`, `ADR-006_Test_Project_Structure_and_Dual_Unity_DotNet_Compilation_v1.0.md`

---

# 1. Решение

Odyssey VTT использует **разделённую модель версий**, в которой версия приложения, идентичность конкретной сборки, версии форматов данных, сетевого протокола, ruleset и content package являются независимыми измерениями.

Обязательные решения:

1. Версия приложения использует SemVer-compatible форму `MAJOR.MINOR.PATCH`.
2. До стабильного публичного контракта приложение находится в линии `0.MINOR.PATCH`.
3. Начальная версия технического каркаса — `0.1.0`; она не считается выпущенной до появления утверждённого release tag.
4. Единственный source of truth версии приложения — корневой `version.json`.
5. Версия не повышается автоматически на каждый commit или pull request.
6. Каждая собранная копия приложения имеет отдельный неизменяемый `BuildIdentity`.
7. `BuildIdentity` всегда связывает artifact с точным Git commit, build channel, CI run или local build, Unity version, configuration и совместимостными версиями.
8. Канонический `BuildId` уникален для каждой фактической сборки, даже если `ApplicationVersion` одинакова.
9. Release artifact создаётся только CI из чистого tagged commit защищённой `main`.
10. Локальная dirty-сборка и PR artifact никогда не считаются Release или Release Candidate.
11. Version strings в UI, логах, diagnostic bundle, release report и artifact manifest генерируются из одного BuildIdentity, а не задаются вручную.
12. `ProjectSettings/ProjectVersion.txt` является source of truth точной версии Unity Editor и не является версией Odyssey VTT.
13. `DatabaseSchemaVersion`, `CampaignFormatVersion`, `ManifestSchemaVersion`, `ContractVersion`, `FingerprintVersion` и `NetworkProtocolVersion` используют независимые монотонные integer versions, начинающиеся с `1`.
14. `RulesetVersion` и `ContentPackageVersion` используют SemVer и сохраняются точными resolved-версиями.
15. Изменение одного version dimension не увеличивает остальные автоматически.
16. Совместимость определяется соответствующей version/capability matrix, а не сравнением только `ApplicationVersion`.
17. Более новая `ApplicationVersion` сама по себе не запрещает открытие кампании или соединение; решение принимается по schema, protocol, contract, required capabilities и ruleset compatibility.
18. Корневой `config/compatibility.json` является source of truth объявляемых support ranges приложения.
19. Migrations, contract registries и protocol registries обязаны подтверждать значения `compatibility.json`; расхождение блокирует build.
20. Git release tags имеют форму `vMAJOR.MINOR.PATCH`; pre-release tags — `vMAJOR.MINOR.PATCH-rc.N`.
21. Release tags неизменяемы и не переносятся на другой commit.
22. Git tag, `version.json`, generated BuildIdentity и имя release artifact обязаны согласовываться; любое расхождение блокирует publication.
23. Каждый опубликованный artifact сопровождается `build-identity.json` и SHA-256 checksums.
24. Codex не повышает version и не создаёт tag без явного scope задачи.
25. Изменение этой модели version dimensions, tag policy или canonical BuildId требует нового ADR.

Этот ADR является нормативным authority по versioning и build identity. Он заменяет предварительный раздел 25 Technical Development Baseline и уточняет версии из Persistence, Networking, Serialization и Test Strategy без изменения продуктового поведения.

---

# 2. Контекст и проблема

В Odyssey одновременно существуют разные понятия «версии»:

- версия самого приложения;
- конкретная сборка одного commit;
- точная версия Unity Editor;
- схема SQLite;
- структура `.odcamp`;
- bootstrap manifest;
- версия JSON payload отдельной команды или события;
- версия алгоритма command fingerprint;
- версия сетевого протокола;
- версия ruleset;
- версия content package;
- версия документации.

Если использовать одно число для всех этих задач, возникают ошибки:

- исправление UI ошибочно требует migration базы;
- изменение ruleset ошибочно повышает schema SQLite;
- одинаковые `0.4.0` builds невозможно различить;
- PR artifact можно принять за release;
- клиент сравнивает application version вместо protocol range;
- пользователь не может сообщить точную сборку;
- журнал тестов не связывается с конкретным commit;
- повторная сборка незаметно заменяет уже проверенный artifact;
- Codex меняет `PlayerSettings.bundleVersion`, но забывает manifest или release tag;
- локальная dirty-сборка публикуется без доказуемого source state.

Нужна единая политика, которая отвечает на вопросы:

1. Как назвать продуктовую версию?
2. Как однозначно идентифицировать конкретный бинарный artifact?
3. Где хранится source of truth?
4. Какие version dimensions независимы?
5. Когда требуется bump каждой версии?
6. Как проверяется совместимость?
7. Как release связывается с Git и CI?
8. Что видит пользователь и что попадает в диагностику?

---

# 3. Движущие факторы

Решение оптимизировано под:

1. Публичный GitHub repository и защищённую `main`.
2. Разработку через Codex и небольшие pull request.
3. Unity 6.3 LTS с exact patch pin.
4. Windows 10/11 x64 artifacts.
5. Повторяемую CI-сборку и traceability до commit.
6. Независимую эволюцию SQLite, `.odcamp`, protocol, events и ruleset.
7. Безопасное открытие старых кампаний и блокировку неизвестных новых форматов.
8. Host-authoritative networking с protocol negotiation.
9. TestEvidence и ReleaseQualityReport, привязанные к неизменяемой сборке.
10. Отсутствие стороннего versioning framework на `SLICE-00`.
11. Читаемые версии для владельца продукта и пользователей.
12. Машинно проверяемые versions для CI, миграций и handshake.
13. Запрет скрытых version bumps и ручного рассинхрона.

---

# 4. Термины

## 4.1 ApplicationVersion

Продуктовая версия Odyssey VTT в форме `MAJOR.MINOR.PATCH`.

Она описывает линию возможностей приложения, но не идентифицирует конкретный artifact.

## 4.2 DisplayVersion

Полная строка, показываемая в dev/status UI и diagnostics. Она включает ApplicationVersion, channel-specific prerelease identity и Git metadata.

Примеры:

```text
0.1.0-local.20260727T181500Z+g1a2b3c4d5e6f.dirty
0.1.0-pr.42.1+g1a2b3c4d5e6f
0.1.0-dev.918273645.1+g1a2b3c4d5e6f
0.1.0-rc.1+g1a2b3c4d5e6f
0.1.0
```

## 4.3 BuildIdentity

Неизменяемый набор provenance и compatibility metadata конкретной фактически созданной сборки.

## 4.4 BuildId

Канонический уникальный машинный идентификатор конкретного build execution.

## 4.5 Build channel

Категория назначения artifact:

```text
Local
PullRequest
Development
ReleaseCandidate
Release
```

## 4.6 Compatibility version

Версия конкретного технического контракта: schema, format, payload, fingerprint или protocol.

## 4.7 Support range

Минимальная, максимальная и preferred/current версия, которую build объявляет поддерживаемой для конкретной границы.

## 4.8 Release provenance

Доказуемая связь опубликованного artifact с repository, tag, commit, workflow run, toolchain и checksums.

## 4.9 Dirty build

Локальная сборка, созданная из working tree с незакоммиченными изменениями или с состоянием, которое CI не может однозначно восстановить по commit.

---

# 5. Независимые version dimensions

Odyssey различает минимум следующие dimensions:

| Dimension | Формат | Source of truth | Назначение |
|---|---|---|---|
| ApplicationVersion | SemVer | `/version.json` | продуктовая линия приложения |
| BuildIdentity | structured object | generated by build script | конкретный artifact |
| UnityVersion | exact Unity version | `ProjectSettings/ProjectVersion.txt` | toolchain проекта |
| DatabaseSchemaVersion | integer | migration registry + compatibility config | SQLite tables/indexes/constraints |
| CampaignFormatVersion | integer | interchange registry + compatibility config | внешний layout кампании/`.odcamp` |
| ManifestSchemaVersion | integer | manifest registry | JSON bootstrap manifest |
| AssetManifestVersion | integer | asset manifest registry | layout asset metadata |
| ContractVersion | integer per ContractType | contract registry | command/event/result payload semantics |
| FingerprintVersion | integer | fingerprint registry | canonical command fingerprint algorithm |
| NetworkProtocolVersion | integer | protocol registry + compatibility config | wire envelope/handshake semantics |
| AssetProtocolVersion | integer | networking capability registry | asset transfer protocol |
| AudioProtocolVersion | integer | networking capability registry | synchronized audio control protocol |
| RulesetVersion | SemVer | ruleset manifest | игровые правила и формулы |
| ContentPackageVersion | SemVer | package manifest | версия content package |
| DocumentationVersion | document-local | document header/file name | документация; не runtime contract |

Правило независимости:

```text
ApplicationVersion bump
≠ automatic DatabaseSchemaVersion bump
≠ automatic ProtocolVersion bump
≠ automatic RulesetVersion bump
```

Каждый bump имеет собственную причину, migration/compatibility evidence и tests.

---

# 6. ApplicationVersion

## 6.1 Формат

До 1.0 используется:

```text
0.MINOR.PATCH
```

Все три части — неотрицательные integer без ведущих нулей.

Запрещены в source of truth:

- `v` внутри значения;
- дата вместо version;
- слова `alpha`, `latest`, `final`;
- floating wildcard;
- автоматическое значение из количества commits.

## 6.2 Начальная версия

При создании репозитория:

```text
ApplicationVersion = 0.1.0
```

Это **planned/current development line**, а не утверждение, что `0.1.0` уже выпущена.

Release существует только после принятого tag и publication workflow.

## 6.3 Правила bump до 1.0

### MINOR

`MINOR` повышается, когда происходит хотя бы одно:

- завершён новый material vertical slice;
- добавлена значимая пользовательская capability;
- изменён внешний/публичный contract несовместимо;
- требуется обязательная migration с заметным продуктовым эффектом;
- владелец продукта начинает новую release line.

Пример:

```text
0.3.4 → 0.4.0
```

После MINOR bump PATCH сбрасывается в `0`.

### PATCH

`PATCH` повышается для:

- исправления defect;
- backward-compatible улучшения;
- performance/stability изменения без новой material capability;
- корректировки packaging/diagnostics, требующей нового release artifact;
- совместимой migration, выпущенной как исправление.

Пример:

```text
0.4.0 → 0.4.1
```

### Без bump

ApplicationVersion не обязана изменяться для:

- каждого commit;
- каждого PR;
- docs-only изменения;
- теста без изменения production behavior;
- новой CI-сборки той же release line;
- changelog/evidence update.

Такие artifacts различаются BuildIdentity.

## 6.4 Переход к 1.0

`1.0.0` допускается только отдельным решением владельца продукта после определения:

- стабильного public compatibility contract;
- обязательного migration window;
- release/support policy;
- update/rollback process;
- завершённого MVP acceptance.

Этот ADR не объявляет 1.0.

---

# 7. Source of truth файлов

## 7.1 `/version.json`

Корневой tracked-файл:

```json
{
  "schemaVersion": 1,
  "applicationVersion": "0.1.0"
}
```

Правила:

- committed в Git;
- reviewable как обычный code change;
- меняется отдельным явным versioning change;
- парсится строгим validator;
- unknown schema version блокирует build;
- дополнительные произвольные поля не становятся contract без обновления schema.

## 7.2 `/config/compatibility.json`

Tracked source of truth support ranges:

```json
{
  "schemaVersion": 1,
  "databaseSchema": {
    "current": 1,
    "minimumMigratable": 1,
    "minimumReadable": 1
  },
  "campaignFormat": {
    "current": 1,
    "minimumReadable": 1
  },
  "manifestSchema": {
    "current": 1,
    "minimumReadable": 1
  },
  "networkProtocol": {
    "minimum": 1,
    "maximum": 1,
    "preferred": 1
  },
  "assetProtocol": {
    "minimum": 1,
    "maximum": 1,
    "preferred": 1
  },
  "audioProtocol": {
    "minimum": 1,
    "maximum": 1,
    "preferred": 1
  }
}
```

Конкретные поля могут расширяться новой schema version файла, но не переименовываются молча.

## 7.3 Запрещённые дублирующие sources

Не являются самостоятельным source of truth:

- `PlayerSettings.bundleVersion`;
- строка в UI;
- `AssemblyVersion`;
- имя ZIP;
- Git branch name;
- последний Git tag;
- package version Unity;
- текст release notes;
- значение в README.

Они генерируются или проверяются относительно `version.json`, compatibility registries и BuildIdentity.

## 7.4 Unity PlayerSettings

Build pipeline перед сборкой устанавливает `PlayerSettings.bundleVersion` из ApplicationVersion.

Ручное расхождение блокирует `verify-repository` или исправляется только generated build step без commit скрытого изменения.

---

# 8. Build channels и DisplayVersion

## 8.1 Local

Локальная сборка:

```text
0.1.0-local.<UTC>+g<ShortSha>[.dirty]
```

Пример:

```text
0.1.0-local.20260727T181500Z+g1a2b3c4d5e6f.dirty
```

Правила:

- `BuildNumber` отсутствует;
- `IsLocalBuild = true`;
- working tree state фиксируется `Clean`, `Dirty` или `Unknown`;
- dirty/local artifact не публикуется как CI artifact, RC или Release;
- локальный username и absolute path не входят в BuildIdentity.

## 8.2 PullRequest

PR artifact:

```text
0.1.0-pr.<PullRequestNumber>.<RunAttempt>+g<ShortSha>
```

PR artifact:

- предназначен для review и tests;
- имеет ограниченный retention;
- не получает release tag;
- явно помечается `NonRelease`.

## 8.3 Development

Сборка защищённой `main` без release tag:

```text
0.1.0-dev.<GitHubRunId>.<RunAttempt>+g<ShortSha>
```

Она может использоваться для внутренних тестов, но не называется RC/Release.

## 8.4 ReleaseCandidate

RC:

```text
0.1.0-rc.N+g<ShortSha>
```

Требования:

- tag `v0.1.0-rc.N`;
- clean commit в `main`;
- все обязательные CI gates зелёные;
- `N` положительный integer и не переиспользуется;
- RC artifact неизменяем.

## 8.5 Release

Release display version:

```text
0.1.0
```

Release artifact всё равно содержит полный BuildIdentity с commit и CI run, даже если user-facing version короткая.

---

# 9. BuildIdentity contract

## 9.1 Обязательные поля

```text
BuildIdentity
├── IdentitySchemaVersion
├── ProductName
├── ApplicationVersion
├── DisplayVersion
├── BuildId
├── BuildChannel
├── BuildNumber?
├── BuildAttempt?
├── GitCommitSha
├── GitShortSha
├── GitRef
├── GitTag?
├── WorkingTreeState
├── BuildTimestampUtc
├── UnityVersion
├── DotNetSdkVersion?
├── BuildConfiguration
├── TargetPlatform
├── TargetArchitecture
├── ScriptingBackend
├── ApiCompatibilityLevel
├── DatabaseSchemaCurrent
├── DatabaseSchemaMinimumReadable
├── DatabaseSchemaMinimumMigratable
├── CampaignFormatCurrent
├── CampaignFormatMinimumReadable
├── ManifestSchemaCurrent
├── NetworkProtocolMinimum
├── NetworkProtocolMaximum
├── NetworkProtocolPreferred
├── AssetProtocolMinimum
├── AssetProtocolMaximum
├── AssetProtocolPreferred
├── AudioProtocolMinimum
├── AudioProtocolMaximum
├── AudioProtocolPreferred
├── ContractRegistryDigest
├── CompatibilityConfigDigest
└── IsReleaseArtifact
```

Дополнительные поля требуют повышения `IdentitySchemaVersion`, если старый reader не может безопасно их интерпретировать.

## 9.2 BuildId

### CI

```text
odyssey-<channel>-<GitHubRunId>.<RunAttempt>-g<ShortSha>
```

Пример:

```text
odyssey-dev-918273645.1-g1a2b3c4d5e6f
```

`GitHubRunId` используется как `BuildNumber` для CI artifact. `RunAttempt` различает повторный execution одного run.

### Local

```text
odyssey-local-<UTC>-g<ShortSha>[-dirty]
```

BuildId нормализуется в ASCII lowercase и разрешённый filename-safe набор символов.

## 9.3 GitCommitSha

- хранится полный 40-character SHA-1 либо полный hash выбранного Git object format;
- UI может показывать ShortSha минимум 12 символов;
- короткий hash не является единственным provenance key;
- commit должен существовать в repository history для published artifact.

## 9.4 GitRef

GitRef используется для диагностики, но не для trust decision.

Branch/tag name нормализуется и не используется напрямую как путь без sanitization.

## 9.5 WorkingTreeState

```text
Clean
Dirty
Unknown
```

Release/RC требует `Clean`.

## 9.6 Timestamp

`BuildTimestampUtc`:

- UTC;
- ISO 8601 round-trip;
- формируется build system;
- не используется для определения совместимости;
- не заменяет Git commit time.

---

# 10. Генерация и размещение BuildIdentity

## 10.1 Генерация

`scripts/generate-build-identity.ps1` получает только явные inputs:

- `version.json`;
- `config/compatibility.json`;
- Git state;
- CI environment allowlist;
- `ProjectVersion.txt`;
- pinned .NET SDK metadata;
- build configuration.

Скрипт:

1. валидирует inputs;
2. запрещает release при dirty/unknown source state;
3. вычисляет registry/config digests;
4. создаёт canonical BuildIdentity;
5. создаёт C# generated representation;
6. создаёт `build-identity.json`;
7. возвращает non-zero exit при любом расхождении.

## 10.2 Generated C#

Generated source:

```text
Assets/Odyssey/Generated/BuildIdentity.g.cs
```

Правила:

- directory gitignored, кроме README/placeholder при необходимости;
- generated file не редактируется вручную;
- generation выполняется до compile/build;
- значения compile-time constants или immutable generated data;
- production code читает build identity через typed interface, а не напрямую из environment variables.

## 10.3 Runtime JSON

В artifact включается:

```text
Odyssey_Data/StreamingAssets/Odyssey/build-identity.json
```

Точный Unity output path зависит от build, но logical location и content должны быть однозначны.

Generated C# и JSON обязаны описывать один BuildIdentity. Их canonical SHA-256 проверяется тестом/build step.

## 10.4 Неизменяемость после build

После создания artifact BuildIdentity не редактируется.

Если metadata неверна, выполняется новая сборка с новым BuildId. Существующий artifact не «исправляется» заменой файла внутри ZIP.

---

# 11. Compatibility version rules

## 11.1 Integer versions

Следующие versions:

```text
DatabaseSchemaVersion
CampaignFormatVersion
ManifestSchemaVersion
AssetManifestVersion
ContractVersion
FingerprintVersion
NetworkProtocolVersion
AssetProtocolVersion
AudioProtocolVersion
```

используют правила:

- первая версия — `1`;
- только положительный integer;
- увеличивается монотонно;
- значение не уменьшается;
- удалённый номер не переиспользуется;
- пропуск номера допускается только с документированным reserved reason;
- версия не выводится из ApplicationVersion;
- unknown required newer version не игнорируется молча.

## 11.2 DatabaseSchemaVersion

- `current` равен итоговой версии migration registry;
- каждая migration имеет стабильный MigrationId, from/to и checksum;
- write mode выполняется только на current schema после успешной migration;
- `minimumMigratable` определяет самую старую schema с полной поддерживаемой цепочкой;
- `minimumReadable` определяет самую старую schema, которую build способен безопасно прочитать;
- более новая schema не получает write access;
- downgrade не поддерживается в MVP.

## 11.3 CampaignFormatVersion

Версионирует внешний container/layout:

- обязательные paths;
- archive layout;
- placement manifest/database/assets/checksums;
- обязательные entry semantics.

Изменение содержимого SQLite без изменения container layout не требует CampaignFormatVersion bump.

## 11.4 ManifestSchemaVersion

Версионирует bootstrap/interchange JSON manifest отдельно от CampaignFormatVersion.

Optional backward-compatible field может не требовать bump только если это явно доказано schema contract и fixtures. По умолчанию изменение semantics требует новую version.

## 11.5 ContractVersion

Следует ADR-003:

```text
(ContractType, ContractVersion)
```

Версия локальна конкретному ContractType. Bump одного события не повышает все events.

## 11.6 FingerprintVersion

Bump требуется при изменении:

- canonical fingerprint material;
- property normalization;
- hash algorithm;
- включаемых semantic fields;
- правила исключения transport metadata.

Старые persisted commands продолжают проверяться своим FingerprintVersion.

## 11.7 ProtocolVersion

`NetworkProtocolVersion` версионирует wire-level обязательные semantics.

Хост объявляет:

```text
Minimum
Maximum
Preferred
```

Соединение допускается только при пересечении диапазонов и наличии required capabilities.

Compatible optional capability может быть добавлена без общего protocol bump, если:

- старый participant безопасно её не использует;
- capability negotiation однозначна;
- required behavior не меняется молча.

Breaking envelope/ordering/security semantics требует protocol bump.

## 11.8 RulesetVersion

Ruleset использует SemVer:

- PATCH — исправление/уточнение без несовместимого изменения сохранённых semantics;
- MINOR — новая backward-compatible механика/content rule;
- MAJOR — несовместимое изменение rules semantics или migration requirement.

Кампания сохраняет точную RulesetVersion. Обновление приложения не меняет её автоматически.

## 11.9 ContentPackageVersion

Content package использует SemVer. Dependency resolution может анализировать constraints в authoring/import workflow, но published campaign/runtime сохраняет точные resolved package versions.

---

# 12. Version bump matrix

| Изменение | Application | DB schema | Campaign format | Contract | Protocol | Ruleset/Content |
|---|---:|---:|---:|---:|---:|---:|
| Исправление UI defect | PATCH release line | — | — | — | — | — |
| Новый vertical slice | MINOR | при необходимости | при необходимости | при необходимости | при необходимости | при необходимости |
| Новый SQLite index/migration | PATCH или MINOR | +1 | — | — | — | — |
| Изменение `.odcamp` layout | PATCH или MINOR | возможно | +1 | manifest возможно +1 | — | — |
| Изменение payload event semantics | PATCH или MINOR | — | — | версия конкретного type +1 | только если wire требует | — |
| Новый fingerprint algorithm | PATCH или MINOR | — | — | — | — | FingerprintVersion +1 |
| Breaking network envelope | MINOR | — | — | transport payload возможно | +1 | — |
| Optional negotiated capability | PATCH/MINOR | — | — | возможно | не обязательно | — |
| Изменение игровой формулы | PATCH/MINOR приложения | — | — | trace/event возможно | — | Ruleset bump |
| Новая content package версия | не обязательно | — | — | — | — | ContentPackage bump |
| Docs-only изменение | — | — | — | — | — | — |
| Повторная CI-сборка того же commit | — | — | — | — | — | —; новый BuildId |

`+1` означает следующий неиспользованный integer, а не обязательное арифметическое увеличение при reserved gap.

---

# 13. Compatibility decisions at runtime

## 13.1 Открытие кампании

ApplicationVersion сравнивается для диагностики, но write/read решение использует:

- CampaignFormatVersion;
- ManifestSchemaVersion;
- DatabaseSchemaVersion;
- required features;
- contract registry support;
- RulesetId/RulesetVersion;
- content package availability.

Более старое приложение не делает вывод «несовместимо» только по `ApplicationVersionLastOpened`.

## 13.2 Network handshake

Handshake включает:

- ApplicationVersion;
- BuildId или diagnostic build reference;
- protocol support range;
- selected/preferred protocol;
- capabilities;
- presentation contract support;
- RulesetVersion metadata;
- campaign/schema metadata для diagnostics.

Admission определяется protocol/capabilities/security compatibility, а не равенством ApplicationVersion.

## 13.3 Unknown newer version

Общее правило:

- mutation запрещена;
- raw data не переписываются;
- safe metadata/read-only path допускается только при явном adapter;
- возвращается `Compatibility` Error из ADR-004;
- пользователь получает требуемое действие: update или manual recovery;
- диагностический отчёт содержит encountered/supported versions.

## 13.4 Старые версии

Поддержка старой версии существует только если:

- она входит в declared support range;
- есть migration/upcaster/adapter;
- compatibility tests проходят;
- security requirements не требуют немедленного отказа.

Наличие старого fixture само по себе не объявляет поддержку.

---

# 14. Git tags и release line

## 14.1 Release tag

Форма:

```text
v0.4.1
```

Условия:

- exact ApplicationVersion в tag равна `version.json`;
- commit принадлежит защищённой `main`;
- working tree CI clean;
- tag указывает на tested commit;
- owner approval получен;
- release gates зелёные;
- tag не существует ранее.

## 14.2 RC tag

Форма:

```text
v0.4.1-rc.1
```

RC counter уникален внутри planned release version.

## 14.3 Неизменяемость

Запрещено:

- force-push release tag;
- удалить и создать тот же tag на другом commit;
- заменить artifact под существующим release без нового version/build;
- использовать floating tag `latest` как normative identifier.

GitHub `Latest release` может быть UI-указателем, но не source of truth.

## 14.4 Release branch

Отдельная постоянная release branch не требуется на `SLICE-00`.

Hotfix/release branch допускается позже только с documented workflow. Tag остаётся единственным immutable release source reference.

---

# 15. Artifact naming и contents

## 15.1 Non-release artifact

```text
Odyssey-VTT_<DisplayVersion>_win-x64_<BuildId>.zip
```

Filename проходит sanitization и не содержит `/`, `\`, spaces from branch names или secrets.

## 15.2 Release artifact

Основное имя:

```text
Odyssey-VTT_0.4.1_win-x64.zip
```

Release page дополнительно хранит BuildId и commit в metadata. Даже кратко названный ZIP не теряет provenance.

## 15.3 Обязательное содержимое publication set

```text
Odyssey-VTT_<version>_win-x64.zip
build-identity.json
checksums.sha256
THIRD_PARTY_NOTICES.md
ReleaseQualityReport.md        # для RC/Release
```

## 15.4 Checksums

- SHA-256;
- lowercase hex;
- normalized filename;
- checksum file создаётся после final packaging;
- изменение ZIP требует новой checksum и нового BuildId;
- release publication проверяет checksum перед upload.

## 15.5 Retention

- PR artifacts — временные;
- dev artifacts — согласно CI retention policy;
- RC/Release artifacts — неизменяемые и сохраняются как release evidence;
- retention duration не является частью ApplicationVersion.

---

# 16. Runtime display и diagnostics

## 16.1 Обязательные места

Build identity доступна:

- в developer/status panel;
- в About/diagnostic view;
- в первой structured startup log record;
- в diagnostic bundle;
- в ReleaseQualityReport;
- в crash/unhandled exception report metadata;
- в host/client incompatibility report.

## 16.2 User-facing краткая форма

Обычный пользователь видит минимум:

```text
Odyssey VTT 0.4.1
```

Для non-release:

```text
Odyssey VTT 0.4.1-dev (1a2b3c4d5e6f)
```

Полный BuildId доступен по раскрытию/копированию.

## 16.3 Logs

Каждый log session включает BuildId один раз в обязательном session header. Не требуется повторять весь BuildIdentity в каждой записи.

CorrelationId/DiagnosticId из ADR-004 не заменяют BuildId.

## 16.4 Redaction

BuildIdentity не содержит:

- токены;
- secrets;
- локальный username;
- абсолютный repository path;
- email;
- machine serial;
- private documentation path;
- environment variable dump.

---

# 17. Assembly и package versions

## 17.1 AssemblyVersion

Для pre-1.0 Core assemblies допускается стабильная assembly compatibility version, определяемая build tooling. Она не используется UI и не заменяет ApplicationVersion.

Assembly file/informational version генерируется из BuildIdentity.

Рекомендуемое отображение:

```text
AssemblyVersion: 0.0.0.0 или контролируемая compatibility version
FileVersion: MAJOR.MINOR.PATCH.<bounded build component>
InformationalVersion: DisplayVersion + BuildId
```

Точная CLR packing implementation должна учитывать ограничения четырёх integer components и не обрезать GitHubRunId молча.

## 17.2 Unity packages

Внутренние embedded packages могут иметь package versions для UPM metadata, но они:

- не являются ApplicationVersion;
- не повышаются на каждый commit;
- синхронизируются только если package реально имеет independent distribution contract;
- до выделения package distribution могут оставаться согласованной internal line.

Нельзя использовать package.json version как единственный runtime build identity.

---

# 18. CI workflow

## 18.1 Pull request

PR pipeline:

1. проверяет `version.json` schema;
2. проверяет compatibility config;
3. проверяет migration/contract/protocol registries;
4. генерирует PullRequest BuildIdentity;
5. запускает compile/tests;
6. при необходимости создаёт non-release Windows artifact;
7. публикует BuildId в summary/evidence.

## 18.2 Main development

После merge в `main`:

- создаётся Development BuildIdentity;
- artifact не получает release status;
- результаты traceability связываются с BuildId.

## 18.3 RC/Release

Tag workflow дополнительно проверяет:

- tag format;
- tag/version equality;
- commit ancestry/branch policy;
- clean source;
- отсутствие запрещённых dependencies/secrets;
- required quality gates;
- checksum generation;
- ReleaseQualityReport;
- immutable publication.

## 18.4 Workflow rerun

Повтор workflow того же commit получает тот же GitHubRunId, но другой `RunAttempt`, следовательно другой BuildId.

Он не заменяет предыдущий artifact автоматически.

---

# 19. Локальная разработка

## 19.1 Editor identity

При запуске из Unity Editor используется Local BuildIdentity, сгенерированный bootstrap script/editor hook.

Если identity ещё не создана:

- Editor показывает `Build identity unavailable` как development diagnostic;
- production build запрещён;
- Core behavior не генерирует ложную release version.

## 19.2 Dirty state

Dirty state допустим для local testing и явно отображается.

Запрещено скрывать `.dirty` marker в diagnostic copy.

## 19.3 Offline build

Локальный build может выполняться offline при наличии pinned dependencies/cache, но остаётся Local channel и не становится Release.

---

# 20. Version change workflow

Version bump выполняется отдельным reviewable change:

1. определить тип bump;
2. изменить `version.json`;
3. обновить compatibility config только при реальной необходимости;
4. добавить migration/contract/protocol changes;
5. обновить compatibility matrix/release notes;
6. запустить version validation tests;
7. получить owner approval для release line/tag.

Codex обязан указать в PR summary:

```text
Application version impact: None | Patch | Minor | Major
Compatibility dimensions changed: [...]
Migration required: Yes | No
Tag requested: Yes | No
```

Если задача не разрешает version bump, Codex не меняет `version.json`.

---

# 21. Reproducibility и provenance

## 21.1 Требуемая воспроизводимость

Для MVP «воспроизводимая сборка» означает:

- известный commit;
- pinned Unity/.NET/dependencies;
- documented command;
- известная configuration;
- одинаковый source inventory;
- сохранённый BuildIdentity;
- возможность заново получить функционально эквивалентный artifact.

Bit-for-bit reproducibility не является обязательной на `SLICE-00`, потому что build timestamp/toolchain packaging могут менять bytes.

## 21.2 Rebuild

Повторная сборка того же commit:

- получает новый BuildId;
- не выдаётся за исходный проверенный artifact;
- проходит проверки заново;
- не заменяет release file без новой publication decision.

## 21.3 Provenance evidence

Для RC/Release сохраняются:

- BuildIdentity;
- workflow/run URL или stable run reference;
- commit SHA;
- tag;
- checksums;
- dependency lock/pins;
- ReleaseQualityReport;
- test evidence references.

Cryptographic artifact signing откладывается в Operations/Security ADR, но checksum/provenance обязательны уже сейчас.

---

# 22. Error model

Versioning failures используют ADR-004.

Примеры stable ErrorCode:

```text
Versioning.Application.Invalid
Versioning.Source.Mismatch
Versioning.Tag.Mismatch
Versioning.Build.DirtyReleaseForbidden
Versioning.Compatibility.InvalidRange
Versioning.Registry.Mismatch
Versioning.Artifact.IdentityMismatch
Versioning.Artifact.ChecksumMismatch
Versioning.Unsupported.IdentitySchema
```

SafeReasonCode не раскрывает internal paths или CI secrets.

Рекомендуемые RetryDirective:

- invalid source/config → `UserActionRequired`;
- temporary Git/CI metadata unavailable → `RetrySameRequest` либо `UserActionRequired` локально;
- unsupported newer schema → `UpgradeRequired`;
- checksum mismatch → `ManualRecoveryRequired`.

---

# 23. Обязательные тесты

Минимальные TestCaseId для `SLICE-00`:

```text
ADR007-T001 VersionJson_ValidInitialVersion_Loads
ADR007-T002 VersionJson_InvalidSemVer_IsRejected
ADR007-T003 VersionJson_UnknownSchema_IsRejected
ADR007-T004 CompatibilityConfig_InvalidRange_IsRejected
ADR007-T005 CompatibilityConfig_CurrentMatchesMigrationRegistry
ADR007-T006 CompatibilityConfig_ProtocolMatchesRegistry
ADR007-T007 BuildIdentity_SameInputs_IsCanonical
ADR007-T008 BuildIdentity_DifferentRunAttempt_HasDifferentBuildId
ADR007-T009 BuildIdentity_LocalDirty_IsMarked
ADR007-T010 ReleaseBuild_DirtySource_IsRejected
ADR007-T011 ReleaseBuild_TagMustMatchApplicationVersion
ADR007-T012 ReleaseBuild_UntaggedCommit_IsRejected
ADR007-T013 PrBuild_IsNeverReleaseArtifact
ADR007-T014 GeneratedCSharp_AndJson_AreEquivalent
ADR007-T015 BuildIdentity_DoesNotContainLocalPathOrSecret
ADR007-T016 ArtifactName_IsFilenameSafe
ADR007-T017 ArtifactChecksum_MatchesFinalPackage
ADR007-T018 DatabaseVersion_DoesNotFollowApplicationAutomatically
ADR007-T019 ContractVersion_IsIndependentFromProtocolVersion
ADR007-T020 NewerDatabaseSchema_BlocksWrite
ADR007-T021 ProtocolRange_NoIntersection_RejectsHandshake
ADR007-T022 RuntimeStatusPanel_ShowsBuildIdentity
ADR007-T023 StartupLog_ContainsBuildId
ADR007-T024 ReleaseQualityReport_ReferencesExactBuildId
ADR007-T025 DocumentationOnlyChange_DoesNotRequireVersionBump
```

Дополнительно обязательны golden/contract fixtures для:

- `version.json` schema v1;
- `compatibility.json` schema v1;
- `build-identity.json` schema v1;
- artifact filename normalization;
- tag parsing;
- error mappings.

---

# 24. CI gates

Pull request блокируется, если:

- ApplicationVersion невалидна;
- version source дублируется и расходится;
- compatibility range некорректен;
- current DB schema не соответствует migration registry;
- protocol preferred вне min/max;
- contract registry digest не формируется;
- generated C# и JSON BuildIdentity различаются;
- artifact не содержит BuildIdentity;
- BuildId неуникален для run attempt;
- release tag не совпадает с version;
- release source dirty/unknown;
- release artifact checksum неверна;
- non-release artifact ошибочно помечен Release;
- test evidence не указывает BuildId.

---

# 25. Реализация в `SLICE-00`

Обязательный минимальный scope:

1. `/version.json` с `0.1.0`;
2. `/config/compatibility.json` schema v1 с начальными integer versions `1`;
3. typed value objects/parsers для ApplicationVersion и compatibility versions;
4. BuildChannel и WorkingTreeState;
5. immutable BuildIdentity model;
6. `scripts/generate-build-identity.ps1`;
7. `scripts/verify-versioning.ps1`;
8. generated C# BuildIdentity;
9. runtime `build-identity.json`;
10. developer/status panel output;
11. startup structured log field;
12. artifact naming/checksum step;
13. pure .NET contract/architecture tests;
14. Unity EditMode parity test;
15. Windows dev-build smoke с читаемой BuildIdentity;
16. CI summary/evidence с BuildId.

Не входит в `SLICE-00`:

- auto-updater;
- patch distribution service;
- cryptographic code signing;
- public download channel;
- long-term support window;
- semantic dependency solver content packages;
- downgrade tool;
- release notes generator с внешним сервисом.

---

# 26. Правила для Codex

Codex обязан:

1. Использовать `version.json` как единственный ApplicationVersion source.
2. Не читать «последний tag» как текущую версию приложения.
3. Не менять version без явного scope.
4. Не повышать schema/protocol/ruleset вместе с app version автоматически.
5. Добавлять version bump только с соответствующими tests/migrations/fixtures.
6. Не создавать release artifact из PR/local branch.
7. Не скрывать dirty marker.
8. Не хардкодить version в UI, логах или network DTO.
9. Не использовать branch name как filename без sanitization.
10. Не включать secrets/environment dump в BuildIdentity.
11. Не заменять существующий artifact при workflow rerun.
12. Не обновлять golden identity fixture без объяснения semantic change.
13. Не использовать timestamp как единственный unique ID.
14. Не считать ApplicationVersion доказательством protocol compatibility.
15. Указывать version impact в каждом PR summary.

---

# 27. Критерии приёмки ADR-007 implementation

ADR считается реализованным, когда:

1. `version.json` и `compatibility.json` существуют и валидируются.
2. Начальная ApplicationVersion равна `0.1.0`.
3. Dev build получает уникальный BuildId.
4. Повтор run attempt получает другой BuildId.
5. Git commit и exact Unity version видны в BuildIdentity.
6. Generated runtime C# и JSON identity совпадают.
7. Build identity видна в клиенте и startup log.
8. Test/Release evidence ссылается на BuildId.
9. Release build из dirty/untagged source технически невозможен.
10. Tag/version mismatch блокирует workflow.
11. DB/format/protocol/contract/ruleset versions независимы.
12. Registry/config mismatch блокирует CI.
13. Artifact содержит checksums и identity.
14. Ни один active source не хранит вторую ручную version string.
15. Все обязательные ADR007 tests проходят в заявленных runners.

---

# 28. Последствия

## 28.1 Положительные

- Любой defect можно связать с точной сборкой и commit.
- PR/dev/RC/release artifacts невозможно спутать.
- Data/protocol/ruleset compatibility не зависит от маркетинговой версии приложения.
- Migration и handshake получают машинно проверяемые ranges.
- Codex не изобретает versioning в каждой подсистеме.
- Release evidence становится воспроизводимым и проверяемым.
- UI, logs и quality report используют одну identity.
- Повторная сборка не подменяет уже протестированный artifact.

## 28.2 Стоимость

- Нужны generation/validation scripts.
- Нужно поддерживать compatibility config и registries согласованно.
- Каждый format/protocol bump требует fixtures и matrix update.
- Release workflow становится строже.
- Локальные builds явно отличаются от CI builds.

Эта стоимость принимается, потому что ошибки version/compatibility в persistence и multiplayer значительно дороже.

---

# 29. Рассмотренные альтернативы

## 29.1 Один version number для всего

Отклонено: связывает несвязанные schema, protocol, ruleset и application changes.

## 29.2 Версия равна номеру commit/build

Отклонено: не выражает продуктовую release line и compatibility semantics.

## 29.3 Версия берётся из последнего Git tag

Отклонено: commits после tag получают ложную release identity; shallow clones и detached builds дают неоднозначность.

## 29.4 Calendar versioning

Отклонено на текущем этапе: дата не выражает compatibility и material capability. Может быть пересмотрено только отдельным ADR.

## 29.5 Автоматический bump на каждый merge

Отклонено: создаёт version noise, конфликты и не отражает owner release decision.

## 29.6 Ручное заполнение version во всех местах

Отклонено: неизбежный рассинхрон UI, manifest, artifact и report.

## 29.7 Один неизменяемый artifact при rerun

Отклонено: повторный build execution может отличаться toolchain/environment; он обязан иметь новую identity.

## 29.8 Только timestamp как BuildId

Отклонено: не связывает artifact с commit и может конфликтовать/быть неверным.

---

# 30. Отложенные решения

Отдельными ADR/Operations документами будут определены:

- cryptographic signing Windows executable/artifacts;
- update channels и auto-update;
- public release hosting;
- rollback/update installation model;
- long-term supported version window;
- minimum supported application version service-side;
- symbol server/crash upload;
- SBOM/provenance attestation format;
- package dependency solver;
- переход к 1.0 и post-1.0 compatibility policy.

До принятия этих решений ADR-007 остаётся достаточным для `SLICE-00`, CI artifacts и ручных RC/Release.

---

# 31. Трассировка

ADR реализует и уточняет:

- Technical Development Baseline, раздел 25;
- Roadmap Stage 1 Delivery и exit criteria версии сборки;
- Persistence independent campaign/schema/ruleset versions;
- Networking protocol range negotiation;
- ADR-003 independent serialization version dimensions;
- ADR-004 compatibility errors;
- ADR-006 BuildVersion/TestEvidence linkage;
- Test Strategy ReleaseQualityReport и traceability.

Связанные будущие задачи:

```text
SLICE00-VERSION-001 version.json and parser
SLICE00-VERSION-002 compatibility.json and registry parity
SLICE00-VERSION-003 BuildIdentity generator
SLICE00-VERSION-004 Unity runtime identity
SLICE00-VERSION-005 artifact/checksum pipeline
SLICE00-VERSION-006 CI tag/release guards
SLICE00-VERSION-007 versioning contract tests
```

---

# 32. Нормативное действие

С даты принятия:

- этот ADR имеет приоритет над предварительным versioning/build identity разделом Technical Development Baseline;
- новые runtime versions создаются только согласно независимым dimensions этого ADR;
- любой CI artifact обязан иметь BuildIdentity;
- первый репозиторий создаётся с `ApplicationVersion = 0.1.0` и compatibility versions `1`;
- merge первого CI artifact запрещён без versioning tests и runtime-visible BuildId;
- изменение принятой модели требует ADR-007 amendment или нового superseding ADR.

---

**Конец документа**
