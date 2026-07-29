# ADR-008 — Deterministic Clock and RNG

**Документ:** `docs/adr/ADR-008_Deterministic_Clock_and_RNG_v1.0.md`  
**ADR:** ADR-008  
**Версия:** 1.0  
**Дата:** 27 июля 2026 года  
**Статус:** Accepted  
**Область:** authoritative wall time, monotonic duration, WorldClock separation, deadlines, virtual scheduler, deterministic random streams, production RNG algorithm, key lifecycle, random evidence, retry/replay semantics, Unity/.NET parity и `SLICE-00` clock/RNG scaffold  
**Связанные этапы:** Roadmap Stage 1, `SLICE-00`, Milestone `M1`, последующие Rules, Dice, Combat, Pending Interaction, Session и Persistence slices  
**Базовые документы:** `TECHNICAL_DEVELOPMENT_BASELINE_Odyssey_VTT_v0.1.md`, `03_Domain_Model_Odyssey_VTT_v0.25.md`, `04_Odyssey_Rules_Engine_Odyssey_VTT_v0.6.md`, `05_Persistence_Odyssey_VTT_v0.8.md`, `06_Networking_and_Session_Sync_Odyssey_VTT_v0.8.md`, `09_Dice_And_Game_Log_Odyssey_VTT_v0.3.md`, `11_Content_Block_System_Odyssey_VTT_v0.1.md`, `12_Combat_And_Actions_Odyssey_VTT_v0.1.md`, `16_Test_Strategy_Odyssey_VTT_v0.1.md`, `ADR-002_Command_and_Domain_Event_Model_v1.0.md`, `ADR-003_Serialization_Strategy_v1.0.md`, `ADR-004_Result_and_Error_Model_v1.0.md`, `ADR-005_Dependency_Composition_v1.0.md`, `ADR-006_Test_Project_Structure_and_Dual_Unity_DotNet_Compilation_v1.0.md`, `ADR-007_Versioning_and_Build_Identity_v1.0.md`

---

# 1. Решение

Odyssey VTT использует **четыре явно разделённых понятия времени** и **контекстно производимые независимые random streams**. Авторитетная игровая логика не читает глобальные часы и не потребляет глобальную последовательность случайных чисел.

Обязательные решения:

1. Реальное UTC-время хоста доступно только через `IWallClock`.
2. Измерение прошедшей длительности доступно только через `IMonotonicClock`.
3. Ожидания и runtime deadlines выполняются через `IDelayScheduler`/виртуализируемый scheduler, а не прямой `Task.Delay` в Application-коде.
4. Кампанийный `WorldClock` является отдельным доменным aggregate и не связан автоматически с часами компьютера.
5. В доменных и авторитетных контрактах wall-clock instant хранится как типизированный UTC instant; локальная timezone используется только при отображении.
6. `DateTime.Now`, `DateTime.UtcNow`, `DateTimeOffset.Now`, `DateTimeOffset.UtcNow`, `Stopwatch`, `Environment.TickCount`, Unity `Time.*` и прямой `Task.Delay` запрещены вне утверждённых infrastructure/presentation adapters.
7. Порядок событий определяется sequence/revision, а не timestamp.
8. Все события одной command transaction получают один общий `OccurredAtHost`, если специализированный контракт явно не требует иного.
9. Длительности, performance measurements и активные runtime timeout используют monotonic time, а не wall clock.
10. Durable deadline хранит UTC due instant; в живом процессе он переводится в monotonic deadline, а после restart пересчитывается заново.
11. Production randomness вызывается только на host-authoritative path после duplicate, authorization, validation, revision и resource checks.
12. В Odyssey отсутствует один mutable global RNG stream для всей кампании или процесса.
13. Каждое авторитетное random decision получает независимый stream, детерминированно производимый из host-secret campaign key и стабильного decision context.
14. Повтор той же команды с тем же `CommandId` не создаёт новый random result; committed result возвращается из command receipt.
15. Если transaction не была committed и команда безопасно повторяется с тем же `CommandId`, stream derivation даёт те же значения при неизменных command/ruleset inputs.
16. Production pseudorandom algorithm версии 1 — `xoshiro256**` с точной зафиксированной state transition.
17. Stream derivation версии 1 — `HMAC-SHA-256` от campaign RNG key и канонического context message.
18. Приведение `UInt64` к целочисленному inclusive range выполняется rejection sampling без modulo bias.
19. Random algorithm, derivation algorithm и bounded-mapping algorithm имеют независимые версии и не меняются молча.
20. Raw campaign RNG key никогда не передаётся клиентам, не попадает в public projection, обычный log или `RngProofData`.
21. `RngProofData` в MVP является diagnostic/reproduction evidence, но не криптографическим доказательством честности броска.
22. Все значимые random outputs сохраняются в DomainEvents/CalculationTrace; replay состояния не вызывает RNG заново.
23. Presentation-only visual randomness разрешена только в Unity Client и не может влиять на авторитетное состояние, сеть, сохранения, журнал или тестовые ожидания.
24. Tests используют fixed/manual clocks, virtual monotonic scheduler, `SequenceRandomSource` либо production-algorithm vectors с явным key/seed evidence.
25. Unity, pure .NET и IL2CPP обязаны давать одинаковые результаты для утверждённых Clock/RNG contract vectors.
26. Изменение production algorithm, derivation message, endian rules, bounded mapping, key lifecycle или authoritative clock semantics требует amendment либо superseding ADR.

Этот ADR является нормативным authority по Clock/RNG. Он заменяет предварительные разделы 15.6–15.7 Technical Development Baseline и уточняет RNG contracts Rules Engine, Dice/Log и Command/Event Model без изменения продуктовой механики.

---

# 2. Контекст и проблема

Odyssey VTT одновременно использует:

- реальные timestamps операций хоста;
- длительность сетевых и инфраструктурных операций;
- таймеры Pending Interaction;
- игровые даты кампании;
- боевые ходы и раунды;
- броски кубов;
- случайные системные решения;
- повтор команд после timeout/reconnect;
- event replay;
- deterministic tests;
- Unity Editor, Mono, pure .NET и IL2CPP.

