# ADR-004 — Result and Error Model

**Документ:** `docs/adr/ADR-004_Result_and_Error_Model_v1.0.md`  
**ADR:** ADR-004  
**Версия:** 1.0  
**Дата:** 27 июля 2026 года  
**Статус:** Accepted  
**Область:** application results, expected failures, command rejections, error codes, safe reason codes, retry directives, validation details, exception boundaries, diagnostic references, transport/UI mapping и security redaction  
**Связанные этапы:** Roadmap Stage 1, `SLICE-00`, Milestone `M1`  
**Базовые документы:** `TECHNICAL_DEVELOPMENT_BASELINE_Odyssey_VTT_v0.1.md`, `ADR-001_Module_Boundaries_and_Dependency_Direction_v1.0.md`, `ADR-002_Command_and_Domain_Event_Model_v1.0.md`, `ADR-003_Serialization_Strategy_v1.0.md`

---

# 1. Решение

Odyssey VTT использует **единый explicit Result/Error contract на Application boundary**, но не заставляет Domain, Rules, transport и UI разделять один физический DTO.

Главные правила:

1. Каждая операция Application, port или adapter, способная ожидаемо завершиться неуспешно, возвращает `Result` или `Result<T>`.
2. `Result<T>` имеет ровно два состояния: `Success` или `Failure`.
3. `Result` без значения является семантическим эквивалентом `Result<Unit>`, а не `bool`.
4. `Failure` всегда содержит типизированный `Error`; `null`, пустая строка, `false` и исключение не являются обычным способом сообщить ожидаемый отказ.
5. Canonical `Result<T>` и `Error` принадлежат `Odyssey.Application`, потому что Infrastructure adapters реализуют Application ports, а Domain/Rules не должны зависеть от Application.
6. Domain и Rules возвращают собственные типизированные decisions/rejections; Application переводит их в `Error` на своей границе.
7. `Error` содержит стабильный полный `ErrorCode`, категорию, безопасный публичный reason, ключ локализации, retry directive, correlation и разрешённые validation details.
8. Полный `ErrorCode` и `SafeReasonCode` являются разными сущностями: внутренний код может быть точным, публичный reason может намеренно скрывать секретную причину.
9. В Error не хранится готовая локализованная строка как authority. Используется `UserMessageKey` и безопасные параметры; UI строит текст на языке пользователя.
10. Исключения не используются для ожидаемых domain, permission, validation, conflict, not-found или compatibility outcomes.
11. Непредвиденное исключение перехватывается только на определённой boundary, регистрируется в diagnostics и преобразуется в безопасный `Internal` error.
12. Stack trace, SQL, абсолютный путь, secret, hidden entity ID, private content и внутренний exception message не попадают в public Result.
13. Retry задаётся не `bool`, а нормативным `RetryDirective`, который описывает требуемое действие перед повтором.
14. `Result<CommandResult>` имеет двухуровневую семантику:
    - outer `Success` означает, что получен авторитетный terminal `CommandResult` (`Accepted`, `Pending` или `Rejected`);
    - outer `Failure` означает, что terminal command outcome не удалось надёжно получить или зафиксировать.
15. `Rejected` command содержит `Error`, но не считается outer infrastructure failure.
16. При outer failure клиент повторяет ту же команду с тем же `CommandId` только если `RetryDirective` это разрешает.
17. Частичный успех batch-операции моделируется успешным типизированным report с per-item outcomes, а не третьим состоянием общего `Result`.
18. Error codes имеют стабильный lowercase dot-separated формат и никогда не переиспользуются с новым смыслом.
19. Transport, persistence и UI используют собственные DTO/view models и явно отображают Application Error; прямой serializer reuse запрещён.
20. Все mappings, retry semantics, redaction и exception boundaries покрываются автоматическими тестами в `SLICE-00`.

Этот ADR является нормативным authority по Result/Error vocabulary и обработке ожидаемых/неожиданных неуспехов. Он уточняет Technical Development Baseline и ADR-002 в пределах указанной области.

---

# 2. Контекст и проблема

Odyssey VTT содержит несколько типов операций:

- игровая команда может быть принята, приостановлена или отклонена;
- permission check может доказать Allow или Deny;
- Rules Engine может определить rule violation;
- SQLite transaction может временно не получить блокировку;
- campaign database может оказаться повреждённой;
- JSON contract может иметь неподдерживаемую версию;
- relay может быть временно недоступен;
- пользователь может отменить импорт;
- batch import может завершиться частично;
- UI может получить устаревшую revision;
- IL2CPP serializer может столкнуться с отсутствующим generated metadata;
- programmer invariant может быть нарушен из-за дефекта.

Без единого контракта Codex может реализовать разные способы сообщения одной и той же ситуации:

- `null` означает not found;
- `false` означает permission denied;
- `InvalidOperationException` означает state conflict;
- строка содержит техническое сообщение SQLite;
- network adapter возвращает numeric code;
- UI самостоятельно распознаёт текст ошибки;
- retry выполняется для любого failure;
- batch с одним плохим файлом считается полностью failed;
- скрытая сущность раскрывается через точный `NotFound`;
- application error сериализуется вместе со stack trace;
- command rejection смешивается с отсутствием durable command outcome.

Такая модель приводит к:

- несовместимым API;
- ошибочным автоматическим повторам;
- двойным эффектам;
- утечкам скрытой информации;
- невозможности локализации;
- невозможности стабильных тестов;
- привязке UI к инфраструктурным деталям;
- сложной диагностике;
- случайной обработке дефекта как нормального игрового отказа;
- разным решениям Codex в соседних vertical slices.

ADR-004 устраняет эту неоднозначность до реализации первых handlers, ports и adapters.

---

# 3. Движущие факторы

Решение оптимизировано под:

1. Однозначные сигнатуры Application operations.
2. Host-authoritative command semantics ADR-002.
3. Безопасный retry после crash, timeout и transport loss.
4. Стабильные machine-readable error codes.
5. Локализованный UI без business logic по строкам.
6. Redaction hidden/GM-only контекста.
7. Разделение expected failure и programming defect.
8. Возможность автоматического тестирования без Unity UI.
9. Явную трансляцию ошибок между слоями.
10. Отсутствие зависимости Domain/Rules от Application.
11. Поддержку partial-success reports.
12. Различение user action, refresh, reconnect, upgrade и manual recovery.
13. Диагностику через `CorrelationId`/`DiagnosticId`.
14. Стабильность публичных контрактов между версиями.
15. Простые обязательные правила для Codex.

---

# 4. Термины

## 4.1 Result

Типизированный итог одной операции Application или port.

`Result<T>` либо содержит `T Value`, либо `Error Error`.

## 4.2 Expected failure

Предсказуемый неуспех, являющийся частью нормального контракта операции:

- validation failed;
- permission denied;
- rule violation;
- object not found;
- revision conflict;
- unsupported version;
- capacity reached;
- transient service unavailable.

Expected failure возвращается как `Result.Failure` или domain-specific rejection, но не через generic exception.

## 4.3 Error

Application-owned immutable описание неуспеха, пригодное для machine handling и безопасного отображения после mapping.

## 4.4 ErrorCode

Полный стабильный machine-readable код причины.

Пример:

```text
permissions.action.denied
board.token.not_found
board.token.revision_conflict
persistence.campaign.corrupted
serialization.contract.unsupported_version
network.session.transport_unavailable
```

## 4.5 SafeReasonCode

Безопасный публичный код, который можно передать конкретной аудитории без раскрытия скрытого контекста.

Несколько внутренних `ErrorCode` могут отображаться в один `SafeReasonCode`.

## 4.6 UserMessageKey

Ключ локализации безопасного пользовательского сообщения.

Он не является `ErrorCode` и не должен использоваться для программной логики.

## 4.7 RetryDirective

Нормативное указание, можно ли и при каком условии повторять операцию.

## 4.8 ValidationDetail

Структурированное описание отдельного нарушения входного контракта или формы.

## 4.9 DiagnosticId

Непрозрачный идентификатор внутренней диагностической записи. Пользователь может сообщить его поддержке, но не получает внутреннее содержимое записи.

## 4.10 Command rejection

Авторитетный `CommandResult.Status = Rejected`, сохранённый согласно ADR-002. Это terminal command outcome, а не отсутствие ответа системы.

## 4.11 Infrastructure failure

Неуспех выполнения port/adapter или получения durable outcome. Он может быть transient или permanent и возвращается outer `Result.Failure`.

## 4.12 Programmer defect

Нарушение инварианта реализации, которое не является допустимым пользовательским исходом. Оно может породить exception, diagnostic и safe internal error на boundary.

---

# 5. Ownership и границы модулей

## 5.1 Odyssey.Domain

Domain владеет:

- domain-specific rejection/decision types;
- invariant violation semantics;
- rule-neutral value validation;
- стабильными domain reason identifiers, если они являются частью предметной области.

Domain не зависит от:

- `Odyssey.Application.Result`;
- `UserMessageKey`;
- retry policy инфраструктуры;
- UI localization;
- SQLite/relay/HTTP codes;
- diagnostic logger.

Domain не формирует пользовательский текст.

## 5.2 Odyssey.Rules

Rules возвращает типизированные calculation/validation decisions.

Rules не должен:

- бросать exception для обычного промаха, недостатка ресурса или недопустимой цели;
- создавать Application Error напрямую;
- выбирать сетевой/public SafeReasonCode;
- логировать private calculation input в публичный канал.

## 5.3 Odyssey.Content

Content возвращает типизированные validation reports и resolution failures.

Content package validation может содержать множество `ValidationDetail`, но не произвольные exception strings.

## 5.4 Odyssey.Application

Application владеет:

- `Result`;
- `Result<T>`;
- `Unit`;
- `Error`;
- `ErrorCode`;
- `ErrorCategory`;
- `SafeReasonCode` application vocabulary;
- `RetryDirective`;
- `ValidationDetail`;
- mapping domain/rules/content decisions в Error;
- mapping infrastructure failures в application-safe errors;
- command gateway outer result.

Application определяет public semantics, но не локализует текст.

## 5.5 Odyssey.Persistence

Persistence:

- реализует Application ports;
- возвращает `Result<T>` контрактов port;
- преобразует SQLite/filesystem/crypto/archive failures в application-owned Error codes;
- сохраняет provider/driver detail только в internal diagnostics;
- не возвращает `SqliteException`, raw SQL или абсолютный путь вызывающему слою.

## 5.6 Odyssey.Networking

Networking:

- преобразует Application Error в audience-safe transport DTO;
- не отправляет internal `ErrorCode`, если он раскрывает секрет;
- не использует numeric transport status как authoritative domain reason;
- сохраняет `CorrelationId` и разрешённый `DiagnosticId`;
- различает terminal `Rejected` и transient delivery failure.

## 5.7 Odyssey.Unity.Client

Unity Client:

- отображает `UserMessageKey` через localization catalog;
- выбирает UX action по `RetryDirective` и feature context;
- не анализирует raw exception text;
- не определяет права или доменную причину самостоятельно;
- может показать `DiagnosticId` в расширенной информации;
- не показывает stack trace обычному пользователю.

---

# 6. Нормативная модель Result

## 6.1 Result<T>

Conceptual contract:

```text
Result<T>
├── IsSuccess
├── Value?        # только Success
└── Error?        # только Failure
```

C# implementation должна обеспечивать инвариант конструкторами/factories, а не соглашением пользователя.

Допустимые factories:

```text
Result<T>.Success(value)
Result<T>.Failure(error)
Result.Success()
Result.Failure(error)
```

Точные C# names могут быть уточнены implementation review, но семантика этого ADR обязательна.

## 6.2 Инварианты

1. Success не содержит Error.
2. Failure не содержит Value.
3. `Error` в Failure не может быть null/default.
4. Success value не может быть null, если `T` не объявлен nullable.
5. Доступ к Value у Failure должен быть невозможен или явно ошибочен.
6. Доступ к Error у Success должен быть невозможен или явно ошибочен.
7. `Result<T>` immutable.
8. `Result<T>` не сериализуется как универсальный persistence/domain DTO.
9. Implicit conversion из `bool`, `string`, `Exception` и arbitrary `T` запрещена.
10. `default(Result<T>)` не должен считаться valid success/failure.

## 6.3 Result без значения

Операция без полезного payload возвращает `Result`, семантически равный `Result<Unit>`.

Запрещено использовать:

- `bool` без Error;
- `Task` с expected exception;
- `null`;
- empty collection как код неуспеха.

## 6.4 Асинхронные операции

Асинхронная expected-failure операция возвращает:

```text
Task<Result<T>>
```

или эквивалентный поддерживаемый async abstraction.

`Task` faulted state не используется для обычного expected failure.

## 6.5 Коллекции

Пустая коллекция является успешным результатом, если контракт допускает отсутствие элементов.

`NotFound` не кодируется автоматически пустым списком, если операция запрашивает один обязательный объект.

## 6.6 Try-паттерн

Низкоуровневый pure parsing может использовать стандартный `TryParse` pattern, если:

- он локален модулю;
- не пересекает Application/port boundary;
- caller немедленно преобразует failure в typed decision/Error;
- потеря причины не мешает требованиям.

На Application boundary используется `Result<T>`.

---

# 7. Модель Error

## 7.1 Conceptual contract

