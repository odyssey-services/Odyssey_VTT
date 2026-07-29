# ADR-005 — Dependency Composition

**Документ:** `docs/adr/ADR-005_Dependency_Composition_v1.0.md`  
**ADR:** ADR-005  
**Версия:** 1.0  
**Дата:** 27 июля 2026 года  
**Статус:** Accepted  
**Область:** composition root, constructor injection, runtime profiles, dependency lifetimes, factories, Unity bootstrap, scene initialization, startup/shutdown, resource ownership, test composition, configuration injection и запрет service locator  
**Связанные этапы:** Roadmap Stage 1, `SLICE-00`, Milestone `M1`  
**Базовые документы:** `TECHNICAL_DEVELOPMENT_BASELINE_Odyssey_VTT_v0.1.md`, `ADR-001_Module_Boundaries_and_Dependency_Direction_v1.0.md`, `ADR-002_Command_and_Domain_Event_Model_v1.0.md`, `ADR-003_Serialization_Strategy_v1.0.md`, `ADR-004_Result_and_Error_Model_v1.0.md`

---

# 1. Решение

Odyssey VTT использует **явную ручную композицию зависимостей** с единственным production composition root в `Odyssey.Unity.Client`.

Главные правила:

1. Production object graph создаётся только в `Odyssey.Unity.Client` через один явный composition root.
2. Основной способ передачи зависимостей — constructor injection.
3. DI-контейнер, reflection-based auto-registration и assembly scanning не используются в M1 и MVP по умолчанию.
4. Добавление DI framework требует отдельного ADR с доказанной необходимостью, лицензией, AOT/IL2CPP-проверкой и планом миграции.
5. `IServiceProvider`, service locator, static mutable registry, глобальный `Instance` и поиск сервиса через scene hierarchy запрещены как application composition mechanism.
6. Composition root разрешено создавать concrete adapters и связывать их с consumer-owned ports, но запрещено размещать в нём business rules и use-case decisions.
7. Один process root владеет process-scoped ресурсами и создаёт явные дочерние runtime scopes.
8. Для MVP определены scopes: `Process`, `Campaign`, `Session`, `Operation` и `Presentation`.
9. Каждый disposable/async-disposable ресурс имеет ровно одного владельца lifetime.
10. Более долгоживущий объект не может напрямую удерживать dependency более короткого lifetime. Для этого используется типизированная factory или scope factory.
11. Dynamic runtime создаётся только через именованные factories, возвращающие типизированный runtime handle; универсальная `Resolve<T>()` запрещена.
12. `MonoBehaviour.Awake`, `Start` и scene load callbacks не создают независимые service graphs.
13. Unity bootstrap scene содержит один runtime host, который запускает composition root и координирует scene initialization.
14. `DontDestroyOnLoad` разрешён только для корневого bootstrap/runtime host и явно утверждённых Unity platform adapters; произвольные manager-singletons запрещены.
15. ScriptableObject может хранить authoring/configuration data, но не является runtime service locator, mutable authoritative state или владельцем application service.
16. Scene components получают зависимости через явный initialization contract после загрузки сцены; они не ищут их через static access, `FindObjectOfType`, `GetComponent` по глобальной иерархии или Resources lookup.
17. Presenters/ViewModels являются обычными C#-объектами и создаются presentation factory соответствующей сцены.
18. Startup выполняется по фиксированным фазам и не объявляет приложение Ready, пока обязательные проверки graph/configuration не завершились успешно.
19. Ошибки startup/shutdown возвращаются и отображаются согласно ADR-004; частично созданные ресурсы освобождаются.
20. Shutdown выполняется в обратном порядке ownership, является повторно вызываемым и не зависит от случайного порядка Unity callbacks.
21. Production configuration загружается один раз, валидируется и передаётся как immutable typed options. Прямое чтение environment/file/PlayerPrefs внутри business services запрещено.
22. Cancellation, clock, random source, correlation и main-thread dispatch передаются явно через contracts/scopes; ambient context запрещён.
23. Test composition создаётся отдельно от production composition и не использует скрытые production fallbacks.
24. Composition graph, lifetimes, startup, shutdown и отсутствие locator-паттернов покрываются автоматическими тестами `SLICE-00`.
25. Любой новый runtime profile, top-level scope, DI framework или глобальный lifetime требует архитектурного review; изменение принципов этого ADR требует нового ADR.

Этот ADR является нормативным authority по созданию object graph, lifetime ownership и Unity bootstrap. Он уточняет предварительный раздел Dependency Composition Technical Development Baseline и применяется совместно с графом модулей ADR-001.

---

# 2. Контекст и проблема

Архитектурные границы ADR-001 определяют, какие модули могут зависеть друг от друга, но сами по себе не определяют:

- где создаются concrete implementations;
- кто выбирает SQLite, relay, file system и Unity adapters;
- сколько экземпляров каждого сервиса существует;
- когда открывается и закрывается campaign runtime;
- когда создаётся session runtime;
- как UI получает Application facade;
- кто освобождает database connection, transport, file handles и subscriptions;
- как тест заменяет production adapter на fake;
- как приложение переживает смену сцены;
- как избежать зависимости от порядка `Awake` и `Start`;
- как остановить скрытое появление глобальных manager-singletons.

Без ADR Codex может реализовать соседние задачи несовместимыми способами:

- один сервис создаётся в bootstrap, второй — в `MonoBehaviour`, третий — в static property;
- UI вызывает `ServiceLocator.Resolve<ICommandBus>()`;
- каждая сцена создаёт новую database connection и новый event publisher;
- session transport продолжает жить после закрытия кампании;
- process singleton удерживает operation-scoped transaction;
- тест случайно использует настоящий file system или часы;
- несколько `DontDestroyOnLoad` объектов создают дубли после возврата в главное меню;
- shutdown закрывает repository раньше, чем завершается outbox publication;
- configuration читается по-разному в разных adapters;
- production graph работает в Mono, но ломается в IL2CPP из-за reflection registration.

Эти ошибки редко видны в первом прототипе, но проявляются как:

- nondeterministic startup;
- утечки подписок и ресурсов;
- двойная обработка событий;
- невозможность безопасно открыть вторую кампанию;
- flaky PlayMode tests;
- сложный reconnect;
- скрытые циклические зависимости;
- невозможность изолировать Core от Unity;
- необходимость переписать все constructors после добавления DI-контейнера;
- разные правила в коде, созданном разными задачами Codex.

ADR-005 определяет один способ сборки приложения до появления production adapters.

---

# 3. Движущие факторы

Решение оптимизировано под:

1. Простую проверяемую архитектуру на старте проекта.
2. Явные зависимости и отсутствие скрытого ambient state.
3. Совместимость с Unity 6.3 LTS, Mono и IL2CPP.
4. Минимум сторонних runtime dependencies.
5. Возможность тестировать Core без Unity.
6. Предсказуемое открытие/закрытие кампании и сессии.
7. Безопасное освобождение SQLite, relay, streams и subscriptions.
8. Поддержку host-authoritative и remote-client runtime profiles.
9. Изоляцию production configuration.
10. Возможность заменять adapters в tests без reflection/mocking container.
11. Предотвращение manager-singleton и service-locator архитектуры.
12. Понятный для Codex шаблон создания нового сервиса.
13. Ациклический module graph ADR-001.
14. Explicit Result/Error semantics ADR-004.
15. Возможность позже добавить DI framework только при реальной боли, а не заранее.

---

# 4. Термины

## 4.1 Composition root

Единственное место production-приложения, где:

- выбираются concrete implementations;
- создаются long-lived objects;
- связываются ports и adapters;
- формируются runtime factories;
- назначается lifetime ownership;
- запускается application shell.

Composition root знает о concrete types. Business modules не знают о composition root.

## 4.2 Object graph

Связанный набор runtime-объектов и их dependencies, созданный composition root или типизированной дочерней factory.

## 4.3 Constructor injection

Dependency передаётся объекту через constructor и после создания не подменяется скрыто.

## 4.4 Scope

Явная граница времени жизни группы объектов и ресурсов.

## 4.5 Runtime handle

Типизированный владеющий объект, представляющий открытый scope и отвечающий за его корректное завершение.

Примеры концептов:

- `AppRuntime`;
- `CampaignRuntime`;
- `SessionRuntime`;
- `PresentationRuntime`.

Точные публичные имена утверждаются реализацией, но семантика ownership обязательна.

## 4.6 Factory

Явный контракт, создающий конкретный короткоживущий объект или scope с известным результатом и ownership.

Factory не является универсальным resolver.

## 4.7 Service locator

Любой механизм, при котором consumer запрашивает dependency из глобального/ambient registry во время выполнения вместо получения dependency явно.

Примеры запрещённого поведения:

```text
ServiceLocator.Get<T>()
GlobalServices.Resolve<T>()
App.Instance.Repository
FindObjectOfType<RuntimeManager>()
Resources.Load<ServiceRegistry>()
```

## 4.8 Bootstrap

Минимальный Unity-specific процесс создания process composition, проверки readiness и передачи управления application shell.

## 4.9 Runtime profile

Явно именованный вариант composition graph для роли процесса: например authoritative host или remote participant.

## 4.10 Owned resource

Ресурс, который владелец обязан завершить/освободить ровно один раз: connection, transaction, transport, stream, subscription, cancellation source и подобные объекты.

---

# 5. Ownership по модулям

## 5.1 Odyssey.Domain

Domain:

- не создаёт infrastructure adapters;
- не знает composition root;
- не знает Unity lifecycle;
- не получает service provider;
- может использовать обычные domain factories для создания aggregates/value objects;
- получает clock/random evidence только через утверждённые contracts согласно ADR-008.

Создание domain entity через `new` или domain factory не является dependency composition и не запрещается этим ADR.

## 5.2 Odyssey.Rules

Rules:

- создаётся как pure/deterministic service или набор pure calculators;
- не разрешает dependencies через registry;
- не владеет infrastructure lifecycle;
- не читает configuration из окружения самостоятельно.

## 5.3 Odyssey.Content

Content:

- получает registries/readers через constructor;
- не создаёт persistence/network adapters;
- не использует static package registry;
- может иметь scope, привязанный к кампании, если content lock/registry зависит от открытой кампании.

## 5.4 Odyssey.Application

Application:

- объявляет consumer-owned ports;
- принимает ports через constructors/factories;
- не знает concrete adapter types;
- не вызывает composition root;
- не запрашивает dependency через `IServiceProvider`;
- владеет orchestration contracts и runtime factories только тогда, когда factory является use-case port, но не concrete construction logic.

Application service не создаёт SQLite repository или relay transport через `new`.

## 5.5 Odyssey.Persistence

Persistence:

- реализует Application ports;
- может создавать свои внутренние low-level objects внутри adapter-owned factories;
- не публикует собственный global connection singleton;
- явно сообщает ownership connection/transaction/stream;
- не обращается к Unity bootstrap.

## 5.6 Odyssey.Networking

Networking:

- реализует Application session/transport ports;
- владеет transport-specific lifecycle внутри session scope;
- не использует global network manager;
- не создаёт Persistence dependencies;
- не переживает свой владеющий SessionRuntime без явного transfer ownership, который для MVP запрещён.

## 5.7 Odyssey.Unity.Client

Unity Client:

- содержит единственный production composition root;
- выбирает runtime profile;
- создаёт platform adapters;
- запускает и завершает scopes;
- инициализирует сцены и presenters;
- не переносит business rules в bootstrap code;
- не предоставляет глобальный resolver остальному коду.

---

# 6. Единственный production composition root

## 6.1 Расположение

Production root находится в `Odyssey.Unity.Client` в выделенной bootstrap/composition области, например:

```text
Assets/Odyssey/UnityClient/Bootstrap/
Assets/Odyssey/UnityClient/Composition/
```

Точное имя папки не является архитектурным контрактом. Контрактом является единственность root и отсутствие второго независимого graph builder.

## 6.2 Разрешённая ответственность

Composition root может:

- принять validated bootstrap input;
- создать immutable options;
- создать logging/diagnostic adapters;
- создать serializers/registries, утверждённые ADR-003;
- создать platform adapters;
- создать factories Persistence/Networking;
- связать Application ports с concrete adapters;
- создать process runtime и presentation shell;
- зарегистрировать shutdown hooks;
- выполнить graph validation;
- вернуть `Result<AppRuntime>`.

## 6.3 Запрещённая ответственность

Composition root не может:

- решать, разрешено ли игровое действие;
- изменять aggregate state;
- рассчитывать формулу атаки;
- выполнять SQL как часть bootstrap logic;
- формировать audience projection;
- хранить authoritative campaign state;
- обрабатывать UI action как business use case;
- содержать ветвление по конкретным игровым сущностям;
- заменять Application handler.

## 6.4 Root не является locator

После создания graph composition root не передаётся в business objects и не предоставляет API вида:

```csharp
T Resolve<T>();
object Get(Type type);
IServiceProvider Services { get; }
```

Root создаёт и возвращает ограниченные typed facades/handles, а не registry всех объектов.

---

# 7. Manual composition как default

## 7.1 Constructor injection

Production service объявляет обязательные dependencies в constructor:

```csharp
internal sealed class MoveTokenHandler
{
    private readonly ICampaignUnitOfWork _unitOfWork;
    private readonly IPermissionEvaluator _permissions;
    private readonly ITokenRules _rules;

    public MoveTokenHandler(
        ICampaignUnitOfWork unitOfWork,
        IPermissionEvaluator permissions,
        ITokenRules rules)
    {
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _permissions = permissions ?? throw new ArgumentNullException(nameof(permissions));
        _rules = rules ?? throw new ArgumentNullException(nameof(rules));
    }
}
```

Пример показывает только форму injection. Конкретные interfaces утверждаются соответствующими contracts.

## 7.2 Property injection

Property/field injection запрещён для обязательных production dependencies.

Допустимы только Unity-serialized references внутри presentation view, если они являются scene objects, а не Application/Infrastructure services. Такие references проверяются в scene validation.

## 7.3 Optional dependencies

Optional behavior моделируется явно:

- Null Object с чёткой семантикой;
- feature-specific interface;
- typed option;
- отдельный factory/profile.

Запрещено:

- nullable service с разбросанными проверками;
- попытка найти optional service в locator;
- silent fallback с production adapter на fake/no-op.

## 7.4 `new` вне root

`new` разрешён вне composition root для:

- value objects;
- domain entities через утверждённый constructor/factory;
- immutable DTO;
- collections;
- operation-local pure helper;
- adapter-internal implementation details, ownership которых не выходит наружу.

`new` запрещён в Application/Domain для concrete infrastructure service или long-lived cross-cutting service.

## 7.5 Constructor complexity

Большое количество constructor dependencies не решается service locator или container magic.

Если service получает слишком много независимых dependencies, PR обязан проверить:

- не нарушена ли single responsibility;
- не требуется ли cohesive facade;
- не смешаны ли application orchestration и infrastructure;
- не скрывает ли service несколько use cases.

Числовой предел не является жёстким архитектурным правилом, но восемь и более dependencies требуют явного review note.

---

# 8. Lifetime scopes

## 8.1 Process scope

Process scope существует от успешного bootstrap до завершения приложения.

Допустимые process-owned объекты:

- immutable build/runtime information;
- validated application configuration;
- diagnostics/logging root;
- serializer contract registries;
- platform dispatcher/adapters, действительно живущие весь процесс;
- factories для campaign/session/presentation runtimes;
- application shell главного меню.

Process scope не хранит открытую campaign transaction и не удерживает session-specific actor context.

## 8.2 Campaign scope

Campaign scope существует от успешного открытия/создания кампании до её закрытия.

Типичные campaign-owned объекты:

- campaign database/repository runtime;
- content lock/registry конкретной кампании;
- asset store конкретной кампании;
- campaign-level application services;
- migration/backup context после завершения соответствующей операции;
- host-authoritative state access для локального Main GM profile.

Закрытие CampaignRuntime освобождает все campaign-owned resources и запрещает новые operations.

## 8.3 Session scope

Session scope существует от создания/подключения сессии до disconnect/stop.

Типичные session-owned объекты:

- transport connection/listener;
- session publisher/subscriber;
- participant identity context;
- audience projection delivery;
- ordering/acknowledgement/reconnect state;
- session cancellation source;
- subscriptions.

Authoritative HostSession обычно зависит от открытого CampaignRuntime. RemoteParticipantSession не получает локальный authoritative campaign repository.

## 8.4 Operation scope

Operation scope создаётся на одну команду, query, import/export step или другую атомарную Application operation.

Типичные operation-owned объекты:

- `CorrelationId`/operation context;
- cancellation link;
- unit of work/transaction;
- deterministic execution context;
- command-specific RNG stream/evidence согласно ADR-008;
- temporary buffers/streams;
- diagnostic span.

Operation scope не кэшируется в process singleton и всегда завершается до возврата terminal result, кроме явно durable `Pending` semantics ADR-002, где ожидание сохраняется как state, а не живой operation object.

## 8.5 Presentation scope

Presentation scope привязан к экрану, сцене или UI flow.

Он владеет:

- presenters/view models;
- UI subscriptions;
- scene-specific controllers;
- cancellation источником UI flow;
- temporary view state.

Presentation scope не владеет campaign database или network transport.

## 8.6 Правило направления lifetime

Объект более длинного lifetime может зависеть только от:

- dependency того же или более длинного lifetime;
- typed factory, создающей более короткий scope;
- immutable data snapshot.

Неверно:

```text
ProcessService → OperationTransaction
CampaignService → ScenePresenter
SessionService → CommandExecutionContext
```

Верно:

```text
ProcessService → ICampaignRuntimeFactory
CampaignService → IOperationScopeFactory
ScenePresenter → Application facade
```

---

# 9. Runtime handles и factories

## 9.1 Typed runtime handle

Каждый long-lived дочерний scope представлен типизированным handle с ограниченным API и ownership.

Концептуальный пример:

```csharp
public interface ICampaignRuntime : IAsyncDisposable
{
    CampaignId CampaignId { get; }
    ICampaignApplication Application { get; }
    Result<CampaignHealth> GetHealth();
}
```