Если использовать глобальные API напрямую, возникают ошибки:

- тест проходит или падает в зависимости от текущей даты;
- перевод системных часов назад ломает timeout;
- local timezone меняет сериализованный timestamp;
- event ordering определяется неточными часами вместо sequence;
- повтор команды после network timeout создаёт новый бросок;
- параллельная обработка меняет порядок потребления global RNG;
- добавление нового случайного вызова в одной подсистеме меняет все будущие результаты кампании;
- Unity и .NET дают разные результаты из-за разных random implementations;
- replay событий повторно выполняет RNG;
- клиент подменяет время или random result;
- `Task.Delay` делает тесты медленными и flaky;
- `UnityEngine.Random` зависит от общего process state;
- доказать источник конкретного броска невозможно.

Нужна единая модель, в которой:

1. каждый тип времени имеет собственное назначение;
2. authoritative logic получает время явно;
3. durable и in-memory timers не смешиваются;
4. random result привязан к конкретному decision context;
5. retry и replay безопасны;
6. production и test implementations взаимозаменяемы через явные ports;
7. алгоритмы закреплены test vectors и version metadata.

---

# 3. Движущие факторы

Решение оптимизировано под:

1. Host-authoritative модель.
2. Идемпотентные команды ADR-002.
3. Atomic persistence state/events/result/outbox.
4. Pure C# Domain/Rules без Unity dependency.
5. Dual Unity/.NET compilation ADR-006.
6. Повторяемые DomainScenario и contract tests.
7. Roll history и CalculationTrace.
8. Restart/reconnect recovery.
9. Отсутствие скрытых числовых GM modifiers.
10. Cross-platform bit-exact integer algorithm на Windows x64/Mono/IL2CPP.
11. Минимум внешних зависимостей.
12. Возможность будущего security/verifiable-randomness ADR без переписывания Domain contracts.
13. Отделение WorldClock от реального времени.
14. Отсутствие зависимости игровой механики от frame rate.
15. Явную диагностику clock discontinuity и RNG evidence.

---

# 4. Термины

## 4.1 Wall clock

Источник календарного UTC-времени хоста. Может корректироваться ОС, NTP или пользователем и потому не является monotonic.

## 4.2 Monotonic clock

Источник относительного времени, который не идёт назад в пределах process lifetime. Используется для измерения duration и живых timeout.

## 4.3 WorldClock

Авторитетный aggregate кампании с вымышленной игровой датой. Изменяется командами и не связан автоматически с wall clock.

## 4.4 Scheduler

Абстракция ожидания/пробуждения по monotonic duration. В тестах заменяется виртуальным scheduler без реального ожидания.

## 4.5 Durable deadline

Persisted правило завершения или истечения, содержащее UTC due instant и policy metadata. Не зависит от существования текущего process.

## 4.6 Random decision

Одна семантически значимая авторитетная операция случайности: roll, die group, target selection или другое versioned rules decision.

## 4.7 Random stream

Изолированная детерминированная последовательность `UInt64`, принадлежащая одному random decision context.

## 4.8 Campaign RNG key

Секретный 256-bit key кампании, используемый HMAC-derivation. Это не пользовательский seed и не public data.

## 4.9 RNG key epoch

Версионированная запись конкретного campaign RNG key. Новые decisions используют active epoch; старые evidence сохраняют использованный epoch ID.

## 4.10 Draw index

Нулевой последовательный индекс логического random draw внутри stream.

## 4.11 Raw step

Один `UInt64` output PRNG. Один logical draw может потребовать несколько raw steps из-за rejection sampling.

## 4.12 RngProofData

Не секретный diagnostic record алгоритма, stream identity и mapping evidence. Не содержит raw key.

---

# 5. Четыре временных домена

| Временной домен | Authority | Хранится | Основное назначение |
|---|---|---:|---|
| Host wall UTC | authoritative host adapter | да, когда является фактом/audit | timestamps, durable deadlines |
| Process monotonic | local runtime | нет | duration, timeout execution, performance |
| Campaign WorldClock | campaign aggregate | да | игровое время и GameTime effects |
| Presentation/local time | client UI | только preference | отображение UTC пользователю |

Запрещено заменять один домен другим.

Примеры:

- cooldown, выраженный игровыми раундами, не использует wall clock;
- network timeout не использует WorldClock;
- event ordering не использует presentation local time;
- анимация UI не продвигает combat turn;
- real-time system clock не продвигает WorldClock.

---

# 6. Типизированные Clock contracts

## 6.1 UtcInstant

В Core используется value object `UtcInstant`, оборачивающий UTC `DateTimeOffset`.

Инварианты:

- offset всегда `+00:00`;
- `DateTimeKind.Unspecified` не принимается;
- local offset нормализуется при входе adapter boundary;
- arithmetic выполняется checked;
- сравнение основано на instant, а не textual offset;
- Domain не вызывает системные API для его создания.

Canonical JSON representation:

```text
YYYY-MM-DDTHH:mm:ss.fffffffZ
```

Используется invariant Gregorian UTC representation независимо от календаря кампании.

## 6.2 IWallClock

```csharp
public interface IWallClock
{
    UtcInstant GetUtcNow();
}
```

Правила:

- вызов разрешён Application/Infrastructure, но не Domain entity и не Rules formula;
- production adapter является единственным местом прямого чтения `DateTimeOffset.UtcNow`;
- один semantic timestamp sampled один раз и передаётся как value;
- вызывающий код не предполагает, что следующий value больше предыдущего.

## 6.3 MonotonicTimestamp

Opaque value object. Он действителен только для конкретного `IMonotonicClock` instance/process lifetime и не сериализуется.

## 6.4 IMonotonicClock

```csharp
public interface IMonotonicClock
{
    MonotonicTimestamp GetTimestamp();
    TimeSpan GetElapsedTime(
        MonotonicTimestamp start,
        MonotonicTimestamp end);
}
```

Production adapter может использовать `Stopwatch`, но `Stopwatch` не просачивается в Core contract.

## 6.5 IDelayScheduler

```csharp
public interface IDelayScheduler
{
    ValueTask DelayAsync(
        TimeSpan delay,
        CancellationToken cancellationToken);
}
```

