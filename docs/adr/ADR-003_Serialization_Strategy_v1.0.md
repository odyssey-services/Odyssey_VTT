# ADR-003 — Serialization Strategy

**Документ:** `docs/adr/ADR-003_Serialization_Strategy_v1.0.md`  
**ADR:** ADR-003  
**Версия:** 1.0  
**Дата:** 27 июля 2026 года  
**Статус:** Accepted  
**Область:** JSON contracts, canonical serialization, command/event payloads, SQLite JSON fields, schema evolution, payload upcasting, `.odcamp` manifests, AOT/IL2CPP compatibility и boundary security  
**Связанные этапы:** Roadmap Stage 1, `SLICE-00`, Milestone `M1`  
**Базовые документы:** `TECHNICAL_DEVELOPMENT_BASELINE_Odyssey_VTT_v0.1.md`, `ADR-001_Module_Boundaries_and_Dependency_Direction_v1.0.md`, `ADR-002_Command_and_Domain_Event_Model_v1.0.md`

---

# 1. Решение

Odyssey VTT использует **несколько явных сериализационных профилей**, а не один универсальный формат для всех границ.

Главные правила:

1. Основной JSON serializer проекта — `System.Text.Json`.
2. Авторитетные доменные объекты не сериализуются напрямую между модулями, в SQLite, сеть или `.odcamp`.
3. Каждая внешняя или долговечная граница использует отдельный versioned contract DTO.
4. CLR type name, assembly-qualified name и runtime type discovery не являются частью контракта.
5. Полиморфизм разрешён только через явный stable discriminator и зарегистрированную версию payload.
6. Команды, события и долговечные JSON payload имеют стабильные `ContractType` и `ContractVersion`.
7. Авторитетный JSON кодируется в UTF-8 без BOM и имеет определённые правила имён, чисел, времени, ID, null и порядка полей.
8. Semantic command fingerprint из ADR-002 строится не из произвольного вывода общего serializer, а из отдельного versioned canonical fingerprint representation.
9. Immutable event payload сохраняется в исходном canonical JSON и не переписывается при чтении новой версией приложения.
10. Старый event payload преобразуется в текущую in-memory модель цепочкой чистых upcaster-ов.
11. Current-state tables и их JSON columns могут изменяться только явной database migration.
12. SQLite остаётся authoritative persistence текущего состояния; JSON не заменяет реляционную схему.
13. `.odcamp` является контейнером с versioned JSON manifests, SQLite backup и отдельными binary assets; binary assets не кодируются в base64 JSON.
14. Сетевой wire format не обязан быть JSON. Он использует отдельные transport DTO и protocol versioning; exact transport codec определяется сетевым ADR.
15. Release-compatible сериализация должна работать в Mono и IL2CPP x64 без runtime assembly scanning.
16. Для production contracts применяются source-generated `JsonSerializerContext` и явно зарегистрированные converters.
17. Reflection-only сериализация, unrestricted polymorphism и автоматическая сериализация arbitrary object graphs запрещены.
18. Любой неизвестный обязательный contract type/version блокирует mutation; система не «угадывает» формат.
19. Парсинг внешних JSON и архивов имеет ограничения размера, глубины, duplicate properties и path safety.
20. Golden fixtures, canonical hash fixtures, upcaster tests и IL2CPP smoke test являются обязательными для `SLICE-00`.

Этот ADR является нормативным authority по сериализации и эволюции JSON payload. Он уточняет Technical Development Baseline, ADR-002, Persistence и Networking contracts в пределах указанной области.

---

# 2. Контекст и проблема

Odyssey VTT должен сериализовать данные с разными сроками жизни и требованиями:

- Application commands;
- immutable DomainEvents;
- durable command results;
- SQLite JSON columns;
- current-state projections;
- CalculationTrace;
- Content Block definitions и snapshots;
- локальные конфигурации;
- `.odcamp` manifests;
- сетевые command/result/delta DTO;
- diagnostic records;
- test fixtures.

У этих данных разные требования:

- event payload должен оставаться читаемым спустя версии приложения;
- command fingerprint должен быть байтово стабилен;
- SQLite должна эффективно фильтровать ID, revision, status и sequence;
- сетевой пакет должен быть компактным и безопасным;
- `.odcamp` должен переноситься между машинами;
- диагностический JSON может быть человекочитаемым, но не является authority;
- Unity/IL2CPP ограничивает reflection и runtime type discovery;
- скрытые данные не должны случайно попадать в transport DTO;
- Codex не должен создавать новый serializer profile в каждой подсистеме.

Без единой стратегии возможны несовместимые решения:

- C# class name станет type discriminator и сломается после rename;
- сериализация Domain aggregate напрямую раскроет private fields;
- разные `JsonSerializerOptions` дадут разные command fingerprints;
- событие будет перезаписано новой формой payload, разрушив audit;
- JSON начнёт заменять foreign keys и revisions;
- старый payload будет молча прочитан с потерей неизвестных полей;
- неизвестный event type будет пропущен, а current state станет недостоверным;
- Mono-сборка пройдёт, а IL2CPP упадёт из-за reflection converter;
- binary asset попадёт в JSON как base64 и раздует память;
- network serializer начнёт принимать arbitrary CLR types;
- archive manifest допустит path traversal или decompression bomb;
- один и тот же decimal/float будет иметь разное текстовое представление;
- missing field и explicit `null` получат случайно одинаковый смысл.

Этот ADR устраняет такие неоднозначности до реализации Core primitives и Persistence adapters.

---

# 3. Движущие факторы

Решение оптимизировано под:

1. Долговечность событий и campaign data.
2. Stable command idempotency fingerprint.
3. Явную совместимость версий.
4. Host-authoritative security boundary.
5. Redaction до transport serialization.
6. SQLite как transactional source of truth.
7. AOT/IL2CPP compatibility.
8. Возможность тестировать Core без Unity Editor.
9. Читаемые и проверяемые manifests.
10. Отсутствие зависимости Domain от serializer annotations.
11. Возможность безопасной миграции без переписывания immutable history.
12. Однозначные правила для Codex.
13. Разделение persisted, transport, UI и diagnostic contracts.
14. Предсказуемое потребление памяти на больших картах и пакетах контента.
15. Возможность заменить transport codec без изменения DomainEvents.

---

# 4. Термины

## 4.1 Runtime model