Это не обязательная точная сигнатура, а нормативная форма: handle раскрывает только необходимый facade и явно владеет lifetime.

## 9.2 Scope factory

Factory:

- принадлежит consumer boundary;
- принимает явный input;
- возвращает `Result<THandle>`/`Task<Result<THandle>>`;
- не возвращает untyped object;
- не сохраняет скрытый текущий scope;
- выполняет cleanup при частичном failure;
- документирует ownership результата.

Пример концепта:

```csharp
Task<Result<ICampaignRuntime>> OpenAsync(
    OpenCampaignRequest request,
    CancellationToken cancellationToken);
```

## 9.3 Запрет универсального resolver

Запрещены factory APIs:

```csharp
T Create<T>();
T Resolve<T>(string name);
object Create(Type runtimeType);
IServiceProvider BuildScope();
```

Named factory создаётся для конкретной ответственности.

## 9.4 Factory и domain creation

Infrastructure/runtime factory не смешивается с domain factory.

- runtime factory создаёт scope/adapters;
- domain factory создаёт valid domain object;
- content factory создаёт content model по contract;
- presentation factory создаёт presenter/view model.

Один универсальный `ObjectFactory` запрещён.

---

# 10. Runtime profiles

## 10.1 Local Authoring profile

Main GM может открыть кампанию без активной сетевой сессии.

Composition включает:

- ProcessRuntime;
- CampaignRuntime;
- local Application facade;
- PresentationRuntime;
- no active session transport.

Этот profile не создаёт fake session для обхода архитектуры.

## 10.2 Authoritative Host profile

Main GM запускает host-authoritative session поверх открытой кампании.

Composition включает:

- ProcessRuntime;
- CampaignRuntime;
- HostSessionRuntime;
- local GM presentation;
- approved networking adapters;
- Application orchestration между campaign transaction и publication ports.

Persistence и Networking по-прежнему не ссылаются друг на друга.

## 10.3 Remote Participant profile

Player, Assistant GM или Observer подключается к удалённому host.

Composition включает:

- ProcessRuntime;
- RemoteSessionRuntime;
- client-side presentation/application facade;
- local non-authoritative cache/projection adapters, если они утверждены stage contract;
- отсутствие локального authoritative campaign repository.

Remote profile не получает host-only ports и не может создать authoritative handlers через locator.

## 10.4 Developer Shell profile

Для `SLICE-00` допускается минимальный Developer Shell:

- использует тот же production composition mechanism;
- может подставлять explicitly selected development adapters;
- показывает build/version/health;
- не объявляется production campaign/session implementation;
- не использует скрытые editor-only singletons.

## 10.5 Dedicated server

Dedicated/headless server не входит в текущий MVP baseline. Его composition profile требует отдельного решения и не добавляется ADR-005 заранее.

---

# 11. Unity bootstrap

## 11.1 Bootstrap scene

Проект имеет минимальную bootstrap scene, включённую первой в Build Settings.

Она содержит:

- один `BootstrapBehaviour`/`RuntimeHost`;
- минимальные serialized platform references;
- loading/error presentation;
- scene transition coordinator.

Точные имена компонентов не являются contract, но количество production roots — один.

## 11.2 Unity lifecycle

`Awake` root component может:

- проверить duplicate root;
- захватить Unity-specific references;
- создать cancellation source process lifetime;
- запустить контролируемый async bootstrap.

`Awake` других components не должен зависеть от готовности Application services.

Business startup не распределяется между случайными `Awake`/`Start`.

## 11.3 Duplicate protection

Если bootstrap scene загружена повторно:

- новый root не создаёт второй graph;
- дубликат безопасно уничтожается или load отклоняется;
- событие фиксируется diagnostics;
- существующий root не заменяется молча.

Duplicate protection не превращает root в публичный static singleton.

## 11.4 `DontDestroyOnLoad`

Разрешён один persistent root GameObject.

Дополнительный persistent Unity object требует явного ownership root и обоснования. Он не предоставляет static `Instance` и завершается root shutdown.

## 11.5 Scene initialization

После загрузки feature scene:

1. SceneCoordinator находит строго определённый scene entry point по известному contract;
2. root создаёт `PresentationRuntime`/presenter factory;
3. entry point получает typed scene dependencies через `Initialize(...)`;
4. UI subscriptions активируются после initialization;
5. при unload PresentationRuntime отключает subscriptions и освобождает state.

Scene component не получает весь AppRuntime или service collection, если ему нужен один facade.

## 11.6 Запрещённый scene lookup

Для Application/Infrastructure dependencies запрещено:

- `FindObjectOfType`/`FindFirstObjectByType` как resolver;
- поиск по tag/name;
- `GameObject.Find`;
- `Resources.Load` registry;
- static event bus;
- обращение к bootstrap singleton из view.

Поиск дочернего visual element или заранее serialized scene reference внутри view допустим и не является service location.

---

# 12. Startup phases

Production bootstrap выполняется последовательно:

## Phase 1 — Bootstrap input

- прочитать build information;
- определить runtime environment/profile request;
- получить platform paths;
- создать minimal safe diagnostic sink;
- создать process cancellation.

## Phase 2 — Configuration

- загрузить configuration из утверждённых источников;
- deserialize по ADR-003;
- проверить schema/version;
- выполнить semantic validation;
- отклонить unknown/unsafe production values;
- сформировать immutable typed options.

## Phase 3 — Core infrastructure

- создать production diagnostics/logger;
- создать serializer contexts/contract registries;
- создать platform adapters;
- проверить required directories/capabilities;
- подготовить factories, но не открывать кампанию без user action.

## Phase 4 — Application graph

- создать Application services/facades;
- связать consumer-owned ports с factories/adapters;
- проверить graph invariants;
- создать AppRuntime.

## Phase 5 — Presentation shell

- создать minimal shell presenter;
- инициализировать UI Toolkit root;
- показать build/version/health;
- открыть главное меню/developer navigation.

## Phase 6 — Ready

Application считается Ready только если все обязательные предыдущие фазы успешны.

Ready не означает, что campaign/session уже открыты.