Правила:

- Application workflow получает scheduler через constructor injection;
- `TimeSpan.Zero` завершается без реального ожидания;
- отрицательная duration отклоняется validation error;
- cancellation не преобразуется в domain rejection;
- business timeout не доказывается произвольным sleep;
- scheduler не является durable job queue.

---

# 7. Sampling policy для команд и событий

## 7.1 ReceivedAtHost

Ingress adapter фиксирует attempt timestamp при получении command message.

Для первого process attempt он может стать `FirstReceivedAtHost` command receipt metadata. Повторная доставка того же `CommandId` может иметь новый diagnostic attempt timestamp, но не изменяет сохранённый исходный command result.

## 7.2 OccurredAtHost

После всех pre-RNG validation и до формирования event batch Application получает один UTC sample:

```text
TransactionOccurredAtHost
```

По умолчанию все события одной command transaction используют этот instant. Их порядок определяется:

```text
TransactionEventIndex
AggregateRevision
GlobalSequence / StreamSequence
```

а не различием timestamp.

## 7.3 CompletedAtHost

Фиксируется после durable commit и используется для response/diagnostics. Он не изменяет event semantics и не участвует в ordering.

## 7.4 Duplicate command

Duplicate attempt:

- не создаёт новый DomainEvent;
- не меняет `OccurredAtHost` исходного result;
- может иметь отдельную transport diagnostic запись;
- возвращает original committed timestamps/result.

## 7.5 Clock regression

Если wall clock идёт назад:

- event sequence остаётся authority порядка;
- timestamps не переписываются задним числом;
- Infrastructure создаёт structured warning с measured wall/monotonic divergence;
- бизнес-логика не сортирует события по timestamp;
- система не подменяет фактический UTC скрытым clamp без отдельного contract.

---

# 8. WorldClock separation

`WorldClock` из Domain Model остаётся отдельным aggregate.

Обязательные правила:

1. `WorldClock.CurrentGameDateTime` не является `UtcInstant`.
2. `IWallClock.GetUtcNow()` не продвигает WorldClock.
3. Перезапуск приложения не продвигает WorldClock на время простоя.
4. GameTime effects истекают после авторитетной команды изменения WorldClock.
5. Во время активного CombatEncounter WorldClock подчиняется правилам Domain Model и не заменяет turn/round counters.
6. Перемотка WorldClock не перематывает event history.
7. Presentation может одновременно показывать real UTC/local time и campaign date, но это разные поля.
8. Конвертация custom calendar date в UTC запрещена, если ruleset явно не определил такую механику.

---

# 9. Runtime durations и performance timing

Для следующих задач используется только monotonic clock:

- network response timeout;
- reconnect grace period;
- UI operation duration;
- startup phase duration;
- asset transfer elapsed time;
- performance benchmark;
- debounce/throttle внутри живого process;
- delay перед infrastructure retry;
- background operation watchdog.

Monotonic measurements:

- не сохраняются как absolute timestamps;
- не сравниваются между разными process;
- не отправляются как авторитетное время клиента;
- могут сохраняться только как duration metric.

Unity `Time.deltaTime`, `Time.unscaledTime` и `Time.realtimeSinceStartup` разрешены в Presentation/visual animation adapters, но запрещены как источник authoritative duration.

---

# 10. Durable deadlines и timeout

## 10.1 Persisted form

Durable timeout record содержит минимум:

```text
DeadlineId
CreatedAtUtc
DueAtUtc
TimeoutPolicyId
TimeoutPolicyVersion
RelatedEntityId
Status
CreatedByCommandId
Revision
```

Duration может храниться как audit metadata, но authority после commit — `DueAtUtc` и policy version.

## 10.2 В живом process

При загрузке deadline:

1. host читает current UTC через `IWallClock`;
2. вычисляет remaining duration;
3. создаёт monotonic wait;
4. по пробуждении повторно проверяет persisted state и current UTC;
5. отправляет отдельную timeout command;
6. только command transaction может перевести domain state в Expired.

Scheduler callback сам не изменяет aggregate.

## 10.3 Restart

После restart:

- in-memory wait не восстанавливается напрямую;
- deadline перечитывается из authoritative persistence;
- remaining duration пересчитывается по host UTC;
- overdue record создаёт timeout command;
- duplicate timeout command безопасен через CommandId/idempotency policy.

## 10.4 Clock discontinuity

Production time service сравнивает wall elapsed с monotonic elapsed в рамках процесса.

При существенном расхождении:

- создаётся diagnostic event;
- runtime waits перепроверяют persisted `DueAtUtc`;
- authority остаётся у host UTC для durable deadline;
- автоматическое изменение WorldClock запрещено;
- exact alert threshold является configuration value и не меняет domain semantics.

## 10.5 Default policy

Как закреплено Dice/Log, timeout RollRequest по умолчанию выключен. Наличие scheduler не включает timeout автоматически.

---

# 11. Запрещённые API времени

В Core production source запрещены прямые вызовы:

```text
DateTime.Now
DateTime.UtcNow
DateTime.Today
DateTimeOffset.Now
DateTimeOffset.UtcNow
Stopwatch.StartNew / GetTimestamp
Environment.TickCount / TickCount64
Thread.Sleep
Task.Delay
UnityEngine.Time.*
UnityEngine.WaitForSeconds*
```

Исключения:

- `SystemWallClockAdapter`;
- `SystemMonotonicClockAdapter`;
- `SystemDelayScheduler`;
- Unity presentation animation adapters;
- bounded infrastructure/test-runner polling, явно не являющийся business timeout.

Исключение должно быть разрешено assembly/path policy и architecture test.

---

# 12. Authoritative RNG architecture

## 12.1 Отсутствие global stream

Odyssey не хранит mutable глобальный PRNG state вида:

```text
Campaign.CurrentRandomState
Session.GlobalRandom
Random.Shared
UnityEngine.Random.state
```

Причины:

- порядок параллельных команд не должен менять результаты друг друга;
- добавление нового random call не должно сдвигать все будущие rolls;
- retry после rollback должен воспроизводиться;
- отдельный roll должен иметь понятное evidence;
- tests должны изолировать decisions.