C#-тип, используемый внутри конкретного модуля для вычислений и поведения.

Runtime model не считается автоматически сериализуемым контрактом.

## 4.2 Contract DTO

Неизменяемый тип данных, предназначенный для конкретной границы:

- persistence;
- transport;
- interchange;
- configuration;
- diagnostics;
- test fixture.

Contract DTO имеет явно определённую версию и ownership.

## 4.3 Payload contract

Versioned JSON-форма содержимого команды, события, trace или другого полиморфного поля.

## 4.4 Contract type

Стабильный строковый идентификатор, не зависящий от C# namespace/class name.

Примеры:

```text
board.token.move
board.token.moved
combat.attack.declare
combat.attack.resolved
content.block.damage
campaign.manifest
```

## 4.5 Contract version

Положительное целое число, описывающее JSON-схему конкретного `ContractType`.

## 4.6 Canonical JSON

Детерминированное UTF-8 представление, используемое там, где байты участвуют в fingerprint или integrity hash.

## 4.7 Upcaster

Чистое преобразование старого versioned payload в следующую поддерживаемую версию in-memory contract.

## 4.8 Migration

Явное изменение persisted current-state schema/data. Migration может переписывать current-state rows, но не immutable event history.

## 4.9 Wire format

Физическая кодировка сетевого transport message. Она не равна DomainEvent payload contract.

---

# 5. Категории сериализации

Проект использует следующие профили.

| Профиль | Назначение | Долговечность | Authority |
|---|---|---:|---|
| `AuthoritativePayloadJson` | Command/Event/CommandResult payload и integrity data | высокая | да |
| `PersistenceJson` | Разрешённые JSON columns в SQLite | высокая | часть authoritative persistence |
| `InterchangeJson` | `.odcamp` manifest, checksums, import/export reports | высокая | authority контейнера, не gameplay state |
| `ConfigurationJson` | локальные настройки и build/runtime configuration | средняя | нет для gameplay state |
| `TransportContract` | network command/result/snapshot/delta DTO | session-scoped | host publication authority |
| `DiagnosticJson` | логи, traces, crash evidence | ограниченная | нет |
| `FixtureJson` | golden compatibility fixtures | постоянная в репозитории | тестовый authority |

Один DTO не должен автоматически использоваться во всех профилях.

Допустимое исключение требует явного доказательства полной семантической идентичности и review.

---

# 6. Ownership по модулям

## 6.1 Odyssey.Domain

Domain владеет:

- domain semantics;
- stable event meaning;
- value object invariants;
- contract-neutral state transitions.

Domain не содержит:

- `[JsonPropertyName]`;
- serializer-specific converters;
- SQLite annotations;
- network discriminators;
- `.odcamp` manifest models;
- generic `object` payloads.

## 6.2 Odyssey.Rules

Rules возвращает типизированные calculation results и traces.

Сериализуемая форма CalculationTrace определяется отдельным contract DTO вне pure calculation logic.

## 6.3 Odyssey.Content

Content владеет stable identifiers и versioned schemas Content Block definitions.

Content runtime executor не принимает arbitrary CLR type metadata из JSON.

## 6.4 Odyssey.Application

Application владеет:

- command contract registry;
- command payload DTO;
- command result DTO;
- mapping transport input → Application command;
- semantic fingerprint material;
- orchestration payload version validation;
- ports для serialization/upcasting.

## 6.5 Odyssey.Persistence

Persistence владеет:

- SQLite row DTO/mapping;
- persisted JSON representation;
- event payload storage;
- payload hashes;
- database schema migrations;
- stored contract version columns;
- reading raw payload и вызовом registered upcaster chain.

Persistence не меняет domain meaning payload.

## 6.6 Odyssey.Networking

Networking владеет:

- transport envelopes;
- protocol DTO;
- wire codec;
- frame size limits;
- compression policy;
- audience-specific DTO после redaction.

Networking не сериализует raw Domain aggregate или raw full DomainEvent для клиента.

## 6.7 Odyssey.Unity.Client

Unity Client владеет:

- UI view models;
- local settings DTO;
- editor/runtime configuration adapters;
- composition of generated serializer contexts.

UI Toolkit state не является persisted domain contract.

---

# 7. Запрет прямой сериализации доменных объектов

## 7.1 Основное правило

Aggregate, entity и rich domain object не передаются через serializer напрямую.

Перед boundary выполняется mapping:

```text
Domain/Application model
→ explicit contract DTO
→ serializer profile
→ bytes/text/storage
```

При чтении:

```text
bytes/text/storage
→ validated contract DTO
→ upcast/mapping
→ runtime model
```

## 7.2 Причины

Прямая сериализация запрещена, потому что она:

- связывает внутренний refactor с persisted contract;
- может включить private/internal state;
- создаёт циклические object graphs;
- затрудняет AOT;
- делает versioning неявным;
- смешивает persistence и transport;
- позволяет UI fields попасть в authoritative payload;
- затрудняет redaction.

## 7.3 Допустимые простые value types

Простые immutable value DTO могут совпадать с runtime value object только если:

- тип не зависит от Unity;
- публичная форма является стабильным контрактом;
- versioning определено;
- нет скрытых полей;
- есть round-trip fixture;
- архитектурный тест разрешает ссылку.

Совпадение должно быть осознанным, а не результатом generic serialization.

---

# 8. Базовый JSON профиль

## 8.1 Кодировка

Все JSON-файлы и JSON payload проекта используют:

```text
Encoding: UTF-8
BOM: prohibited
Newline in canonical payload: none
Newline in human-readable files: LF
```

Импорт может принимать UTF-8 BOM только как compatibility concession для вручную созданных configuration/interchange файлов. После нормализации BOM не сохраняется.

Авторитетные payload с BOM отклоняются либо нормализуются до admission согласно boundary contract; raw bytes с BOM никогда не участвуют в canonical hash.

## 8.2 Имена свойств

Базовый стиль JSON:

```text
lowerCamelCase
```

Пример:

```json
{
  "contractType": "board.token.move",
  "contractVersion": 1,
  "campaignId": "019b2fa0-61d8-7f90-8b20-0cd3b2af6001"
}
```

Имена свойств являются частью контракта и не меняются вслед за rename C# property.

Для production contract каждое имя задаётся source-generated metadata и/или явным `[JsonPropertyName]` на contract DTO, но не на Domain type.

## 8.3 Регистр contract type