---

# 13. Startup failure

## 13.1 Result contract

Bootstrap возвращает `Result<AppRuntime>` или async equivalent согласно ADR-004.

Expected failures получают стабильные codes, например conceptually:

```text
bootstrap.configuration.invalid
bootstrap.platform.capability_missing
bootstrap.composition.invalid
bootstrap.initialization.cancelled
```

Точные ErrorCode регистрируются по ADR-004 и не должны копировать exception text.

## 13.2 Partial construction cleanup

Если failure произошёл после создания части ресурсов:

- уже созданные owned resources освобождаются в обратном порядке;
- AppRuntime не публикуется;
- UI не получает частичный graph;
- cleanup failure фиксируется отдельной diagnostic записью;
- первоначальная безопасная причина startup сохраняется.

## 13.3 Retry

Повтор bootstrap допускается только после полного cleanup и согласно RetryDirective.

Нельзя повторно использовать partially failed root.

## 13.4 No silent fallback

Production bootstrap не может молча заменить:

- SQLite adapter на in-memory;
- relay adapter на no-op;
- invalid config на default;
- unavailable encryption/key storage на plaintext;
- failed serializer registry на reflection mode.

Development fallback разрешён только явным Developer profile и видимой диагностикой.

---

# 14. Configuration composition

## 14.1 Typed immutable options

Configuration преобразуется в маленькие immutable options по подсистемам:

```text
PersistenceOptions
NetworkingOptions
SerializationOptions
DiagnosticsOptions
GraphicsOptions
```

Названия являются примерами. Один mutable `GlobalConfig` запрещён.

## 14.2 Источники configuration

Точный приоритет sources утверждается ADR-009/Operations contract. Независимо от источника:

- source читается только bootstrap/configuration adapter;
- business services не читают environment variables напрямую;
- `PlayerPrefs` не является general application configuration registry;
- secrets не помещаются в public options/logs;
- options проходят validation до создания dependent service.

## 14.3 Изменяемые настройки пользователя

Runtime user settings могут изменяться через отдельный Application use case и settings repository.

Они не подменяют immutable bootstrap options скрыто. Если изменение требует пересоздания subsystem, создаётся новый scope/runtime по явной процедуре.

## 14.4 Feature flags

Feature flag:

- имеет stable identifier;
- передаётся через typed options;
- не используется для обхода MVP scope;
- не создаёт скрытые альтернативные architectures;
- тестируется в каждом поддерживаемом состоянии.

---

# 15. Resource ownership и shutdown

## 15.1 Один владелец

Каждый owned resource создаётся с однозначным owner:

| Ресурс | Типичный owner |
|---|---|
| process cancellation source | AppRuntime |
| campaign database/runtime | CampaignRuntime |
| host/remote transport | SessionRuntime |
| transaction/unit of work | OperationScope |
| UI subscriptions | PresentationRuntime |
| temporary import stream | Import operation |

Shared ownership через несколько `Dispose` запрещён.

## 15.2 Disposal order

Shutdown выполняется в обратном порядке creation/dependency:

1. запретить новые UI/application operations;
2. отменить presentation flows;
3. завершить/отключить active session;
4. дождаться разрешённых in-flight operations по policy;
5. закрыть campaign runtime;
6. закрыть process adapters;
7. flush/close diagnostics последним допустимым этапом.

Точный network/persistence drain policy уточняется подсистемными ADR, но ownership order не меняется.

## 15.3 Idempotent shutdown

Повторный `DisposeAsync`/shutdown:

- не создаёт второй side effect;
- не бросает expected exception только потому, что scope уже закрыт;
- может вернуть сохранённый terminal result/diagnostic summary;
- безопасен при Unity quit и explicit logout/menu transition.

## 15.4 Shutdown timeout

Долгий shutdown имеет bounded timeout/cancellation policy.

Forced termination:

- фиксируется diagnostics;
- не объявляет данные успешно сохранёнными без commit;
- не скрывает незавершённый outbox/transport state;
- использует crash-recovery contracts Persistence/Networking.

## 15.5 Finalizer не является lifecycle

Finalizer/GC не используется как основной способ закрытия database, transport или subscriptions.

---

# 16. Concurrency, cancellation и main thread

## 16.1 Cancellation

CancellationToken передаётся явно от владеющего scope к operation.

Запрещены:

- глобальный static cancellation token;
- создание независимого бесконечного background task без owner;
- игнорирование process/session cancellation;
- использование destroyed MonoBehaviour как единственного cancellation signal.

## 16.2 Background tasks

Каждый background loop:

- создаётся владельцем scope;
- имеет имя/diagnostic identity;
- получает cancellation;
- отслеживается owner;
- awaited или корректно завершён при shutdown;
- не является fire-and-forget без error boundary.

## 16.3 Unity main thread

Unity API вызывается только через Unity-owned presentation/platform adapters на main thread.

Core service не знает Unity SynchronizationContext.

Если Application result нужно применить к UI:

- Unity Client использует injected main-thread dispatcher/scene coordinator;
- dispatcher является port/adapter с явным lifetime;
- consumer не ищет dispatcher глобально.

## 16.4 Scope capture

Background task более длинного lifetime не захватывает operation/presentation scope.

CI/tests должны выявлять как минимум известные случаи dangling subscriptions и tasks after scope disposal.

---

# 17. Application composition

## 17.1 Handlers

Command/query handlers создаются:

- process/campaign scope, если stateless и зависят только от long-lived factories/facades;
- operation scope, если они удерживают operation-specific state;
- через explicit handler registry, созданный composition root, без reflection scanning.

Точное решение по individual handler lifetime выбирается по ownership, а не по удобству container.

## 17.2 Handler registry

Если нужен dispatcher registry:

- mapping command type → handler создаётся compile-time/explicit code;
- duplicate registration является startup error;
- missing registration является validation error до запуска соответствующего feature;
- registry не раскрывается как universal service resolver;
- Domain/Rules не знают registry.

## 17.3 Decorators/pipeline

Cross-cutting pipeline behaviors, например validation, diagnostics или authorization orchestration:

- связываются явно в composition;
- имеют определённый порядок;
- не добавляются reflection magic;
- не дублируют domain permission/rules checks;
- покрываются order tests.

## 17.4 Circular construction

Circular runtime dependencies не решаются setter injection, lazy locator или post-build mutation.

При cycle необходимо:

- пересмотреть ownership;
- выделить consumer-owned port;
- использовать event/publication contract через Application;
- разделить responsibility.

---

# 18. Presentation composition

## 18.1 View

UI Toolkit view:

- содержит visual references и rendering logic;
- получает presenter/view model через explicit initialization;
- не получает repository/transport;
- не открывает campaign/session сама.

## 18.2 Presenter/ViewModel

Presenter:

- является обычным C# object;
- получает ограниченный Application facade;
- владеет только presentation subscriptions/state;
- возвращает/обрабатывает Result по ADR-004;
- освобождается PresentationRuntime.

## 18.3 Scene entry point

Каждая feature scene имеет один явный entry point или installer contract, но не собственный production composition root.

Он получает typed dependencies от root SceneCoordinator.

## 18.4 Prefabs

Prefab не хранит ссылку на global service asset.

При runtime instantiation:

- visual prefab создаётся Unity factory;
- presenter/view dependencies передаются после instantiate через typed initialization;
- отсутствие initialization выявляется validation/test, а не silent fallback.

---

# 19. Test composition

## 19.1 Отдельный graph

Tests не используют production root с environment switch внутри.

Создаются явные builders/fixtures:

```text
CoreTestComposition
ApplicationTestComposition
PersistenceIntegrationComposition
NetworkingContractComposition
UnityPlayModeComposition
```

Названия могут отличаться, но responsibility разделяется.

## 19.2 Deterministic defaults

Test builder по умолчанию использует:

- fake/frozen clock;
- deterministic RNG согласно ADR-008;
- in-memory или temporary approved adapters;
- isolated temporary paths;
- no network unless test explicitly requests transport;
- no real user secrets;
- unique campaign/session identities.

## 19.3 Explicit overrides

Override выполняется typed method:

```csharp
builder.WithClock(fakeClock);
builder.WithCampaignRepository(repository);
builder.WithSessionPublisher(publisher);
```

Запрещено передавать dictionary `Type → object` как скрытый test locator.

## 19.4 Isolation

Каждый test получает новый owned graph, если test suite явно не доказывает безопасную immutable sharing.

Static mutable test fixture запрещён для campaign/session state.

## 19.5 Production parity

Integration/PlayMode composition должна повторять production wiring order и contracts, заменяя только конкретно указанные adapters.

Тест, который вручную вызывает handler в обход обязательного pipeline, не доказывает production composition.

---

# 20. DI framework policy

## 20.1 Default prohibition

В M1 не добавляются:

- Microsoft.Extensions.DependencyInjection;
- Zenject/Extenject;
- VContainer;
- Autofac;
- другой runtime DI container.

Это не оценка качества библиотек; проект пока не доказал необходимость дополнительной dependency.

## 20.2 Когда возможен новый ADR

Предложение DI framework должно предоставить:

1. измеримую проблему manual composition;
2. количество registrations/scopes и конкретную сложность;
3. сравнение с generated/manual factories;
4. лицензионную совместимость;
5. Unity 6.3/IL2CPP/AOT proof;
6. отсутствие reflection-only runtime requirement;
7. startup validation;
8. план сохранения consumer-owned ports;
9. запрет `IServiceProvider` в business code;
10. migration и rollback plan.

## 20.3 Даже при будущем container

Остаются обязательными:

- один composition root;
- constructor injection;
- explicit lifetimes;
- no service locator;
- no container reference в Domain/Rules/Application contracts;
- no hidden auto-registration по всему AppDomain;
- graph validation в CI;
- typed factories для dynamic scopes.

---

# 21. Security и публичный репозиторий

Composition code находится в публичном репозитории и не содержит:

- secrets;
- production tokens;
- owner key material;
- private paths;
- credentials в default options;
- скрытые endpoint credentials;
- test backdoor, активируемый только magic service registration.

Secret provider создаётся platform composition и передаёт только необходимые secret handles конкретному adapter. UI/Application не получает весь secret store без необходимости.

Diagnostics composition следует ADR-004 и не регистрирует unsafe exception/data sinks.

Development adapter:

- видимо маркируется;
- не выбирается production profile автоматически;
- не даёт дополнительных игровых permissions;
- не попадает в release graph без явной build validation.

---

# 22. Composition validation

## 22.1 Startup validation

До Ready проверяется:

- все обязательные ports связаны;
- duplicate registrations отсутствуют;
- runtime profile не содержит запрещённый adapter;
- options валидны;
- lifetime graph не содержит известного captive dependency;
- factory может создать минимальный scope;
- serializer registries готовы;
- Unity scene root единственный.

## 22.2 Compile-time и static checks

CI проверяет:

- module references по ADR-001;
- отсутствие незарегистрированного DI package;
- отсутствие production reference на test composition;
- отсутствие известных locator APIs/patterns вне allowlist;
- отсутствие static mutable service registry;
- отсутствие `UnityEngine` references в Core;
- отсутствие нескольких production composition roots.

Static pure constants/functions не запрещены. Проверка должна отличать их от mutable service state.

## 22.3 Runtime smoke tests

Минимальный набор:

1. process graph строится;
2. graph корректно завершается;
3. bootstrap failure освобождает уже созданные ресурсы;
4. campaign scope открывается и закрывается;
5. campaign можно открыть повторно после закрытия;
6. host session start/stop не оставляет active subscriptions;
7. remote session не получает authoritative ports;
8. scene load/unload создаёт и освобождает PresentationRuntime;
9. duplicate bootstrap не создаёт второй graph;
10. shutdown повторно безопасен.

## 22.4 IL2CPP proof

Windows x64 IL2CPP development build должен:

- создать production/developer shell graph;
- пройти startup readiness;
- выполнить минимальную Application operation;
- закрыться без missing reflection registration и unobserved task errors.

---

# 23. Нормативный пример composition

Ниже приведён концептуальный пример. Он показывает направление wiring, а не обязательные имена классов:

```csharp
internal static class OdysseyCompositionRoot
{
    public static Result<AppRuntime> Build(BootstrapInput input)
    {
        var configResult = BootstrapConfiguration.LoadAndValidate(input);
        if (configResult.IsFailure)
        {
            return Result<AppRuntime>.Failure(configResult.Error);
        }

        var owned = new ConstructionScope();

        try
        {
            var diagnostics = owned.Own(
                DiagnosticsFactory.Create(configResult.Value.Diagnostics));

            var serializers = SerializerRegistryFactory.Create(
                configResult.Value.Serialization);

            var platform = owned.Own(
                UnityPlatformAdapters.Create(input.UnityContext, diagnostics));

            var campaignFactory = new CampaignRuntimeFactory(
                configResult.Value,
                diagnostics,
                serializers,
                platform.Paths);

            var sessionFactory = new SessionRuntimeFactory(
                configResult.Value,
                diagnostics,
                serializers,
                platform.Dispatcher);

            var shell = new ApplicationShell(
                campaignFactory,
                sessionFactory,
                diagnostics);

            var runtime = new AppRuntime(
                shell,
                platform,
                diagnostics,
                owned.Commit());

            return Result<AppRuntime>.Success(runtime);
        }
        catch (Exception exception)
        {
            var error = BootstrapExceptionBoundary.Map(exception);
            owned.DisposeSafely();
            return Result<AppRuntime>.Failure(error);
        }
    }
}
```

Ограничения примера:

- `ConstructionScope` не является service provider;
- он только отслеживает ownership при partial construction;
- business code не получает его;
- exception mapping следует ADR-004;
- concrete factories находятся у composition boundary;
- реальные signatures уточняются реализацией.

---

# 24. Запрещённые обходные решения

Запрещено:

- `ServiceLocator`, `GlobalServices`, `AppServices`;
- `IServiceProvider`/container injection в handler, presenter, domain service или adapter;
- static mutable `Instance` для repository, command bus, logger, transport или application state;
- отдельный composition root в каждой Unity scene;
- создание production services в случайных `MonoBehaviour.Awake`;
- `FindObjectOfType` как dependency resolution;
- ScriptableObject service registry;
- reflection assembly scanning для auto-registration;
- string-keyed service lookup;
- universal `ObjectFactory`/`ManagerFactory`;
- hidden fallback на fake/no-op adapter;
- process singleton, удерживающий campaign/session/operation dependency;
- background task без owner и cancellation;
- несколько owners одного `IDisposable`;
- создание второго AppRuntime при возврате в bootstrap scene;
- передача всего AppRuntime/ViewModel сервисам вместо минимального facade;
- direct environment/PlayerPrefs read в business service;
- container package, добавленный Codex без отдельного ADR;
- production/test registrations в одном mutable registry;
- optional dependency, извлекаемая через locator;
- circular dependency, разорванная setter injection;
- бизнес-решение внутри installer/composition code.

---

# 25. Рассмотренные альтернативы

## 25.1 DI container с первого дня

Отклонено.

На Stage 1 количество services ограничено, а manual graph остаётся обозримым. Container добавляет dependency, lifecycle semantics и возможные IL2CPP/reflection риски раньше доказанной необходимости.

## 25.2 Service locator

Отклонено.

Он скрывает dependencies, усложняет tests, допускает runtime missing service и превращает любой consumer в composition boundary.

## 25.3 Unity manager singletons

Отклонено.

`DontDestroyOnLoad` managers создают неявный lifetime, зависят от scene order и плохо изолируются в tests.

## 25.4 ScriptableObject-based service registry

Отклонено.

ScriptableObject полезен для authoring/configuration assets, но registry смешивает editor assets, runtime services и mutable state.

## 25.5 Reflection auto-registration

Отклонено.

Скрывает graph, ухудшает AOT/IL2CPP predictability и делает missing/duplicate registration runtime-проблемой.

## 25.6 Создавать dependencies внутри каждого handler

Отклонено.

Это связывает Application с concrete infrastructure, ломает transaction/lifetime ownership и затрудняет tests.

## 25.7 Один global AppContext со всеми services

Отклонено.

Это service locator под другим именем, создающий широкие зависимости и captive scopes.

## 25.8 Один lifetime на всё приложение

Отклонено.

Campaign, session, operation и scene имеют разные границы. Единый process lifetime приводит к stale state и resource leaks.

---

# 26. Последствия

## 26.1 Положительные

- dependencies видны в constructors;
- graph можно проверить без запуска полного gameplay;
- Core остаётся независимым от Unity/container;
- scopes campaign/session можно открывать и закрывать безопасно;
- tests получают deterministic adapters;
- IL2CPP не зависит от reflection registration;
- Codex получает один шаблон добавления сервиса;
- утечки manager-singletons обнаруживаются раньше;
- DI framework можно добавить позже осознанно;
- runtime profiles не смешивают host и remote authority.

## 26.2 Стоимость

- composition code будет более многословным;
- потребуется писать typed factories и runtime handles;
- constructors могут меняться при развитии features;
- lifecycle tests обязательны;
- часть Unity convenience patterns запрещена;
- разработчик должен явно решать ownership каждого ресурса.

Стоимость принимается как необходимая для host-authoritative VTT с Persistence, Networking и несколькими runtime scopes.

---

# 27. План реализации в `SLICE-00`

## 27.1 PR-003 / module skeleton

Создать:

- assemblies по ADR-001;
- Unity bootstrap scene;
- `Odyssey.Unity.Client` composition namespace/area;
- минимальный `AppRuntime` owner;
- developer shell facade;
- production-safe interfaces без real persistence/network implementation.

## 27.2 PR-004 / Core primitives

Подключить:

- Result/Error ADR-004;
- command/event contracts ADR-002;
- serialization registries ADR-003;
- typed options;
- construction cleanup helper без service resolution.

## 27.3 PR-005 / fast CI

Добавить:

- composition smoke tests;
- duplicate bootstrap PlayMode test;
- startup failure cleanup test;
- shutdown idempotency test;
- architecture scan на locator/container patterns;
- IL2CPP developer-shell proof.

