# ADR-002 — Command and Domain Event Model

**Документ:** `docs/adr/ADR-002_Command_and_Domain_Event_Model_v1.0.md`  
**ADR:** ADR-002  
**Версия:** 1.0  
**Дата:** 27 июля 2026 года  
**Статус:** Accepted  
**Область:** application commands, domain events, command lifecycle, host authority, idempotency, optimistic concurrency, event batches, compensation, correlation и transactional publication  
**Связанные этапы:** Roadmap Stage 1, `SLICE-00`, Milestone `M1`  
**Базовые документы:** `TECHNICAL_DEVELOPMENT_BASELINE_Odyssey_VTT_v0.1.md`, `ADR-001_Module_Boundaries_and_Dependency_Direction_v1.0.md`

---

# 1. Решение

Odyssey VTT использует явную модель **Command → Validation → Domain decision → Atomic commit → Authoritative publication**.

Главные правила:

1. `Command` выражает одно намерение изменить авторитетное состояние.
2. Команда не является фактом и не может напрямую применяться клиентом как авторитетное изменение.
3. Только host-side `Odyssey.Application` принимает решение о выполнении команды.
4. Один `CommandId` обозначает одну логическую попытку и является каноническим ключом идемпотентности команды.
5. Одна авторитетная транзакция имеет ровно одну корневую команду.
6. Одна команда может атомарно создать упорядоченный batch из нескольких `DomainEvent` и изменить несколько aggregates.
7. Внутренние детерминированные реакции внутри той же транзакции создают дополнительные события, но не скрытые вложенные команды.
8. Продолжение после пользовательского выбора, таймера, reconnect, внешнего ответа или другого асинхронного рубежа оформляется новой командой с новым `CommandId`.
9. `DomainEvent` является неизменяемым фактом уже принятого изменения и именуется в прошедшем времени.
10. Состояние, events, command receipt/result и outbox записываются в одной authoritative transaction boundary.
11. Успешный ответ клиенту не отправляется до commit.
12. Повтор той же команды не создаёт новые события, не расходует ресурсы и не вызывает RNG повторно.
13. Исправление committed history выполняется компенсирующей командой и новыми событиями, а не изменением или удалением старого события.
14. Network message, delivery acknowledgement, UI preview, GameLogEntry и CalculationTrace не являются `DomainEvent`.
15. Raw domain events не отправляются клиентам напрямую; сеть публикует audience-specific projections после redaction.

Этот ADR является нормативным authority по командам и доменным событиям. Он уточняет и, где указано явно, заменяет предварительные command/event формулировки Technical Development Baseline, Domain Model, Persistence и Networking contracts.

---

# 2. Контекст и проблема

Odyssey VTT одновременно поддерживает:

- локальный host-authoritative режим;
- сетевую доставку с повтором сообщений;
- несколько изменяемых aggregates в одной игровой операции;
- optimistic concurrency через revisions;
- скрытые данные и audience projections;
- автоматические броски и детерминированные правила;
- `PendingInteraction` и продолжение действий;
- append-only журнал изменений;
- SQLite transaction и outbox;
- compensation вместо удаления истории;
- работу Codex над независимыми вертикальными срезами.

Без одного точного command/event контракта разные реализации могут принять несовместимые решения:

- клиент начнёт применять optimistic state как авторитетный;
- `CommandId` и отдельный `IdempotencyKey` будут использоваться по-разному;
- duplicate delivery повторно выполнит бросок или спишет боеприпасы;
- handler запишет state, а затем не запишет event;
- event будет опубликован до commit;
- rejected command создаст частичный effect;
- nested command handlers создадут неявные transaction boundaries;
- продолжение `PendingInteraction` изменит старый command result;
- correction перепишет старое событие;
- сетевой DTO станет доменной командой без повторной валидации;
- raw event раскроет скрытую сущность участнику без доступа;
- одинаковый `CommandId` с другим payload вернёт чужой сохранённый результат;
- client timestamp начнёт определять порядок действий.

Этот ADR устраняет такие неоднозначности до создания Core primitives.

---

# 3. Движущие факторы

Решение оптимизировано под следующие требования:

1. Exactly-once **effect** поверх at-least-once доставки.
2. Host authority и недоверие к client-provided state.
3. Atomicity многоагрегатных игровых действий.
4. Детерминированность Rules Engine, Clock и RNG.
5. Совместимость локального и сетевого command path.
6. Возможность безопасного retry после потери ответа.
7. Stable audit/correlation всей цепочки действия.
8. Неизменяемая история и явная компенсация.
9. Разделение domain facts, UI history, diagnostics и transport messages.
10. Возможность тестировать command handlers без Unity Editor и сети.
11. Однозначные правила для Codex и автоматических проверок.
12. Отсутствие требования полного event sourcing: snapshots/state tables остаются авторитетными вместе с event journal.

---

# 4. Термины

## 4.1 Command

Неизменяемое описание намерения изменить авторитетное состояние.

Примеры:

```text
MoveToken
DeclareAttack
AnswerPendingInteraction
CreateScene
ImportContentPackage
StartSession
CompensateActionResolution
```

Команда не утверждает, что действие уже произошло.

## 4.2 Query

Операция чтения, которая не изменяет авторитетное состояние и не создаёт DomainEvents.

Query не маскируется под command. Для обычного чтения не требуется `AppliedCommands` и durable idempotency receipt.

## 4.3 DomainEvent

Неизменяемый авторитетный факт изменения домена.

Примеры:

```text
TokenMoved
AttackDeclared
AttackResolved
PendingInteractionCreated
SceneCreated
ContentPackageImported
ActionResolutionCompensated
```

## 4.4 Command receipt

Durable запись о принятой к обработке команде, её identity fingerprint и результате. В persistence contract она реализуется через `AppliedCommands` либо эквивалентную таблицу.

