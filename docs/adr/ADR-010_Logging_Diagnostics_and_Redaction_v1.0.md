# ADR-010 — Logging, Diagnostics and Redaction

**Документ:** `docs/adr/ADR-010_Logging_Diagnostics_and_Redaction_v1.0.md`  
**ADR:** ADR-010  
**Версия:** 1.0  
**Дата:** 28 июля 2026 года  
**Статус:** Accepted  
**Область:** structured logging, diagnostic correlation, exception capture, local log storage, rotation, diagnostic bundles, data classification, redaction, secret handling, crash markers, build/runtime context, support evidence и обязательные проверки `SLICE-00`  
**Связанные этапы:** Roadmap Stage 1, `SLICE-00`, Milestone `M1`, Developer Shell, startup/shutdown, Persistence/Networking integration и все последующие production slices  
**Базовые документы:** `TECHNICAL_DEVELOPMENT_BASELINE_Odyssey_VTT_v0.1.md`, `16_Test_Strategy_Odyssey_VTT_v0.1.md`, `ADR-001_Module_Boundaries_and_Dependency_Direction_v1.0.md`, `ADR-003_Serialization_Strategy_v1.0.md`, `ADR-004_Result_and_Error_Model_v1.0.md`, `ADR-005_Dependency_Composition_v1.0.md`, `ADR-007_Versioning_and_Build_Identity_v1.0.md`, `ADR-008_Deterministic_Clock_and_RNG_v1.0.md`, `ADR-009_Unity_Project_and_Build_Baseline_v1.0.md`

---

# 1. Решение

Odyssey VTT использует собственный минимальный **structured diagnostics contract** без обязательной внешней logging library. Диагностика является локальной, audience-safe по умолчанию и строится на allowlist-модели: в log event допускаются только явно разрешённые поля и типы данных.

Обязательные решения:

1. Нормативный logging port называется `IOdysseyLogger` либо эквивалентно, но его contract и semantics должны соответствовать этому ADR.
2. Production logging contract принадлежит `Odyssey.Application.Diagnostics`, поскольку Infrastructure adapters зависят от Application, а Domain/Rules не должны зависеть от logging framework.
3. `Odyssey.Domain` и `Odyssey.Rules` не логируют нормальный control flow и не зависят от logger. Они возвращают typed decisions/results; Application решает, что и на каком уровне логировать.
4. Каждый log event является структурированной записью `LogEventV1`, а не произвольной строкой.
5. Минимальный набор полей: `SchemaVersion`, `TimestampUtc`, `Level`, `EventCode`, `Subsystem`, `BuildId`, `CorrelationId?`, `DiagnosticId?`, `MessageTemplateKey`, `SafeProperties` и `ExceptionSummary?`.
6. `TimestampUtc` получает host wall clock из ADR-008. Порядок доменных событий не определяется timestamp.
7. Допустимые уровни: `Trace`, `Debug`, `Information`, `Warning`, `Error`, `Critical`.
8. `EventCode` является стабильным lowercase dot-separated идентификатором; текст сообщения не является машинным контрактом.
9. В normal production logs разрешены только поля классов `Public` и `OperationalSafe`.
10. `Personal`, `HiddenGameplay` и `Secret` данные запрещены в normal logs. Они не маскируются «после записи», а не допускаются при построении события.
11. Произвольный `object`, reflection dump, `ToString()` domain object, command/event payload, serialized DTO и raw exception data нельзя передавать как log property.
12. Любая строка считается небезопасной по умолчанию. Разрешённая строка должна быть создана через typed safe value или пройти специализированный sanitizer.
13. Secrets, authentication material, owner key, campaign RNG key, private key, access/refresh token, relay credential, environment secret и encryption material никогда не логируются и не входят в diagnostic bundle.
14. Скрытые GM-only данные, private chat plaintext, hidden token payload, fog secret geometry и неразрешённые projection data никогда не логируются на клиенте, которому они не предназначены.
15. Absolute path по умолчанию заменяется безопасным path category, конечным именем файла либо необратимым локальным fingerprint. Полный путь допускается только в специально помеченном local developer sink, выключенном в Release.
16. Обычный `Error` из ADR-004 не содержит stack trace. Подробная exception information сохраняется только во внутренней diagnostic record, связанной через `DiagnosticId`.
17. Каждый unexpected exception на Application/Unity boundary получает `DiagnosticId`, если diagnostic runtime доступен.
18. `CorrelationId` связывает одну логическую операцию между UI, Application, Persistence и Networking. Он не заменяет `CommandId`, `SessionId`, `DiagnosticId` или domain sequence.
19. Diagnostic context передаётся явно либо через scoped immutable context, который не создаёт скрытых бизнес-зависимостей.
20. Основные local sinks MVP:
    - bounded in-memory ring buffer;
    - rolling JSON Lines files;
    - Unity Console sink только для development/editor profiles;
    - emergency text sink для отказа основного logger.
21. Remote telemetry, автоматическая отправка crash reports и background upload диагностических данных не входят в MVP.
22. Rolling file policy по умолчанию: новый файл ежедневно или при достижении 10 MiB; хранение не более 10 файлов, 14 дней и 100 MiB суммарно — применяется первое достигнутое ограничение.
23. Production minimum level — `Information`; Development-Debug — `Debug`; `Trace` включается только явной временной diagnostic session.
24. Log writer использует bounded queue. При перегрузке сначала отбрасываются `Trace`/`Debug`, затем `Information`; `Warning`/`Error`/`Critical` должны получить синхронный emergency fallback либо явный drop-counter event после восстановления.
25. Logging failure не должен падать в бесконечную рекурсию и не должен завершать авторитетную операцию, если сама операция может безопасно продолжиться.
26. При shutdown logger прекращает приём новых низкоприоритетных событий, drain-ит очередь и flush-ит sinks в ограниченный срок.
27. При fatal exception создаются crash marker и best-effort flush с верхней границей времени; приложение не ждёт бесконечно.
28. Diagnostic bundle создаётся только явным действием пользователя или approved support flow, локально, без автоматической отправки.
29. Diagnostic bundle строится по allowlist, содержит manifest, BuildIdentity, безопасный system summary, redacted logs, crash marker и redaction report; campaign database, content assets, закрытая документация, private chat и secrets не включаются.
30. Перед сохранением bundle пользователь видит состав категорий данных и целевой путь. В MVP нет скрытой автоматической отправки.
31. Diagnostic bundle имеет отдельную версию manifest, checksums и размерный лимит 50 MiB; превышение приводит к безопасному сокращению oldest/low-severity logs с отчётом.
32. Game Log, event store и audit/domain history не являются diagnostic log. Диагностические записи не используются как источник авторитетного состояния.
33. Log files не являются API между модулями и не парсятся для бизнес-логики.
34. Log event schema сериализуется по правилам ADR-003 через source-generated `System.Text.Json` context и проверяется в IL2CPP.
35. Logging и redaction покрываются architecture, unit, contract, Unity и security tests до закрытия `SLICE-00`.
36. ADR-010 является нормативным authority по logging, diagnostics и redaction и заменяет предварительный раздел 20 Technical Development Baseline в пределах этой области.