`ContractType` использует:

- ASCII lowercase;
- сегменты через точку;
- цифры допускаются не первым символом сегмента;
- без пробелов, underscore и CLR namespace;
- длина не более 128 символов.

Pattern:

```text
[a-z][a-z0-9]*(\.[a-z][a-z0-9]*)+
```

## 8.4 Property order

Для обычного чтения JSON порядок свойств не имеет значения.

Для canonical JSON порядок фиксирован explicit canonical writer-ом. Нельзя полагаться только на порядок declaration в C# или текущую реализацию `System.Text.Json`.

## 8.5 Whitespace

- canonical JSON не содержит незначащих пробелов и переносов;
- human-readable manifests могут использовать indentation;
- hash authoritative payload вычисляется только по canonical bytes, не по pretty-printed файлу.

## 8.6 Duplicate properties

Внешний JSON с duplicate property names отклоняется.

Пример запрещён:

```json
{
  "campaignId": "A",
  "campaignId": "B"
}
```

Parser не применяет правило «последнее значение побеждает» для security-sensitive contracts.

## 8.7 Trailing commas и comments

Для authoritative, interchange и transport JSON:

- trailing commas запрещены;
- comments запрещены.

Для developer-only configuration допускается отдельный relaxed reader, но результат нормализуется и такой reader не используется для campaign/event/network data.

## 8.8 Maximum depth

Default maximum depth production JSON — 64.

Более высокий лимит допускается только для конкретного Content/Trace contract после:

- отдельного размера;
- recursion limit;
- load test;
- memory test;
- explicit configuration.

## 8.9 Размеры

Каждая boundary определяет maximum payload size.

Отсутствие лимита считается дефектом.

Общие initial ceilings для `SLICE-00` test harness:

```text
Command payload: 256 KiB
Single event payload: 1 MiB
Manifest JSON: 4 MiB
Diagnostic JSON record: 1 MiB
```

Эти значения не являются финальными product limits и могут быть снижены специализированным контрактом. Увеличение требует измерения и ADR/task evidence.

---

# 9. Представление примитивов

## 9.1 Boolean

Только JSON `true`/`false`.

Строки `"true"`, `"false"`, `0`, `1` не принимаются как boolean в authoritative contracts.

## 9.2 Целые числа

Discrete domain quantities используют целочисленные JSON numbers:

- revisions;
- sequences;
- levels;
- action counts;
- ammunition;
- resource points;
- grid indices.

Число вне диапазона contract type отклоняется до domain handling.

## 9.3 Decimal

Точные нецелые domain values используют `decimal` и JSON number с invariant culture.

Запрещено сериализовать точное значение как locale-dependent string.

Contract обязан определить scale/range, если они значимы.

## 9.4 Floating point

`float`/`double` допускаются для:

- visual coordinates;
- camera/UI values;
- non-authoritative diagnostics;
- measured performance values.

Для authoritative spatial values contract обязан определить:

- допустимую точность;
- normalisation;
- range;
- meaning координат.

`NaN`, `Infinity`, `-Infinity` запрещены.

`-0` canonical writer нормализует в `0`.

## 9.5 Enums

Persisted и transport enum сериализуется stable string token, а не numeric ordinal и не автоматически через `Enum.ToString()`.

Пример:

```json
{ "status": "pending" }
```

Каждый token явно маппится converter-ом. Rename C# enum member не меняет contract token.

Unknown enum token:

- отклоняется для authoritative mutation;
- может отображаться как `Unknown` только в diagnostic/read-only projection, если это явно разрешено contract;
- не преобразуется молча в default enum value.

## 9.6 Typed IDs

Typed ID сериализуется как canonical string.

Для UUID-compatible ID используется lowercase hyphenated `D` form:

```text
019b2fa0-61d8-7f90-8b20-0cd3b2af6001
```

Правила:

- uppercase input может быть принят на external boundary и нормализован до lowercase;
- braces и compact `N` form не выводятся;
- empty/default ID запрещён там, где поле required;
- конкретный generation algorithm определяется identity decision, но не меняет JSON representation без новой contract version.

## 9.7 Date и time

Авторитетный абсолютный timestamp использует UTC `DateTimeOffset`, нормализованный в RFC 3339 / round-trip form с `Z`.

Пример:

```text
2026-07-27T10:30:45.1234567Z
```

Правила:

- local timezone offset при записи нормализуется в UTC;
- `DateTimeKind.Unspecified` запрещён на authority boundaries;
- client timestamp не определяет authoritative order;
- event order определяется sequence из ADR-002;
- durations сериализуются как целое количество миллисекунд либо как отдельная domain unit, но не как culture-dependent `TimeSpan.ToString()`;
- monotonic runtime time не сериализуется как wall clock.

## 9.8 Strings

Строки:

- остаются Unicode;
- проходят contract-specific length validation;
- не нормализуются глобально без domain requirement;
- control characters, запрещённые boundary, отклоняются;
- user text не используется как type discriminator, file path или SQL fragment.

## 9.9 Binary data

Binary data не помещается в JSON как base64, кроме небольших cryptographic values, если конкретный security contract это явно требует.

Карты, изображения, аудио, SQLite database и большие blobs хранятся отдельными файлами/streams с content hash и metadata reference.

---

# 10. Null, missing и default semantics

## 10.1 Authoritative payload

Для command/event/result canonical payload:

- required property всегда присутствует;
- optional property имеет явно определённую семантику;
- `missing` и `null` не считаются автоматически одинаковыми;
- default CLR value не подставляется молча при отсутствии required property;
- unknown property не превращается в domain field.

## 10.2 Canonical output

Canonical authoritative writer выводит все поля версии контракта в фиксированном порядке.

Optional nullable property выводится как `null`, если версия контракта включает поле и значение отсутствует.

Это правило предотвращает изменение fingerprint из-за разных global ignore-null settings.

## 10.3 Human-readable configuration

Configuration profile может опускать необязательные `null` и default values, если loader имеет документированные defaults.

Configuration defaults не используются для immutable event interpretation.

---

# 11. Contract envelope

## 11.1 Payload descriptor

Каждый полиморфный долговечный payload сопровождается:

```text
ContractType
ContractVersion
PayloadJson
```

В JSON wrapper допускается форма:

```json
{
  "contractType": "board.token.moved",
  "contractVersion": 1,
  "payload": {
    "tokenId": "019b2fa0-61d8-7f90-8b20-0cd3b2af6002",
    "fromX": 10,
    "fromY": 4,
    "toX": 11,
    "toY": 4
  }
}
```

В SQLite `EventType` и `EventContractVersion` остаются отдельными typed columns, а `PayloadJson` содержит только payload object без дублирования envelope, если это определено schema.

## 11.2 Stable type identity

Contract identity не включает:

- C# namespace;
- class name;
- assembly name;
- generic type name;
- package version;
- Unity GUID;
- database table name.

## 11.3 Version range

`ContractVersion`:

- начинается с `1`;
- увеличивается при изменении JSON semantics;
- не уменьшается;
- не переиспользуется;
- не равен application version;
- не равен database schema version;
- не равен protocol version.

---

# 12. Полиморфизм и registry

## 12.1 Explicit registry

Каждый модуль, владеющий contracts, предоставляет compile-time registry:

```text
(ContractType, ContractVersion)
→ DTO type
→ JsonTypeInfo
→ validator
→ mapper/upcaster
```

Registry собирается composition root-ом.

## 12.2 Запрещённые механизмы

Запрещены:

- `Type.GetType()` по входному JSON;
- assembly-qualified names;
- unrestricted `object` deserialization;
- runtime scan всех assemblies для поиска converters;
- arbitrary constructor activation;
- Newtonsoft `TypeNameHandling` или эквивалент;
- fallback «неизвестный type → Dictionary<string, object> → попытка выполнить».

## 12.3 Unknown contract

Если mutation требует неизвестный type/version:

- command не допускается к domain handling;
- campaign с обязательным неизвестным persisted payload не получает write access;
- import не завершается;
- создаётся stable compatibility error;
- raw payload сохраняется для diagnostic/export, если boundary безопасна;
- система не удаляет и не преобразует неизвестные данные.

Read-only diagnostic mode может показывать metadata без выполнения payload.

---

# 13. System.Text.Json configuration

## 13.1 Central options

Проект не создаёт произвольные `new JsonSerializerOptions()` в feature code.

Каждый profile имеет централизованную factory/immutable options instance.

Минимальные profiles:

```text
AuthoritativeJsonOptions
InterchangeJsonOptions
ConfigurationJsonOptions
DiagnosticJsonOptions
```

Transport codec определяет собственную configuration в Networking.

## 13.2 Source generation

Для production root contracts используются source-generated contexts.

Правила:

- context расположен в owning module/adapter;
- root types перечислены явно;
- converters зарегистрированы явно;
- generated metadata проверяется в Core tests;
- reflection fallback выключается для release-critical path;
- новый contract type без source-generation registration блокирует CI.

## 13.3 Converters

Custom converter допускается только если:

- формат стабилен и документирован;
- converter не выполняет I/O;
- converter детерминирован;
- converter имеет positive/negative/golden tests;
- converter совместим с IL2CPP;
- converter не принимает arbitrary CLR type metadata.

## 13.4 Options immutability

После composition serializer options считаются immutable.

Feature code не может временно менять:

- naming policy;
- enum converter;
- number handling;
- ignore-null policy;
- max depth;
- reference handling.

## 13.5 Reference handling

Object reference preservation (`$id`, `$ref`) запрещён для authoritative contracts.

Contract DTO должен быть деревом без циклов.

---

# 14. Canonical JSON

## 14.1 Где требуется canonical form

Canonical JSON обязателен для:

- semantic command fingerprint material;
- immutable event `PayloadHash`;
- checksums manifest entries, если hash относится к JSON content;
- golden canonical fixtures;
- signed data в будущем security contract.

## 14.2 Canonical writer

Canonical representation создаёт отдельный project-owned writer поверх validated contract DTO.

Он обеспечивает:

1. UTF-8 без BOM;
2. без whitespace;
3. фиксированный порядок полей контракта;
4. stable enum tokens;
5. lowercase canonical IDs;
6. UTC timestamp form;
7. invariant numeric formatting;
8. `-0` → `0`;
9. запрет NaN/Infinity;
10. явный `null` для optional fields authoritative version;
11. deterministic escaping;
12. отсутствие duplicate properties.

Generic dictionary не используется в fingerprint material, если key ordering не нормализован.

## 14.3 Hash algorithm

Initial integrity/fingerprint hash — SHA-256.

Вывод:

```text
lowercase hexadecimal, 64 characters
```

Hash algorithm identity хранится рядом с hash там, где формат должен допускать будущую замену.

## 14.4 Canonical fixtures

Каждый critical contract имеет fixture:

```text
input semantic values
expected canonical JSON
expected SHA-256
```

Изменение fixture означает изменение контракта и требует review/version decision.

---

# 15. Semantic command fingerprint

## 15.1 Отдельное представление

Fingerprint из ADR-002 не вычисляется путём сериализации полного `ApplicationCommand` object graph.

Для него строится `CommandFingerprintMaterialV1` с фиксированными полями:

```text
fingerprintVersion
commandType
commandVersion
campaignId
sessionId?
issuerKind
actorUserId?
actorCharacterId?
rootCommandId
parentCommandId?
correlationId
expectedCampaignRevision?
expectedSessionSequence?
expectedAggregateRevisions[]
payloadContractType
payloadContractVersion
canonicalPayload
```

`CommandId` не входит в semantic fingerprint, потому что он является ключом поиска receipt, а fingerprint проверяет неизменность значения под этим ключом.

Transport-only metadata не входит:

- connection ID;
- retry counter;
- frame ID;
- received packet timestamp;
- compression flag;
- relay route.

## 15.2 Aggregate revision order

`expectedAggregateRevisions[]` сортируется canonical образом по:

```text
aggregateType
aggregateId
```

Порядок входного массива клиента не влияет на fingerprint, если contract semantics считают набор неупорядоченным.

## 15.3 Fingerprint version

Алгоритм fingerprint имеет собственную `FingerprintVersion`.

Изменение canonical rules не пересчитывает старые receipts автоматически.

Persistence хранит:

```text
FingerprintAlgorithm
FingerprintVersion
CommandFingerprint
```

Retry старой receipt проверяется алгоритмом её версии.

---

# 16. Command serialization

## 16.1 Admission

Transport DTO сначала проходит:

1. frame/protocol validation;
2. size/depth validation;
3. JSON/binary decode;
4. envelope schema validation;
5. contract registry lookup;
6. payload DTO validation;
7. identity/session binding;
8. mapping в immutable Application command;
9. semantic fingerprint calculation.

Только после этого команда считается admitted по ADR-002.

## 16.2 Unknown fields

Backward-compatible optional additions допускаются только при contract policy, где старый reader может безопасно игнорировать неизвестное поле.

Для mutation нельзя полагаться только на «System.Text.Json игнорирует unknown properties».

Совместимость определяется парой `(CommandType, CommandVersion)`, а validator явно знает допустимую schema.

## 16.3 Command persistence

`AppliedCommands` хранит:

- command identity metadata;
- fingerprint metadata;
- terminal result contract type/version;
- canonical result payload либо нормализованные result columns;
- timestamps/revisions согласно ADR-002.

Полный transport frame не является durable command authority.

## 16.4 Rejected payload

Security-sensitive invalid raw payload не сохраняется без ограничения.

Diagnostics могут хранить:

- hash;
- size;
- safe type metadata;
- stable error code;
- redacted excerpt при явной policy.

Секреты и приватные поля не дублируются в лог.

---

# 17. DomainEvent serialization

## 17.1 Immutable payload

После commit сохраняются:

```text
EventType
EventContractVersion
PayloadJson
PayloadHash
```

`PayloadJson` — canonical JSON object текущей event contract version на момент создания.

## 17.2 Неизменность stored bytes/meaning

Существующая event row не переписывается потому, что:

- C# class переименован;
- появился новый optional field;
- current runtime использует новую DTO;
- serializer library обновилась;
- pretty-print policy изменилась.

Integrity verification повторно canonical-читает raw JSON только по правилам сохранённой contract/fingerprint version.

## 17.3 Event upcasting

При чтении старого события:

```text
Raw payload v1
→ validate v1
→ Upcast v1→v2
→ validate v2
→ ...
→ current in-memory event DTO
→ Domain/Application mapping
```

Upcaster:

- pure;
- deterministic;
- не выполняет I/O;
- не читает current campaign state;
- не вызывает Clock/RNG;
- не меняет EventId/sequence/causality;
- не переписывает исходную row;
- имеет fixture tests.

## 17.4 Missing upcaster

Если цепочка отсутствует:

- event не применяется как current known event;
- write access campaign блокируется, если event необходим для корректности;
- доступен safe compatibility report;
- raw payload остаётся сохранённым;
- автоматическое удаление/skip запрещено.

## 17.5 Breaking semantic change

Если событие изменило смысл, создаётся новый `EventType` либо новая breaking contract version с явным upcaster policy.

Нельзя дать старому type/version новый смысл.

---

# 18. CommandResult serialization

`CommandResult` имеет отдельный stable contract.

Envelope включает минимум:

```text
commandId
status
resultType
resultVersion
campaignRevision?
sessionSequence?
eventSequenceRange?
errorCode?
payload?
```

Правила:

- `Accepted`, `Pending`, `Rejected` соответствуют ADR-002;
- retry возвращает совместимый сохранённый result;
- client-safe result отделён от internal receipt details;
- internal fingerprint и hidden event metadata не сериализуются клиенту;
- точный Result/Error vocabulary определяется ADR-004.

---

# 19. SQLite serialization strategy

## 19.1 Гибридная схема

SQLite использует:

- typed relational columns для identity, keys, revisions, sequence, status, timestamps, visibility и query fields;
- JSON только для разрешённых полиморфных/сложных payload;
- foreign keys и constraints вместо ссылок внутри opaque JSON;
- explicit schema migrations.

## 19.2 Разрешённые JSON columns

Согласно Persistence contract, допустимы:

- immutable event payload;
- CalculationTrace tree;
- complex mechanics snapshot;
- Content Block payload;
- migration details;
- diagnostic technical payload.

Каждая JSON column имеет:

- owning contract;
- contract type/version columns либо schema binding;
- size limit;
- validation;
- migration/upcast policy;
- tests.

## 19.3 Запрещённые применения

JSON не заменяет:

- CampaignId/EntityId;
- aggregate revision;
- EventSequence;
- lifecycle status;
- ownership/membership keys;
- visibility class;
- searchable/filterable timestamps;
- referential integrity;
- uniqueness constraints;
- NetworkOutbox delivery status.

## 19.4 Domain isolation

Domain assemblies не имеют ORM/SQLite/JSON persistence annotations.

Mapping расположен в Persistence adapter.

## 19.5 Payload hash

Для event payload:

1. contract DTO валидируется;
2. canonical UTF-8 payload создаётся;
3. SHA-256 вычисляется по canonical bytes;
4. exact canonical JSON text сохраняется в `PayloadJson`;
5. lowercase hash сохраняется в `PayloadHash`;
6. verification повторно кодирует сохранённый text в UTF-8 и сравнивает hash по сохранённой algorithm/version policy.

## 19.6 JSON query

SQLite JSON functions могут использоваться для diagnostics или non-critical projections.

Gameplay correctness не должна зависеть от хрупкого path query внутри unversioned JSON.

---

# 20. Current-state migrations

## 20.1 Отличие от event upcasting

- Event upcasting преобразует raw immutable payload только in-memory.
- Database migration изменяет current-state schema/data и записывает новую schema version.

Эти процессы не взаимозаменяемы.

## 20.2 Migration policy

Migration текущих JSON columns:

- имеет ID и checksum;
- выполняется над temp copy/pre-migration backup согласно Persistence contract;
- идёт последовательно;
- валидирует каждую преобразованную row;
- не переписывает immutable DomainEvents;
- фиксирует report и failure reason;
- не заменяет рабочую DB при ошибке.

## 20.3 Ruleset/content migration

Изменение RulesetVersion или Content definition не считается автоматически JSON/database migration.

Оно использует отдельный domain workflow с preview, backup и events.

---

# 21. `.odcamp` interchange format

## 21.1 Логическая структура

```text
Campaign.odcamp
├── manifest.json
├── campaign.db
├── Assets/
├── checksums.json
└── export-manifest.json
```

Физический archive codec определяется Persistence implementation decision, но logical paths и manifests являются стабильными contracts.

## 21.2 Manifest

`manifest.json` содержит только bootstrap metadata, необходимую до открытия DB:

```text
manifestType
manifestVersion
campaignId
campaignDisplayName
createdAt
exportedAt
applicationVersion
minimumReaderVersion?
databaseSchemaVersion
rulesetId
rulesetVersion
assetManifestRevision
requiredFeatures[]
```