```text
Error
├── Code: ErrorCode
├── Category: ErrorCategory
├── SafeReasonCode
├── UserMessageKey
├── SafeMessageArguments[]
├── RetryDirective
├── CorrelationId
├── ValidationDetails[]
├── DiagnosticId?
└── Metadata[]?              # только allowlisted machine-safe metadata
```

`Error` immutable.

## 7.2 Что не входит в Error

Public/application Error не содержит:

- `Exception` instance;
- stack trace;
- raw provider message;
- SQL query;
- connection string;
- absolute file path;
- secret/token/key;
- private message content;
- hidden entity payload;
- unrestricted dictionary/object;
- localized ready-made authority string;
- arbitrary rejected user value;
- runtime CLR type name.

Internal diagnostic record может хранить разрешённую часть этих данных согласно ADR-010 и privacy rules, но это другой объект.

## 7.3 Error immutability

После формирования Error его Code/Category/retry/public meaning не меняются.

Mapping на transport/UI создаёт новый DTO/view model, а не модифицирует исходный Error.

## 7.4 Primary error

Один `Result.Failure` имеет один primary Error.

Множественные нарушения формы хранятся в `ValidationDetails[]` этого Error.

Независимые per-item outcomes batch-а хранятся в typed report, а не в массиве несвязанных primary errors.

---

# 8. ErrorCategory vocabulary

Нормативные категории:

```text
Validation
Authorization
RuleViolation
NotFound
Conflict
Precondition
Capacity
Compatibility
Integrity
TransientInfrastructure
PermanentInfrastructure
Cancelled
Security
Internal
```

## 8.1 Validation

Вход не соответствует контракту формы/типа/диапазона до доменного выполнения.

Примеры:

- обязательное поле отсутствует;
- координата не finite;
- размер файла превышен;
- JSON shape invalid.

## 8.2 Authorization

Actor не имеет разрешения на действие или не может доказать право.

Публичная причина не раскрывает скрытый target.

## 8.3 RuleViolation

Вход валиден, но игровое/предметное правило не разрешает результат.

Примеры:

- недостаточно movement;
- ресурс недоступен;
- цель недопустима;
- действие запрещено текущим состоянием боя.

## 8.4 NotFound

Запрашиваемая открытая/разрешённая сущность отсутствует.

Для hidden entity NotFound может быть отображён как более общий `TargetUnavailable`.

## 8.5 Conflict

Запрос конфликтует с новой authoritative revision/sequence/lease/lock.

## 8.6 Precondition

Не выполнена предварительная предпосылка workflow.

Примеры:

- session not active;
- approval required;
- interaction expired;
- campaign not loaded.

## 8.7 Capacity

Превышен явный лимит:

- users;
- active playbacks;
- archive size;
- queued operations;
- memory-safe parser limit.

## 8.8 Compatibility

Версия, формат, protocol, ruleset или content contract не поддерживается.

## 8.9 Integrity

Данные повреждены, checksum не совпадает или authoritative invariants persisted data нарушены.

## 8.10 TransientInfrastructure

Временная инфраструктурная причина, при которой повтор может быть безопасен после указанного условия.

Примеры:

- temporary SQLite busy;
- relay unavailable;
- temporary filesystem lock;
- timeout без доказанного terminal outcome.

## 8.11 PermanentInfrastructure

Инфраструктурная операция не может быть завершена без изменения среды/конфигурации.

Примеры:

- permission denied by OS;
- disk path invalid;
- required package missing;
- unsupported graphics/runtime capability.

## 8.12 Cancelled

Операция остановлена по явной отмене caller/user/system lifecycle.

Timeout не считается автоматически Cancelled.

## 8.13 Security

Обнаружено нарушение security boundary или подозрительная форма запроса.

Обычная rule validation failure не классифицируется как Security только потому, что запрос пришёл по сети.

## 8.14 Internal

Непредвиденный defect или invariant failure реализации.

Internal Error всегда сопровождается DiagnosticId, если diagnostics storage доступен.

---

# 9. ErrorCode

## 9.1 Формат

Нормативный формат:

```text
<area>.<subject>.<condition>
```

Все сегменты:

- lowercase ASCII;
- разделены точкой;
- внутри сегмента допускается underscore;
- не содержат пробелов;
- не содержат версии приложения;
- не содержат numeric HTTP/SQLite/provider code как смысл.

Примеры:

```text
validation.command.payload_invalid
permissions.action.denied
board.token.not_found
board.token.revision_conflict
combat.action.insufficient_resource
persistence.campaign.corrupted
persistence.transaction.temporarily_locked
serialization.contract.unsupported_version
network.session.protocol_unsupported
network.session.transport_unavailable
audio.import.file_unsupported
audio.playback.capacity_reached
application.operation.cancelled
application.internal.unexpected
```

## 9.2 Stability

ErrorCode:

- не переименуется без compatibility review;
- не переиспользуется с новым смыслом;
- после удаления остаётся reserved/deprecated;
- не зависит от текста сообщения;
- не зависит от класса exception;
- тестируется как публичный machine contract, если пересекает boundary.

## 9.3 Ownership

Код принадлежит модулю/подсистеме, чей контракт определяет смысл.

Infrastructure adapter не создаёт код в namespace другого модуля без mapping.

Пример:

```text
SQLite BUSY
    ↓ Persistence mapping
persistence.transaction.temporarily_locked
```

## 9.4 Registry

После создания репозитория ведётся:

```text
docs/errors/ERROR_CODES.md
```

Registry содержит минимум:

- Code;
- Owner module;
- Category;
- SafeReasonCode default;
- RetryDirective default;
- Introduced version;
- Deprecated/reserved status;
- security notes;
- test reference.

Codex не добавляет новый ErrorCode без обновления registry и тестов.

## 9.5 Нельзя программировать по сообщению

Запрещено:

```text
if message contains "not found"
if exception.Message == ...
if localized text == ...
```

Machine logic использует ErrorCode, Category или RetryDirective.

---

# 10. SafeReasonCode

## 10.1 Назначение

SafeReasonCode является public/audience-safe reason.

Он может быть:

- более общим, чем ErrorCode;
- одинаковым для нескольких внутренних причин;
- различным для GM и Player projection;
- специфичным для подсистемы, если это безопасно и полезно UX.

## 10.2 Базовый vocabulary

Core-safe reasons:

```text
InvalidRequest
PermissionDenied
ActionNotAllowed
TargetUnavailable
StateChanged
ResourceUnavailable
CapacityReached
ApprovalRequired
InteractionExpired
VersionUnsupported
UpdateRequired
DataCorrupted
ServiceUnavailable
OperationTimedOut
OperationCancelled
ManualRecoveryRequired
UnexpectedError
```

Специализированные безопасные причины, уже определённые subsystem contracts, допустимы:

```text
MembershipInactive
SceneUnavailable
ObjectLocked
InsufficientMovement
ProtocolVersionUnsupported
```

Они должны быть зарегистрированы и не раскрывать hidden state.

## 10.3 Security collapse

Пример:

```text
Internal ErrorCode:
board.hidden_token.not_visible
board.token.not_found
permissions.object.view_denied

Public SafeReasonCode for ordinary Player:
TargetUnavailable
```

GM может получить более точную безопасную проекцию, если permissions contract это разрешает.

## 10.4 SafeReasonCode не заменяет ErrorCode

- ErrorCode нужен диагностике, tests и internal mapping.
- SafeReasonCode нужен UI/network audience.
- UserMessageKey нужен локализованному тексту.

Эти поля не взаимозаменяемы.

---

# 11. Пользовательские сообщения и локализация

## 11.1 UserMessageKey

Формат рекомендуется:

```text
errors.<area>.<subject>.<condition>
```

Примеры:

```text
errors.permissions.action_denied
errors.board.target_unavailable
errors.persistence.campaign_corrupted
errors.network.service_unavailable
errors.application.unexpected
```

## 11.2 SafeMessageArguments

Разрешены только allowlisted безопасные примитивы:

- локализуемый enum/reference key;
- число/лимит;
- duration;
- уже известное пользователю display name;
- публичная версия;
- безопасный action label.

Запрещены:

- hidden IDs;
- file system absolute paths;
- secret modifier names;
- private roll values;
- tokens/keys;
- exception messages;
- arbitrary object serialization.

## 11.3 Конкретная причина

Требование `PR-UX-005` выполняется настолько конкретно, насколько это безопасно.

При конфликте между UX-конкретностью и secrecy/privacy побеждает secrecy/privacy.

Пример:

```text
Безопасно:
"Этот токен уже изменён. Обновите сцену и повторите действие."

Небезопасно:
"Токен SecretBoss был перемещён GM в скрытую сцену."
```

## 11.4 Возможное действие пользователя

User-facing mapping должен по возможности указывать действие, соответствующее RetryDirective:

- обновить состояние;
- переподключиться;
- повторить позже;
- выбрать другой файл;
- обновить приложение;
- обратиться к GM;
- восстановить backup;
- открыть diagnostics.

## 11.5 Fallback

Если localization key отсутствует:

- UI показывает безопасный общий fallback;
- записывает diagnostic defect;
- не показывает key как единственный текст обычному пользователю;
- test/build должен обнаружить отсутствующий обязательный key.

---

# 12. RetryDirective

## 12.1 Vocabulary

Нормативные значения:

```text
DoNotRetry
RetrySameRequest
RetryWithBackoff
RefreshStateThenRetry
ReconnectThenRetry
UserActionRequired
UpgradeRequired
ManualRecoveryRequired
```

## 12.2 DoNotRetry

Автоматический повтор запрещён.

Примеры:

- permission denied;
- invalid payload;
- deterministic rule violation;
- unsupported permanent operation.

Пользователь может создать новое намерение после изменения условий, но это не retry того же request автоматически.

## 12.3 RetrySameRequest

Разрешён немедленный безопасный повтор **того же operation identity**.

Для команды используется тот же `CommandId`.

Применяется только когда система гарантирует идемпотентность и transient причина исчезла или могла быть ложной transport failure.

## 12.4 RetryWithBackoff

Разрешён повтор после bounded backoff/jitter.

Точные интервалы задаёт subsystem/networking policy, а не Error object.

## 12.5 RefreshStateThenRetry

Нужно получить актуальный snapshot/delta/revision, затем пользователь или UI создаёт корректный повтор.

Для изменившегося намерения может требоваться новый CommandId. Старый CommandId нельзя отправлять с изменённым payload согласно ADR-002.

## 12.6 ReconnectThenRetry

Нужно восстановить session/transport identity, затем повторить тот же безопасный operation identity, если terminal outcome неизвестен.

## 12.7 UserActionRequired

Повтор бессмысленен, пока пользователь не изменит вход или окружение.

Примеры:

- выбрать существующий файл;
- освободить место;
- запросить разрешение;
- завершить pending interaction.

## 12.8 UpgradeRequired

Требуется обновление приложения, protocol, ruleset или content package.

Автоматический retry без upgrade запрещён.

## 12.9 ManualRecoveryRequired

Требуется GM/operator recovery:

- восстановить backup;
- выбрать корректную campaign copy;
- выполнить repair/export workflow;
- обратиться к поддержке с DiagnosticId.

## 12.10 Retry не является обещанием успеха

RetryDirective определяет безопасный способ повтора, но не гарантирует, что причина исчезнет.

## 12.11 UI policy

UI может скрыть кнопку `Retry`, если feature context не позволяет действие, но не может ослабить directive и автоматически повторять `DoNotRetry`.

---

# 13. ValidationDetail

## 13.1 Contract

```text
ValidationDetail
├── FieldPath?
├── Code
├── UserMessageKey
├── SafeMessageArguments[]
└── Severity              # Error / Warning только для report contexts
```

В `Result.Failure` blocking details имеют severity Error.

## 13.2 FieldPath

FieldPath является стабильным contract/UI path, например:

```text
payload.destination.x
manifest.assets[3].relative_path
character.attributes.strength
```

Он не является C# reflection path или SQL column name.

## 13.3 Rejected values

Raw rejected value не включается по умолчанию.

Если значение безопасно и необходимо UX, используется allowlisted sanitized representation с size limit.

## 13.4 Множественная валидация

Parser/form validation может вернуть все безопасно обнаруженные независимые нарушения за один проход.

После security-sensitive или structural fatal violation parser может остановиться раньше.

## 13.5 Warnings

Warnings не превращают `Result` в Failure сами по себе.

Операции preview/import/publish могут возвращать успешный report с warnings.

---

# 14. CommandResult и двухуровневая модель

## 14.1 Gateway contract

Conceptual signature:

```text
Result<CommandResult> Submit(Command command)
```

## 14.2 Outer Success + Accepted

Означает:

- authoritative mutation committed;
- durable receipt существует;
- события/sequence range зафиксированы.

## 14.3 Outer Success + Pending

Означает:

- pending state committed;
- исходная команда terminal;
- continuation использует новый CommandId.

## 14.4 Outer Success + Rejected

Означает:

- deterministic rejection authoritatively обработан;
- gameplay state не изменён;
- durable rejection receipt существует;
- `CommandResult.Error` объясняет rejection безопасно.

Это **не** outer `Result.Failure`.

## 14.5 Outer Failure

Означает, что система не может подтвердить authoritative terminal outcome вызывающему слою.

Примеры:

- database transaction не началась/не завершилась;
- durable receipt не удалось сохранить;
- transport оборвался до получения ответа;
- host unavailable;
- unexpected exception на boundary.

