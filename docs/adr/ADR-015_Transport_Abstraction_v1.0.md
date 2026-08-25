# ADR-015 — Transport Abstraction

**Документ:** `docs/adr/ADR-015_Transport_Abstraction_v1.0.md`
**ADR:** ADR-015
**Версия:** 1.0
**Дата:** 25 августа 2026 года
**Статус:** Accepted
**Область:** сигнатура и жизненный цикл `ISessionTransport` (Application port), формат `NetworkEnvelope`/`RealtimeEnvelope`, протокол version negotiation на уровне транспорта, in-process/mock реализация транспорта для тестов
**Связанные этапы:** Roadmap Этап 3 (`SLICE-02`), Milestone `M3`, backlog `ODY-S02-001`
**Базовые документы:** `06_Networking_and_Session_Sync_Odyssey_VTT_v0.8.md` (разделы 4, 5, 10, 11), `ADR-001_Module_Boundaries_and_Dependency_Direction_v1.0.md` §6.6, `ADR-004_Result_and_Error_Model_v1.0.md`, `ADR-011_Local_Campaign_Format_v1.1.md` (структурный образец), `docs/tasks/SLICE-02_BACKLOG.md`

---

# 1. Решение

Odyssey VTT определяет единственный Application-level port транспортного уровня — `ISessionTransport` — через который Application отправляет и получает сетевые сообщения, не зависящий от конкретного provider SDK (relay, P2P, будущий self-host), в соответствии с намерением `06_Networking_and_Session_Sync` §4.3.

Обязательные решения:

1. `ISessionTransport` — интерфейс, объявленный в `Odyssey.Application` (`Runtime/Networking/SessionTransportContracts.cs`), реализуемый в `Odyssey.Networking` (`ADR-001` §6.6). Application не имеет прямой зависимости ни на один конкретный transport SDK.
2. Сигнатура порта построена как асинхронные I/O-операции, возвращающие `Result`/`Result<T>` (`ADR-004`), и синхронный, poll-style drain для чтения входящих сообщений — не bare `Task`/`IAsyncEnumerable`, как в иллюстративном наброске `06_Networking...` §4.3 (обоснование — раздел 8 этого документа):
   ```csharp
   public interface ISessionTransport
   {
       Task<Result<ConnectionHandle>> ConnectAsync(SessionEndpoint endpoint, ProtocolVersionRange clientProtocolRange, CancellationToken cancellationToken);
       Task<Result> SendReliableAsync(ConnectionHandle connection, NetworkEnvelope envelope, CancellationToken cancellationToken);
       Task<Result> SendRealtimeAsync(ConnectionHandle connection, RealtimeEnvelope envelope, CancellationToken cancellationToken);
       Result<IReadOnlyList<NetworkEnvelope>> DrainReliable(ConnectionHandle connection);
       Result<IReadOnlyList<RealtimeEnvelope>> DrainRealtime(ConnectionHandle connection);
       Result Disconnect(ConnectionHandle connection);
   }
   ```