## 27.4 До production adapters

До Stage 2 разрешены explicit in-memory/test adapters, только если:

- они выбираются Developer/Test profile явно;
- production profile не fallback-ится на них;
- lifetime contracts совпадают с production ports;
- документация не объявляет их final persistence/network solution.

---

# 28. Acceptance criteria

ADR-005 считается реализованным для M1, когда:

1. в production code существует один composition root;
2. приложение открывает bootstrap scene без order-dependent `Awake` errors;
3. AppRuntime создаётся через Result и отображает readiness;
4. graph не содержит service locator/container;
5. Domain/Rules/Application не ссылаются на Unity или composition types;
6. campaign/session factories являются typed;
7. resource ownership описан и проверен;
8. shutdown выполняется в обратном порядке и повторно безопасен;
9. scene load/unload не оставляет presenter subscriptions;
10. duplicate bootstrap не создаёт второй runtime;
11. test composition использует отдельный builder;
12. CI блокирует запрещённые dependencies/patterns;
13. Mono и IL2CPP smoke tests проходят;
14. Codex task template указывает lifetime и composition impact для нового service;
15. отсутствуют скрытые production fallbacks.

---

# 29. Обязательные тестовые сценарии

| ID | Сценарий | Ожидаемый результат |
|---|---|---|
| CMP-001 | Build minimal process graph | `Success`, один AppRuntime |
| CMP-002 | Invalid bootstrap configuration | safe Failure, graph не опубликован |
| CMP-003 | Adapter creation fails midway | уже созданные resources освобождены |
| CMP-004 | Bootstrap scene загружена повторно | второй graph не создан |
| CMP-005 | Open and close campaign | campaign resources закрыты |
| CMP-006 | Reopen another campaign | stale state/subscriptions отсутствуют |
| CMP-007 | Start and stop host session | transport/subscriptions завершены |
| CMP-008 | Build remote participant profile | authoritative persistence ports недоступны |
| CMP-009 | Load/unload feature scene | PresentationRuntime освобождён |
| CMP-010 | Shutdown called twice | no duplicate side effects |
| CMP-011 | Process cancellation during startup | bounded cleanup и cancelled Result |
| CMP-012 | Background loop after scope dispose | loop завершён, unobserved errors отсутствуют |
| CMP-013 | Missing explicit handler registration | startup/feature validation failure |
| CMP-014 | Duplicate handler registration | composition validation failure |
| CMP-015 | Test override one adapter | остальные deterministic defaults сохранены |
| CMP-016 | Production profile requests fake adapter | startup rejected |
| CMP-017 | IL2CPP developer shell | graph строится и закрывается |
| CMP-018 | Static locator pattern introduced | CI architecture check fails |
| CMP-019 | Long-lived service captures operation resource | lifetime test/review gate fails |
| CMP-020 | Shutdown during active presentation/session | порядок завершения соблюдён |

---

# 30. Правила для Codex

Каждая задача, добавляющая service/adapter/factory, обязана указать:

- owning module;
- lifetime scope;
- consumer-owned port;
- concrete implementation location;
- кто создаёт объект;
- кто его освобождает;
- нужен ли typed factory;
- production и test composition changes;
- configuration/options impact;
- startup/shutdown impact;
- required composition tests.

Codex запрещено самостоятельно:

- добавлять DI package;
- создавать global manager/singleton;
- передавать service provider;
- создавать второй composition root;
- использовать scene lookup как resolver;
- добавлять silent fallback;
- менять lifetime без ADR/review;
- создавать background task без owner;
- помещать business rules в bootstrap;
- обходить Application через direct adapter reference.

Если ownership/lifetime нельзя определить однозначно, Codex останавливает реализацию и отмечает architectural blocker вместо создания `Common`, `Manager` или locator.

---

# 31. Отложенные решения

Этот ADR намеренно не фиксирует полностью:

- структуру test projects и fixture conventions — ADR-006;
- application/build/schema version policy — ADR-007;
- concrete Clock/RNG lifetimes и deterministic streams — ADR-008;
- точный Unity package/configuration/bootstrap asset baseline — ADR-009;
- logging implementation и sinks — ADR-010;
- конкретный SQLite driver/connection strategy — Persistence ADR;
- конкретный relay provider/transport lifecycle — Networking ADR;
- dedicated server composition — post-MVP research;
- возможность будущего DI framework — только новый ADR.

Отложенное решение не может вводить locator, нарушать scopes или менять ownership без superseding ADR.

---

# 32. Трассировка

| Источник | Связь |
|---|---|
| Technical Development Baseline §18 | Заменяет preliminary manual composition правила точной моделью scopes/root/bootstrap |
| ADR-001 | Соблюдает module graph, consumer-owned ports и Unity Client composition ownership |
| ADR-002 | Создаёт handlers, operation context и publication ports без nested locator |
| ADR-003 | Композирует explicit serializer registries и AOT-safe contexts |
| ADR-004 | Определяет startup/factory/shutdown Result/Error и exception boundaries |
| Persistence contract | Campaign/operation resource ownership и future adapter lifecycle |
| Networking contract | Host/remote SessionRuntime lifecycle и отсутствие direct Persistence dependency |
| Test Strategy | Требует Core, integration, PlayMode и IL2CPP evidence |
| Roadmap Stage 1 | Закрывает обязательное решение до bootstrap implementation |

---

# 33. Нормативное действие

С момента принятия ADR-005:

1. Раздел 18 Technical Development Baseline считается предварительным и уточняется этим ADR.
2. Новый production service не принимается без явного lifetime и composition location.
3. Production DI container отсутствует.
4. `Odyssey.Unity.Client` остаётся единственным production composition root.
5. Любой service locator/global mutable manager является архитектурным нарушением.
6. Campaign, Session, Operation и Presentation resources не могут жить дольше своего owner scope.
7. Scene/UI code получает dependencies только через explicit initialization/factory.
8. Startup/shutdown и partial-construction cleanup являются обязательной частью `SLICE-00`.
9. Нарушение rules блокирует pull request.
10. Изменение этих принципов требует нового ADR, который явно supersedes ADR-005.

---

**Конец документа**