---

# 2. Контекст и проблема

Odyssey VTT объединяет Unity UI, локальную SQLite-базу, сетевую сессию, скрытые данные мастера, команды, события, импорт пользовательских файлов и будущие security-sensitive механизмы. Без единого diagnostic contract разные задачи Codex могут:

- писать свободный текст без стабильного EventCode;
- логировать целиком command/event DTO;
- выводить stack trace и SQL в пользовательский интерфейс;
- сохранять owner key, токен relay либо campaign RNG key;
- помещать скрытые данные мастера в лог игрока;
- логировать полный путь Windows с именем пользователя;
- использовать Unity Console как единственное диагностическое хранилище;
- писать логи синхронно на игровом потоке;
- терять важные ошибки при переполнении очереди;
- создавать несколько независимых logger instances в сценах;
- включать автоматическую телеметрию без согласия пользователя;
- формировать diagnostic archive из всей папки приложения;
- использовать лог как неявный event store;
- удалять полезную correlation information при переходе между слоями;
- генерировать гигантские файлы без ротации;
- получить recursion crash, когда сам logger не может записать ошибку.

Нужен единый контракт, который одновременно обеспечивает:

1. достаточную информацию для локальной диагностики;
2. безопасность скрытых и персональных данных;
3. воспроизводимость и correlation между слоями;
4. минимальное влияние на производительность;
5. одинаковое поведение в .NET, Unity Mono и IL2CPP;
6. понятный support artifact без автоматической передачи данных;
7. возможность Codex добавлять события только через контролируемый registry и typed properties.

---

# 3. Движущие факторы

Решение оптимизировано под:

1. Публичный GitHub repository и отсутствие секретов в source/history.
2. Локальную Windows 10/11 desktop application.
3. Host-authoritative архитектуру и скрытые GM data.
4. Явный Result/Error contract ADR-004.
5. BuildIdentity и version provenance ADR-007.
6. Deterministic clock/RNG ADR-008.
7. Один composition root и контролируемые lifetimes ADR-005.
8. Отсутствие обязательной внешней logging library.
9. Простую диагностику проблем startup, SQLite, serialization, networking и Unity lifecycle.
10. Минимальную нагрузку на main thread.
11. Безопасный diagnostic bundle, который пользователь может изучить перед отправкой.
12. Возможность автоматического security scanning и contract tests.
13. Отсутствие remote telemetry в MVP.
14. Стабильные machine-readable codes при изменяемом developer-facing тексте.

---

# 4. Термины

## 4.1 Log event

Одна immutable структурированная diagnostic запись, описывающая произошедшее техническое наблюдение.

## 4.2 EventCode

Стабильный machine-readable код события, например:

```text
app.startup.completed
persistence.sqlite.open_failed
network.session.reconnect_started
serialization.contract.version_unsupported
```

## 4.3 CorrelationId

Идентификатор логической операции, проходящей через несколько слоёв. Он позволяет связать command handling, persistence transaction, network publication и UI result.

## 4.4 DiagnosticId

Идентификатор конкретной внутренней diagnostic record или incident. Может безопасно отображаться пользователю как ссылка для поиска подробностей в локальных логах.

## 4.5 Sink

Получатель уже построенного и очищенного log event: memory buffer, JSONL file, Unity Console или emergency file.

## 4.6 Redaction

Удаление, обобщение, fingerprinting или замена запрещённых данных до записи в sink либо diagnostic bundle.

## 4.7 Diagnostic bundle

Локально сформированный архив с allowlisted технической информацией для поддержки и анализа.

## 4.8 Crash marker

Минимальный отдельный файл, указывающий, что предыдущий процесс завершился аварийно либо не прошёл корректный shutdown.

## 4.9 Safe property

Структурированное значение разрешённого типа и классификации, которое прошло проверку до создания `LogEventV1`.

## 4.10 Diagnostic session

Явно включённый пользователем или разработчиком ограниченный по времени режим повышенной детализации. Он не снимает запреты на secrets и hidden data.

---

# 5. Ownership и module boundaries

## 5.1 Application

`Odyssey.Application.Diagnostics` владеет:

- `IOdysseyLogger`;
- `LogLevel`;
- `EventCode` value object;
- `DiagnosticId`/`CorrelationId` integration;
- safe property vocabulary;
- diagnostic context contract;
- high-level incident recording port;
- правилами перехода Error → diagnostic record.

Application не знает о Unity Console, JSONL path, file rotation или конкретной queue implementation.

## 5.2 Domain и Rules

Domain/Rules:

- не зависят от logger;
- не создают log events для ожидаемого поведения;
- возвращают typed result/decision;
- могут включать безопасные machine details в domain decision, которые Application затем явно отображает в Error/log event;
- не знают про `DiagnosticId` как часть бизнес-логики.

## 5.3 Persistence

Persistence:

- реализует file/JSONL sinks и diagnostic storage adapters при необходимости;
- логирует только safe metadata об операции;
- не логирует SQL parameter values, raw rows, campaign payload или encryption keys;
- сообщает Application typed failure, а не использует лог как error transport.

## 5.4 Networking

Networking:

- сохраняет correlation при переходе между transport и Application;
- не логирует access token, relay credential, raw packet payload и hidden projection;
- может логировать safe protocol/version, message type, byte count, duration и sanitized endpoint category;
- не отправляет local diagnostic records другим участникам сессии.

## 5.5 Unity Client

Unity Client:

- создаёт process-scoped diagnostic runtime в composition root;
- подключает Unity Console sink только для разрешённых profiles;
- отображает пользователю safe message и `DiagnosticId`;
- не показывает stack trace в обычном UI;
- создаёт diagnostic bundle через Application use case.

## 5.6 TestKit

TestKit предоставляет:

- in-memory capturing logger;
- deterministic clock;
- fake sinks;
- queue saturation controls;
- redaction assertions;
- bundle inspection helpers.

TestKit не входит в production Player.

---

# 6. Canonical log event schema

## 6.1 `LogEventV1`

Минимальная логическая схема:

```text
LogEventV1
├── SchemaVersion: 1
├── TimestampUtc
├── Level
├── EventCode
├── Subsystem
├── BuildId
├── ProcessInstanceId
├── CorrelationId?
├── DiagnosticId?
├── CommandId?
├── SessionReference?
├── MessageTemplateKey
├── SafeProperties{}
└── ExceptionSummary?
```

`ProcessInstanceId` генерируется при startup и не является постоянным user/device identifier.

## 6.2 Обязательные поля

Обязательны:

- `SchemaVersion`;
- `TimestampUtc`;
- `Level`;
- `EventCode`;
- `Subsystem`;
- `BuildId`, если BuildIdentity уже загружен;
- `ProcessInstanceId`;
- `MessageTemplateKey`;
- `SafeProperties`, даже если map пуст.

## 6.3 Optional references

`CorrelationId`, `DiagnosticId`, `CommandId` и `SessionReference` включаются только при наличии соответствующего контекста.

## 6.4 Immutable record

После создания event не изменяется. Каждый sink получает одну и ту же уже очищенную logical record либо сериализованную копию.

Sink не имеет права добавлять raw context или расширять запись небезопасными данными.

## 6.5 Schema version

Изменение обязательных полей или семантики event schema повышает `LogEventSchemaVersion` и требует compatibility tests. Версия приложения не используется вместо версии log schema.

---

# 7. Log levels

## 7.1 Trace

Очень подробное последовательное наблюдение для короткой diagnostic session.

Примеры:

- переход между безопасными фазами state machine;
- измеренный шаг startup;
- повторная проверка queue/reconnect без payload.

`Trace` выключен по умолчанию во всех profiles.

## 7.2 Debug

Информация для разработки, не необходимая обычной эксплуатации.

Примеры:

- выбранный runtime profile;
- safe package/settings summary;
- lifecycle transition;
- cache hit/miss без содержимого.

`Debug` включён в Development-Debug и может быть временно включён диагностической настройкой.

## 7.3 Information

Нормальное значимое состояние процесса.

Примеры:

- приложение успешно запущено;
- кампания открыта;
- сессия начата/завершена;
- migration успешно выполнена;
- diagnostic bundle создан.

Information не используется для каждой frame/tick/packet операции.

## 7.4 Warning

Неожиданное, но обработанное состояние, не приведшее к немедленному провалу операции либо требующее внимания.

Примеры:

- fallback D3D12 → D3D11;
- пропущен повреждённый необязательный import item;
- queue pressure;
- reconnect attempt;
- deprecated compatible contract.

## 7.5 Error

Операция завершилась неуспешно, но процесс может продолжить работу в контролируемом состоянии.

Примеры:

- не удалось открыть выбранную кампанию;
- transaction rollback;
- serialization failure;
- network session start failure.

## 7.6 Critical

Процесс либо authority runtime не может безопасно продолжить обычную работу.

Примеры:

- corruption авторитетного store без безопасной recovery path;
- bootstrap graph не создан;
- unhandled exception на critical boundary;
- невозможность гарантировать consistency после commit uncertainty.

## 7.7 Level не равен ErrorCategory

`LogLevel` описывает диагностическую важность записи. `ErrorCategory` ADR-004 описывает тип неуспеха. Между ними нет автоматического one-to-one mapping.

---

# 8. EventCode registry

## 8.1 Формат

```text
<subsystem>.<area>.<event>
```

Только lowercase ASCII letters, digits и underscore внутри сегмента; сегменты разделены точками.

Примеры:

```text
app.startup.started
app.startup.completed
app.shutdown.completed
persistence.campaign.open_failed
network.session.reconnect_scheduled
serialization.contract.upcast_failed
diagnostics.queue.low_priority_dropped
```

## 8.2 Стабильность

- EventCode не переиспользуется с новым смыслом.
- Изменение developer-facing текста не требует нового EventCode.
- Изменение семантики требует нового EventCode либо versioned field.
- Удалённый EventCode остаётся зарезервированным.

## 8.3 Registry

В repository создаётся machine-readable registry, например:

```text
config/diagnostics/event-codes.json
```

Для каждого кода фиксируются:

- owner subsystem;
- default level;
- allowed property keys;
- property classifications;
- краткое назначение;
- status: active/deprecated/reserved.

## 8.4 Codex rule

Codex не добавляет новый EventCode без обновления registry и соответствующих contract/redaction tests.

---

# 9. Safe property model

## 9.1 Разрешённые primitive types

Normal log property может иметь только:

- boolean;
- bounded integer;
- bounded decimal;
- enum/string code из registry;
- GUID-like technical identifier разрешённого класса;
- UTC timestamp;
- duration;
- byte count;
- safe hash/fingerprint;
- bounded safe string;
- bounded list перечисленных выше значений, если EventCode явно разрешает список.

## 9.2 Запрещённые generic values

Запрещены:

- `object`;
- arbitrary dictionary;
- arbitrary JSON node;
- exception object как property;
- domain entity;
- DTO;
- command/event envelope;
- byte array;
- stream;
- Unity Object;
- serialized payload;
- reflection dump.

## 9.3 Bounded values

Каждая safe string имеет ограничение длины. Baseline maximum — 256 Unicode scalar values, если EventCode registry не задаёт меньшее значение.

Слишком длинное значение сокращается с явным marker и original length metadata, если сама длина безопасна.

## 9.4 `ToString()` prohibition

Нельзя логировать:

```text
logger.Info("...", entity.ToString())
logger.Error(exception.ToString())
logger.Debug(JsonSerializer.Serialize(command))
```

Допустимы специализированные mappers, возвращающие typed `SafeLogValue`.

## 9.5 Collections

Список не должен содержать больше 20 элементов в normal log. Для больших наборов логируются count, category summary и safe hash, но не все элементы.

---

# 10. Классификация данных

## 10.1 Public

Данные, которые безопасны для отображения и публикации без привязки к пользователю.

Примеры:

- ApplicationVersion;
- BuildId;
- Unity version;
- public EventCode;
- supported protocol version.

## 10.2 OperationalSafe

Технические данные, необходимые для локальной диагностики и не раскрывающие содержимое кампании или identity.

Примеры:

- duration;
- result count;
- byte count;
- schema version;
- safe error code;
- graphics API;
- quality profile;
- command type code;
- non-secret CommandId/CorrelationId.

## 10.3 Personal

Данные, способные идентифицировать пользователя или устройство.

Примеры:

- display name;
- email;
- account identifier;
- IP address;
- Windows username;
- полный absolute path;
- persistent hardware identifier.

Personal data не входит в normal logs. При необходимости используется локальный salted fingerprint либо обобщённая category.

## 10.4 HiddenGameplay

Данные, которые разрешены не всем участникам кампании.

Примеры:

- GM-only notes;
- hidden token identity/position;
- unrevealed fog geometry;
- private roll details;
- private chat plaintext;
- secret character fields.