## 4.5 Event batch

Упорядоченный набор событий, созданных одной командой и committed одной транзакцией.

## 4.6 Root command

Команда, начавшая логический workflow. Для root command `RootCommandId == CommandId`.

## 4.7 Continuation command

Новая команда, продолжающая workflow после асинхронного рубежа. Она имеет новый `CommandId`, наследует `RootCommandId` и `CorrelationId`, а `ParentCommandId` указывает непосредственную причину продолжения.

## 4.8 Compensation

Новое авторитетное действие, которое явно исправляет последствия ранее committed действия. Compensation не является физическим удалением старых событий и не гарантирует восстановление мира «как будто события не было».

---

# 5. Разделение ответственности по модулям

## 5.1 Odyssey.Domain

Владеет:

- domain event types и payload semantics;
- aggregate invariants;
- pure state transitions;
- domain decision results;
- compensation vocabulary;
- stable domain rejection codes, относящимися к бизнес-правилам.

Domain не владеет:

- network envelopes;
- SQLite transaction;
- outbox;
- command transport retry;
- authenticated connection context;
- UI pending indicators;
- serializer/provider-specific DTO.

## 5.2 Odyssey.Rules

Владеет детерминированными вычислениями, которые command handler использует для принятия решения.

Rules не commit-ит events, не вызывает repository и не публикует сообщения.

## 5.3 Odyssey.Content

Владеет безопасным выполнением Content Block definitions и формированием детерминированных proposals/results для Application.

Content execution не обходит command transaction и не пишет state напрямую.

## 5.4 Odyssey.Application

Владеет:

- command contracts и handlers;
- orchestration command pipeline;
- authentication/authorization integration ports;
- concurrency policy конкретной команды;
- transaction boundary;
- вызовом Domain, Rules и Content;
- формированием event batch;
- command result;
- ports для persistence, outbox, clock, RNG и diagnostics.

Только Application handler может инициировать authoritative state-changing transaction.

## 5.5 Odyssey.Persistence

Владеет реализацией:

- atomic transaction;
- state persistence;
- immutable event append;
- command receipt/result storage;
- sequence allocation;
- outbox persistence;
- recovery после crash.

Persistence не решает, разрешена ли команда по игровым правилам.

## 5.6 Odyssey.Networking

Владеет:

- transport envelopes;
- connection/session binding;
- command admission до Application;
- доставкой command result;
- audience-specific publication;
- retry, acknowledgement и reconnect.

Network DTO преобразуется в Application command. Он не передаётся напрямую в Domain или repository.

## 5.7 Odyssey.Unity.Client

Владеет:

- сбором пользовательского намерения;
- локальным preview;
- отправкой команды;
- отображением pending/success/error;
- применением authoritative snapshot/delta.

Unity Client не применяет DomainEvent самостоятельно как доверенный источник и не меняет authoritative campaign state заранее.

---

# 6. Каноническая модель команды

## 6.1 Command envelope

Канонический Application envelope:

```text
ApplicationCommand
├── CommandId
├── CommandType
├── CommandVersion
├── CampaignId?
├── SessionId?
├── Issuer
│   ├── IssuerKind          # User | HostSystem | Migration | Recovery
│   ├── ActorUserId?
│   └── ActorCharacterId?
├── OriginClientInstanceId?
├── RootCommandId
├── ParentCommandId?
├── CorrelationId
├── ExpectedCampaignRevision?
├── ExpectedSessionSequence?
├── ExpectedAggregateRevisions[]
├── IssuedAtClient?
├── ReceivedAtHost
├── PayloadVersion
└── Payload
```

## 6.2 Обязательные identity rules

- `CommandId` обязателен для любой state-changing команды.
- `CommandId` создаётся один раз в origin и сохраняется при retry.
- `CommandId` глобально уникален в рамках формата identity проекта.
- `RootCommandId` равен `CommandId` для root command.
- continuation наследует `RootCommandId`.
- `CorrelationId` обязателен и объединяет весь workflow, traces, events и diagnostics.
- `ParentCommandId` используется только для явного продолжения либо system command, вызванной ранее committed workflow.

## 6.3 CommandId и IdempotencyKey

Для Application command **`CommandId` является каноническим idempotency key**.

Отдельное поле `IdempotencyKey` в core command envelope запрещено, поскольку два независимых ключа создают неоднозначность.

Термин `IdempotencyKey` может использоваться:

- как общее описание свойства `CommandId`;
- в других протоколах, не являющихся Application command, например delivery/import operation;
- в provider-specific boundary, если он маппится один-к-одному на `CommandId` и не попадает в Core как второй ключ.

Этот раздел нормативно заменяет предварительное наличие одновременно `CommandId` и `IdempotencyKey` в разделе 15.4 Technical Development Baseline.

## 6.4 Issuer

Пользовательская команда требует `IssuerKind=User` и `ActorUserId`.

Host-side автоматическое действие использует явный системный issuer. Системная команда не маскируется под пользователя.

Допустимые системные источники первого baseline:

```text
HostSystem
Migration
Recovery
```

Добавление нового issuer kind требует изменения command contract и тестов authorization/audit.

## 6.5 Client-provided fields

Поля от клиента считаются claims, а не доказательствами.

Host обязан:

- получить реального пользователя из authenticated connection;
- проверить совпадение с claimed `ActorUserId`;
- проверить campaign/session binding;
- не доверять claimed role, stats, position, visibility, inventory или resource values;
- использовать `IssuedAtClient` только для диагностики/UX;
- назначить `ReceivedAtHost` через injected host clock.

## 6.6 Command payload

Payload содержит только намерение и минимальные input values.

Запрещено передавать как авторитетные результаты:

- рассчитанный hit result;
- итоговый damage;
- текущую защиту цели;
- разрешение visibility;
- уже уменьшенный resource balance;
- готовое новое состояние aggregate;
- произвольный serialized Character/Scene snapshot.

Host пересчитывает такие данные из authoritative state.

---

# 7. Command type и versioning

## 7.1 Stable type identity

Каждая команда имеет стабильный `CommandType` и целочисленный `CommandVersion`.

Рекомендуемый стиль type identity:

```text
campaign.create
scene.create
board.token.move
combat.attack.declare
interaction.answer
content.package.import
```

Точный формат регистра и namespace закрепляется ADR-003, но type identity не должен зависеть от C# class name.

## 7.2 Изменение payload

- backward-compatible optional addition может увеличить payload schema version согласно ADR-003;
- breaking change создаёт новую command version;
- неизвестная обязательная version отклоняется до domain handling;
- handler выбирается по `(CommandType, CommandVersion)`;
- runtime reflection-based «угадывание» типа не является контрактом.

## 7.3 Command immutability

После admission envelope и payload не изменяются.

Нормализация transport DTO выполняется до создания ApplicationCommand. Любая derived authoritative value загружается отдельно и не записывается обратно в исходный payload.

---

# 8. Command admission boundary

Не каждый входящий пакет становится Application command.

## 8.1 Transport rejection до admission

До Application допускается отклонить сообщение из-за:

- отсутствия authenticated connection;
- неверного session binding;
- неподдерживаемой protocol/message version;
- превышения размера payload;
- невозможности parse/schema validation;
- rate limit/abuse;
- повреждённого envelope.

Такой пакет может не создавать запись `AppliedCommands`, поскольку authoritative command ещё не существует.

Transport rejection:

- не раскрывает campaign state;
- может создавать security/diagnostic record;
- не создаёт DomainEvent;
- не создаёт GameLogEntry.

## 8.2 Admitted command

После успешной identity/session/schema проверки создаётся неизменяемая ApplicationCommand.

С этого момента:

- `CommandId` участвует в durable idempotency;
- payload fingerprint фиксируется;
- результат должен быть повторяемым, если он был durable;
- дальнейший отказ считается Application command result.

---

# 9. Identity fingerprint и защита от подмены duplicate

## 9.1 Fingerprint

Для admitted command вычисляется stable fingerprint минимум из:

```text
CommandType
CommandVersion
CampaignId
SessionId?
IssuerKind
ActorUserId?
ActorCharacterId?
RootCommandId
ParentCommandId?
CorrelationId
Expected revisions/sequences
PayloadVersion
CanonicalPayload
```

Transport-only поля, которые законно меняются при retry, не входят в semantic fingerprint.

## 9.2 Повтор с тем же CommandId

Если существует durable receipt с тем же `CommandId` и тем же fingerprint:

- handler не запускается повторно;
- aggregates не загружаются для повторного применения;
- RNG и Clock для domain decision не вызываются повторно;
- новые events не создаются;
- сохранённый result возвращается повторно;
- клиенту может дополнительно быть предложен safe projection refresh.

## 9.3 CommandId collision/mismatch

Если тот же `CommandId` приходит с другим fingerprint:

- команда отклоняется с stable security-safe code `CommandIdentityMismatch`;
- исходный result не раскрывается другому actor/session;
- создаётся security diagnostic/audit record;
- никакие domain effects не применяются;
- автоматический retry с новым payload под старым ID запрещён.

## 9.4 In-flight duplicate

Параллельные доставки одного `CommandId` должны быть single-flight либо сериализованы database uniqueness constraint.

Допустимый результат:

- один execution выполняет transaction;
- остальные ожидают его durable result либо после conflict перечитывают receipt;
- ни один duplicate не выполняет domain handler второй раз.

---

# 10. Optimistic concurrency

## 10.1 Typed expectations

Команда может содержать:

```text
ExpectedCampaignRevision?
ExpectedSessionSequence?
ExpectedAggregateRevisions[]
```

Каждый command type определяет concurrency policy:

- какие aggregate revisions обязательны;
- допустима ли команда без expected revision;
- требуется ли точная session sequence;
- можно ли безопасно rebase/retry после refresh.

## 10.2 Revision validation

Revisions проверяются host-side после загрузки authoritative state и до RNG/state mutation.

При конфликте:

- команда получает `Rejected`;
- events не создаются;
- resources не расходуются;
- RNG не вызывается;
- клиент получает safe refresh metadata;
- автоматическое повторное выполнение с новыми revisions запрещено без нового пользовательского намерения и нового `CommandId`, кроме отдельно утверждённых idempotent UI workflows.

## 10.3 Visibility и revision

Знание ID или старой revision не предоставляет право на команду.

Host отдельно проверяет:

- visibility;
- control grant;
- current permission;
- active scene/session context.

Safe error не должен подтверждать существование никогда не видимой сущности.

---

# 11. Нормативный command processing pipeline

Для admitted command host выполняет следующий порядок:

```text
1. Bind authenticated execution context.
2. Validate CommandType/Version/Payload schema.
3. Compute semantic fingerprint.
4. Resolve durable duplicate by CommandId.
5. Check campaign/session write mode and lifecycle state.
6. Check authoritative permissions and control grants.
7. Load required aggregates/projections.
8. Validate expected revisions and session sequence.
9. Validate domain/rules/content preconditions.
   - Если шаги 5–9 дают deterministic rejection, сохранить durable rejection receipt и завершить pipeline.
10. Obtain required host clock/RNG values through injected ports.
11. Produce ordered state changes and DomainEvent proposals.
12. Validate final invariants across affected aggregates.
13. Begin/enter authoritative persistence transaction.
14. Persist new aggregate/state snapshots.
15. Append immutable DomainEvents in deterministic order.
16. Persist CalculationTrace/GameLog source data where applicable.
17. Persist audience projection outbox entries.
18. Persist command fingerprint and CommandResult.
19. Commit transaction.
20. Publish committed outbox/delta.
21. Return/replay CommandResult.
```