3. Два логических канала — reliable ordered (`SendReliableAsync`/`DrainReliable`) и realtime preview (`SendRealtimeAsync`/`DrainRealtime`) — оба являются частью baseline-контракта этого ADR, не будущим placeholder-ом (обоснование — раздел 5).
4. `ConnectionHandle` — Application-safe handle (`SessionId`, согласованный `ProtocolVersion`, момент подключения), никогда не раскрывающий сырой socket/relay session объект — тот же паттерн, что `CampaignHandle` уже установил для persistence (`ADR-011`).
5. Version negotiation на уровне транспорта использует monotonic integer `ProtocolVersion` (не SemVer), совместимый по формату с `CampaignFormatVersion`/`DatabaseSchemaVersion` (`ADR-011` §6.1). Диапазон поддерживаемых версий (`ProtocolVersionRange`: `Min`, `Max`, `Preferred`) объявляется каждой стороной; соединение возможно только при пересечении диапазонов (раздел 7).
6. `NetworkEnvelope` — DTO конверта reliable-канала, поле-в-поле соответствующий `06_Networking...` §11.1: `MessageId, SessionId, SenderUserId?, SenderClientInstanceId?, MessageKind, ProtocolVersion, CorrelationId?, CausationId?, SentAtHostTime?, PayloadType, PayloadVersion, Payload`. `RealtimeEnvelope` — облегчённый DTO без `MessageId`/`CorrelationId`/`CausationId` (нечего дедуплицировать/коррелировать для transient-данных).
7. Этот ADR определяет **in-process/mock реализацию** `ISessionTransport` (`Odyssey.Networking.InProcess.InProcessSessionTransport`) — детерминированную, без реального сетевого I/O, предназначенную только для automated tests, как явно предусмотрено `06_Networking...` §11.3. Реальная relay-backed реализация — предмет `ODY-S02-002`/`ODY-S02-003`, не этого ADR.
8. `TransportTimeoutPolicy` (`ConnectTimeout`, `SendTimeout`, `MaxRetries`, `RetryBackoff`) — конструктор-инжектируемая политика с sane default (`10s`/`5s`/`3`/`500ms`), тот же паттерн, что `BackupRotationPolicy` уже установил (`ADR-011`-смежная реализация `SLICE-01`). Точные значения — предмет дальнейшей эмпирической настройки в `ODY-S02-002`/`003`, не жёстко фиксируются этим ADR как неизменные.
9. Этот ADR не определяет: реальный relay/rendezvous транспорт (`ODY-S02-002`/`ODY-S02-003`), snapshot/delta/reconnect протокол поверх транспорта (`ODY-S02-004`), identity/permissions baseline (`ODY-S02-005`/`ODY-S02-006`), asset channel (`06_Networking...` §5.3, отдельная будущая ADR).

Этот ADR является нормативным authority по сигнатуре `ISessionTransport` и формату `NetworkEnvelope`/`RealtimeEnvelope`. Он реализует и уточняет `06_Networking_and_Session_Sync_Odyssey_VTT_v0.8.md` разделы 4, 5, 10, 11 применительно к `SLICE-02` без изменения продуктового поведения, описанного там.

---

# 2. Контекст и проблема