HiddenGameplay не входит в normal logs и diagnostic bundle.

## 10.5 Secret

Ключи и credentials, раскрытие которых нарушает безопасность.

Примеры:

- passwords;
- access/refresh tokens;
- owner key material;
- campaign RNG key;
- private/encryption keys;
- relay credentials;
- GitHub Actions secrets;
- environment secrets.

Secret запрещён во всех sinks и bundle без исключения.

---

# 11. Redaction architecture

## 11.1 Allowlist first

Система не пытается распознать все возможные secrets в уже готовой строке. Event builder принимает только разрешённые typed properties.

Pattern-based secret scanning остаётся дополнительной защитой, а не основной моделью.

## 11.2 Redaction до sink

Порядок:

```text
Call site
→ typed event builder
→ classification/allowlist validation
→ sanitizer/redactor
→ immutable LogEventV1
→ queue
→ sinks
```

Ни один sink не получает raw object/context.

## 11.3 Safe path representation

Путь преобразуется в:

```text
PathCategory: CampaignStorage | ImportSource | LogStorage | BuildArtifact | Unknown
FileName: optional safe terminal name
Extension: optional
PathFingerprint: optional local salted hash
```

Directory hierarchy и Windows username по умолчанию не сохраняются.

## 11.4 Network endpoint representation

IP/hostname не логируется напрямую. Допустимы:

- endpoint category: relay/local/loopback;
- address family;
- port category, если безопасно;
- per-process fingerprint для correlation.

## 11.5 User/session identity

Display name и account identity не логируются. При необходимости используется ephemeral `ParticipantReference`, который действует только внутри process/session и не является permanent tracking identifier.

## 11.6 Payload representation

Для payload разрешены только:

- contract type;
- contract version;
- byte length;
- safe content hash, если hash не создаёт oracle для секретного маломощного значения;
- item count;
- validation result summary.

Raw payload и parsed content запрещены.

## 11.7 Hashing не всегда является redaction

Hash email, короткого имени, IP или значения из малого пространства может быть перебран. Для Personal data используется keyed local fingerprint с per-install diagnostic salt либо данные не записываются вовсе.

Campaign RNG key и secrets не хешируются для обычного лога как способ «разрешить» их запись. Допустимый `RngKeyEpochHash` определяется ADR-008 и не раскрывает key material.

---

# 12. Logging API

## 12.1 Минимальный port

Логический contract:

```text
IOdysseyLogger
├── IsEnabled(level, eventCode)
└── Write(LogEventDraft)
```

Convenience extensions могут предоставлять typed methods, но не должны принимать arbitrary `params object[]`.

## 12.2 Typed event builders

Предпочтительный pattern:

```text
DiagnosticsEvents.AppStartupCompleted(
    correlationId,
    duration,
    runtimeProfile,
    graphicsApi)
```

а не:

```text
logger.Info("Startup completed {0} {1}", a, b)
```

Generated/handwritten typed builders гарантируют property allowlist.

## 12.3 MessageTemplateKey

Developer-facing renderer может отображать English template, но source record хранит стабильный key:

```text
log.app.startup.completed
```

Он не является пользовательским `UserMessageKey` ADR-004 и не используется UI как основное сообщение об ошибке.

## 12.4 Disabled level

Если level/EventCode выключен, expensive diagnostic values не вычисляются. API должен поддерживать lazy safe-value factory либо предварительную проверку `IsEnabled`.

## 12.5 Scope

Immutable diagnostic scope может добавлять:

- CorrelationId;
- CommandId;
- SessionReference;
- CampaignReference;
- subsystem.

Scope не хранит mutable business state и не используется как service locator.

---

# 13. Correlation model

## 13.1 CorrelationId lifecycle

- UI/Application use case без входящего correlation создаёт новый `CorrelationId`.
- Входящая remote command использует проверенный transport correlation либо создаёт host correlation с сохранением safe remote reference.
- Application передаёт correlation Persistence/Networking adapters.
- Все связанные log events используют один CorrelationId.

## 13.2 CommandId

`CommandId` сохраняется как отдельное поле, когда операция связана с командой. Он не заменяет CorrelationId:

- одна command обычно имеет один correlation;
- continuation commands разделяют CorrelationId, но имеют разные CommandId;
- non-command operations также имеют CorrelationId.

## 13.3 DiagnosticId

DiagnosticId создаётся для incident/detail record, а не для каждого Information event.

Один incident может иметь несколько log events с одним DiagnosticId.

## 13.4 Causation

При необходимости safe `CausationReference` может связывать continuation/incident, но не дублирует full domain event envelope.

## 13.5 Client/server boundary

Host не отправляет внутренний DiagnosticId клиенту автоматически, если он связан с host-only details. Transport mapping решает, можно ли передать safe support reference конкретной аудитории.

---

# 14. Exception handling

## 14.1 Expected failures

Ожидаемые validation, authorization, conflict и compatibility outcomes возвращаются через ADR-004 Result/Error и не требуют stack trace.

Они могут породить `Information`, `Warning` или `Error` event в зависимости от operational meaning и sampling policy.

## 14.2 Unexpected exception boundary

На approved boundary:

1. exception перехватывается;
2. создаётся DiagnosticId;
3. строится safe `ExceptionSummary`;
4. полная internal exception record сохраняется только в защищённом local diagnostic store;
5. Application получает safe Internal/Infrastructure Error;
6. пользователь видит UserMessageKey и DiagnosticId.

## 14.3 `ExceptionSummary`

Normal JSONL log может содержать:

- exception category/code;
- exception type allowlist/normalized type;
- safe subsystem;
- safe stack fingerprint;
- inner exception count;
- transient classification;
- DiagnosticId.

Он не содержит raw `Exception.Message`, Data dictionary и full stack trace.

## 14.4 Internal exception detail

Full stack trace может храниться в отдельном local restricted diagnostic record, но перед записью:

- absolute paths очищаются;
- source code snippets не включаются;
- exception messages проходят sanitizer;
- secrets scan выполняется до bundle export.

В Release такие details включаются в bundle только при allowlist и после повторной redaction.

## 14.5 Cancellation

Expected cancellation не логируется как Error. Обычно это Debug/Information либо отсутствие записи, если событие незначимо.

## 14.6 Exception storms

Повторяющиеся одинаковые incidents rate-limit-ятся по safe fingerprint. Logger сохраняет первый incident, summary counters и периодические aggregate events, не создавая бесконечный поток одинаковых stack records.

---

# 15. Sinks

## 15.1 In-memory ring buffer

- process-scoped;
- хранит последние очищенные events;
- bounded по count/bytes;
- используется diagnostic overlay и bundle builder;
- не является authoritative store.