Клиент следует `RetryDirective` outer Error.

## 14.6 Same CommandId

При неизвестном terminal outcome и разрешённом retry повторяется тот же `CommandId` и тот же semantic payload.

Новый CommandId создаст новое намерение и может привести к повторному эффекту.

## 14.7 Rejected error fields

`CommandResult` для Rejected содержит минимум:

```text
ErrorCode
ErrorCategory
SafeReasonCode
UserMessageKey
SafeMessageArguments[]
RetryDirective
ValidationDetails[]
CorrelationId
DiagnosticId?
```

Accepted/Pending не содержат rejection Error.

## 14.8 Причины Rejected

Обычные deterministic rejections:

- Authorization;
- RuleViolation;
- NotFound/TargetUnavailable;
- Conflict;
- Precondition;
- Validation после command admission;
- Capacity, если лимит является authoritative domain/session policy.

Infrastructure error не маскируется под Rejected, если terminal rejection receipt не может быть durably зафиксирован.

---

# 15. Domain/Rules decision mapping

## 15.1 Domain-specific decisions

Domain/Rules используют типизированные варианты, например:

```text
MoveTokenDecision
├── Allowed(proposedEvents)
└── Denied(MoveTokenRejection)

MoveTokenRejection
├── InsufficientMovement
├── DestinationBlocked
├── TokenNotControllable
└── RevisionChanged
```

Точная форма определяется feature contract.

## 15.2 Application mapping

Application mapping:

- выбирает ErrorCode;
- выбирает ErrorCategory;
- применяет audience-safe SafeReasonCode;
- назначает UserMessageKey;
- назначает RetryDirective;
- сохраняет CorrelationId.

## 15.3 Не терять семантику

Запрещено преобразовывать все domain rejections в один `InvalidOperation`.

## 15.4 Не протаскивать UI в Domain

Domain rejection не содержит:

- локализованный текст;
- цвет notification;
- кнопку UI;
- сетевой status;
- DiagnosticId.

---

# 16. Infrastructure mapping

## 16.1 Provider detail

Adapter может получить:

```text
SQLite code
IOException
Socket error
relay response
archive parser error
OS permission error
```

Adapter обязан:

1. классифицировать ожидаемую причину;
2. создать стабильный Application ErrorCode;
3. определить RetryDirective;
4. записать provider detail во внутреннюю diagnostics;
5. вернуть безопасный Error вызывающему слою.

## 16.2 Не leaking provider codes

Provider numeric code может быть diagnostic metadata, но не становится единственным публичным ErrorCode.

## 16.3 Mapping completeness

Каждый adapter имеет тесты для:

- known transient failures;
- known permanent failures;
- cancellation;
- timeout;
- unknown exception fallback.

## 16.4 Unknown provider error

Unknown provider error mapping:

```text
Category: Internal или TransientInfrastructure по доказанному контексту
SafeReasonCode: UnexpectedError или ServiceUnavailable
RetryDirective: conservative
DiagnosticId: required when possible
```

Нельзя предполагать retryability без доказательства.

---

# 17. Exception policy

## 17.1 Expected failures не являются exceptions

Запрещено бросать generic exception для:

- permission denied;
- invalid target;
- insufficient resource;
- not found;
- revision conflict;
- unsupported user file;
- version mismatch;
- capacity reached;
- ordinary network unavailable;
- expected cancellation.

## 17.2 Когда exception допустим

Exception допустим для:

- programmer invariant violation;
- impossible state;
- defect в mapping/serializer registration;
- unexpected provider failure;
- runtime/system failure, который API выражает exception и boundary ещё не преобразовала его.

## 17.3 Boundary catch

`catch (Exception)` допускается только на утверждённых outer boundaries:

- Application command/query gateway;
- background job runner;
- Unity entrypoint/update callback boundary;
- network message dispatch boundary;
- persistence adapter public port boundary;
- import/export top-level boundary;
- process/build/bootstrap boundary.

Внутренние Domain/Rules methods не должны catch-all и превращать defect в rule rejection.

## 17.4 Boundary behavior

Boundary:

1. не оставляет partial authoritative state;
2. создаёт DiagnosticId;
3. пишет internal exception diagnostics;
4. возвращает безопасный Internal/Infrastructure Error;
5. не подтверждает terminal command outcome без durable receipt;
6. сохраняет CorrelationId;
7. уважает cancellation отдельно.

## 17.5 Не swallow

Запрещено:

```text
catch { return success; }
catch { return null; }
catch (Exception) { log warning and continue authoritative mutation; }
```

## 17.6 Exception message

`Exception.Message` не является UserMessageKey, ErrorCode или SafeReasonCode.

## 17.7 AggregateException

Aggregate/multiple exception разворачивается для diagnostics, но public Result получает один primary safe Error.

---

# 18. Cancellation, timeout и abandonment

## 18.1 Caller cancellation

Явная отмена до commit:

```text
Category: Cancelled
SafeReasonCode: OperationCancelled
RetryDirective: DoNotRetry или UserActionRequired по контексту
```

Gameplay mutation не должна применяться частично.

## 18.2 Cancellation после commit

Если authoritative commit уже завершён, cancellation ожидания клиента не отменяет факт.

Command receipt/reconnect возвращает committed outcome.

## 18.3 Timeout

Timeout не доказывает, что операция не завершилась.

Для command gateway:

- terminal outcome считается unknown;
- retry использует тот же CommandId после reconnect/backoff;
- UI не создаёт новый CommandId автоматически.

## 18.4 Background abandonment

Background operation не может просто исчезнуть.

Она должна иметь:

- cancellation state;
- durable job state, если требуется recovery;
- Result/report;
- diagnostic при unexpected termination.

---

# 19. Partial success и batch operations

## 19.1 Нет третьего состояния общего Result

Общий `Result<T>` не получает состояния `PartialSuccess`.

## 19.2 Typed report

Пример:

```text
Result<ImportBatchReport>

ImportBatchReport
├── BatchId
├── CompletedAt
├── Candidates[]
│   ├── SourceDisplayName
│   ├── Status: Imported / Skipped / Failed
│   ├── AssetId?
│   ├── WarningCodes[]
│   └── Error?
└── Summary
```

## 19.3 Outer Success

Outer Success означает, что batch workflow завершён контролируемо и полный report достоверен, даже если отдельные items Failed.

## 19.4 Outer Failure

Outer Failure означает:

- batch не удалось начать;
- report не может быть надёжно сформирован;
- authoritative transaction/report storage нарушена;
- unexpected fatal failure прервал contract.

## 19.5 Per-item Error

Per-item Error использует те же ErrorCode/category/safe rules, но может иметь item-local Correlation/Diagnostic reference.