## 12.2 Random decision context

Каждое decision описывается:

```text
RandomDecisionContext
├── CampaignId
├── RootCommandId
├── DecisionOrdinal
├── Purpose
├── RulesetVersion
├── RngAlgorithmVersion
├── RngDerivationVersion
├── BoundedMappingVersion
└── RngKeyEpochId
```

Дополнительные metadata (`SessionId`, `CombatEncounterId`, `RollId`, `TargetId`) могут сохраняться для trace, но не меняют derivation message, если не включены новой версией derivation contract.

## 12.3 DecisionOrdinal

`DecisionOrdinal` — нулевой стабильный порядковый номер random decision внутри root command execution plan.

Правила:

- формируется после validation;
- порядок определяется rules contract, canonical target order и formula AST;
- не зависит от thread scheduling;
- не создаётся через новый GUID;
- при retry той же команды формируется одинаково;
- изменение порядка decisions является rules/contract change и требует tests/version review.

Для dice roll один roll обычно владеет одним stream, а отдельные dice используют draw indexes внутри него.

---

# 13. Campaign RNG key lifecycle

## 13.1 Создание

При создании кампании Infrastructure создаёт 256-bit key через OS cryptographic random source.

Key generation:

- выполняется только approved key generator adapter;
- использует cryptographically secure random bytes;
- не использует `System.Random`, timestamp, GUID или user password как entropy;
- сохраняется до принятия команд, которым нужен RNG.

## 13.2 Хранение

Authoritative persistence хранит:

```text
RngKeyEpoch
├── RngKeyEpochId
├── CampaignId
├── KeyVersion
├── SecretKeyMaterial
├── CreatedAtUtc
├── ActivatedAtUtc
├── RetiredAtUtc?
└── Status
```

Правила:

- запись относится к host-secret storage;
- key не входит в player snapshot/projection;
- key не попадает в обычный application log;
- key не отображается UI;
- full owner backup должен сохранять активные key epochs;
- sanitized/content-only export исключает key и создаёт новый key при создании новой кампании;
- encryption-at-rest и owner-key protection уточняются будущим Security ADR.

## 13.3 Rotation

MVP не требует пользовательской ротации, но schema поддерживает epoch ID.

При будущей ротации:

- новые decisions используют новый active epoch;
- старые key epochs сохраняются, пока нужны для unresolved retry/pending diagnostics;
- сохранённые rolls не пересчитываются;
- event replay использует сохранённые outputs и не требует key;
- удаление key epoch требует отдельной retention/security policy.

## 13.4 Seed terminology

В production используется термин `CampaignRngKey`, а не публичный `seed`.

В тестах допускается явный deterministic seed/key fixture. Он сохраняется в TestEvidence.

---

# 14. Stream derivation v1

## 14.1 Алгоритм

```text
RngDerivationVersion = 1
Algorithm = HMAC-SHA-256
```

HMAC key — 32 bytes `CampaignRngKey` выбранного epoch.

## 14.2 Canonical message

Message состоит из полей в строгом порядке:

```text
1. UTF-8 "odyssey-rng-stream-v1"
2. CampaignId canonical lowercase string
3. RootCommandId canonical lowercase string
4. DecisionOrdinal as UInt32 big-endian
5. Purpose canonical case-sensitive identifier
6. RulesetVersion canonical SemVer string
7. RngAlgorithmVersion as UInt32 big-endian
8. BoundedMappingVersion as UInt32 big-endian
9. RngKeyEpochId canonical lowercase string
```

Каждое UTF-8/string field кодируется:

```text
UInt32 big-endian byte length
raw UTF-8 bytes without BOM
```

Integer fields кодируются без textual conversion.

Запрещены:

- culture-sensitive formatting;
- platform-native GUID byte order;
- JSON serializer defaults;
- unordered dictionaries;
- optional omission неизвестных полей;
- изменение регистра идентификаторов.

## 14.3 State creation

32-byte HMAC result разбивается на четыре consecutive `UInt64` little-endian words:

```text
s0, s1, s2, s3
```

Если все четыре равны нулю, state повторно производится через HMAC того же message с дополнительным byte `0x01`.

`StreamId` является SHA-256 от canonical message без secret key и может сохраняться публично. Он идентифицирует context, но не раскрывает key/state.

`SeedCommitment` является SHA-256 от:

```text
"odyssey-rng-key-commitment-v1" || RngKeyEpochId || SecretKeyMaterial
```

и сохраняется только как hash.

---

# 15. Production PRNG v1 — xoshiro256**

## 15.1 Version

```text
RngAlgorithmVersion = 1
RngAlgorithmId = odyssey.xoshiro256starstar.v1
```

## 15.2 Arithmetic

- unsigned 64-bit integers;
- unchecked wraparound modulo `2^64`;
- fixed left rotate;
- no floating-point operations;
- exact state transition below.

## 15.3 NextUInt64

Псевдокод:

```text
result = rotl(s1 * 5, 7) * 9

t = s1 << 17

s2 = s2 XOR s0
s3 = s3 XOR s1
s1 = s1 XOR s2
s0 = s0 XOR s3

s2 = s2 XOR t
s3 = rotl(s3, 45)

return result
```

`rotl(x, k)`:

```text
(x << k) OR (x >> (64 - k))
```

Все операции выполняются над `UInt64`.

## 15.4 Почему выбран этот алгоритм

- простая полностью целочисленная реализация;
- одинаковое поведение в .NET, Mono и IL2CPP;
- достаточная скорость для dice/rules decisions;
- маленькое состояние;
- легко закрепляется golden vectors;
- не требует стороннего package.

Он **не является криптографически безопасным**. За непредсказуемость streams отвечает secret HMAC-derived state и отсутствие key у клиентов; этот ADR не объявляет verifiable fairness.

---

# 16. Inclusive integer mapping v1

## 16.1 Contract

```csharp
RandomSample NextInclusive(
    int minInclusive,
    int maxInclusive,
    int drawIndex);
```

Validation выполняется до raw step:

- `minInclusive <= maxInclusive`;
- range вычисляется в widened integer arithmetic;
- unsupported range отклоняется без потребления stream;
- draw index обязан совпадать с expected next logical index.

## 16.2 Rejection sampling

```text
BoundedMappingVersion = 1
BoundedMappingId = odyssey.rejection-u64.v1

range = UInt64(Int64(max) - Int64(min) + 1)
threshold = ((0 - range) modulo 2^64) modulo range

repeat:
    raw = NextUInt64()
until raw >= threshold

offset = raw modulo range
result = Int64(min) + Int64(offset)
```

Это исключает modulo bias.

## 16.3 Draw accounting

- один logical draw увеличивает `DrawIndex` на 1;
- `RawStepCount` может быть больше 1;
- rejection count сохраняется evidence;
- следующий logical draw начинается после последнего raw step предыдущего;
- invalid request не изменяет stream state.

## 16.4 Dice

- `d20` использует `NextInclusive(1, 20)`;
- `d100` использует один logical draw `NextInclusive(1, 100)`;
- `NdX` выполняет dice в canonical AST/group order;
- каждый natural result сохраняется отдельно;
- modifiers никогда не меняют natural result.

---

# 17. RNG interfaces

## 17.1 Factory

```csharp
public interface IAuthoritativeRandomStreamFactory
{
    Result<IAuthoritativeRandomStream> Create(
        RandomDecisionContext context);
}
```

Factory:

- является Application-owned service, реализованным в `Odyssey.Application`;
- получает secret key через Application-owned host-only port, реализованный Persistence;
- не доступна remote client profile;
- не вызывается до pre-RNG validation;
- не сохраняет global mutable stream state.

## 17.2 Stream

```csharp
public interface IAuthoritativeRandomStream
{
    RandomStreamIdentity Identity { get; }

    Result<RandomSample> NextInclusive(
        int minInclusive,
        int maxInclusive,
        int drawIndex);
}
```

`RandomSample` содержит минимум:

```text
Value
DrawIndex
RawStepCount
RngAlgorithmVersion
RngDerivationVersion
BoundedMappingVersion
StreamId
RngKeyEpochId
SeedCommitment
```

## 17.3 Rules boundary

Rules Engine получает уже открытый stream либо минимальный `IRandomSource`, привязанный к одному decision. Rules не получает campaign key и не создаёт streams самостоятельно.

Application отвечает за:

1. validation;
2. stable decision ordinal;
3. stream creation;
4. передачу stream Rules;
5. сбор evidence;
6. atomic persistence результата.

---

# 18. Retry, rollback и replay

## 18.1 Duplicate после commit

Если command receipt существует:

- RNG не вызывается;
- stream не создаётся;
- возвращается original `CommandResult`;
- original events/rolls остаются неизменными.

## 18.2 Retry после transaction rollback

Если terminal outcome не был committed:

- клиент повторяет тот же `CommandId` согласно ADR-004;
- CampaignRngKey epoch остаётся доступным;
- canonical context повторяется;
- stream state и outputs совпадают;
- новый successful commit сохраняет один result.

## 18.3 Retry после version change

Random command разрешается только под pinned campaign `RulesetVersion` и поддерживаемыми RNG versions.

Если required version больше не поддерживается:

- command не пересчитывается по новому алгоритму;
- возвращается compatibility failure;
- требуется upgrade/migration policy;
- скрытая смена алгоритма запрещена.

## 18.4 Event replay

Replay:

- применяет сохранённые event payload;
- не создаёт random stream;
- не проверяет fairness повторным roll;
- может отдельно выполнить diagnostic reproduction tool;
- reproduction никогда не меняет authoritative state.

## 18.5 Pending workflow

Command, завершившаяся `Pending`, не расходует RNG, если random decision ещё не должен был произойти.

Continuation command:

- имеет новый `CommandId`;
- получает собственные decision ordinals/streams;
- сохраняет causation/correlation links;
- не продолжает mutable stream предыдущей command transaction.

---

# 19. RngProofData

## 19.1 Минимальный состав

```text
RngProofData
├── RngAlgorithmId
├── RngAlgorithmVersion
├── RngDerivationVersion
├── BoundedMappingVersion
├── RngKeyEpochId
├── SeedCommitment
├── StreamId
├── DecisionOrdinal
├── DrawIndex
├── RequestedMin
├── RequestedMax
├── RawStepCount
└── Result
```

## 19.2 Visibility

Player-visible projection может содержать:

- algorithm/version;
- StreamId;
- SeedCommitment;
- draw/range/result evidence, если audience roll это разрешает.

Никогда не содержит:

- secret key;
- derived internal state words;
- другие hidden rolls;
- GM-secret context fields;
- future stream state.

## 19.3 Не является proof of fairness

MVP evidence позволяет:

- диагностировать версию алгоритма;
- повторить roll владельцу authoritative key/test fixture;
- проверить mapping и сохранённый result;
- обнаружить рассинхрон implementation.

Оно не позволяет игроку независимо доказать, что GM не выбрал другой campaign key заранее. Commit-reveal, public beacon или cryptographic verifiable RNG требуют отдельного Security ADR.

---

# 20. Presentation-only randomness

Unity Client может использовать visual randomness для:

- particle variation;
- decorative animation;
- non-authoritative sound variation;
- editor preview, не сохраняемого как campaign fact.

Ограничения:

- не влияет на hit, damage, target, resource, visibility или permissions;
- не отправляется как authoritative network state;
- не попадает в DomainEvent;
- не меняет command fingerprint;
- не используется для ID;
- не используется для stable UI ordering;
- visual test может заменить его deterministic source.

`UnityEngine.Random` разрешён только в явно presentation-only assembly/path. Architecture tests запрещают его в Domain, Rules, Content, Application, Persistence и Networking.

---

# 21. ID generation и randomness

ID generation является отдельным concern.

- `Guid.NewGuid()` не используется как игровой random result.
- Core получает `IIdGenerator` через composition.
- Тесты используют deterministic ID source.
- RandomDecision `DecisionOrdinal` не создаётся новым GUID.
- RollId/EventId могут быть generated identifiers, но stream derivation не зависит от случайно созданного RollId.
- Изменение ID generator не должно менять dice output.