Baseline capacity: 2 000 events либо 8 MiB, применяется первое ограничение.

## 15.2 Rolling JSONL file sink

Формат — одна JSON-запись на строку UTF-8 без BOM.

Путь:

```text
<Application.persistentDataPath>/Diagnostics/Logs/
```

Фактический absolute path не выводится в normal log.

## 15.3 Unity Console sink

Разрешён в:

- Unity Editor;
- Development-Debug;
- Development-Profile при явном включении.

Release/RC не обязаны дублировать normal logs в Unity Console.

Console renderer получает уже очищенный event.

## 15.4 Emergency sink

Минимальный append-only text file либо OS stderr fallback для:

- ошибки инициализации основного diagnostic runtime;
- queue writer failure;
- JSON serialization failure;
- fatal exception во время shutdown.

Emergency sink принимает только fixed EventCode, timestamp, DiagnosticId и короткий sanitized message. Он не принимает arbitrary properties или stack trace.

## 15.5 Sink independence

Отказ одного sink не останавливает остальные. Sink failure порождает bounded internal incident без рекурсивного вызова того же failing sink.

---

# 16. Queue, threading и backpressure

## 16.1 Single writer

File sink обслуживается одним background writer. Call sites не выполняют обычный disk IO на main thread.

## 16.2 Bounded queue

Baseline capacity — 4 096 events либо 16 MiB estimated payload, применяется первое ограничение.

## 16.3 Drop policy

При queue pressure:

1. отбрасывается Trace;
2. затем Debug;
3. затем Information;
4. Warning/Error/Critical направляются в priority lane или emergency fallback.

После восстановления создаётся:

```text
diagnostics.queue.events_dropped
```

с count по уровню и interval, без потерянных payload.

## 16.4 No unbounded blocking

Warning/Error call не блокирует main thread бесконечно. Emergency fallback имеет короткую верхнюю границу времени.

## 16.5 Ordering

Внутри одного process sink стремится сохранять enqueue order. Лог не является domain ordering authority; event/command sequence определяется ADR-002/Persistence.

## 16.6 Shutdown drain

Baseline flush budget:

- обычный shutdown: до 2 секунд;
- fatal shutdown: до 500 миллисекунд best effort.

Точные значения могут быть скорректированы measured PR без изменения semantic contract.

---

# 17. Rotation и retention

## 17.1 Rotation triggers

Новый JSONL file создаётся:

- при смене UTC-даты;
- при достижении 10 MiB;
- при новом process после crash, если предыдущий active file не был корректно закрыт.

## 17.2 Retention limits

Хранятся данные до первого достигнутого ограничения:

- 10 log files;
- 14 дней;
- 100 MiB суммарно.

Удаляются самые старые closed files.

## 17.3 Active file

Активный файл не удаляется retention worker во время использования.

## 17.4 Disk pressure

При недостатке места:

- retention выполняется немедленно;
- Information/Debug file logging может временно отключиться;
- Warning/Error/Critical переходят на memory/emergency sink;
- создаётся safe disk-pressure incident.

## 17.5 User controls

Пользователь может:

- открыть папку диагностики;
- очистить closed logs;
- создать bundle;
- временно включить расширенную diagnostic session.

Очистка active file во время работы запрещена либо выполняется через controlled rotation.

---

# 18. Diagnostic session

## 18.1 Explicit opt-in

Trace/extended diagnostics включаются только явным действием пользователя либо developer profile.

## 18.2 Duration

Production diagnostic session автоматически завершается максимум через 30 минут либо при shutdown.

## 18.3 Scope

Session может включать дополнительные approved EventCodes и operational properties, но не снимает запреты на Personal/HiddenGameplay/Secret.

## 18.4 Visible indicator

UI показывает, что расширенная диагностика включена, и оставшееся время.

## 18.5 No persistence of consent

Trace session не включается автоматически после следующего запуска, если отдельная support procedure не требует иного явного подтверждения.

---

# 19. Crash handling

## 19.1 Process marker

При startup создаётся process state marker:

```text
process-started.json
```

При корректном shutdown он заменяется/помечается как completed.

Наличие незавершённого marker при следующем startup означает suspected crash, но не доказывает конкретную причину.

## 19.2 Fatal hooks

Unity Client подключает approved hooks:

- Application-level unhandled boundary;
- `AppDomain.UnhandledException`;
- `TaskScheduler.UnobservedTaskException` как diagnostic signal;
- Unity log callback только с защитой от duplicate/reentrancy.

## 19.3 Fatal record

Fatal handler:

- создаёт DiagnosticId;
- записывает minimal Critical event;
- сохраняет crash marker;
- выполняет bounded flush;
- не пытается продолжить authority operation в неизвестном состоянии.

## 19.4 Next startup

При обнаружении crash marker пользователь получает безопасное предложение:

- продолжить запуск;
- открыть diagnostic details;
- создать diagnostic bundle.

Bundle не создаётся и не отправляется автоматически.

## 19.5 Native crash dumps

OS/native dump не входит в baseline diagnostic bundle. Его добавление требует отдельного security/privacy review.

---

# 20. Diagnostic bundle

## 20.1 Creation flow

```text
User action
→ collect allowlisted sources
→ redact/sanitize
→ validate size and schema
→ show category summary
→ create archive locally
→ calculate checksums
```

## 20.2 Manifest

`diagnostic-manifest.json` содержит:

- bundle format version;
- BundleId;
- creation UTC;
- BuildIdentity;
- included categories/files;
- excluded/truncated categories;
- redaction counters;
- checksums;
- total size;
- optional user-entered issue description after explicit consent.

## 20.3 Allowlisted contents

Допускаются:

- recent redacted JSONL logs;
- current/previous crash marker;
- BuildIdentity;
- Application/Unity/.NET/runtime versions;
- OS edition/build category;
- CPU architecture, logical core count;
- RAM bucket;
- GPU model/driver/graphics API;
- quality profile и window/display configuration без user path;
- package/version summary;
- safe config flags;
- redaction report;
- checksum report.

## 20.4 Forbidden contents

Не включаются:

- campaign SQLite database;
- `.odcamp`;
- maps, images, audio или пользовательские assets;
- закрытая документация/task bundle;
- private chat plaintext;
- GM notes и hidden projections;
- credentials/secrets;
- environment variable values;
- raw network payload/packet capture;
- full registry dump;
- browser history;
- unrelated files;
- native memory dump по умолчанию.

## 20.5 System information minimization

System summary содержит только данные, необходимые для совместимости. Serial numbers, machine name, Windows username и persistent device identifiers запрещены.

## 20.6 Size cap