## 19.6 Atomic batch

Если конкретная операция по контракту атомарна, любой item failure приводит к outer Failure или typed rejected outcome без частичного commit.

Partial-success policy задаётся feature contract, не выводится автоматически из Result model.

---

# 20. NotFound, скрытые сущности и enumeration safety

## 20.1 Открытая сущность

Если actor имеет право знать о сущности, допустим точный:

```text
ErrorCode: board.token.not_found
SafeReasonCode: TargetUnavailable или конкретный safe NotFound reason
```

## 20.2 Скрытая сущность

Если точный ответ раскроет существование hidden entity:

- internal ErrorCode может отражать permission/hidden reason;
- public SafeReasonCode схлопывается в `TargetUnavailable`/`ActionNotAllowed`;
- UserMessage не подтверждает существование;
- timing/metadata не должны намеренно различаться без необходимости.

## 20.3 GM diagnostics

GM получает подробность только если permissions/audit contract разрешает её для данного scope.

## 20.4 Search/list operations

Filtered list обычно возвращает Success с разрешёнными элементами, а не Error за каждый скрытый элемент.

---

# 21. Conflict и optimistic concurrency

## 21.1 Revision conflict

Типичный mapping:

```text
Code: board.token.revision_conflict
Category: Conflict
SafeReasonCode: StateChanged
RetryDirective: RefreshStateThenRetry
```

## 21.2 Sequence conflict

Client должен получить authoritative resync instruction, а не повторять stale payload бесконечно.

## 21.3 Locks/leases

Temporary lock может быть:

- `Conflict + RefreshStateThenRetry`;
- `Precondition + UserActionRequired`;
- `TransientInfrastructure + RetryWithBackoff`;

Выбор зависит от владельца lock и контракта.

## 21.4 Не скрывать конфликт как NotFound

Если actor знает объект и проблема в revision, необходимо использовать conflict reason, чтобы UX мог обновиться.

---

# 22. Compatibility и migration errors

## 22.1 Unsupported future version

```text
Category: Compatibility
SafeReasonCode: VersionUnsupported или UpdateRequired
RetryDirective: UpgradeRequired
```

## 22.2 Corrupted data

```text
Category: Integrity
SafeReasonCode: DataCorrupted
RetryDirective: ManualRecoveryRequired
```

## 22.3 Migration preview

Ожидаемые mapping conflicts возвращаются typed preview report, а не exception.

## 22.4 Migration transaction failure

Infrastructure failure откатывает migration и возвращает Error; исходная рабочая копия не заменяется.

## 22.5 Unknown ErrorCode из будущей версии

Transport/UI должен:

- сохранить generic safe category/reason;
- показать безопасный fallback;
- не падать при неизвестном optional code;
- блокировать mutation только если protocol/contract compatibility требует.

---

# 23. Diagnostic model

## 23.1 CorrelationId

Каждый Error на Application boundary имеет CorrelationId.

Он связывает:

- command;
- transaction;
- network request;
- log entries;
- import report;
- user-visible error.

## 23.2 DiagnosticId

DiagnosticId создаётся, когда существует отдельная internal diagnostic record.

Он особенно обязателен для:

- Internal;
- Integrity;
- unknown infrastructure failure;
- repeated transport failure;
- migration failure;
- crash recovery anomaly.

## 23.3 Public display

UI может показать:

```text
Код диагностики: ABCD-1234
```

Он не должен раскрывать internal sequence/path/host secret.

## 23.4 Internal diagnostic record

Точная схема будет определена ADR-010, но должна уметь хранить:

- DiagnosticId;
- CorrelationId;
- timestamp;
- module/operation;
- ErrorCode;
- exception type/stack для internal use;
- provider code;
- build/schema/protocol versions;
- redacted context;
- occurrence count.

## 23.5 Privacy

Diagnostics не содержит автоматически:

- private messages;
- secret GM notes;
- authentication token;
- full campaign asset content;
- raw passwords/keys;
- неограниченный user input.

## 23.6 User export

Diagnostic archive export использует explicit consent и redaction согласно Product Requirements.

---

# 24. Mapping на transport

## 24.1 Transport DTO

Пример:

```text
ErrorDto
├── SafeReasonCode
├── UserMessageKey
├── SafeMessageArguments[]
├── RetryDirective
├── CorrelationId
├── ValidationDetails[]
└── DiagnosticId?
```

Полный internal ErrorCode включается только если protocol contract считает его безопасным и необходимым.

## 24.2 Audience projection

Один Application Error может иметь разные safe projections:

- issuing Player;
- Main GM;
- Assistant GM;
- Observer;
- local host diagnostics.

## 24.3 Transport status

Transport status/frame type может отражать delivery category, но не заменяет ErrorDto.

## 24.4 Unknown DTO fields/codes

Поведение соответствует ADR-003 versioning rules.

## 24.5 No raw exception

Network adapter никогда не сериализует exception object/stack/runtime type.

---

# 25. Mapping на UI

## 25.1 View model

UI создаёт `ErrorPresentation` или аналогичный view model:

```text
TitleKey
BodyKey
Arguments
ActionKind
CanDismiss
CanRetry
DiagnosticId?
SeverityPresentation
```

Это UI type, не Application Error.

## 25.2 ActionKind

UI action выводится из RetryDirective и feature context:

- None;
- Retry;
- Refresh;
- Reconnect;
- OpenSettings;
- SelectFile;
- UpdateApplication;
- OpenRecovery;
- ContactGM;
- CopyDiagnosticId.

## 25.3 Notifications

Не каждый Error требует modal dialog.

UI выбирает toast/inline/modal/report по контексту, но не меняет semantic category.

## 25.4 Duplicate errors

Повторяющиеся transient errors могут агрегироваться визуально, но каждый authoritative operation outcome остаётся трассируемым.

## 25.5 Accessibility

Ошибка передаётся текстом и структурой; цвет/звук не являются единственным индикатором.

---

# 26. Queries и read operations

## 26.1 Query result

Application query возвращает `Result<TProjection>`.

## 26.2 Empty vs not found

- list query может успешно вернуть пустой список;
- required single query возвращает NotFound, если actor имеет право знать;
- hidden single query использует safe projection.

## 26.3 Stale cache

Локальный cache miss не равен authoritative NotFound. Adapter может запросить host/refresh либо вернуть transient/local cache Error.

## 26.4 Diagnostics query

Ordinary client не может получить internal Error через diagnostic query без permission.

---

# 27. Background jobs, imports и audio

## 27.1 Job lifecycle

Background job имеет explicit status и terminal Result/report.

## 27.2 Audio isolation

Локальная ошибка конкретного аудиофайла:

- не становится gameplay command failure;
- создаёт audio-specific per-item/playback Error;
- может запустить fallback по Audio contract;
- не останавливает сессию целиком.