---

# 22. Concurrency и ordering

Независимые streams позволяют параллельную подготовку решений только там, где Application contract разрешает concurrency.

Обязательные правила:

1. Одинаковый context всегда создаёт одинаковый stream.
2. Разные commands не делят mutable PRNG state.
3. Thread scheduling не влияет на result.
4. Multi-target order задаётся canonical target order из rules contract.
5. `DecisionOrdinal` вычисляется до параллельного выполнения.
6. Event batch ordering остаётся ADR-002 authority.
7. Random output не используется как tie-breaker, если ruleset не определил это явно.
8. Dictionary/hash iteration order не определяет draw order.

---

# 23. Serialization и persistence

## 23.1 Clock

Persisted wall instant использует canonical UTC format ADR-003.

Не сохраняются:

- monotonic timestamps;
- Stopwatch ticks;
- Unity frame/time values;
- local timezone offset как authority.

## 23.2 RNG

Persisted random facts включают:

- final natural values;
- algorithm/derivation/mapping versions;
- StreamId;
- key epoch ID;
- non-secret evidence;
- CalculationTrace links.

Campaign RNG key material хранится отдельно от event payload и player projections.

## 23.3 Atomicity

Для resolved action в одной authoritative transaction сохраняются:

- state changes;
- event batch;
- DiceRoll/CalculationTrace;
- RngProofData;
- command result/receipt;
- outbox records.

Crash не может оставить result без evidence или evidence без committed action.

---

# 24. Network semantics

- remote client не генерирует authoritative RNG;
- client-provided die value игнорируется/отклоняется;
- client timestamp не становится `OccurredAtHost`;
- latency измеряется monotonic локально и не определяет правила;
- host projection передаёт только разрешённую аудитории random evidence;
- duplicate/reconnect возвращает committed result;
- clock offset клиента может использоваться только для presentation diagnostics;
- network timeout не означает command rejection;
- после timeout client проверяет status по тому же `CommandId`.

---

# 25. Test implementations

## 25.1 FixedWallClock

Возвращает один заданный `UtcInstant`.

Используется для:

- event timestamp assertions;
- serialization vectors;
- deterministic command tests.

## 25.2 ManualWallClock

Тест явно переводит wall time вперёд или назад.

Используется для:

- durable deadline;
- clock regression;
- restart recovery;
- timezone-independent tests.

## 25.3 VirtualMonotonicClock

Начинается с нулевого opaque timestamp и продвигается только test API.

## 25.4 VirtualDelayScheduler

Delay регистрирует ожидание и завершается при продвижении virtual monotonic time. Реальный sleep отсутствует.

## 25.5 SequenceRandomSource

Rules golden test может передать заранее заданную последовательность logical values. Такой test проверяет rules interpretation, а не production PRNG implementation.

## 25.6 Production vector fixture

Contract tests используют фиксированный 32-byte key и contexts для проверки:

- HMAC message encoding;
- initial state;
- первые `NextUInt64` values;
- rejection mapping;
- evidence;
- Unity/.NET/IL2CPP parity.

Test key не используется production campaign.

---

# 26. Обязательные contract vectors

Repository содержит versioned vectors минимум для:

1. UTC instant canonical serialization.
2. Wall clock normalization from non-zero offset.
3. Monotonic elapsed calculation.
4. Virtual scheduler completion order.
5. HMAC derivation message bytes.
6. HMAC digest.
7. Initial xoshiro state words.
8. First 16 raw `UInt64` outputs.
9. Inclusive range `1..20` outputs.
10. Inclusive range `1..100` outputs.
11. Negative/positive integer range.
12. Rejection path с `RawStepCount > 1`.
13. Different DecisionOrdinal gives different StreamId.
14. Same context gives identical stream.
15. Different campaign key gives different result.
16. Duplicate command causes zero RNG calls.
17. Roll evidence serialization.
18. Mono/.NET/IL2CPP parity.

Golden vector update требует:

- явного version impact;
- ADR amendment/superseding ADR, если semantics изменились;
- review владельца архитектуры;
- запрета auto-accept Codex.

---

# 27. Architecture enforcement

Static/architecture checks блокируют production references к запрещённым API.

Минимальные правила:

```text
Domain/Rules/Content/Application:
- no System.Random construction
- no Random.Shared
- no UnityEngine.Random
- no DateTime*.Now/UtcNow
- no Stopwatch
- no Environment.TickCount
- no Task.Delay/Thread.Sleep
- no UnityEngine.Time

Persistence/Networking:
- allowed only through approved adapter paths
- no gameplay random
- no client timestamp authority

Unity Client:
- visual Time/Random only in Presentation namespace/path
- no authoritative gameplay decision
```

Review-only комментарий без автоматической проверки не считается достаточным.

---

# 28. Error model

Clock/RNG failures используют ADR-004.

Минимальные stable codes:

```text
Time.InvalidUtcInstant
Time.InvalidDuration
Time.Deadline.Invalid
Time.ClockDiscontinuity.Detected
Random.Context.Invalid
Random.KeyEpoch.NotFound
Random.KeyMaterial.Unavailable
Random.Range.Invalid
Random.DrawIndex.Mismatch
Random.Algorithm.Unsupported
Random.Derivation.Unsupported
Random.Mapping.Unsupported
Random.Evidence.Mismatch
```

Категории:

- invalid input → `Validation`;
- unsupported version → `Compatibility`;
- missing/corrupt secret key → `Integrity` или `Configuration`;
- unexpected adapter failure → `TransientInfrastructure`/`Internal`;
- clock discontinuity warning сам по себе не является command rejection.

Secret data не включается в Error metadata.

---

# 29. Composition и lifetimes

По ADR-005:

## Process scope

- `SystemWallClockAdapter`, `SystemMonotonicClockAdapter` и `SystemDelayScheduler` как platform adapters Unity Client composition root;
- approved cryptographic key generator adapter Unity Client;
- immutable RNG algorithm registries Application.

## Campaign scope

- campaign RNG key repository/accessor, реализованный Persistence;
- Application-owned active RNG key epoch port;
- Application-owned authoritative random stream factory;
- deterministic HMAC/xoshiro/mapping implementations Application.