Реализация может начать SQLite transaction до загрузки state, если это необходимо для isolation, но логический порядок проверок и единая atomic boundary должны сохраняться.

Успешная network publication до шага commit запрещена.

---

# 12. Одна команда — одна transaction boundary

## 12.1 Root transaction

Одна state-changing ApplicationCommand создаёт не более одной authoritative state transaction.

Внутри неё допустимы:

- изменение нескольких aggregates;
- несколько domain decisions;
- выполнение Rules Engine;
- безопасное ContentExecution;
- несколько событий;
- несколько GameLogEntries;
- один или несколько CalculationTrace records;
- outbox entries для разных audiences.

## 12.2 Nested command handlers запрещены

Command handler не вызывает другой command handler для выполнения части той же транзакции.

Причины:

- скрытые transaction boundaries;
- неоднозначная idempotency;
- конфликт `CommandId` и correlation;
- повторная permission validation в середине commit;
- неочевидный порядок событий;
- сложная компенсация.

Общая логика выносится в Domain/Rules/Application services и возвращает proposals/events текущему root handler.

## 12.3 Когда нужна новая команда

Новая continuation command обязательна, если workflow пересекает:

- пользовательский выбор;
- GM confirmation;
- timer/turn boundary;
- reconnect;
- внешний provider/result;
- отдельную очередь;
- restart/recovery;
- асинхронный asset/import этап;
- отложенное системное условие.

Continuation имеет новый `CommandId`, но наследует `RootCommandId` и `CorrelationId`.

---

# 13. Command result model

## 13.1 Статусы

Нормативные статусы:

```text
Accepted
Pending
Rejected
```

## 13.2 Accepted

`Accepted` означает:

- авторитетная transaction committed;
- команда завершила своё намерение;
- result receipt durable;
- указанный event/sequence range существует;
- клиент применяет authoritative delta, а не локальную mutation.

## 13.3 Pending

`Pending` является **успешным committed terminal result именно этой команды**.

Он означает:

- committed создано авторитетное состояние ожидания;
- существует `PendingInteraction`, reservation или другой explicit suspended workflow state;
- исходная команда позже не меняет статус на Accepted;
- продолжение выполняется новой command;
- duplicate исходной команды возвращает тот же Pending result.

## 13.4 Rejected

`Rejected` означает:

- намерение не применено;
- gameplay/domain state не изменён;
- DomainEvents не созданы;
- resource/ammo/action не расходованы;
- RNG для результата не потреблён;
- safe reason/result может быть durable.

Security/administrative audit rejection не превращается автоматически в игровой DomainEvent.

## 13.5 CommandResult envelope

```text
CommandResult
├── CommandId
├── CommandFingerprint       # internal receipt only; не отправляется клиенту
├── Status
├── ReasonCode?
├── SafeMessageKey?
├── ErrorCategory?
├── RetryPolicy
├── RootCommandId
├── CorrelationId
├── TransactionId?
├── CampaignRevision?
├── EventSequenceFrom?
├── EventSequenceTo?
├── SessionSequenceFrom?
├── SessionSequenceTo?
├── AffectedAggregateRevisions[]
├── PendingInteractionId?
├── TraceId?
└── CompletedAtHost
```

Точные `ErrorCategory`, retry vocabulary и публичные сообщения закрепляет ADR-004.

## 13.6 Infrastructure failure без durable outcome

Если persistence failure не позволяет durably сохранить ни state, ни command result:

- host не заявляет terminal Accepted/Pending/Rejected как авторитетный результат;
- клиент получает transient transport/application failure;
- retry выполняется с тем же `CommandId`;
- после retry host либо найдёт committed receipt, либо выполнит команду впервые.

Это необходимо для безопасного crash-after-commit сценария.

---

# 14. Каноническая модель DomainEvent

## 14.1 Event envelope

```text
DomainEvent
├── DomainEventId
├── EventType
├── EventVersion
├── CampaignId
├── SessionId?
├── AggregateType
├── AggregateId
├── AggregateRevision
├── CampaignRevision
├── EventSequence
├── SessionSequence?
├── TransactionId
├── RootCommandId
├── CausationCommandId
├── CorrelationId
├── Actor
│   ├── IssuerKind
│   └── ActorUserId?
├── OccurredAtHost
├── VisibilityPolicy
├── AudienceClassification
├── IsCompensating
├── CompensatesEventIds[]
├── ReasonCode?
├── PayloadVersion
└── Payload
```

## 14.2 Именование

Event type описывает уже произошедший факт и именуется в прошедшем времени.

Правильно:

```text
TokenMoved
DamageApplied
PendingInteractionCreated
SessionEnded
ActionResolutionCompensated
```

Неправильно:

```text
MoveToken
ApplyDamage
CreatePendingInteraction
EndSession
```

## 14.3 Неизменяемость

После commit запрещены обычные:

- `UPDATE DomainEvents`;
- `DELETE DomainEvents`;
- замена payload;
- изменение actor/reason/visibility metadata;
- перенумерация sequence.

Schema migration может создавать новую physical representation только при сохранении исходной семантики, hashes/evidence и migration history согласно Persistence contract.

## 14.4 DomainEventId

`DomainEventId` является каноническим названием identity доменного события.

Общий typed ID `EventId` может существовать как namespace-level abstraction, но Application/Domain contract не должен одновременно содержать два независимых ID одного события.

## 14.5 OccurredAtHost