Максимальный baseline размер bundle — 50 MiB.

При превышении:

1. удаляются oldest Trace/Debug logs;
2. затем oldest Information logs;
3. Warning/Error/Critical сохраняются при возможности;
4. manifest фиксирует truncation.

## 20.7 Archive format

Baseline — ZIP с UTF-8 names, deterministic logical layout и SHA-256 checksums. ZIP не считается security boundary; пользователь сам контролирует передачу файла.

## 20.8 Preview

До создания/сохранения UI показывает категории, но не обязан отображать каждую строку. Пользователь может исключить optional issue description и extended logs.

## 20.9 No automatic upload

В MVP нет endpoint, background uploader или support token. Передача bundle находится вне приложения и выполняется пользователем вручную.

---

# 21. Diagnostic overlay

Developer Shell/diagnostic overlay может показывать:

- BuildIdentity;
- ProcessInstanceId;
- текущий minimum log level;
- queue usage/drop counters;
- active sinks;
- last safe Warning/Error events;
- recent DiagnosticId;
- current runtime profile;
- graphics API/quality profile;
- кнопки create bundle/open logs.

Overlay не показывает:

- secret values;
- raw payload;
- hidden campaign state;
- private chat;
- полный stack trace обычному пользователю.

Доступность overlay в Release определяется product/UI task, но безопасный bundle flow обязателен.

---

# 22. Sampling и rate limiting

## 22.1 Не логировать каждый frame

Frame, pointer move, audio sample, network packet и token interpolation не логируются по одному событию в normal mode.

## 22.2 Aggregation

Высокочастотные события агрегируются:

- count;
- min/max/average duration;
- error count;
- interval;
- safe category.

## 22.3 Security events

Repeated authorization failures могут агрегироваться, но лог не должен создавать identity tracking либо раскрывать скрытый target.

## 22.4 Deterministic sampling

Если используется sampling, решение основывается на safe stable inputs и фиксированной версии policy, но sampling не влияет на бизнес-логику.

Error/Critical incidents, необходимые для support/recovery, не sample-ятся без отдельного policy.

---

# 23. Game Log, audit и diagnostic log

## 23.1 Game Log

Игровой журнал из документа 09 является пользовательским gameplay artifact с permissions и visibility rules.

## 23.2 Domain event store

Event store хранит авторитетные immutable facts и участвует в recovery/replay.

## 23.3 Diagnostic log

Diagnostic log:

- может быть удалён без изменения campaign state;
- не участвует в replay;
- не заменяет audit trail;
- не доказывает окончательный command result;
- не используется клиентом для синхронизации.

## 23.4 Запрет смешения

Codex не должен писать gameplay event только в diagnostic log либо копировать скрытый DomainEvent целиком в diagnostic sink.

---

# 24. Security и privacy

## 24.1 Secret scanning

Обязательны:

- static scan source/config;
- fixture scan generated logs;
- diagnostic bundle scan;
- known secret marker tests;
- entropy/pattern heuristic как дополнительный слой.

## 24.2 Test secrets

Тесты используют явно помеченные fake secrets и проверяют их полное отсутствие во всех sinks/bundle.

## 24.3 Public repository

Sample logs в repository должны быть искусственными, очищенными и не содержать реальные user/system paths.

## 24.4 Permissions

Diagnostic bundle не наследует автоматически GM permissions и не включает больше данных только потому, что его создаёт Main GM.

## 24.5 Support reference

DiagnosticId не должен кодировать secret, timestamp precision, account identity или path. Используется случайный opaque identifier.

## 24.6 Data deletion

Пользователь может удалить local logs и bundles. Удаление diagnostic data не повреждает campaign database.

---

# 25. Serialization contract

## 25.1 JSON profile

`LogEventV1` и `DiagnosticManifestV1` имеют отдельные source-generated `JsonSerializerContext`.

## 25.2 Naming

JSON property names стабильны и задаются явно; CLR type names не являются contract identifiers.

## 25.3 Unknown fields

Reader bundle/log tooling может игнорировать неизвестные additive fields совместимой версии, но неизвестная major schema обрабатывается как Compatibility Error.

## 25.4 Duplicate properties

JSON с duplicate properties отклоняется diagnostic tooling по правилам ADR-003.

## 25.5 Parser limits

Задаются ограничения:

- line length;
- nesting depth;
- property count;
- string length;
- bundle file count;
- archive decompression size.

Diagnostic tooling не доверяет даже собственному старому bundle без проверки.

---

# 26. Performance requirements

## 26.1 Main thread budget

Построение и enqueue обычного enabled event не выполняет disk IO и не должно создавать крупные allocations.

## 26.2 Disabled path

Disabled Trace/Debug call имеет минимальную стоимость и не вычисляет expensive properties.

## 26.3 Allocation control

Typed builders, pooled buffers и source-generated serialization допускаются после measurement. Optimization не может ослаблять redaction.

## 26.4 Benchmarks

До M1 достаточно baseline micro/soak tests queue/sink. Production performance budgets уточняются отдельной measured task.

## 26.5 Failure isolation

Logger не удерживает campaign transaction open во время disk write.

---

# 27. Composition и lifecycle

## 27.1 Bootstrap logger

До построения полного AppRuntime используется минимальный bootstrap diagnostics adapter с memory/emergency sink.

После успешной composition он передаёт buffered safe events основному runtime либо сохраняет их как startup segment.

## 27.2 Process scope

Основной diagnostic runtime живёт Process scope и имеет одного owner в Unity composition root.

## 27.3 Campaign/session context

Campaign и Session scopes добавляют только safe references/context; они не создают отдельные независимые logger graphs.

## 27.4 Shutdown

Diagnostic runtime закрывается последним среди ordinary process resources, чтобы записать завершение остальных scopes.

Emergency sink остаётся доступным до конца bounded shutdown.

## 27.5 Double bootstrap

Защита ADR-005 предотвращает создание второго file writer и конкурирующую запись в один active log file.

---

# 28. Error model integration

## 28.1 Error → log

Не каждый `Result.Failure` автоматически логируется на Error level. Application use case решает operational level, чтобы избежать duplicate/noisy logs.

## 28.2 Internal Error

`ErrorCategory.Internal` и unexpected Infrastructure failure получают DiagnosticId при доступном diagnostics storage.

## 28.3 Safe fields

В normal log допускаются:

- ErrorCode, если он не раскрывает hidden reason;
- SafeReasonCode;
- ErrorCategory;
- RetryDirective;
- DiagnosticId;
- safe ValidationDetail codes/count.

Validation raw user input не логируется.

## 28.4 Duplicate logging

Exception логируется подробно один раз на owning boundary. Верхние слои добавляют context event без повторного full stack.