## Session scope

- live deadline coordinator;
- latency/performance metrics;
- session-specific diagnostics.

## Operation scope

- sampled command time context;
- random decision streams;
- CalculationTrace builder.

Remote participant profile не получает campaign RNG key accessor или authoritative stream factory.

---

# 30. CI и quality gates

Pull request блокируется, если:

1. Core использует запрещённый global time/random API.
2. Clock/RNG vectors не проходят pure .NET.
3. Unity EditMode vectors отличаются от .NET.
4. IL2CPP smoke/vector test отличается.
5. Duplicate command вызывает RNG.
6. Retry same command/context даёт другой result.
7. Event replay вызывает RNG.
8. invalid random range потребляет raw step.
9. raw key появляется в log/snapshot/projection fixture.
10. test использует arbitrary sleep для business semantics.
11. WorldClock связан с real system clock.
12. golden vectors изменены без version/ADR review.

---

# 31. Обязательные тестовые сценарии `SLICE-00`

```text
ADR008-T001 WallClock_NormalizesToUtc
ADR008-T002 UtcInstant_CanonicalRoundTrip
ADR008-T003 Domain_HasNoDirectWallClockAccess
ADR008-T004 MonotonicClock_ElapsedNeverUsesWallClock
ADR008-T005 VirtualScheduler_CompletesWithoutRealDelay
ADR008-T006 CommandTransaction_UsesSingleOccurredAt
ADR008-T007 EventOrdering_UsesSequenceNotTimestamp
ADR008-T008 DuplicateCommand_DoesNotSampleClockForNewEvents
ADR008-T009 DuplicateCommand_DoesNotCreateRandomStream
ADR008-T010 ValidationFailure_DoesNotConsumeRng
ADR008-T011 RevisionFailure_DoesNotConsumeRng
ADR008-T012 SameContext_SameKey_SameStream
ADR008-T013 DifferentOrdinal_DifferentStream
ADR008-T014 DifferentKey_DifferentStream
ADR008-T015 XoshiroRawVector_MatchesV1
ADR008-T016 InclusiveD20Vector_MatchesV1
ADR008-T017 InclusiveD100Vector_MatchesV1
ADR008-T018 RejectionSampling_IsUnbiasedMappingContract
ADR008-T019 InvalidRange_DoesNotAdvanceStream
ADR008-T020 DrawIndexMismatch_IsRejected
ADR008-T021 RetryAfterRollback_ReproducesValues
ADR008-T022 Replay_DoesNotInvokeRng
ADR008-T023 PendingBeforeDecision_DoesNotConsumeRng
ADR008-T024 Continuation_UsesNewCommandStream
ADR008-T025 RngProofData_DoesNotContainSecretKey
ADR008-T026 RemoteClient_HasNoAuthoritativeRandomFactory
ADR008-T027 WorldClock_DoesNotAdvanceWithWallClock
ADR008-T028 DurableDeadline_RestartRecalculatesRemaining
ADR008-T029 ClockRegression_DoesNotReorderEvents
ADR008-T030 DotNetAndUnity_RngVectorsMatch
ADR008-T031 Il2Cpp_RngVectorsMatch
ADR008-T032 ArchitectureScan_BlocksGlobalRandomAndTime
ADR008-T033 PresentationRandom_CannotReachAuthoritativeAssembly
ADR008-T034 TestEvidence_RecordsClockAndRngFixture
ADR008-T035 FullOwnerBackup_PreservesRngKeyEpoch
ADR008-T036 SanitizedExport_ExcludesRngKey
```

---

# 32. `SLICE-00` implementation scaffold

Минимальные артефакты:

```text
Packages/com.odyssey.domain/
  Runtime/Time/UtcInstant.cs
  Runtime/Time/Duration.cs

Packages/com.odyssey.application/
  Runtime/Time/IWallClock.cs
  Runtime/Time/IMonotonicClock.cs
  Runtime/Time/IDelayScheduler.cs
  Runtime/Time/CommandTimeContext.cs
  Runtime/Random/RandomDecisionContext.cs
  Runtime/Random/ICampaignRngKeyProvider.cs
  Runtime/Random/IAuthoritativeRandomStream.cs
  Runtime/Random/IAuthoritativeRandomStreamFactory.cs
  Runtime/Random/RandomSample.cs
  Runtime/Random/RngProofData.cs
  Runtime/Random/HmacSha256StreamDeriverV1.cs
  Runtime/Random/Xoshiro256StarStarV1.cs
  Runtime/Random/RejectionUInt64MapperV1.cs

Packages/com.odyssey.persistence/
  Runtime/Random/CampaignRngKeyRepository.cs

Packages/com.odyssey.unity.client/
  Runtime/Platform/Time/SystemWallClockAdapter.cs
  Runtime/Platform/Time/SystemMonotonicClockAdapter.cs
  Runtime/Platform/Time/SystemDelayScheduler.cs
  Runtime/Platform/Random/SystemCryptographicKeyGenerator.cs

Tests/TestKit/
  FixedWallClock.cs
  ManualWallClock.cs
  VirtualMonotonicClock.cs
  VirtualDelayScheduler.cs
  SequenceRandomSource.cs
  FixedCampaignRngKeyProvider.cs

Tests/ContractVectors/clock-rng-v1.json
```

Точные namespaces могут быть уточнены без изменения архитектуры, но ownership модулей и semantics обязательны.

---

# 33. Правила для Codex

Codex обязан:

1. Использовать injected Clock/RNG ports.
2. Выполнять validation до stream creation.
3. Не добавлять прямой system time/random API в Core.
4. Не использовать `UnityEngine.Random` для игрового результата.
5. Не использовать timestamp/GUID как seed.
6. Не добавлять mutable global random state.
7. Не менять algorithm constants, endian или derivation fields без ADR scope.
8. Не обновлять vectors автоматически ради зелёного CI.
9. Сохранять random outputs/evidence атомарно с action.
10. Не логировать campaign RNG key/state.
11. Не связывать WorldClock с real time.
12. Использовать virtual scheduler в deterministic tests.
13. Указывать Clock/RNG impact в PR summary.
14. Запускать pure .NET, Unity EditMode и требуемый IL2CPP vector gate.
15. При неизвестной версии возвращать compatibility error, а не fallback на новый алгоритм.