- задаётся injected host clock;
- client timestamp не используется;
- события одного transaction batch могут иметь общий commit instant;
- порядок определяется sequences, а не сравнением timestamp.

---

# 15. Event batch, revisions и sequences

## 15.1 TransactionId

Все события одной команды имеют один `TransactionId`.

CommandResult с Accepted/Pending ссылается на тот же TransactionId.

## 15.2 CampaignRevision

`CampaignRevision` увеличивается один раз на каждую committed state-changing command transaction.

Все события batch содержат итоговую CampaignRevision этой транзакции.

## 15.3 AggregateRevision

Для каждого затронутого aggregate:

- revision монотонна;
- каждое событие, изменяющее aggregate, получает следующую revision;
- если batch создаёт несколько событий одного aggregate, revisions идут последовательно;
- итоговое persisted aggregate state имеет revision последнего события этого aggregate.

## 15.4 EventSequence

`EventSequence`:

- монотонна внутри кампании;
- назначается persistence в commit order;
- уникальна для каждого DomainEvent;
- образует непрерывный диапазон внутри batch.

## 15.5 SessionSequence

Для session-scoped publication события получают `SessionSequence` согласно Networking contract.

Команда может создать campaign-level event без session sequence.

## 15.6 Deterministic order

Application формирует ordered event proposals. Persistence сохраняет их в этом порядке и назначает sequences без перестановки.

Порядок должен быть воспроизводим в тестах при одинаковых inputs, clock и RNG.

---

# 16. State snapshots и event journal

Odyssey VTT не требует полного event sourcing.

Authoritative transaction сохраняет согласованно:

- актуальное aggregate/state representation;
- immutable DomainEvents;
- command receipt;
- traces/log source data;
- outbox.

Следствия:

- event journal является авторитетной историей изменений;
- current state не обязан каждый раз пересобираться из всех событий;
- derived projections могут пересобираться;
- state и event не могут расходиться после committed transaction;
- handler не может записать только state без events для state-changing command.

State-changing Accepted/Pending command должен создать минимум один DomainEvent.

Операция без domain state change должна быть Query либо explicit technical operation, а не «успешная пустая команда».

---

# 17. DomainEvent и соседние типы записей

## 17.1 GameLogEntry

Пользовательское представление игровой истории.

- может объединять несколько events;
- локализуется;
- фильтруется по audience;
- не является источником state;
- редактируемый комментарий не изменяет DomainEvent.

## 17.2 CalculationTrace

Техническое/игровое доказательство вычисления.

- содержит rolls, modifiers и промежуточные значения;
- immutable после commit;
- может иметь более строгую visibility;
- не заменяет DomainEvent.

## 17.3 AdministrativeAudit / SecurityDiagnostic

Записывает:

- rejected/abusive commands;
- permission/admin operations;
- security anomalies;
- protocol violations.

Не является игровым DomainEvent, если сам факт не изменяет campaign domain state.

## 17.4 Network message

Snapshot, delta, acknowledgement, heartbeat, asset chunk и CommandResult delivery не являются DomainEvents.

## 17.5 Outbox entry

Техническая committed инструкция публикации. Она создаётся в той же transaction, но не является доменным фактом.

## 17.6 Private/E2EE message

Private plaintext/E2EE message не является Campaign DomainEvent и не попадает в event journal, если продуктовый контракт явно не определит отдельную метадату без содержания.

---

# 18. Publication и redaction

## 18.1 Raw events не являются wire payload

Networking не отправляет raw persisted DomainEvent всем участникам.

После commit:

1. ProjectionBuilder читает committed state/events.
2. Для каждого audience применяются permissions и visibility rules.
3. Формируется snapshot/delta operation.
4. Outbox публикует только разрешённую projection.

## 18.2 Hidden information

Если event относится к никогда не видимой сущности:

- ID;
- тип;
- payload;
- sequence gap, позволяющий вывести существование;
- diagnostic reason

не должны раскрываться неразрешённому клиенту.

Session/network sequences могут описывать projection stream, а не давать клиенту raw global event count.

## 18.3 Publication failure

Ошибка publication после commit:

- не откатывает command transaction;
- сохраняет outbox pending;
- исправляется retry/reconnect;
- duplicate command возвращает committed result;
- client получает пропущенный delta либо snapshot.

---

# 19. Clock и RNG

## 19.1 Clock

- `ReceivedAtHost`, `OccurredAtHost` и `CompletedAtHost` задаются через `IClock`.
- Domain/Rules не читают system wall clock напрямую.
- Тесты используют fixed/virtual clock.
- Monotonic session duration не вычисляется по client local time.

## 19.2 RNG

- RNG вызывается только host-authoritative path.
- RNG недоступен до duplicate, permission и revision validation.
- Все значимые random outputs фиксируются в events и/или CalculationTrace.
- Duplicate command не вызывает RNG.
- Replay command result не пересчитывает roll.
- Continuation command может использовать новый RNG только если это предусмотрено её отдельным domain decision.

Точный RNG contract, seed/evidence и stream ownership закрепляет ADR-008.

---

# 20. PendingInteraction и suspended workflows

## 20.1 Создание Pending

Если command требует внешнего решения:

- текущая command transaction создаёт `PendingInteractionCreated` либо эквивалентный event;
- reservations и suspended execution state сохраняются;
- result текущей команды — `Pending`;
- вся pending state durable и восстанавливается после restart.

## 20.2 Ответ

Ответ оформляется новой командой:

```text
AnswerPendingInteraction
├── new CommandId
├── same RootCommandId
├── same CorrelationId
├── ParentCommandId = command that created/last advanced pending workflow
├── PendingInteractionId
├── ExpectedInteractionRevision
└── AnswerPayload
```

## 20.3 Разрешение

Continuation command может:

- завершить workflow;
- создать новый PendingInteraction;
- отклониться из-за устаревшей revision;
- быть отменена GM/system command.

Исходный Pending result не переписывается.

---

# 21. Compensation и correction

## 21.1 Общие правила

Committed event не удаляется и не заменяется.

Correction выполняется:

```text
CompensatingCommand
→ validation against current state
→ new state changes
→ compensating DomainEvents
→ new GameLog/Trace/Audit entries
```

## 21.2 Compensation metadata

Компенсирующее событие содержит:

- `IsCompensating=true`;
- `CompensatesEventIds[]` либо ссылку на компенсируемый transaction/action;
- `ReasonCode`;
- actor/authorization;
- текущие revisions;
- новый TransactionId.

## 21.3 Не generic rollback

Универсальная команда «откатить любое событие» запрещена.

Каждый поддерживаемый compensation use case имеет отдельный command type и domain rules, например:

```text
UndoCommittedSceneEdit
CompensateActionResolution
CorrectCharacterProgression
GMOverrideResolution
```

Compensation может быть невозможна, если current state уже не допускает безопасное исправление. В этом случае требуется другой explicit correction workflow.

## 21.4 Undo/Redo

- uncommitted UI edit history может быть локальной;
- committed editor undo создаёт compensating command/event;
- redo после committed undo также является новой command;
- старые events остаются в журнале.

---

# 22. Rejected commands и diagnostics

## 22.1 Domain rejection

Ожидаемые причины:

```text
PermissionDenied
SessionPaused
SessionEnding
ActorNotControlled
EntityRevisionConflict
SessionSequenceConflict
InvalidTarget
RuleValidationFailed
ResourceUnavailable
PendingInteractionRequired
ObjectLocked
```

Точный публичный vocabulary закрепляется ADR-004.

## 22.2 Safe response

Reason не должен раскрывать:

- существование скрытой сущности;
- secret rule modifier;
- private roll;
- GM-only note;
- internal file path;
- stack trace;
- secret/token/key.

## 22.3 Durable rejection receipt

После command admission любой deterministic `Rejected` result обязан быть сохранён как durable receipt с fingerprint до отправки terminal ответа клиенту.

Rejection receipt записывается отдельной короткой transaction без изменения gameplay state, DomainEvents и gameplay outbox.

Если receipt не удалось durably записать:

- host не отправляет terminal `Rejected` как авторитетный результат;
- клиент получает transient failure;
- retry выполняется с тем же `CommandId`;
- повторная обработка либо найдёт receipt, либо снова валидирует команду.

## 22.4 Rejection не создаёт gameplay event

Rejected command не создаёт DomainEvent только ради аудита. Для этого используется отдельный diagnostic/audit store.

Исключение допустимо, если продуктовая модель прямо считает отказ видимым доменным фактом, например formal GM ruling. Такой use case должен иметь отдельную domain command/event пару.

---

# 23. Local и network path

## 23.1 Единый Application path

Local host UI и remote client используют один Application command handler path.

Разница только в adapter до Application:

```text
Local UI Adapter ─┐
                  ├─> Application Command Gateway
Network Adapter ──┘
```

Local path не может обходить:

- authorization;
- revisions;
- idempotency;
- transaction;
- events;
- command result.

## 23.2 Host-side GM actions

Даже действие Main GM создаёт command и event. Прямое изменение SQLite/aggregate из Unity Inspector или UI callback запрещено.

## 23.3 Offline mode

Отсутствие network transport не отменяет CommandId, receipt и outbox/state consistency. Outbox может использовать local projection adapter.

---

# 24. Recovery и crash semantics

## 24.1 Crash до commit

После restart:

- state changes отсутствуют;
- events отсутствуют;
- outbox отсутствует;
- durable command result отсутствует;
- retry того же `CommandId` выполняет команду впервые.

## 24.2 Crash после commit до response

После restart:

- state/events/receipt/outbox присутствуют;
- retry того же `CommandId` возвращает сохранённый result;
- эффект не повторяется;
- pending outbox публикуется.

## 24.3 Partial write запрещён

Не допускается committed состояние, в котором существует только часть набора:

```text
state
DomainEvents
AppliedCommand receipt
outbox
```

Если конкретный storage adapter не может обеспечить такую atomicity, он не соответствует MVP contract.

---

# 25. Command handler contract

Каждый handler должен явно определить:

```text
CommandType/Version
Required permissions
Required aggregates
Expected revision policy
Allowed session states
Domain/Rules/Content services used
Clock/RNG requirements
Possible DomainEvents
Possible PendingInteractions
Possible rejection codes
Compensation relationship
Visibility/publication implications
Required tests
```

Handler обязан:

- быть stateless либо иметь только injected dependencies;
- не хранить authoritative state между вызовами;
- не обращаться к Unity API;
- не открывать собственную nested transaction;
- не публиковать сеть напрямую;
- не ловить expected domain failure как generic exception;
- не создавать случайный новый CommandId для retry исходного намерения;
- возвращать ordered decision/result.

---

# 26. Пример: MoveToken

```text
Client/UI:
Create CommandId C1
Submit board.token.move v1
Expected TokenRevision = 8
Payload: TokenId, Destination

Host/Application:
Authenticate actor
Check duplicate C1
Check permissions/control/visibility
Load Token revision 8 and scene obstacles
Validate movement using authoritative state
Produce TokenMoved(revision 9)
Persist state + event + outbox + result atomically
Commit CampaignRevision 42, EventSequence 155
Return Accepted(C1, sequence 155)

Client:
Apply authoritative delta
Remove ghost preview
```

Если response потерян:

```text
Retry exact C1
→ same fingerprint
→ stored Accepted result
→ no second TokenMoved
```

Если C1 повторён с другим Destination:

```text
→ CommandIdentityMismatch
→ no state change
→ security diagnostic
```