Точный набор уточняется `.odcamp` schema, но:

- manifest не дублирует всю campaign state;
- secrets/tokens отсутствуют;
- абсолютные исходные пути отсутствуют;
- property names и versions следуют этому ADR.

## 21.3 Checksums

`checksums.json` использует:

- normalized relative paths с `/`;
- SHA-256 lowercase hex;
- размер файла;
- algorithm identity;
- manifest version.

Path normalization выполняется до checksum lookup.

## 21.4 Binary assets

Assets копируются как отдельные files/streams.

JSON содержит metadata reference и content hash, но не base64 file contents.

## 21.5 Import validation

До открытия campaign:

1. archive size и entry count проверяются;
2. каждый path нормализуется;
3. absolute path, `..`, drive prefix и symlink escape запрещаются;
4. duplicate normalized paths запрещаются;
5. manifest parse/schema/version проверяется;
6. checksums проверяются;
7. SQLite integrity/schema проверяется;
8. import выполняется в staging;
9. только успешный staging публикуется в target folder.

## 21.6 Unknown manifest version

Если manifest version новее поддерживаемой:

- import mutation блокируется;
- пользователь получает compatibility report;
- архив не модифицируется;
- приложение не пытается открыть `campaign.db` write mode.

---

# 22. Configuration serialization

## 22.1 Scope

Configuration JSON используется для:

- локальных пользовательских настроек;
- quality profile overrides;
- developer/test settings;
- non-secret build metadata;
- feature configuration, если она разрешена build contract.

## 22.2 Неавторитетность

Configuration не может напрямую задавать:

- роль пользователя;
- campaign ownership;
- character stats;
- result броска;
- permissions;
- hidden visibility;
- current authoritative revision.

## 22.3 Secrets

Secrets не хранятся в committed JSON.

Local secret storage определяется Security ADR. В public repository допускаются только placeholders/examples.

## 22.4 Corrupt settings

Повреждённая пользовательская configuration:

- сохраняется/переименовывается для diagnostics;
- заменяется безопасными defaults;
- не повреждает campaign database;
- не приводит к silent enabling опасной функции.

---

# 23. Transport serialization boundary

## 23.1 Отдельный contract

Networking не отправляет:

- Domain aggregate;
- Persistence row DTO;
- raw full DomainEvent;
- SQLite JSON payload без redaction;
- UI view model как command.

Используется pipeline:

```text
Authoritative state/events
→ permission/redaction projection
→ transport DTO
→ wire codec
```

## 23.2 JSON и wire codec

На `SLICE-00` JSON может использоваться для local adapter, fixtures и debug transport prototype.

Production wire codec может быть JSON или binary. Выбор не меняет:

- Application command type/version;
- DomainEvent type/version;
- redaction rules;
- command idempotency;
- persisted payload.

## 23.3 Protocol version

Protocol version отделён от payload contract version.

Один protocol может переносить несколько command/event projection versions.

Unknown mandatory protocol/message version отклоняется до Application admission.

## 23.4 Compression

Compression применяется только на framed transport layer после size validation policy.

Decompressed size имеет отдельный limit. Compression не разрешает обход payload ceiling.

---

# 24. Redaction и serialization

Redaction выполняется **до** transport serialization.

Запрещено:

- сериализовать полный object и удалить поля после создания bytes;
- передавать hidden object с `visible=false`;
- сохранять full private payload в client cache contract;
- включать secret EventId/EntityId в generic metadata, если policy требует safe substitute;
- логировать unredacted transport DTO на клиенте.

Audience projection имеет собственный contract type/version и fixture tests.

---

# 25. Security limits

## 25.1 Parser limits

Каждый external parser имеет:

- maximum bytes;
- maximum depth;
- maximum string length;
- maximum array count;
- maximum object property count;
- duplicate property rejection;
- cancellation/timeout policy;
- allocation monitoring для large inputs.

## 25.2 Validation order

Сначала выполняется дешёвая structural validation, затем expensive semantic validation.

Не допускается создавать большие domain graphs до проверки лимитов.

## 25.3 Deserialization side effects

Constructor/converter/deserializer не выполняет:

- file I/O;
- network I/O;
- database writes;
- service resolution;
- command execution;
- logging raw secrets;
- Clock/RNG calls.

## 25.4 Untrusted JSON

Любой JSON из:

- network;
- imported package;
- `.odcamp`;
- user-selected file;
- clipboard;
- mod/content package

считается недоверенным до полной валидации.

---

# 26. AOT и IL2CPP

## 26.1 Requirement

Работа сериализации только в Mono не считается готовностью.

Release-critical contracts должны проходить IL2CPP x64 smoke tests.

## 26.2 Запрещённые assumptions

Нельзя полагаться на:

- runtime code generation;
- dynamic assembly emit;
- unbounded reflection discovery;
- generic type creation только по входному type string;
- private member reflection без explicit preservation;
- editor-only metadata.

## 26.3 Source-generated contexts

В `SLICE-00` создаются минимум:

```text
ApplicationJsonContext
DomainEventJsonContext
InterchangeJsonContext
TestFixtureJsonContext
```

Фактическое размещение следует ADR-001 ownership.

## 26.4 Linker preservation

Если converter/type требует linker preservation, это:

- описывается в коде/metadata;
- тестируется IL2CPP build;
- не заменяет source-generation registration;
- не оформляется широким `preserve all` без обоснования.

---

# 27. Версии и совместимость

## 27.1 Независимые version dimensions

Odyssey различает:

```text
ApplicationVersion
BuildVersion
DatabaseSchemaVersion
ManifestVersion
ContractType + ContractVersion
FingerprintVersion
ProtocolVersion
RulesetVersion
ContentPackageVersion
```

Изменение одного измерения не увеличивает автоматически остальные.

Полная политика поддержки версий определяется ADR-007.

## 27.2 Backward-compatible addition

Добавление optional field может оставаться той же major contract semantics только если:

- старый reader безопасно игнорирует поле;
- отсутствие имеет определённый default/meaning;
- canonical representation старой версии не изменяется;
- старые fixtures проходят;
- mutation не становится двусмысленной.

Практически persisted commands/events используют новую integer `ContractVersion`, если изменение влияет на payload bytes или interpretation.