---

# 34. Definition of Done ADR-008 implementation

ADR считается реализованным в коде, когда:

1. Clock/RNG interfaces существуют в правильных модулях.
2. Production adapters собраны composition root.
3. Domain/Rules не имеют direct global API.
4. Fixed/manual/virtual test implementations готовы.
5. Campaign RNG key создаётся CSPRNG adapter и хранится host-only.
6. HMAC derivation v1 реализована bit-exact.
7. xoshiro256** v1 реализован bit-exact.
8. rejection mapping v1 реализован без bias.
9. vectors совпадают .NET/Unity/IL2CPP.
10. duplicate/retry/replay tests проходят.
11. RngProofData сохраняется без secret material.
12. durable deadline test проходит без real sleep.
13. architecture scans блокируют запрещённые API.
14. Developer Shell показывает non-secret Clock/RNG implementation versions.
15. Все `ADR008-T*` обязательные сценарии имеют traceability.

---

# 35. Последствия

## 35.1 Положительные

- броски воспроизводимы и диагностируемы;
- retry не создаёт другой random result;
- параллельные команды не сдвигают общий RNG;
- tests быстрые и не ждут реальные секунды;
- Unity/.NET/IL2CPP имеют один контракт;
- WorldClock не путается с часами компьютера;
- clock jumps не ломают event ordering;
- алгоритм можно эволюционировать через явные versions;
- клиент не становится authority времени или random;
- будущий verifiable RNG может быть добавлен поверх evidence model.

## 35.2 Отрицательные

- больше типов и adapters, чем при прямом `DateTime.UtcNow`/`Random`;
- требуется хранить host-secret campaign key;
- требуется поддерживать old algorithm versions;
- exact vectors усложняют изменение implementation;
- durable deadlines требуют повторной проверки после restart;
- diagnostic reproduction требует доступа к owner-side key/evidence.

Эти затраты приняты как необходимые для host-authoritative VTT и Codex-разработки.

---

# 36. Отклонённые альтернативы

## 36.1 `System.Random` с одним campaign seed

Отклонено: mutable stream order зависит от concurrency и новых random calls; стандарт implementation может меняться между runtimes.

## 36.2 `UnityEngine.Random`

Отклонено для authority: global state, Unity dependency, frame/presentation coupling и сложная parity-проверка.

## 36.3 Cryptographic RNG на каждый die без deterministic derivation

Отклонено как основной MVP path: безопасно по entropy, но не воспроизводит retry/replay diagnostics. CSPRNG используется для campaign key.

## 36.4 Seed = timestamp или CommandId hash

Отклонено: предсказуемо и не содержит host secret.

## 36.5 Один stream на session

Отклонено: порядок команд и параллелизм меняют все будущие outcomes.

## 36.6 Persist PRNG mutable state после каждого draw

Отклонено: усложняет atomicity, retry и concurrency; независимые derived streams проще.

## 36.7 Использовать wall clock для timeout duration

Отклонено: clock correction может ускорить, задержать или обратить duration.

## 36.8 Использовать monotonic timestamp как persisted deadline

Отклонено: timestamp недействителен после process restart.

## 36.9 Автоматически связывать WorldClock с real time

Отклонено продуктовым контрактом: игровое время продвигается авторитетными командами.

## 36.10 Хранить raw RNG key в RngProofData

Отклонено: раскрывает future streams и секретные данные кампании.

## 36.11 Сразу вводить commit-reveal/verifiable dice

Отложено: требует security, identity и participant protocol contract; не нужно для M1.

---

# 37. Не входит в ADR-008

- криптографически доказуемая честность бросков;
- commit-reveal между участниками;
- hardware RNG;
- online randomness beacon;
- encryption-at-rest campaign RNG keys;
- user-facing seed entry;
- deterministic lockstep networking;
- replay всей кампании через повторное выполнение команд;
- real-time calendar synchronization WorldClock;
- cron/long-running server scheduler;
- distributed clock synchronization между hosts;
- выбор exact clock-skew alert threshold;
- weighted/random-table algorithms, не требуемые текущим ruleset.

Эти вопросы требуют отдельных задач/ADR при появлении реальной необходимости.

---

# 38. Связь с последующими ADR

После ADR-008:

1. ADR-009 фиксирует Unity project/build profiles, graphics API и scripting backend.
2. ADR-010 фиксирует Logging and Diagnostics, включая clock discontinuity и redaction RNG secrets.
3. Security ADR позже определит protection/rotation/export campaign RNG keys и возможный verifiable RNG.
4. Dice/Combat implementation использует этот ADR без выбора нового algorithm.

---

# 39. Traceability

| Решение | Источник |
|---|---|
| Host-authoritative RNG | Product Requirements, Dice/Log, ADR-002 |
| Injected Clock/RNG | Technical Baseline, Rules Engine, Test Strategy |
| Duplicate не вызывает RNG | ADR-002, Dice/Log |
| WorldClock не двигается real time | Domain Model |
| Fixed RNG в sandbox/tests | Domain Model, Content Block System, Test Strategy |
| RNG evidence хранится с roll | Rules Engine, Dice/Log, Persistence |
| Dual runtime parity | ADR-006 |
| Independent algorithm versions | ADR-007 |
| Secret redaction/error model | ADR-004 |
| Composition lifetimes | ADR-005 |

---

# 40. Вступление в силу

С даты принятия:

- этот ADR имеет приоритет над предварительными Clock/RNG разделами Technical Development Baseline;
- production authoritative code не может использовать global time/random API;
- первым production RNG contract является HMAC-SHA-256 derivation v1 + xoshiro256** v1 + rejection mapping v1;
- `SLICE-00` обязан создать Clock/RNG interfaces, test doubles, vectors и architecture guards;
- Dice/Rules implementation не может выбирать другой algorithm без нового ADR;
- изменение принятой модели требует ADR-008 amendment либо нового superseding ADR.

---

**Конец документа**