## 27.3 Import batch

Следует partial-success model §19.

## 27.4 Progress

Progress update не является Result и не объявляет terminal success.

## 27.5 Retry

Повтор item import создаёт новую item operation identity, если предыдущий item outcome terminal и вход изменён.

---

# 28. Security rules

## 28.1 Fail closed

Если mapping не может доказать безопасный reason/message projection, используется более общий safe Error.

## 28.2 No secret metadata

`Metadata` имеет allowlist по ErrorCode. Arbitrary dictionary запрещён.

## 28.3 Size limits

Error payload имеет ограничения:

- количество ValidationDetails;
- длина arguments;
- длина field paths;
- число metadata entries;
- размер diagnostic/public text.

Точные числа определяются implementation constants и тестируются.

## 28.4 Untrusted ErrorDto

Клиент не доверяет ErrorDto как authoritative mutation instruction. Он используется для UX/retry, а состояние приходит через authoritative snapshots/deltas/results.

## 28.5 Malicious classification

Repeated invalid requests не считаются автоматически атакой. Security classification выполняется отдельной policy с evidence.

## 28.6 Log injection

Untrusted strings sanitizes/structured-logged; они не конкатенируются в raw log lines без escaping.

---

# 29. API design rules для Codex

Codex обязан:

1. Использовать `Result/Result<T>` на Application/port boundaries.
2. Не возвращать `null`, `false` или строку вместо Error.
3. Не использовать exception для expected failure.
4. Добавлять ErrorCode в registry.
5. Назначать Category, SafeReasonCode и RetryDirective явно.
6. Добавлять localization key.
7. Не включать raw user/secret/provider detail в public Error.
8. Писать mapping tests.
9. Различать CommandResult.Rejected и outer Result.Failure.
10. Использовать тот же CommandId при разрешённом retry unknown outcome.
11. Не добавлять `PartialSuccess` в общий Result.
12. Не сериализовать Application Error напрямую как universal DTO.
13. Не создавать `CommonError`, `UtilsError` или catch-all code `operation_failed` без review.
14. Не добавлять новый ErrorCategory без нового ADR/revision ADR-004.
15. Не менять semantics существующего ErrorCode.

Pull request, нарушающий эти правила, не готов к merge.

---

# 30. Нормативные примеры

## 30.1 Permission denied

```text
Code: permissions.action.denied
Category: Authorization
SafeReasonCode: PermissionDenied
UserMessageKey: errors.permissions.action_denied
RetryDirective: DoNotRetry
CorrelationId: ...
```

## 30.2 Hidden target

```text
Internal Code: permissions.hidden_target.view_denied
Category: Authorization
Player SafeReasonCode: TargetUnavailable
Player UserMessageKey: errors.board.target_unavailable
RetryDirective: DoNotRetry
```

## 30.3 Revision conflict

```text
Code: board.token.revision_conflict
Category: Conflict
SafeReasonCode: StateChanged
UserMessageKey: errors.board.state_changed
RetryDirective: RefreshStateThenRetry
```

## 30.4 SQLite temporary lock

```text
Code: persistence.transaction.temporarily_locked
Category: TransientInfrastructure
SafeReasonCode: ServiceUnavailable
UserMessageKey: errors.persistence.try_again
RetryDirective: RetryWithBackoff
DiagnosticId: ...
```

## 30.5 Campaign corruption

```text
Code: persistence.campaign.corrupted
Category: Integrity
SafeReasonCode: DataCorrupted
UserMessageKey: errors.persistence.campaign_corrupted
RetryDirective: ManualRecoveryRequired
DiagnosticId: ...
```

## 30.6 Protocol mismatch

```text
Code: network.session.protocol_unsupported
Category: Compatibility
SafeReasonCode: ProtocolVersionUnsupported
UserMessageKey: errors.network.protocol_unsupported
RetryDirective: UpgradeRequired
```

## 30.7 User cancelled import

```text
Code: audio.import.cancelled
Category: Cancelled
SafeReasonCode: OperationCancelled
UserMessageKey: errors.audio.import_cancelled
RetryDirective: DoNotRetry
```

## 30.8 Unexpected exception

```text
Code: application.internal.unexpected
Category: Internal
SafeReasonCode: UnexpectedError
UserMessageKey: errors.application.unexpected
RetryDirective: DoNotRetry или ManualRecoveryRequired по boundary
CorrelationId: ...
DiagnosticId: ...
```

---

# 31. Обязательные тесты SLICE-00

## 31.1 Result invariants

- Success содержит Value и не содержит Error.
- Failure содержит Error и не содержит Value.
- default/invalid state отклоняется.
- nullable semantics явны.
- Result immutable.

## 31.2 Error invariants

- ErrorCode не empty и соответствует формату.
- Category определена.
- SafeReasonCode определён.
- UserMessageKey определён.
- RetryDirective определён.
- CorrelationId определён.
- Internal Error получает DiagnosticId при доступной diagnostics.

## 31.3 Registry tests

- ErrorCode уникальны.
- deprecated codes не переиспользованы.
- каждый code имеет owner/category/retry/message mapping.
- каждый public message key существует.

## 31.4 Mapping tests

- Domain rejection → ожидаемый Application Error.
- SQLite/provider error → стабильный ErrorCode.
- unknown provider exception → safe fallback.
- hidden entity → TargetUnavailable для Player.
- GM projection получает только разрешённую детализацию.

## 31.5 Retry tests

- DoNotRetry не запускает автоматический повтор.
- RetryWithBackoff bounded.
- RefreshStateThenRetry сначала выполняет refresh.
- ReconnectThenRetry сохраняет operation identity.
- unknown command outcome повторяет тот же CommandId.
- changed payload с тем же CommandId отклоняется ADR-002.

## 31.6 Command tests

- Rejected command возвращается как outer Success + CommandResult.Rejected.
- failure записи rejection receipt возвращается outer Failure.
- Accepted/Pending не содержат Error.
- Rejected не создаёт gameplay DomainEvent.

## 31.7 Exception tests

- expected domain failure не бросает generic exception.
- boundary unexpected exception создаёт diagnostic и safe Internal Error.
- stack trace отсутствует в transport DTO.
- partial authoritative write откатывается.

## 31.8 Validation tests

- multiple safe details возвращаются.
- raw secret/rejected values не попадают в details.
- field paths стабильны.
- size limits применяются.

## 31.9 Batch tests

- один failed audio item не делает outer batch failure при partial-success contract.
- fatal report failure возвращает outer Failure.
- per-item errors трассируются.

## 31.10 Localization tests

- key существует для поддерживаемых языков/fallback catalog.
- missing key вызывает test failure или diagnostic fallback.
- machine logic не зависит от локализованной строки.