## 27.3 Breaking change

Breaking change требует:

- новой contract version;
- upcaster либо explicit unsupported decision;
- fixtures обеих версий;
- compatibility matrix update;
- migration, если current-state data переписывается.

## 27.4 Downgrade

Автоматический downcast authoritative payload не поддерживается.

Старое приложение не получает write access к campaign/package с неизвестной обязательной version.

---

# 28. Diagnostics и logs

## 28.1 Diagnostic serialization

Diagnostic JSON может быть более гибким, но:

- имеет отдельный profile;
- не используется для replay или command fingerprint;
- не становится persistence authority;
- проходит redaction;
- имеет size limits;
- не содержит raw secrets.

## 28.2 Raw payload logging

По умолчанию raw command/event/network payload не логируется.

Разрешены:

- type/version;
- IDs, если policy допускает;
- size;
- hash;
- stable error code;
- safe structured fields.

Developer-only full payload logging требует явного opt-in и запрещено для production release.

---

# 29. Error handling

Serialization boundary возвращает structured result, а не бросает необработанное исключение в UI/network loop.

Минимальные категории до ADR-004:

```text
UnsupportedContractType
UnsupportedContractVersion
MalformedJson
DuplicateJsonProperty
PayloadTooLarge
DepthLimitExceeded
InvalidContractField
CanonicalizationFailed
PayloadHashMismatch
UpcastPathMissing
ManifestIncompatible
```

Точные public/internal error codes и presentation определяет ADR-004.

Stack trace и internal type names не отправляются remote client.

---

# 30. Testing strategy

## 30.1 Unit tests

Для каждого converter/canonical writer:

- valid round-trip;
- invalid input;
- range boundaries;
- null/missing behavior;
- unknown enum;
- duplicate property;
- UTF-8 behavior;
- deterministic output.

## 30.2 Golden fixtures

Golden fixture является committed file и содержит:

- contract type/version;
- canonical JSON;
- expected hash;
- expected in-memory semantic values;
- compatibility expectation.

Fixture не обновляется автоматически snapshot tool-ом без review.

## 30.3 Upcaster tests

Каждый upcaster test подтверждает:

- old fixture читается;
- step pure/deterministic;
- original raw JSON не меняется;
- current DTO соответствует ожидаемой semantics;
- missing path даёт controlled incompatibility.

## 30.4 SQLite integration tests

Проверяются:

- event payload canonical storage;
- hash verification;
- transaction rollback;
- current-state JSON migration;
- unknown version read-only behavior;
- no Domain serializer annotations.

## 30.5 `.odcamp` tests

Проверяются:

- manifest round-trip;
- normalized paths;
- duplicate normalized path rejection;
- path traversal;
- corrupt checksum;
- missing optional asset;
- unknown manifest version;
- excessive archive size/entry count;
- no secrets/absolute paths.

## 30.6 AOT tests

Минимум:

- source-generated serialization smoke test в Unity Editor;
- IL2CPP Windows x64 development build;
- runtime round-trip critical fixtures;
- no reflection fallback warning;
- contract registry completeness.

## 30.7 Cross-profile tests

Тест обязан доказать, что:

- transport DTO не сериализуется Persistence profile случайно;
- Domain aggregate не зарегистрирован как root contract;
- diagnostic serializer не используется для fingerprint;
- audience projection не содержит hidden fixture fields.

---

# 31. CI gates

Pull request блокируется, если:

- critical DTO не зарегистрирован в source-generated context;
- golden fixture изменилась без version/approval marker;
- canonical hash нестабилен;
- duplicate property test не проходит;
- unknown mandatory version принимается как mutation;
- event payload переписывается при чтении;
- Domain assembly получает serializer dependency/attributes;
- IL2CPP serialization smoke test падает на требуемом gate;
- новая JSON column не имеет contract/version/migration policy;
- новый external parser не имеет size/depth limits;
- новый dependency serializer добавлен без разрешения.

Fast CI может запускать Core fixtures без Unity. IL2CPP gate допускается в отдельном required workflow согласно стоимости выполнения.

---

# 32. Реализация в SLICE-00

Минимальный объём ADR-003 для первого технического среза:

1. centralized JSON profiles;
2. `ContractType` и `ContractVersion` value types;
3. explicit contract registry interface;
4. source-generated context для test command/event/result;
5. stable typed ID converter;
6. stable UTC timestamp converter/validator;
7. stable enum token converter example;
8. canonical JSON writer;
9. `CommandFingerprintMaterialV1`;
10. SHA-256 fingerprint fixture;
11. event payload canonical fixture и hash verification;
12. upcaster interface и sample v1→v2 fixture;
13. rejection unknown type/version;
14. duplicate property validation;
15. size/depth validation;
16. in-memory persistence adapter round-trip;
17. SQLite spike с `PayloadJson`/`PayloadHash`, если SQLite adapter уже входит в PR; иначе contract test;
18. `.odcamp` manifest DTO/fixture без полного exporter;
19. Mono Core test;
20. Unity Editor test;
21. IL2CPP Windows x64 smoke build/test;
22. documentation для добавления нового contract.

Не требуется на этом PR:

- полная production database schema;
- финальный relay codec;
- все игровые payload;
- полный `.odcamp` exporter/importer;
- все migrations продукта.

---

# 33. Правила для Codex

Codex обязан:

- использовать существующий serializer profile;
- создавать explicit DTO на boundary;
- добавлять type/version в registry;
- добавлять fixture и negative tests;
- не сериализовать Domain aggregate напрямую;
- не добавлять `object`, CLR type name или unrestricted polymorphism;
- не создавать feature-local `JsonSerializerOptions` без архитектурного основания;
- не менять canonical fixture без contract version decision;
- не добавлять Newtonsoft.Json или иной serializer без утверждённой задачи/ADR;
- проверять IL2CPP compatibility для release-critical converter;
- не использовать JSON как замену relational schema;
- не логировать raw private payload;
- указывать migration/upcast impact в PR.

PR description для нового/изменённого contract содержит:

```text
ContractType
OldVersion → NewVersion
Backward compatibility
Upcaster required?
DB migration required?
Protocol impact?
Golden fixtures
IL2CPP evidence
Security limits
```

---

# 34. Критерии приёмки

ADR считается реализованным, когда:

- [ ] System.Text.Json является единственным default JSON serializer;
- [ ] Domain types не имеют serializer/persistence annotations;
- [ ] production contracts используют explicit DTO;
- [ ] contract type не зависит от CLR name;
- [ ] contract version хранится явно;
- [ ] centralized profiles созданы;
- [ ] source-generated contexts работают;
- [ ] reflection fallback отсутствует на critical release path;
- [ ] typed ID, enum и timestamp имеют stable representation;
- [ ] NaN/Infinity и duplicate properties отклоняются;
- [ ] canonical writer детерминирован;
- [ ] command fingerprint fixture стабилен;
- [ ] event payload hash fixture стабилен;
- [ ] immutable event payload не переписывается;
- [ ] upcaster chain pure и тестируется;
- [ ] unknown mandatory version блокирует mutation;
- [ ] SQLite JSON columns не заменяют typed columns/constraints;
- [ ] `.odcamp` manifests versioned и path-safe;
- [ ] binary assets не кодируются base64 в manifests;
- [ ] transport DTO отделены от Domain/Persistence DTO;
- [ ] redaction выполняется до serialization;
- [ ] parser limits существуют;
- [ ] Core tests проходят без Unity;
- [ ] Unity Editor serialization tests проходят;
- [ ] IL2CPP Windows x64 smoke test проходит;
- [ ] CI блокирует нарушения.

---

# 35. Последствия

## 35.1 Положительные

- persisted contracts переживают refactor C# classes;
- command fingerprint становится воспроизводимым;
- immutable event history сохраняет исходный смысл;
- current-state migration отделена от event upcasting;
- Domain остаётся чистым;
- JSON не захватывает всю database schema;
- transport можно оптимизировать независимо;
- снижается риск скрытых утечек;
- IL2CPP проблемы выявляются рано;
- Codex получает однозначный шаблон добавления contracts;
- архивы проверяются безопасно;
- fixtures дают доказательство совместимости.

## 35.2 Стоимость

- требуется больше DTO и mapping code;
- необходимо поддерживать registry и source-generated contexts;
- нужны upcasters и migrations;
- canonical writer сложнее обычного `JsonSerializer.Serialize`;
- golden fixtures требуют review;
- IL2CPP tests увеличивают CI time;
- нельзя быстро сохранить arbitrary object graph.

Стоимость принимается как необходимая для долговечного host-authoritative продукта.

---

# 36. Рассмотренные альтернативы

## 36.1 Unity JsonUtility

Отклонено как основной serializer из-за ограниченного contract/versioning/polymorphism контроля и связи с Unity runtime.

## 36.2 Newtonsoft.Json как default

Не выбран. Дополнительная зависимость не требуется до доказанной необходимости; AOT/reflection configuration увеличивает риск. Может быть рассмотрен отдельным ADR, если System.Text.Json не проходит mandatory spike.

## 36.3 Прямая сериализация Domain objects

Отклонено: связывает внутреннюю модель с persisted/wire contract и создаёт риск утечки.

## 36.4 Один JSON blob для всей кампании

Отклонено: нарушает SQLite transactional/query/integrity requirements и масштабирование assets.

## 36.5 Полностью relational events без JSON payload

Отклонено для полиморфного event journal: потребует отдельную таблицу на каждый event и усложнит evolution. Typed metadata остаётся relational, payload — versioned JSON.

## 36.6 CLR type names как discriminator

Отклонено: небезопасно, нестабильно при rename и несовместимо с AOT registry.

## 36.7 Автоматическое переписывание старых events

Отклонено: разрушает append-only audit и hash evidence.

## 36.8 Игнорирование unknown events

Отклонено: current state/projections могут стать неверными без видимой ошибки.

## 36.9 Base64 assets внутри JSON

Отклонено: увеличивает размер, memory pressure и мешает streaming/checksum handling.

## 36.10 Один DTO для Persistence и Networking

Отклонено: persistence хранит полный authority, сеть требует redaction и protocol evolution.

---

# 37. Отложенные решения

Этот ADR намеренно не фиксирует полностью:

- полный Result/Error vocabulary — ADR-004;
- dependency composition и serializer context registration — ADR-005;
- test project layout — ADR-006;
- support window application/schema/protocol versions — ADR-007;
- concrete Clock/RNG serialization details — ADR-008;
- exact Unity patch/package baseline — ADR-009;
- log sink/retention/redaction — ADR-010;
- exact SQLite provider и migration tool — Persistence implementation ADR;
- exact `.odcamp` physical archive codec/compression — Persistence implementation ADR;
- exact relay wire codec/compression — Networking implementation ADR;
- cryptographic signing/encryption — Security ADR;
- final UUID generation algorithm — identity implementation decision.

Отложенное решение не может нарушать инварианты этого ADR.

---

# 38. Трассировка

| Источник | Связь |
|---|---|
| Technical Development Baseline §15–17 | Уточняет System.Text.Json, SQLite и `.odcamp` baseline |
| ADR-001 | Определяет ownership DTO, converters и adapters |
| ADR-002 §7, §9, §29, §31 | Фиксирует type/version, canonical fingerprint и immutable events |
| Domain Model | Требует stable IDs, versions, definitions/instances и event metadata |
| Persistence §8, §12, §24–27 | Фиксирует hybrid schema, PayloadJson/Hash, migrations и `.odcamp` |
| Networking | Требует protocol separation, redaction и bounded transport input |
| Content Block System | Требует versioned safe polymorphic definitions без CLR execution metadata |
| Test Strategy | Требует contract, migration, security и compatibility evidence |

---

# 39. Нормативное действие

С момента принятия ADR-003:

- `System.Text.Json` является default JSON serializer проекта;
- authoritative contracts используют explicit versioned DTO;
- Domain aggregate не сериализуется напрямую;
- CLR type names запрещены как persisted/wire discriminator;
- canonical fingerprint/hash строится отдельным deterministic writer;
- `CommandFingerprintMaterialV1` исключает `CommandId` и transport-only metadata;
- immutable event payload не переписывается при version upgrade;
- event evolution выполняется pure upcaster chain;
- current-state evolution выполняется database migration;
- unknown mandatory contract блокирует mutation;
- JSON не заменяет typed SQLite schema;
- `.odcamp` хранит manifests + SQLite backup + отдельные assets;
- production critical contracts должны пройти source-generation и IL2CPP x64 test;
- конфликтующая реализация считается архитектурным дефектом и блокирует merge.

---

**Конец документа**