---

# 27. Пример: атака с PendingInteraction

```text
DeclareAttack Command C10
→ validate actor, target, ammo, action budget and revisions
→ reserve required resources
→ create PendingInteractionCreated
→ persist suspended execution
→ return Pending(C10, PendingInteractionId=P7)
```

Игрок/GM отвечает:

```text
AnswerPendingInteraction Command C11
RootCommandId=C10
ParentCommandId=C10
CorrelationId inherited
ExpectedInteractionRevision=1
→ resolve rolls/rules
→ create ordered attack events
→ commit
→ return Accepted(C11)
```

Повтор C10 всегда возвращает исходный Pending result. Он не запускает атаку заново и не создаёт P8.

---

# 28. Пример: многоагрегатная транзакция

Transfer item может затронуть:

- source Inventory;
- target Inventory;
- ItemInstance;
- encumbrance-derived state;
- GameLog/Trace.

Одна команда создаёт один TransactionId и ordered events, например:

```text
ItemRemovedFromInventory       SourceInventory revision 13
ItemAddedToInventory           TargetInventory revision 6
EncumbranceChanged             TargetCharacter revision 22
```

CampaignRevision увеличивается один раз. EventSequence увеличивается для каждого события. Ни один клиент не видит промежуточное состояние между событиями batch.

---

# 29. Запрещённые реализации

Запрещено:

- использовать UI callback как command handler;
- изменять authoritative state до command validation;
- применять network DTO напрямую к aggregate;
- использовать client timestamp как порядок;
- иметь одновременно независимые `CommandId` и `IdempotencyKey` в core command;
- повторять command с новым ID после network timeout до проверки старого ID;
- вызывать RNG до duplicate/revision validation;
- создавать nested command handler внутри root transaction;
- публиковать event до commit;
- сохранять state без event для state-changing command;
- сохранять event без соответствующего state;
- менять committed event;
- возвращать Accepted до durable receipt;
- отправлять raw event всем клиентам;
- использовать GameLogEntry как source of truth;
- считать Pending временным in-memory состоянием;
- автоматически менять статус старой Pending command после continuation;
- generic rollback произвольного event;
- раскрывать original result при CommandId mismatch;
- хранить authoritative command state в MonoBehaviour/ScriptableObject.

---

# 30. Enforcement

## 30.1 Compile-time

- command handlers находятся в `Odyssey.Application`;
- DomainEvent types находятся в `Odyssey.Domain`;
- network envelopes находятся в `Odyssey.Networking`;
- persistence records/mappers находятся в `Odyssey.Persistence`;
- Unity Client не содержит production implementations handler interfaces.

## 30.2 Static checks

CI должен проверять минимум:

- отсутствие отдельного core `IdempotencyKey` поля рядом с `CommandId`;
- отсутствие direct handler-to-handler invocation;
- отсутствие `UnityEngine` в command/event Core assemblies;
- отсутствие direct network publish из Application handler;
- отсутствие update/delete DomainEvents в normal repositories;
- наличие CommandId/CorrelationId/Version в command fixtures;
- наличие TransactionId/CausationCommandId/Version в event fixtures.

## 30.3 Runtime guards

- unique constraint для `AppliedCommands.CommandId`;
- fingerprint verification;
- unique campaign EventSequence;
- unique `(AggregateId, AggregateRevision)`;
- transaction commit guard;
- outbox publication только committed records;
- unknown type/version rejection;
- correlation propagation assertions в development builds/tests.

---

# 31. Обязательные тесты

Минимальный test suite `SLICE-00`:

## 31.1 Command identity

- новый CommandId принимается;
- exact duplicate возвращает исходный result;
- duplicate не вызывает handler второй раз;
- duplicate не вызывает RNG второй раз;
- duplicate с другим payload отклоняется;
- duplicate другого actor не раскрывает result;
- in-flight duplicate создаёт один effect.

## 31.2 Revisions

- актуальная revision принимает command;
- stale aggregate revision отклоняет command;
- stale session sequence отклоняет context-sensitive command;
- rejection не создаёт event/state/resource change;
- safe refresh не раскрывает hidden entity.

## 31.3 Event batch

- один command создаёт ordered events;
- все events имеют один TransactionId;
- AggregateRevisions последовательны;
- CampaignRevision увеличивается один раз;
- EventSequences непрерывны;
- state соответствует последнему event revision.

## 31.4 Atomicity

- failure до commit не оставляет state/event/result/outbox;
- crash after commit before response возвращает stored result;
- outbox retry не повторяет domain effect;
- multi-aggregate command не виден частично.

## 31.5 Pending workflow

- Pending command durable;
- duplicate Pending не создаёт второе interaction;
- continuation имеет новый CommandId;
- RootCommandId/CorrelationId наследуются;
- resolved continuation не переписывает старый result;
- restart восстанавливает pending state.

## 31.6 Compensation

- committed event остаётся неизменным;
- compensating command создаёт новый event;
- reason и compensated references обязательны;
- compensation проверяет текущие revisions/permissions;
- generic arbitrary rollback отсутствует.

## 31.7 Separation

- Query не создаёт events;
- rejected transport message не становится DomainEvent;
- GameLogEntry не применяется как state;
- raw hidden event не попадает в player projection;
- local и network adapters дают одинаковый Application result для одинакового command/context.

## 31.8 Versioning

- known command/event version сериализуется round-trip;
- unknown mandatory command version отклоняется;
- event payload immutable fixture не меняется;
- canonical fingerprint стабилен.

---

# 32. Реализация в SLICE-00

ADR-002 реализуется в PR `Core Primitives` после создания module skeleton по ADR-001.

Минимальный объём:

1. typed `CommandId`, `DomainEventId`, `TransactionId`, `CorrelationId`;
2. `ApplicationCommand` envelope;
3. `CommandIssuer`;
4. `ExpectedAggregateRevision`;
5. command fingerprint abstraction;
6. `CommandResult` со статусами Accepted/Pending/Rejected;
7. `DomainEvent` envelope;
8. ordered `DomainEventBatch`;
9. interfaces command gateway, transaction, command receipt store, clock, RNG и outbox;
10. fake in-memory adapter для contract tests;
11. duplicate/single-flight tests;
12. revision tests;
13. atomic commit simulation;
14. Pending continuation test;
15. compensation metadata test;
16. JSON round-trip fixtures, не фиксирующие provider-specific persistence shape.

На этом PR не требуется реализация полной SQLite schema, сетевого транспорта или боевой логики.

---

# 33. Критерии приёмки

ADR считается реализованным, когда:

- [ ] одна command model используется local и network adapters;
- [ ] `CommandId` является единственным core idempotency key;
- [ ] command fingerprint реализован и тестируется;
- [ ] duplicate не создаёт повторный effect;
- [ ] CommandId mismatch безопасно отклоняется;
- [ ] command statuses ограничены Accepted/Pending/Rejected;
- [ ] Pending является committed terminal result команды;
- [ ] continuation создаёт новый CommandId;
- [ ] RootCommandId и CorrelationId распространяются;
- [ ] DomainEvent immutable;
- [ ] event batch имеет TransactionId и deterministic order;
- [ ] CampaignRevision/AggregateRevision/EventSequence соответствуют разделу 15;
- [ ] state-changing command не может committed без event;
- [ ] rejected command не меняет gameplay state;
- [ ] event publication невозможна до commit;
- [ ] raw event не используется как unrestricted wire message;
- [ ] compensation создаёт новые events;
- [ ] crash-before/after-commit tests проходят;
- [ ] Core tests запускаются без Unity Editor;
- [ ] CI блокирует нарушение ключевых contracts;
- [ ] task/PR evidence содержит результаты tests.

---

# 34. Последствия

## 34.1 Положительные

- единый command path для offline и online;
- безопасный retry;
- предсказуемая atomicity;
- детерминированные rolls;
- явные pending workflows;
- хорошая трассировка;
- понятная compensation history;
- меньше риска скрытых side effects в Codex-generated code;
- независимое тестирование Core;
- безопасная audience publication.

## 34.2 Стоимость

- необходимо хранить command receipts и fingerprints;
- потребуется mapping transport/application/persistence DTO;
- continuation workflows создают больше явных command types;
- нельзя быстро вызывать handler из handler;
- compensation требует отдельного domain design;
- event metadata становится объёмнее;
- atomic outbox/persistence реализация сложнее простого CRUD.

Стоимость принимается как необходимая для host-authoritative VTT.

---

# 35. Рассмотренные альтернативы

## 35.1 Client-authoritative mutations

Отклонено: нарушает permissions, visibility, revisions и безопасность.

## 35.2 Полное event sourcing

Не требуется для MVP. Оно увеличивает migration/rebuild complexity. Odyssey сохраняет current state и event journal атомарно.

## 35.3 CRUD без DomainEvents

Отклонено: теряются audit, replay evidence, compensation и sync basis.

## 35.4 Два ключа: CommandId + IdempotencyKey

Отклонено для Core: создаёт конфликт identity и неясную duplicate semantics.

## 35.5 Nested command bus

Отклонено: скрывает transaction/correlation и усложняет idempotency.

## 35.6 Mutable Pending result

Отклонено: retry старой команды становится недетерминированным.

## 35.7 Raw event broadcast

Отклонено: нарушает redaction и позволяет утечки hidden state.

## 35.8 Удаление/редактирование event при undo

Отклонено: разрушает audit и causality.

---

# 36. Отложенные решения

Этот ADR намеренно не фиксирует полностью:

- JSON polymorphism/upcasting — ADR-003;
- полный Result/Error vocabulary — ADR-004;
- concrete dependency composition — ADR-005;
- test project organization — ADR-006;
- application/build/schema version policy — ADR-007;
- RNG algorithm/seed streams и Clock details — ADR-008;
- exact SQLite schema/driver — Persistence implementation ADR;
- relay transport serializer — Networking implementation ADR;
- cryptographic ID generation algorithm — Core identity implementation decision, совместимое с ADR-003/007.

Отложенное решение не может нарушать инварианты этого ADR.

---

# 37. Трассировка

| Источник | Связь |
|---|---|
| Technical Development Baseline §15 | Уточняет Core command/event/revision/idempotency primitives |
| ADR-001 | Назначает ownership Command handlers и DomainEvents модулям |
| Domain Model §28–29 | Уточняет общие command/event envelopes и causality |
| Persistence §10–11 | Закрепляет AppliedCommands, atomic state/event/outbox/result transaction и crash semantics |
| Networking §12–15 | Закрепляет host processing, CommandResult и sequence/revision behavior |
| Rules Engine | Требует deterministic execution и fixed RNG evidence |
| Content Block System | Требует atomic/idempotent execution без direct state write |
| Test Strategy | Формирует Core/contract/integration tests и release evidence |

---

# 38. Нормативное действие

С момента принятия ADR-002:

- `CommandId` является единственным core idempotency key Application command;
- поле `IdempotencyKey` из предварительного command envelope Technical Development Baseline не применяется как отдельное поле;
- действует envelope и lifecycle этого ADR;
- одна command transaction не содержит nested command handlers;
- Pending result не мутирует после commit;
- DomainEvent envelope этого ADR является техническим baseline для Core primitives;
- raw DomainEvent не является публичным network DTO;
- конфликтующая реализация считается архитектурным дефектом и блокирует merge.

---

**Конец документа**