## 28.5 User-facing message

UI использует ADR-004 UserMessageKey. Developer log template не показывается пользователю как локализованное сообщение.

---

# 29. Build profiles

## 29.1 Development-Debug

- minimum Debug;
- Unity Console sink включён;
- diagnostic overlay включён;
- full local sanitized exception details;
- Trace можно включить вручную.

## 29.2 Development-Profile

- minimum Information или Debug по задаче profiling;
- Console sink опционален;
- logging не должен искажать performance measurement без отметки.

## 29.3 Release-Candidate

- minimum Information;
- file + memory + emergency sinks;
- no automatic upload;
- production redaction;
- diagnostic bundle flow включён;
- IL2CPP contract tests обязательны.

## 29.4 Release

- minimum Information;
- Debug/Trace выключены по умолчанию;
- temporary diagnostic session возможна только явно;
- secrets/hidden-data tests обязательны;
- stack traces не показываются обычному пользователю.

---

# 30. CI и quality gates

PR блокируется, если:

1. добавлен EventCode без registry entry;
2. property key не указан в allowlist EventCode;
3. production code принимает arbitrary object/dictionary в logger API;
4. Domain/Rules ссылаются на logging assembly;
5. обнаружен secret marker в generated logs/bundle;
6. hidden gameplay fixture попал в client log;
7. absolute path/username не очищен;
8. logger queue не bounded;
9. sink failure вызывает recursion/crash;
10. log schema не проходит .NET/Unity/IL2CPP vectors;
11. Release profile включает unsafe Console/raw exception output;
12. diagnostic bundle содержит запрещённую категорию;
13. retention/size limits не выполняются;
14. TestKit попадает в production Player;
15. active baseline/ADR references расходятся.

---

# 31. Обязательные тестовые сценарии `SLICE-00`

Минимальные TestCaseId:

```text
DIAG-001 Information event serializes as LogEventV1.
DIAG-002 Disabled Debug event does not evaluate lazy property.
DIAG-003 EventCode outside registry is rejected in tests/build validation.
DIAG-004 Arbitrary object property is rejected.
DIAG-005 Safe bounded string truncates with marker.
DIAG-006 Secret fixture is absent from memory sink.
DIAG-007 Secret fixture is absent from JSONL sink.
DIAG-008 Secret fixture is absent from diagnostic bundle.
DIAG-009 Hidden GM payload is absent from Player-side logs.
DIAG-010 Private chat plaintext is absent from logs and bundle.
DIAG-011 Absolute Windows path is sanitized.
DIAG-012 Windows username is absent from path representation.
DIAG-013 Network endpoint is generalized/fingerprinted.
DIAG-014 CorrelationId propagates UI → Application → Persistence adapter.
DIAG-015 CommandId remains distinct from CorrelationId.
DIAG-016 Unexpected exception creates DiagnosticId.
DIAG-017 Public Error contains no stack trace.
DIAG-018 Duplicate exception is not fully logged at every layer.
DIAG-019 Queue accepts events concurrently without corruption.
DIAG-020 Queue drops Trace before Warning under pressure.
DIAG-021 Drop counters are emitted after recovery.
DIAG-022 Warning uses emergency fallback when priority queue is unavailable.
DIAG-023 Sink failure does not recurse infinitely.
DIAG-024 Logger failure does not change successful domain result.
DIAG-025 Normal shutdown drains queue within budget.
DIAG-026 Fatal shutdown returns after bounded flush.
DIAG-027 Crash marker is detected on next startup.
DIAG-028 Correct shutdown clears/completes process marker.
DIAG-029 Daily rotation creates a new file.
DIAG-030 Size rotation occurs at configured threshold.
DIAG-031 Retention removes oldest closed files.
DIAG-032 Active file is not deleted by retention.
DIAG-033 Diagnostic session expires automatically.
DIAG-034 Diagnostic session does not enable secret fields.
DIAG-035 Bundle manifest lists included and excluded categories.
DIAG-036 Bundle checksums match files.
DIAG-037 Bundle respects 50 MiB cap and records truncation.
DIAG-038 Campaign database is absent from bundle.
DIAG-039 Closed documentation is absent from bundle.
DIAG-040 Machine name and persistent device ID are absent from system summary.
DIAG-041 Same contract vector serializes identically in .NET and Unity Mono.
DIAG-042 Same contract vector serializes identically in IL2CPP Windows x64.
DIAG-043 Unknown future major log schema returns Compatibility Error.
DIAG-044 Duplicate JSON property is rejected by diagnostic reader.
DIAG-045 Logger does not hold SQLite transaction during file write.
DIAG-046 Domain and Rules assemblies have no logger dependency.
DIAG-047 Release build has safe minimum level and no unsafe Console sink.
DIAG-048 TestKit/fixtures do not enter Player build.
DIAG-049 Game Log deletion/retention is independent from diagnostic logs.
DIAG-050 Diagnostic log deletion does not alter campaign state.
```

---

# 32. `SLICE-00` implementation scaffold

До закрытия M1 создаются:

```text
Packages/com.odyssey.application/Runtime/Diagnostics/
├── IOdysseyLogger.cs
├── LogLevel.cs
├── EventCode.cs
├── LogEventV1.cs
├── DiagnosticContext.cs
├── SafeLogValue.cs
└── DiagnosticId integration

Assets/Odyssey/Runtime/Diagnostics/
├── OdysseyDiagnosticRuntime.cs
├── UnityConsoleSink.cs
├── JsonLinesFileSink.cs
├── InMemoryRingBufferSink.cs
├── EmergencySink.cs
├── LogQueue.cs
├── LogRotationPolicy.cs
├── CrashMarkerService.cs
├── DiagnosticBundleBuilder.cs
└── DiagnosticOverlayPresenter.cs

config/diagnostics/
├── event-codes.json
└── diagnostics-policy.json

Tests/
├── Odyssey.Tests.Diagnostics/
├── Odyssey.Tests.Security/
├── Odyssey.Tests.Unity.EditMode/
└── Odyssey.Tests.Unity.PlayMode/
```

Точные имена файлов могут меняться без ADR, если ownership и contract сохраняются.

---

# 33. Правила для Codex

Codex обязан:

1. использовать существующий typed EventCode registry;
2. добавлять новый EventCode и allowlist properties в той же задаче;
3. не логировать arbitrary object, DTO, command или event payload;
4. не использовать `ToString()` как redaction;
5. не добавлять full path, username, IP, email, chat или hidden state;
6. не логировать secret даже в Debug/Trace;
7. сохранять CorrelationId через все изменяемые adapters;
8. создавать DiagnosticId только через approved generator/runtime;
9. логировать exception detail только на owning boundary;
10. не добавлять remote telemetry/upload без нового ADR и решения владельца продукта;
11. не использовать diagnostic log как event store или business API;
12. добавлять redaction/security tests для новых property categories;
13. проверять Release/IL2CPP behavior;
14. не отключать failing secret/redaction test ради прохождения CI;
15. не расширять diagnostic bundle wildcard-копированием директорий.