## 31.11 Transport security tests

- hidden IDs/notes/modifiers не сериализуются.
- exception/provider messages не сериализуются.
- DiagnosticId непрозрачен.
- unknown optional safe code обрабатывается fallback-ом.

---

# 32. CI и enforcement

Fast CI обязан проверять:

- компиляцию Result/Error primitives;
- unit tests инвариантов;
- ErrorCode registry validation;
- mapping tests Core/Application;
- architecture rule: Domain/Rules не зависят от Application Error;
- отсутствие forbidden exception patterns в критических handlers по анализаторам/ревью;
- отсутствие raw stack/provider details в DTO fixtures;
- localization key coverage для добавленных codes.

Integration CI проверяет:

- SQLite failure injection;
- command receipt failure semantics;
- network timeout/reconnect retry;
- IL2CPP serialization ErrorDto;
- diagnostic archive redaction.

Нарушение critical Result/Error semantics блокирует merge.

---

# 33. Последствия

## 33.1 Положительные

- единый machine-readable язык ошибок;
- безопасные command retries;
- ясная граница Rejected vs unavailable outcome;
- UI локализует сообщения без анализа строк;
- provider/SQLite/relay details не протекают в Core/UI;
- hidden state защищён;
- batch partial success моделируется честно;
- exception не скрывает обычную бизнес-семантику;
- тесты могут утверждать стабильные codes/retry;
- Codex получает однозначный шаблон.

## 33.2 Стоимость

- нужны mapping tables и registry;
- каждый новый failure требует code/message/retry/test;
- adapters пишут явные mappings;
- localization catalog должен поддерживаться;
- exception boundary требует дисциплины;
- часть внутренних domain decisions не может просто использовать общий Error.

Стоимость принимается как необходимая для надёжного host-authoritative приложения.

---

# 34. Рассмотренные альтернативы

## 34.1 Исключения для всех неуспехов

Отклонено: смешивает ожидаемое поведение и defects, усложняет retry и тесты.

## 34.2 `bool + out string`

Отклонено: нет стабильных кодов, категорий, retry и локализации.

## 34.3 `null` как NotFound

Отклонено на Application boundary: неоднозначно с nullable success и теряет причину.

## 34.4 Один универсальный Error DTO во всех слоях

Отклонено: нарушает ADR-001, протаскивает UI/infrastructure semantics в Domain.

## 34.5 HTTP status codes как Error model

Отклонено: Odyssey не является HTTP-domain; status недостаточно точен и привязывает Core к transport.

## 34.6 Raw provider error

Отклонено: нестабильно, небезопасно и не локализуется.

## 34.7 Retryable boolean

Отклонено: не объясняет refresh/reconnect/backoff/upgrade/manual recovery.

## 34.8 Третье состояние PartialSuccess

Отклонено: partial semantics зависят от typed report и atomicity feature.

## 34.9 Все command rejections как outer Failure

Отклонено: теряется различие между durable terminal rejection и отсутствием terminal outcome.

## 34.10 Один общий `operation_failed`

Отклонено: не позволяет UX, diagnostics, retry и regression tests.

---

# 35. Отложенные решения

Этот ADR намеренно не фиксирует полностью:

- concrete DI/composition — ADR-005;
- test project organization — ADR-006;
- version compatibility lifecycle ErrorCode registry — ADR-007;
- Clock/RNG-specific errors — ADR-008;
- exact Unity notification visual style — UI/UX contract;
- exact log sinks, retention и diagnostic schema — ADR-010;
- exact retry timing/backoff constants — Networking/Persistence implementation policies;
- supported localization languages и translation workflow — UI/UX contract;
- crash reporter/provider — Deployment/Operations contract.

Отложенное решение не может нарушать инварианты этого ADR.

---

# 36. Трассировка

| Источник | Связь |
|---|---|
| Technical Development Baseline §15.3 | Заменяет предварительный Result/Error outline точным contract |
| ADR-001 | Сохраняет ownership: Application Result, Domain-specific decisions, adapters mappings |
| ADR-002 §13 | Определяет точный Error/Retry vocabulary для CommandResult |
| ADR-002 §22 | Уточняет durable rejection и safe response |
| ADR-003 | Разделяет runtime Error и transport/persistence DTO, запрещает raw exception serialization |
| Product Requirements PR-PERM-017 | Реализует safe reason без secret disclosure |
| Product Requirements PR-UX-005 | Даёт конкретную безопасную причину и возможное действие |
| Product Requirements NFR-OBS-001 | Структурирует понятную ошибку |
| Permissions contract | Сохраняет SafeReasonCode/InternalReason separation |
| Persistence contract | Определяет mapping transaction/corruption/migration failures |
| Networking contract | Определяет transport-safe reason/retry projection |
| Test Strategy | Добавляет regression, fault injection, redaction и controlled failure tests |

---

# 37. Definition of Done ADR-004

ADR считается реализованным в коде, когда:

- [ ] существуют Application-owned `Result`, `Result<T>`, `Unit`, `Error`;
- [ ] Result/Error invariants защищены типами и тестами;
- [ ] определён `ErrorCategory` vocabulary этого ADR;
- [ ] определён `RetryDirective` vocabulary этого ADR;
- [ ] создан `docs/errors/ERROR_CODES.md`;
- [ ] базовые codes зарегистрированы;
- [ ] Domain/Rules не зависят от Application Error;
- [ ] первый command gateway возвращает `Result<CommandResult>`;
- [ ] Rejected и outer Failure различаются тестами;
- [ ] persistence/network adapters имеют explicit mappings;
- [ ] unexpected exception создаёт DiagnosticId и safe error;
- [ ] transport DTO не содержит stack/provider/secret details;
- [ ] localization keys проверяются;
- [ ] partial batch report покрыт тестом;
- [ ] Fast CI блокирует registry/mapping violations.

---

# 38. Запреты

Запрещено без нового ADR или revision ADR-004:

1. Добавлять третье состояние `Result<T>`.
2. Использовать exception как обычный domain rejection.
3. Возвращать raw provider exception за Application boundary.
4. Показывать stack trace обычному пользователю.
5. Использовать локализованную строку как machine code.
6. Переиспользовать ErrorCode с новым смыслом.
7. Добавлять новый ErrorCategory ad hoc.
8. Автоматически retry `DoNotRetry`.
9. Менять command payload при повторе того же CommandId.
10. Кодировать partial batch как общий Success/Failure без typed report.
11. Раскрывать hidden target через точный reason.
12. Сериализовать runtime Error как universal persistence/network contract.
13. Создавать catch-all `operation_failed` вместо доказанного mapping.
14. Swallow unexpected exception и продолжать authoritative mutation.
15. Хранить secret/private content в Error metadata.

---

**Конец документа**