`06_Networking_and_Session_Sync` описывает намерение и иллюстративный набросок транспортного порта, но остаётся product-документом, не ADR: он не является нормативным authority для реализации. `ADR-001` §6.6 уже определил границу модуля `Odyssey.Networking` (что разрешено и запрещено), но ни одна задача до `ODY-S02-001` не реализовывала ничего против этой границы — `Odyssey.Networking.csproj` не существовал как реальный проект до этой задачи (подтверждено `ODY-S02-000`'s verified-facts).

`SLICE-02` не может начать реализацию реального транспорта (`ODY-S02-002`/`003`) или snapshot/delta/reconnect протокола (`ODY-S02-004`) без принятого технического контракта, определяющего:

1. Точную сигнатуру `ISessionTransport`: где она объявлена (Application port, не Networking-internal деталь), какие операции обязательны, какой стиль ошибок (raw exceptions vs `Result`).
2. Является ли realtime/unreliable канал частью baseline или будущим расширением.
3. Формат/тип версии транспортного протокола (integer vs SemVer) и алгоритм согласования диапазонов.
4. Точный состав полей `NetworkEnvelope`/`RealtimeEnvelope`.
5. Нужна ли и какой должна быть in-process/mock реализация для тестирования остальных `SLICE-02` задач без реального сетевого стека.
6. Где проходит граница между этим ADR и последующими задачами (`ODY-S02-002`–`ODY-S02-006`) — без этой границы работа над ними не может начаться параллельно и независимо.

Без этого ADR `ODY-S02-002`–`ODY-S02-004` были бы вынуждены изобретать сигнатуру порта по ходу кода, что противоречит установленному в `SLICE-00`/`SLICE-01` процессу (прецедент `ADR-002`–`ADR-014`).

---

# 3. Термины

## 3.1 `ISessionTransport`

Application-level port (интерфейс), объявленный в `Odyssey.Application`, представляющий абстракцию сетевого транспорта одной активной сессии между двумя участниками (host и client, либо client и relay). Application-код зависит только от этого интерфейса, не от конкретного provider SDK (`ADR-001` §6.6).

## 3.2 Reliable channel / Realtime channel

Reliable channel — обязательный к доставке, упорядоченный, дедуплицированный по идентичности сообщения канал (`06_Networking...` §5.1): auth handshake, lobby, command envelopes/results, snapshot, delta, PendingInteraction, permissions, scene activation, ready status, inventory, rolls, journal, moderation, session end. Realtime channel — необязательный к доставке, не сохраняемый в `campaign.db`, никогда не изменяющий authoritative state канал (`06_Networking...` §5.2): drag preview, pointer, ruler preview, cursor, selection area, typing indicator, voice/audio telemetry.

## 3.3 `NetworkEnvelope` / `RealtimeEnvelope`

DTO-обёртки сообщений, передаваемых через reliable/realtime каналы соответственно. Не являются доменными сущностями и не передаются напрямую в Persistence repository (`06_Networking...` §11.2).

## 3.4 `ProtocolVersion` / `ProtocolVersionRange`

`ProtocolVersion` — monotonic positive integer, аналогичный по конвенции `CampaignFormatVersion` (`ADR-011` §6.1), не SemVer. `ProtocolVersionRange` — объявляемый каждой стороной диапазон поддерживаемых версий (`Min`, `Max`, `Preferred`), используемый для negotiation при подключении.

## 3.5 In-process/mock transport

Реализация `ISessionTransport`, не выполняющая реального сетевого I/O — два связанных экземпляра обмениваются `NetworkEnvelope`/`RealtimeEnvelope` через in-memory очереди. Предназначена исключительно для automated tests (`06_Networking...` §11.3), не для production использования.

---

# 4. Сигнатура `ISessionTransport`

## 4.1 Расположение и стиль

`ISessionTransport` объявлен в `Odyssey.Application` (`Runtime/Networking/SessionTransportContracts.cs`), не в `Odyssey.Networking` — как Application port, реализуемый Networking-модулем (`ADR-001` §6.6, "Odyssey.Networking implements session/network ports Application declares"). Все операции, выполняющие реальный I/O (`ConnectAsync`, `SendReliableAsync`, `SendRealtimeAsync`), возвращают `Task<Result<T>>`/`Task<Result>` — ни одна не бросает наружу сырое provider-исключение (`ADR-004`, "no raw provider exceptions/Tasks-with-exceptions escape a public API").

## 4.2 Операции

```csharp
public interface ISessionTransport
{
    Task<Result<ConnectionHandle>> ConnectAsync(SessionEndpoint endpoint, ProtocolVersionRange clientProtocolRange, CancellationToken cancellationToken);
    Task<Result> SendReliableAsync(ConnectionHandle connection, NetworkEnvelope envelope, CancellationToken cancellationToken);
    Task<Result> SendRealtimeAsync(ConnectionHandle connection, RealtimeEnvelope envelope, CancellationToken cancellationToken);
    Result<IReadOnlyList<NetworkEnvelope>> DrainReliable(ConnectionHandle connection);
    Result<IReadOnlyList<RealtimeEnvelope>> DrainRealtime(ConnectionHandle connection);
    Result Disconnect(ConnectionHandle connection);
}
```

`ConnectAsync` согласует `ProtocolVersion` вызывающей стороны (собственный диапазон реализации) против переданного диапазона удалённой стороны и возвращает `ConnectionHandle` с зафиксированным согласованным значением, либо типизированную ошибку (раздел 7).

## 4.3 `ConnectionHandle`

```csharp
public sealed class ConnectionHandle
{
    public SessionId SessionId { get; }
    public ProtocolVersion NegotiatedProtocolVersion { get; }
    public UtcInstant ConnectedAt { get; }
}
```

Никогда не раскрывает сырой socket/relay session объект — тот же принцип "safe handle, not a live resource", который `CampaignHandle` уже установил для persistence-слоя.

## 4.4 Отказ от `IAsyncEnumerable`

Иллюстративный набросок `06_Networking...` §4.3 использует `Task`/`IAsyncEnumerable<T>` без типизированной модели ошибок. Этот ADR намеренно отклоняется от него в пользу `Task<Result<T>>` для I/O-операций и синхронного poll-style `DrainReliable`/`DrainRealtime` для чтения — обоснование в разделе 8.

---

# 5. Каналы

## 5.1 Reliable ordered channel — обязательный, часть baseline

`06_Networking...` §5.1 явно перечисляет reliable channel как несущий auth handshake, lobby, command envelopes/results, snapshot, delta, PendingInteraction, permissions, scene activation, ready status, inventory, rolls, journal, moderation, session end — то есть большинство критичной для геймплея коммуникации. `SendReliableAsync`/`DrainReliable` реализуют этот канал в baseline `ISessionTransport` этого ADR.

## 5.2 Realtime preview channel — обязательный, часть baseline, не placeholder

`06_Networking...` §5.2 описывает realtime-канал (drag preview, pointer, ruler preview, cursor, selection area, typing indicator, voice/audio telemetry) как отдельный от reliable, с явными свойствами: не сохраняется в `campaign.db`, может быть потерян, никогда не меняет authoritative state, имеет rate limit, не требует полного replay. Этот канал **уже присутствует** в иллюстративном наброске `06_Networking...` §4.3 собственного `ISessionTransport` (`SendRealtimeAsync`/`ReadRealtimeAsync`) — то есть продуктовый документ сам определяет его как часть baseline интерфейса, не будущее расширение. Этот ADR следует этому решению: `SendRealtimeAsync`/`DrainRealtime` — часть `ISessionTransport` с версии 1.0, не добавляются отдельным amendment позже.

## 5.3 Asset channel — не входит в этот ADR

`06_Networking...` §5.3 описывает отдельный asset channel (signed access, chunk/range download, resume, checksum, expiry, audience auth) с существенно иной моделью (временное хранилище, не сессионный транспорт). Этот канал явно исключён из объёма `ISessionTransport` этого ADR; его контракт — предмет отдельной будущей ADR, не зафиксированной в текущем `SLICE-02_BACKLOG.md` как обязательная для этой ревизии.

---

# 6. `NetworkEnvelope` и `RealtimeEnvelope`

## 6.1 `NetworkEnvelope`

Поле-в-поле соответствует `06_Networking...` §11.1:

```text
MessageId
SessionId
SenderUserId?
SenderClientInstanceId?
MessageKind
ProtocolVersion
CorrelationId?
CausationId?
SentAtHostTime?
PayloadType
PayloadVersion
Payload (byte[])
```

`MessageId` — новый типизированный идентификатор (`Odyssey.Domain.Identity.MessageId`, префикс `msg_`), генерируемый отправителем свежим при каждой отправке через `MessageId.NewId(UtcInstant)` — в отличие от `SessionId`/`UserId`, назначаемых внешне, `MessageId` не имеет смысла переиспользовать между сообщениями (`06_Networking...` §11.2, "MessageId unique").

## 6.2 `RealtimeEnvelope`

Облегчённая версия без `MessageId`/`CorrelationId`/`CausationId` — нечего дедуплицировать или коррелировать для transient-данных, которые могут теряться (раздел 5.2):

```text
SessionId
SenderUserId?
SenderClientInstanceId?
ProtocolVersion
PayloadType
Payload (byte[])
```

## 6.3 `NetworkMessageKind`

Минимальный enum: `Handshake = 1, ApplicationPayload = 2, Heartbeat = 3`. Не перечисляет полную будущую таксономию сообщений — реальная типизация переносится в `PayloadType`/`PayloadVersion`, как и предусматривает `06_Networking...` §11.2 ("wire DTO is not a domain entity").

## 6.4 Wire DTO, не доменная сущность

`NetworkEnvelope`/`RealtimeEnvelope` никогда не передаются напрямую в Persistence repository и не являются доменными сущностями (`06_Networking...` §11.2). Маппинг wire-сообщений в Application-контракты — ответственность `Odyssey.Networking` (`ADR-001` §6.6), не входит в объём этого ADR за пределами определения самих DTO.

---

# 7. Version negotiation

## 7.1 Формат версии

`ProtocolVersion` — monotonic positive integer, начиная с `1`, а не SemVer, по аналогии с `CampaignFormatVersion`/`DatabaseSchemaVersion` (`ADR-011` §6.1). Обоснование: транспортный протокол — не продуктовая feature с семантикой major/minor/patch, а плоская последовательность несовместимых редакций wire-формата, для которой monotonic integer уже проверенно однозначен в этой кодовой базе.

## 7.2 `ProtocolVersionRange`

```csharp
public sealed class ProtocolVersionRange
{
    public ProtocolVersion Min { get; }
    public ProtocolVersion Max { get; }
    public ProtocolVersion Preferred { get; } // Min <= Preferred <= Max, иначе ArgumentException

    public ProtocolVersion? NegotiateWith(ProtocolVersionRange other);
}
```

`NegotiateWith` возвращает наибольшую версию, входящую в пересечение обоих диапазонов (`max(Min, other.Min) .. min(Max, other.Max)`), либо `null`, если пересечения нет. Соответствует `06_Networking...` §10.2 ("Host declares MinSupportedProtocolVersion, MaxSupportedProtocolVersion, PreferredProtocolVersion; connection possible only when ranges overlap").

## 7.3 Отказ

При отсутствии пересечения `ConnectAsync` возвращает типизированную ошибку `NetworkingFailures.ProtocolVersionUnsupported` (`ErrorCategory.Compatibility`, `SafeReasonCode.VersionUnsupported`, `RetryDirective.UpgradeRequired`) — согласуется с `06_Networking...` §10.3 ("incompatible client receives ConnectionRejected, ReasonCode=ProtocolVersionUnsupported... never receives a snapshot"). Полный протокол `ConnectionRejected` (`HostVersion`, `RequiredClientRange` в теле ответа) — предмет реализации `ODY-S02-002`/`003`, не этого ADR, который фиксирует только сам факт типизированного отказа на уровне порта.

## 7.4 Полное handshake-содержимое — не входит

`06_Networking...` §10.1 перечисляет полный набор полей handshake (`ApplicationVersion, ProtocolVersion, RulesetVersion, CampaignFormatVersion, DatabaseSchemaVersion, AssetProtocolVersion, AudioProtocolVersion`). Этот ADR фиксирует только `ProtocolVersion`/`ProtocolVersionRange` как часть транспортного порта; остальные измерения handshake — предмет `ODY-S02-004` (snapshot/delta/reconnect протокол), где handshake реализуется поверх этого транспорта.

---

# 8. Отказ от `Task`/`IAsyncEnumerable` в пользу `Result`/drain — обоснование

`06_Networking...` §4.3 приводит иллюстративный набросок с bare `Task` (кидающим исключения при ошибке) и `IAsyncEnumerable<T>` для чтения. Этот ADR сознательно адаптирует набросок:

1. **`Task<Result<T>>` вместо bare `Task`/exceptions**: `ADR-004` требует, чтобы ни один публичный API не пропускал наружу сырые provider-исключения. Транспортные сбои (недоступность peer, timeout, несовместимая версия) — ожидаемые, типизируемые исходы, не заслуживающие исключений по семантике `ADR-004`.
2. **Синхронный `DrainReliable`/`DrainRealtime` вместо `IAsyncEnumerable`**: на момент этого ADR не существует ни одной реальной async-транспортной реализации, которая бы использовала backpressure/cancellation семантику `IAsyncEnumerable` по назначению — единственная реализация этого ADR (`InProcessSessionTransport`, раздел 9) детерминированный in-memory mock, для которого poll-style drain проще реализовать и проще тестировать детерминированно. `IAsyncEnumerable` может быть введён в будущем amendment этого ADR, когда `ODY-S02-002`/`003` реализуют реальный push-based транспорт и его отсутствие станет ощутимым архитектурным трением — не заранее, спекулятивно.

Это осознанное отклонение от иллюстративного наброска продуктового документа, не расхождение с его намерением: набор операций (connect, send reliable, send realtime, read reliable, read realtime) и разделение каналов сохранены полностью.

---

# 9. In-process/mock transport

## 9.1 Назначение

`Odyssey.Networking.InProcess.InProcessSessionTransport` — единственная реализация `ISessionTransport`, вводимая этим ADR. Два экземпляра, созданные вместе через `InProcessSessionTransport.CreatePair(hostRange, clientRange, clock)`, доставляют конверты друг другу через in-memory `ConcurrentQueue`, без сокета и без реального сетевого I/O. Предназначена исключительно для automated tests этой и последующих `SLICE-02` задач (`06_Networking...` §11.3), не для production использования.

## 9.2 Timeout/cancellation без `Task.Delay`

`ADR-008`'s forbidden-global-API scan (`scripts/verify-test-structure.ps1`, `Test-ForbiddenGlobalApis`) запрещает `Task.Delay` в любом файле `Packages/com.odyssey.*/Runtime`. `InProcessSessionTransport` не использует искусственную задержку для симуляции timeout: каждая I/O-операция синхронно проверяет `cancellationToken.IsCancellationRequested` на входе и возвращает типизированный `NetworkingFailures.OperationCancelled`, если токен уже отменён — этого достаточно для контрактного теста поведения "cancellation возвращает типизированную ошибку, не бросает исключение", не требуя реальной задержки во времени.

## 9.3 Отказ при недоступности

`ConnectAsync` возвращает `NetworkingFailures.ConnectFailed`, если у экземпляра нет связанного peer (`_peer == null`) — состояние, недостижимое через единственный публичный конструктор `CreatePair` сегодня (обе стороны пары всегда связаны), но зафиксированное в реализации для будущего реального transport failure mode (например, relay-сессия, закрывшаяся до завершения negotiation). Отправка (`SendReliableAsync`/`SendRealtimeAsync`) без предварительного успешного `ConnectAsync` на этом же экземпляре возвращает `NetworkingFailures.NotConnected`; `Disconnect` сбрасывает локальное состояние подключения, после чего дальнейшие отправки с этого экземпляра также возвращают `NotConnected`.

## 9.4 Не входит в 9

Реальная сетевая доставка, потеря пакетов, задержка, конкурентный доступ нескольких потоков к одному transport-экземпляру сверх базовой потокобезопасности очереди — не тестируются и не гарантируются этой mock-реализацией. Эти свойства — предмет `ODY-S02-002`/`003` против реального provider SDK.

---

# 10. Соответствие module boundaries (ADR-001)

`Odyssey.Networking` реализует networking ports, объявленные `Odyssey.Application` (`ADR-001` §6.6). Контракт, определённый этим ADR, обязан оставаться реализуемым в рамках уже принятых границ:

- `Odyssey.Networking` не имеет права изменять Domain state напрямую, не читает SQLite, не принимает authoritative игровых решений (`ADR-001` §6.6).
- Host-authoritative команды всегда идут через Application; client payload — запрос, не готовое изменение состояния.
- `ISessionTransport` объявлен в `Odyssey.Application`, не в `Odyssey.Networking` — Application не зависит от конкретного provider SDK (`ADR-001` §6.6, "Application layer не зависит от конкретного SDK", дословно из `06_Networking...` §4.3).
- Transport abstraction и provider adapters, command ingress/egress envelopes, network message DTOs — явно разрешённая ответственность `Odyssey.Networking` по таблице классификации `ADR-001`.

Этот ADR не переопределяет и не ослабляет ни одно из этих правил; он их подтверждает применительно к конкретной сигнатуре транспортного порта.

---

# 11. Не входит в ADR-015

Явно исключено из объёма этого ADR (владеют другие задачи backlog `SLICE-02`, см. `docs/tasks/SLICE-02_BACKLOG.md` §4):

- **Реальная сетевая реализация транспорта** (relay provider adapter, rendezvous/discovery, реальный socket/relay SDK) — `ODY-S02-002`/`ODY-S02-003`.
- **Snapshot/delta/reconnect протокол** поверх reliable-канала (полный handshake из `06_Networking...` §10.1, PendingInteraction lifecycle, reconnect resume) — `ODY-S02-004`.
- **Identity baseline** (`18_Account_And_Identity.md`, подтверждено отсутствующим — `ODY-S02-000`) — `ODY-S02-005`.
- **Permissions baseline** — `ODY-S02-006`.
- **Asset channel** (`06_Networking...` §5.3) — не зафиксирован в текущей ревизии `SLICE-02_BACKLOG.md` как отдельная задача; остаётся будущим расширением вне объёма этого ADR (раздел 5.3).
- Любое production-использование `ISessionTransport` за пределами определения интерфейса, in-process/mock реализации и собственных контрактных тестов этой задачи.

---

# 12. Открытые вопросы

## 12.1 Точные значения `TransportTimeoutPolicy` — `[OPEN]`

Раздел 1.8 фиксирует `TransportTimeoutPolicy` с sane default (`10s`/`5s`/`3`/`500ms`), но не проверяет их эмпирически против реального provider SDK — на момент этого ADR ни один реальный транспорт не существует для такой проверки. Аналогично `ADR-011` §12.1 (выбор SQLite provider-библиотеки был отложен до `SP-02`), точные production-значения этой политики — предмет эмпирической проверки в рамках `ODY-S02-002`/`003`, не фиксируются здесь как окончательные. Default-значения этого ADR — стартовая точка, не нормативный потолок.

## 12.2 Формат `SessionEndpoint` за пределами `EndpointId` — `[OPEN]`

`SessionEndpoint` этого ADR содержит только `EndpointId` (строка, до 128 символов) — сознательно минимальный, поскольку реальная форма relay/rendezvous endpoint (URL, connection string, discovery token) зависит от решения `ODY-S02-002`/`003` (Rendezvous/Relay Strategy ADR), ещё не принятого на момент этого ADR. Точная структура `SessionEndpoint` остаётся открытой до той ADR.

---

# 13. Правила для Codex

Codex обязан:

1. Использовать `ISessionTransport` из раздела 4 как нормативную сигнатуру Application port; не изобретать альтернативную сигнатуру без amendment этого ADR.
2. Не добавлять реальную сетевую реализацию транспорта под этим ADR — она принадлежит `ODY-S02-002`/`ODY-S02-003`.
3. Не использовать `Task.Delay` или иной запрещённый `ADR-008` global API в `Packages/com.odyssey.*/Runtime`, включая `Odyssey.Networking` — таймауты/cancellation реализуются синхронной проверкой токена (раздел 9.2), не искусственной задержкой.
4. Не считать realtime-канал placeholder-ом или опциональным для будущей версии — он часть baseline с версии 1.0 (раздел 5.2).
5. Не передавать `NetworkEnvelope`/`RealtimeEnvelope` напрямую в Persistence repository — они wire DTO, не доменные сущности (раздел 6.4).
6. Не вводить SemVer для `ProtocolVersion` — только monotonic integer (раздел 7.1).
7. Не реализовывать snapshot/delta/reconnect протокол, identity или permissions baseline под этим ADR — они принадлежат `ODY-S02-004`, `ODY-S02-005`, `ODY-S02-006` соответственно.
8. Указывать в PR summary, если задача меняет `ProtocolVersion` diapазон по умолчанию или `TransportTimeoutPolicy.Default`.

---

# 14. Definition of Done / критерии приёмки ADR-015 implementation

ADR считается реализованным (для той части, которую он определяет), когда:

1. `ISessionTransport` объявлен в `Odyssey.Application` с сигнатурой раздела 4.2.
2. `NetworkEnvelope`/`RealtimeEnvelope` содержат все поля разделов 6.1/6.2.
3. `ProtocolVersionRange.NegotiateWith` реализует пересечение диапазонов согласно разделу 7.2 и покрыт тестом на успешное и неуспешное пересечение.
4. Отказ по несовместимой версии возвращает типизированную `Result`-ошибку `NetworkingProtocolVersionUnsupported`, не исключение.
5. `InProcessSessionTransport` реализует `ISessionTransport` полностью, без использования запрещённых `ADR-008` API.
6. Контрактные тесты покрывают минимум: успешную отправку/приём сообщения через reliable-канал, отправку/приём через realtime-канал, cancellation (typed `OperationCancelled`, не exception), отказ при отсутствии подключения (`NotConnected`), отказ после `Disconnect`.
7. `Odyssey.Networking.csproj` существует, включён в решение, зависит только от `Odyssey.Domain`, `Odyssey.Content`, `Odyssey.Application` (`ADR-001` module boundary).
8. `docs/errors/ERROR_CODES.md` содержит все новые networking error codes этого ADR.

---

# 15. Рассмотренные альтернативы

## 15.1 `IAsyncEnumerable<T>` для чтения, как в наброске продуктового документа

Отклонено на этом этапе: не существует ни одной реальной push-based транспортной реализации, которая бы использовала его backpressure/cancellation семантику по назначению; единственная реализация этого ADR — детерминированный in-process mock, для которого синхронный poll-style drain проще и детерминированнее тестировать (раздел 8).

## 15.2 Bare `Task` с исключениями вместо `Result<T>`

Отклонено: прямо противоречит `ADR-004` ("no raw provider exceptions/Tasks-with-exceptions escape a public API"); транспортные сбои — ожидаемые, типизируемые исходы, не exceptional control flow.

## 15.3 SemVer для `ProtocolVersion`

Отклонено: транспортный протокол — плоская последовательность несовместимых wire-редакций, не продуктовая feature с semantics major/minor/patch; monotonic integer уже проверенный в этой кодовой базе паттерн для аналогичных version dimensions (`CampaignFormatVersion`, `DatabaseSchemaVersion`).

## 15.4 Realtime-канал как placeholder до отдельного будущего ADR

Отклонено: продуктовый документ (`06_Networking...` §4.3, §5.2) сам включает realtime-канал в исходный набросок интерфейса как часть единого транспортного контракта; выделение его в отдельный будущий ADR создало бы искусственное расхождение между этим ADR и продуктовым намерением без причины.

## 15.5 Симуляция timeout через `Task.Delay` в mock-транспорте

Отклонено: `Task.Delay` — запрещённый глобальный API по `ADR-008` blanket text-match scan для любого файла `Packages/com.odyssey.*/Runtime`; синхронная проверка `CancellationToken.IsCancellationRequested` на входе операции даёт эквивалентный, детерминированно тестируемый контракт без нарушения этого правила.

## 15.6 Полный handshake-протокол (`06_Networking...` §10.1) внутри этого ADR

Отклонено: смешало бы транспортный порт (предмет этого ADR) с snapshot/reconnect протоколом поверх него (предмет `ODY-S02-004`), что противоречило бы принципу разделения задач `SLICE-02_BACKLOG.md` и сделало бы этот ADR излишне широким для независимой параллельной реализации `ODY-S02-002`–`004`.

---

# 16. Трассировка

ADR реализует и уточняет:

- `06_Networking_and_Session_Sync_Odyssey_VTT_v0.8.md`, разделы 4 (`ISessionTransport` набросок), 5 (каналы), 10 (version negotiation), 11 (envelope-формат);
- `17_Roadmap_Odyssey_VTT_v0.11.md` §11 (Этап 3, `SLICE-02`);
- `ADR-001_Module_Boundaries_and_Dependency_Direction_v1.0.md` §6.6 (Networking module boundary);
- `ADR-004_Result_and_Error_Model_v1.0.md` (Result/Error model discipline);
- `ADR-008_Deterministic_Clock_and_RNG_v1.0.md` (forbidden global APIs, включая `Task.Delay`);
- `ADR-011_Local_Campaign_Format_v1.1.md` §6.1 (monotonic integer version dimension convention, использована как образец для `ProtocolVersion`).

Связанные будущие задачи (`docs/tasks/SLICE-02_BACKLOG.md`):

```text
ODY-S02-002  ADR: Rendezvous/Relay Strategy
ODY-S02-003  Technical Spike SP-03: Internet Connectivity
ODY-S02-004  ADR: Snapshot/Delta/Reconnect Protocol
ODY-S02-005  ADR: Identity Baseline
ODY-S02-006  ADR: Permissions Baseline
ODY-S02-007  Technical Spike SP-04: Hidden Data Boundary
```

---

# 17. Нормативное действие

Принято как ADR этой задачи (`ODY-S02-001`) без ожидания отдельного product owner review цикла — обоснование: этот ADR не зависит от эмпирических данных ещё не проведённого спайка (в отличие от `ODY-S02-003`, зависящей от `SP-03`/`SP-04`), а фиксирует сигнатуру Application port на основании уже принятого продуктового документа (`06_Networking...` §4.3's собственный набросок интерфейса) и уже принятых архитектурных ADR (`ADR-001`, `ADR-004`, `ADR-011`). Решения раздела 1 — прямое применение уже установленных паттернов этой кодовой базы к новому модулю, не открытие нового архитектурного вопроса, требующего эмпирической проверки перед принятием.

С даты принятия (`Accepted`):

- ни одна implementation-задача `SLICE-02` не создаёт альтернативную сигнатуру `ISessionTransport` или альтернативный формат `NetworkEnvelope`/`RealtimeEnvelope` в противоречии с разделами 4–7;
- `ODY-S02-002`, `ODY-S02-003`, `ODY-S02-004` авторизованы опираться на этот ADR как на принятую основу и не обязаны повторно решать вопросы разделов 4–9;
- открытые вопросы раздела 12 остаются открытыми до отдельного решения и не считаются молчаливо решёнными фактом принятия этого ADR;
- изменение принятого контракта требует amendment этого ADR или нового superseding ADR, не молчаливого отклонения в реализации.

---

**Конец документа**