---

# 34. Definition of Done реализации ADR-010

ADR-010 считается реализованным в `SLICE-00`, когда:

1. process-scoped diagnostic runtime создаётся composition root;
2. Domain/Rules не зависят от diagnostics assembly;
3. EventCode registry и typed safe properties работают;
4. memory, rolling JSONL, development Console и emergency sinks работают;
5. queue bounded и имеет проверенную drop/fallback policy;
6. CorrelationId/DiagnosticId проходят через Error/use-case flow;
7. startup/shutdown/crash markers покрыты тестами;
8. retention и rotation работают;
9. diagnostic bundle создаётся локально по allowlist;
10. bundle не содержит campaigns, hidden data, private chat, secrets и закрытую документацию;
11. .NET, Unity Mono и IL2CPP serialization vectors совпадают;
12. Release profile не показывает raw exception details;
13. все DIAG-001–DIAG-050 проходят в применимых контурах;
14. architecture/secret scans блокируют нарушение;
15. документация, registry и CI commands синхронизированы.

---

# 35. Последствия

## 35.1 Положительные

- Ошибки startup, persistence, networking и Unity lifecycle можно связать через correlation.
- Пользователь получает безопасный DiagnosticId вместо stack trace.
- Скрытая информация кампании не должна попадать в support archive.
- Логи остаются machine-readable и пригодными для automated analysis.
- Codex не может свободно расширять payload логов.
- Logger не становится скрытым глобальным service locator.
- Bundle создаётся без remote telemetry и пользователь контролирует передачу.
- Retention ограничивает использование диска.
- Общая модель работает в Mono и IL2CPP.

## 35.2 Отрицательные

- Добавление нового event требует registry и tests.
- Typed builders требуют больше кода, чем свободные строки.
- Некоторые проблемы сложнее диагностировать без raw payload.
- Full stack details требуют отдельного local restricted record.
- Отсутствие автоматической телеметрии увеличивает роль ручного bundle flow.
- Очистка paths/identities может уменьшить точность диагностики, если safe fingerprint не реализован корректно.

## 35.3 Принятый компромисс

Проект предпочитает доказуемую безопасность данных и стабильный diagnostic contract максимальной свободе логирования. Дополнительная диагностическая детализация добавляется через явные allowlisted поля, а не через временное отключение redaction.

---

# 36. Отклонённые альтернативы

## 36.1 Только `Debug.Log`

Отклонено: нет стабильной структуры, ротации, correlation, bundle и безопасной Release policy.

## 36.2 Обязательный внешний logging framework с первого дня

Отклонено: добавляет dependency/license/AOT risk до доказанной необходимости. Adapter может быть добавлен позже без изменения Application contract.

## 36.3 Log4Net/Serilog/NLog types в Application

Отклонено: связывает Core с implementation framework и нарушает portability/dual compilation.

## 36.4 Логировать целые команды и события

Отклонено: раскрывает hidden data, создаёт огромные логи и связывает diagnostics с persisted contracts.

## 36.5 Redaction только при создании bundle

Отклонено: secrets уже окажутся на диске в normal logs.

## 36.6 Хешировать все запрещённые данные

Отклонено: hash не делает малое пространство безопасным и создаёт ненужное tracking correlation.

## 36.7 Unbounded async queue

Отклонено: может исчерпать память при incident storm.

## 36.8 Полностью синхронный file logging

Отклонено: блокирует main thread и удерживает операции на disk latency.

## 36.9 Автоматическая отправка telemetry/crash reports

Отклонено для MVP: требует consent, privacy/security policy, backend и отдельного ADR.

## 36.10 Использовать diagnostic logs как audit/event history

Отклонено: retention и best-effort semantics несовместимы с authority/replay.

## 36.11 Включать campaign database в support bundle

Отклонено: слишком высокий риск скрытых и персональных данных. Отдельный user-approved campaign repair export потребует самостоятельного design.

---

# 37. Не входит в ADR-010

Этот ADR не определяет:

- remote telemetry backend;
- cloud crash reporting;
- support portal;
- user analytics;
- product metrics;
- native memory dumps;
- full security incident response process;
- cryptographic encryption diagnostic bundle;
- automatic email/upload;
- gameplay Game Log design;
- domain audit event semantics;
- final UI visual design diagnostic overlay;
- GDPR/legal retention policy для будущего cloud service.

Эти вопросы требуют отдельных решений при появлении соответствующего scope.

---

# 38. Связь с последующими документами

- `AGENTS.md` должен кратко повторить запреты на secrets/payload/ToString и обязательность EventCode registry.
- `PLANS.md` должен требовать redaction impact для задач, добавляющих данные в logs/bundle.
- Security and Privacy contract должен уточнить incident response и будущую remote processing policy.
- Deployment/Operations contract должен определить release support procedure и хранение полученных пользователем bundles вне приложения.
- UI/UX contract определит внешний вид overlay, crash notice и bundle preview.

---

# 39. Traceability

ADR-010 реализует и уточняет:

- Technical Development Baseline §20;
- ADR-004 DiagnosticId, exception boundaries и safe/public error mapping;
- ADR-005 process scope, startup/shutdown и resource ownership;
- ADR-007 BuildIdentity в diagnostic evidence;
- ADR-008 clock source и secret RNG key prohibition;
- ADR-009 Unity bootstrap, build profiles и IL2CPP validation;
- Test Strategy security, hidden-data и diagnostic bundle scenarios.

Основные implementation evidence:

- EventCode registry;
- architecture dependency report;
- generated redaction test report;
- queue/backpressure tests;
- rotation/retention tests;
- diagnostic bundle manifest fixture;
- .NET/Unity/IL2CPP serialization vectors;
- Player build content scan;
- secret scan report.

---

# 40. Вступление в силу

ADR-010 вступает в силу немедленно после включения в `ACTIVE_DOCUMENTATION_BASELINE`.

Любое изменение, которое:

- разрешает remote telemetry/upload;
- ослабляет классификацию Secret/HiddenGameplay;
- включает campaign payload/database в bundle;
- меняет ownership diagnostic runtime;
- делает внешний logging framework частью Application contract;
- использует diagnostic log как authoritative store;
- снимает обязательную redaction до sink;

требует amendment либо superseding ADR и явного одобрения владельца продукта.

---

**Конец документа**
