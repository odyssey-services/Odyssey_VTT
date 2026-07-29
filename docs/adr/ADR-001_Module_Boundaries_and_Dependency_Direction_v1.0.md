# ADR-001 — Module Boundaries and Dependency Direction

**Документ:** `docs/adr/ADR-001_Module_Boundaries_and_Dependency_Direction_v1.0.md`  
**ADR:** ADR-001  
**Версия:** 1.0  
**Дата:** 27 июля 2026 года  
**Статус:** Accepted  
**Область:** модульные границы, compile-time dependencies, ownership типов, ports/adapters, Unity assembly definitions и архитектурные проверки  
**Связанные этапы:** Roadmap Stage 1, `SLICE-00`, Milestone `M1`  
**Базовый документ:** `TECHNICAL_DEVELOPMENT_BASELINE_Odyssey_VTT_v0.1.md`

---

# 1. Решение

Odyssey VTT использует ациклическую модульную архитектуру с направлением зависимостей от внешних механизмов к внутренним правилам.

Активные production-модули первого технического каркаса:

```text
Odyssey.Domain
Odyssey.Rules
Odyssey.Content
Odyssey.Application
Odyssey.Persistence
Odyssey.Networking
Odyssey.Unity.Client
```

Тестовый код размещается в отдельных test assemblies и .NET test projects. `Odyssey.Tests` не является production-модулем и не может быть runtime dependency.

Единственный разрешённый граф compile-time dependencies:

```text
Odyssey.Domain

Odyssey.Rules
    └── Odyssey.Domain

Odyssey.Content
    ├── Odyssey.Domain
    └── Odyssey.Rules

Odyssey.Application
    ├── Odyssey.Domain
    ├── Odyssey.Rules
    └── Odyssey.Content

Odyssey.Persistence
    ├── Odyssey.Domain
    ├── Odyssey.Content
    └── Odyssey.Application

Odyssey.Networking
    ├── Odyssey.Domain
    ├── Odyssey.Content
    └── Odyssey.Application

Odyssey.Unity.Client
    ├── Odyssey.Domain
    ├── Odyssey.Rules
    ├── Odyssey.Content
    ├── Odyssey.Application
    ├── Odyssey.Persistence
    └── Odyssey.Networking
```

Любая dependency, отсутствующая в этом графе, запрещена до принятия нового ADR.

---

# 2. Контекст и проблема

Продуктовые документы Odyssey определяют большое количество пересекающихся подсистем: доменную модель, Rules Engine, content packages, persistence, host-authoritative networking, redaction, UI и Unity platform integration.

Без жёстких compile-time boundaries возникают системные риски:

- игровая логика переносится в MonoBehaviour, UI callbacks или сетевой транспорт;
- Domain начинает зависеть от SQLite, JSON provider или Unity types;
- Persistence и Networking начинают напрямую вызывать друг друга;
- transport DTO становятся доменными объектами;
- Content Block execution обходит Application transaction и authorization;
- одна задача Codex создаёт новую «общую» библиотеку, а последующие задачи складывают туда несвязанные типы;
- Core невозможно тестировать без Unity Editor;
- циклические ссылки делают вертикальные срезы зависимыми от незавершённых адаптеров.

Technical Development Baseline задал общий layered/hexagonal подход, но предварительная матрица содержала формулировки `limited contracts`. Для реализации assemblies этого недостаточно: Codex и CI должны иметь бинарное правило — ссылка либо разрешена, либо запрещена.

Этот ADR устраняет неопределённость и заменяет предварительную матрицу точным ациклическим графом.

---

# 3. Движущие факторы

Решение оптимизировано под следующие требования:

1. Core должен компилироваться и тестироваться без Unity Editor.
2. Domain и Rules должны оставаться детерминированными.
3. Application должен быть единственной точкой orchestration игровых use cases.
4. Persistence и Networking являются заменяемыми адаптерами, а не владельцами игровых решений.
5. Unity Client является composition root, но не authoritative state store.
6. Один и тот же source code Core должен использоваться Unity и pure .NET test solution.
7. Нарушения границ должны обнаруживаться до review, а не после интеграции.
8. Задача Codex должна иметь однозначное место для каждого нового типа.
9. Архитектура должна позволять Stage 2+ без преждевременного выбора SQLite driver, relay SDK или account provider.
10. Новая реализация не должна наследовать границы старого прототипа.

---

# 4. Module, package, assembly и namespace

Эти понятия не взаимозаменяемы.

## 4.1 Module

Module — архитектурная область ответственности из списка в разделе 1. Модуль владеет публичным контрактом и может позднее содержать несколько assemblies, если это необходимо для adapter-specific implementation.

Создание нового top-level module требует отдельного ADR.

## 4.2 Embedded Unity package

Каждый Core/infrastructure module размещается в отдельном embedded package:

```text
Packages/com.odyssey.domain/
Packages/com.odyssey.rules/
Packages/com.odyssey.content/
Packages/com.odyssey.application/
Packages/com.odyssey.persistence/
Packages/com.odyssey.networking/
```

Unity Client размещается под `Assets/Odyssey/Client/`.

## 4.3 Assembly

На `SLICE-00` каждый module получает одну runtime assembly:

```text
Odyssey.Domain
Odyssey.Rules
Odyssey.Content
Odyssey.Application
Odyssey.Persistence
Odyssey.Networking
Odyssey.Unity.Client
```

Дополнительная assembly внутри существующего module допустима только когда:

- она изолирует provider/platform-specific adapter;
- не создаёт новую архитектурную ответственность;
- соблюдает граф этого ADR;
- имеет отдельный `.asmdef` и тесты;
- её необходимость указана в задаче или ADR.

Примеры допустимого будущего разделения:

```text
Odyssey.Persistence.Sqlite
Odyssey.Networking.Transport.<Provider>
Odyssey.Unity.Client.Editor
```

Такое разделение не разрешает обратную dependency к adapter assembly.

## 4.4 Namespace

Корневой namespace совпадает с assembly name. Namespace не используется для обхода assembly boundary.

Код одного module не размещается под namespace другого module.

---

# 5. Нормативная матрица зависимостей

`✓` — compile-time reference разрешена. `—` — запрещена.

| From / To | Domain | Rules | Content | Application | Persistence | Networking | Unity Client |
|---|---:|---:|---:|---:|---:|---:|---:|
| Domain | — | — | — | — | — | — | — |
| Rules | ✓ | — | — | — | — | — | — |
| Content | ✓ | ✓ | — | — | — | — | — |
| Application | ✓ | ✓ | ✓ | — | — | — | — |
| Persistence | ✓ | — | ✓ | ✓ | — | — | — |
| Networking | ✓ | — | ✓ | ✓ | — | — | — |
| Unity Client | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | — |

Дополнительные правила:

- `Persistence → Networking` запрещено.
- `Networking → Persistence` запрещено.
- `Content → Application` запрещено.
- `Domain → Rules` запрещено.
- любой production module → `Unity.Client` запрещено.
- production module → test assembly запрещено.
- прямые циклы и транзитивные циклы запрещены.
- BCL references не считаются межмодульной dependency, но должны соответствовать утверждённому .NET baseline.

Этот раздел нормативно заменяет предварительную таблицу раздела 12 Technical Development Baseline v0.1.

---

# 6. Ownership модулей

## 6.1 Odyssey.Domain

`Odyssey.Domain` владеет бизнес-смыслом и инвариантами, которые остаются истинными независимо от UI, базы данных и сети.

Разрешено:

- typed identity types;
- value objects;
- entities и aggregate roots;
- domain invariants;
- domain events как факты завершённого изменения;
- stable domain error codes;
- permission vocabulary и visibility vocabulary;
- immutable domain primitives;
- pure domain services, если поведение не принадлежит одному aggregate.

Запрещено:

- `UnityEngine` и Unity packages;
- JSON/SQLite/network provider annotations;
- repository implementations;
- file paths и platform dialogs;
- HTTP, sockets, relay SDK;
- wall-clock и global random access;
- UI state, view models и localized text;
- application command handlers;
- transport/persistence DTO.

Domain не определяет interfaces «на всякий случай». Interface создаётся только при наличии реального domain-level polymorphism, а не для infrastructure dependency inversion.

## 6.2 Odyssey.Rules

`Odyssey.Rules` владеет детерминированными вычислениями системы правил.

Разрешено:

- формулы;
- expression evaluation;
- roll/calculation trace;
- ruleset execution contracts;
- targeting и outcome calculations;
- deterministic validation;
- pure calculators;
- Rules-specific value objects.

Rules получает время, RNG outcomes и внешнее состояние через явные входные параметры или утверждённые abstractions. Rules не читает clock, random, database или session state самостоятельно.

Rules не публикует network messages и не сохраняет результаты.

## 6.3 Odyssey.Content

`Odyssey.Content` владеет безопасной моделью контентных пакетов и блоков.

Разрешено:

- Content Block definitions;
- package manifest model;
- dependency graph;
- validation rules;
- publication model;
- import/export contracts;
- content hash и content identity types;
- безопасные inputs для Rules execution;
- validation результата подготовки контента.

Content зависит от Domain и Rules, но не знает Application use cases.

Content не выполняет транзакцию, не изменяет campaign aggregate и не решает authorization. Application передаёт валидированный Content в Domain/Rules в рамках use case.

## 6.4 Odyssey.Application

`Odyssey.Application` владеет выполнением use cases.

Разрешено:

- commands, queries и handlers;
- application services;
- orchestration Domain, Rules и Content;
- transaction boundaries;
- authorization и redaction calls;
- idempotency workflow;
- event publication;
- ports, необходимые use case;
- application results и stable error mapping;
- application-level projections;
- coordination of persistence/network side effects through ports.

Application определяет port на стороне потребителя. Например:

```text
ICampaignRepository
IApplicationTransaction
IEventOutbox
ISessionPublisher
IClock
IRandomSource
IDiagnosticsSink
```

Concrete implementations находятся во внешних modules.

Application не создаёт `SqliteConnection`, transport client, Unity object или file dialog.

## 6.5 Odyssey.Persistence

`Odyssey.Persistence` реализует persistence ports, объявленные Application.

Разрешено:

- SQLite implementation;
- database schema и migrations;
- repositories;
- transaction/outbox implementation;
- snapshots, journal, backup и recovery;
- persisted DTO/records;
- `.odcamp` staging/import/export;
- content-addressed asset storage;
- mapping между persisted representation и Domain/Content/Application contracts.

Persistence не владеет игровыми инвариантами и не вызывает Networking.

Persistence не имеет права считать запись успешной до соблюдения Application transaction contract.

## 6.6 Odyssey.Networking

`Odyssey.Networking` реализует session/network ports, объявленные Application.

Разрешено:

- transport abstraction и provider adapters;
- command ingress/egress envelopes;
- network message DTO;
- ordering, acknowledgement и reconnect mechanisms;
- snapshot/delta transport;
- projection delivery;
- redaction transport tests;
- session diagnostics;
- mapping wire messages к Application contracts.

Networking не изменяет Domain state напрямую, не читает SQLite и не принимает authoritative game decisions.

Host-authoritative command всегда проходит через Application. Полученный от клиента payload является запросом, а не готовым изменением состояния.

## 6.7 Odyssey.Unity.Client

`Odyssey.Unity.Client` владеет Unity-specific presentation и composition.

Разрешено:

- bootstrap и composition root;
- UI Toolkit views;
- presenters/view models;
- Unity scenes и lifecycle;
- input mapping;
- graphics/audio/platform adapters;
- developer diagnostics UI;
- build/runtime information presentation;
- thin integration code для вызова Application.

Запрещено:

- authoritative campaign state в MonoBehaviour или ScriptableObject;
- игровые инварианты в UI callbacks;
- прямое изменение Domain aggregate из view;
- прямое чтение/запись SQLite из UI;
- отправка transport messages минуя Application;
- service locator как основной способ composition.

Unity Client является единственным production composition root на `SLICE-00`.

---

# 7. Ownership типов и контрактов

Для каждого нового типа сначала определяется владелец.

| Тип | Владелец |
|---|---|
| Entity, aggregate, value object, domain event | Domain |
| Formula, rules trace, deterministic calculator | Rules |
| Content package/block definition | Content |
| Command, query, handler, use-case port | Application |
| Database row/record, migration, repository implementation | Persistence |
| Wire message, transport envelope implementation | Networking |
| View, presenter, view model, Unity adapter | Unity Client |
| Fixture, fake, test builder | Test project соответствующего уровня |

## 7.1 DTO не являются универсальными

Запрещено создавать один `SharedDto`, используемый UI, сетью и persistence.

Каждая boundary имеет собственное представление:

- Application input/output contract;
- Networking wire DTO;
- Persistence record/DTO;
- Content package DTO;
- Unity presentation model.

Mapping является явным и тестируемым.

## 7.2 Domain events и integration messages

- Domain event находится в Domain и описывает завершившийся доменный факт.
- Application notification находится в Application и описывает результат use case для ports.
- Network message находится в Networking и описывает wire contract.
- Persisted journal record находится в Persistence.

Один класс не используется одновременно во всех четырёх ролях.

## 7.3 Public API

- типы `internal` по умолчанию;
- `public` используется только для межмодульного API;
- mutable public fields запрещены;
- public API не раскрывает concrete provider type;
- `InternalsVisibleTo` разрешён только test assemblies;
- `InternalsVisibleTo` между production modules запрещён.

---

# 8. Ports and adapters

## 8.1 Consumer-owned ports

Port определяется внутренним module, который его потребляет.

Для Odyssey основной владелец infrastructure ports — Application.

Это означает:

- Application определяет `ICampaignRepository`;
- Persistence реализует `ICampaignRepository`;
- Application определяет `ISessionPublisher`;
- Networking реализует `ISessionPublisher`;
- Unity composition root передаёт implementations в Application.

Adapter не заставляет внутренний module зависеть от provider API.

## 8.2 Запрет adapter-to-adapter coordination

Persistence и Networking не координируют use case напрямую.

Неверно:

```text
Persistence commit → direct call Networking.Broadcast(...)
```

Верно:

```text
Application handler
    → transaction/persistence port
    → domain/application events
    → networking publication port
```

Если требуется надёжная публикация после commit, контракт outbox определяется Application, а реализация — Persistence. Networking потребляет разрешённый Application-level publication contract через orchestration, а не через ссылку на Persistence.

## 8.3 Platform ports

File picker, clipboard, OS paths, key storage и другие platform functions объявляются на границе Application или Unity Client в зависимости от use case.

Domain и Rules не знают platform ports.

---

# 9. Нормативные runtime flows

## 9.1 Локальная команда

```text
UI Toolkit View
    → Presenter/ViewModel
    → Application Command Dispatcher
    → Authorization / Validation / Idempotency
    → Domain + Rules + Content
    → Persistence ports
    → Domain/Application events
    → Updated projection
    → Presenter/ViewModel
    → View
```

## 9.2 Сетевая команда на host

```text
Client wire message
    → Networking ingress validation
    → Application command
    → Authorization / Validation / Idempotency
    → Domain + Rules + Content
    → Persistence transaction
    → Projection + redaction
    → Networking publication port
    → recipient-specific wire messages
```

Networking не передаёт client-provided state как authoritative result.

## 9.3 Загрузка кампании

```text
Unity Client requests Open Campaign use case
    → Application port
    → Persistence implementation
    → persisted records mapped to domain state
    → Application projection
    → Unity presentation model
```

Unity Client не десериализует campaign database самостоятельно.

---

# 10. Assembly definition rules

На `SLICE-00` создаются `.asmdef` с exact names из раздела 4.3.

Для Core assemblies:

- references указываются явно;
- `autoReferenced` отключается;
- `allowUnsafeCode` выключен;
- `overrideReferences` не включается без необходимости;
- `noEngineReferences` включён для Domain, Rules, Content и Application;
- platform constraints не должны случайно исключать pure Core;
- Editor code находится в отдельной Editor assembly;
- test assemblies не входят в player build.

Для Persistence и Networking baseline также стремится к `noEngineReferences = true`. Если конкретный provider требует Unity API, provider-specific code выносится в отдельную adapter assembly. Core contract module не загрязняется Unity dependency.

`Odyssey.Unity.Client` имеет explicit references на необходимые modules и не включается в pure .NET solution.

---

# 11. Unity package dependencies

`package.json` каждого embedded package отражает тот же граф, что и `.asmdef`.

Минимальная зависимость packages:

```text
com.odyssey.domain        → none
com.odyssey.rules         → com.odyssey.domain
com.odyssey.content       → com.odyssey.domain, com.odyssey.rules
com.odyssey.application   → com.odyssey.domain, com.odyssey.rules, com.odyssey.content
com.odyssey.persistence   → com.odyssey.domain, com.odyssey.content, com.odyssey.application
com.odyssey.networking    → com.odyssey.domain, com.odyssey.content, com.odyssey.application
```

`package.json` не использует floating versions.

Изменение dependency graph выполняется отдельным pull request и требует обновления ADR либо нового ADR.

---

# 12. Pure .NET compilation

Domain, Rules, Content и Application обязательно компилируются в `DotNet/Odyssey.Core.sln` из тех же source files.

Persistence и Networking могут подключаться в отдельные .NET projects после выбора providers, но их contracts и provider-independent code должны по возможности оставаться совместимыми с pure .NET.

Правила:

- source code не копируется между `Packages` и `DotNet`;
- `.csproj` использует explicit includes или shared build props;
- conditional compilation не меняет бизнес-поведение;
- Core не требует Unity defines;
- Unity и .NET build используют совместимый language/API baseline;
- расхождение результатов одинакового Core test считается defect.

---

# 13. Architecture enforcement

CI обязан проверять границы автоматически.

Минимальные проверки:

1. каждый production `.asmdef` имеет только разрешённые references;
2. package dependency graph совпадает с этим ADR;
3. Core source не содержит `using UnityEngine` и ссылок на Unity assemblies;
4. Domain не содержит provider-specific namespaces;
5. Persistence и Networking не ссылаются друг на друга;
6. production assemblies не ссылаются на test assemblies;
7. dependency graph ацикличен;
8. pure .NET Core solution компилируется;
9. запрещённые references блокируют pull request.

Architecture check должен выдавать:

- нарушившую assembly;
- запрещённую target assembly;
- правило ADR-001;
- путь к `.asmdef`, `.csproj` или `package.json`.

Ручное review не заменяет автоматическую проверку.

---

# 14. Запрещённые обходные решения

Следующие подходы запрещены:

- папка или module `Common`, `Shared`, `Utils` как склад несвязанных типов;
- circular dependency через interfaces, partial classes или conditional compilation;
- reflection для доступа к internal API другого module;
- service locator или глобальный mutable singleton;
- static event bus, скрывающий dependency и lifetime;
- `UnityEngine.Object` в Domain/Application contracts;
- ScriptableObject как authoritative domain storage;
- repository interface в Persistence вместо Application, если его потребляет use case;
- прямой SQL из Application или Unity Client;
- прямой transport call из Domain, Rules, Content или Persistence;
- использование persistence DTO как network payload;
- использование wire DTO как domain entity;
- добавление production reference к test utilities;
- новая top-level assembly без зарегистрированной ответственности.

Нейтральные helper-типы размещаются у владельца поведения. Если владельца определить невозможно, задача останавливается на архитектурном review, а не создаёт `Common` автоматически.

---

# 15. Изменение границ

Новый ADR обязателен для:

- добавления top-level module;
- изменения стрелки в матрице;
- разрешения adapter-to-adapter dependency;
- добавления Unity dependency в Core;
- переноса authoritative state в Unity layer;
- создания общего cross-cutting runtime module;
- отказа от consumer-owned ports;
- объединения двух текущих modules;
- разделения module так, что меняется его public responsibility.

Обычный pull request может:

- добавлять тип внутри существующего ownership;
- создавать internal namespace;
- создавать test assembly;
- создавать provider-specific subassembly внутри module, если граф не меняется и это разрешено задачей.

---

# 16. Рассмотренные альтернативы

## 16.1 Один Unity assembly

Отклонено: не позволяет технически отделить Domain, adapters и UI; нарушения обнаруживаются слишком поздно.

## 16.2 Feature-first assemblies

Отклонено для первого каркаса: крупные вертикальные функции пересекают persistence, network и UI, что создаёт дублирование infrastructure и затрудняет host-authoritative guarantees.

Feature folders внутри module разрешены, но module boundaries остаются архитектурными.

## 16.3 Domain зависит от Rules

Отклонено: базовая модель должна существовать без конкретного rules execution layer. Rules использует Domain vocabulary, а не наоборот.

## 16.4 Content зависит от Application

Отклонено: это создаёт цикл, поскольку Application должен orchestrate Content use cases. Content является pure capability, вызываемой Application.

## 16.5 Persistence напрямую публикует в Networking

Отклонено: adapter-to-adapter coordination скрывает transaction semantics и делает offline/local режим зависимым от network module.

## 16.6 Общий Contracts module

Не принят на `SLICE-00`: отдельный generic contracts module быстро становится свалкой и размывает ownership. Потребность в нём должна быть доказана новым ADR.

---

# 17. Последствия

## 17.1 Положительные

- Core тестируется без Unity.
- Codex получает однозначное место для нового кода.
- Persistence и Networking можно заменять независимо.
- UI не становится источником истины.
- host authority и transaction boundaries остаются в Application.
- циклы обнаруживаются до merge.
- provider SDK не проникает в Domain и Rules.
- mapping между boundaries становится явным и тестируемым.

## 17.2 Стоимость

- потребуется больше mapping code;
- один use case может затрагивать несколько projects/packages;
- package и asmdef configuration необходимо поддерживать;
- нельзя быстро «переиспользовать» один DTO во всех слоях;
- часть простых изменений потребует архитектурной дисциплины.

Эта стоимость принимается ради стабильности долгого MVP-пути и разработки через Codex.

---

# 18. Реализация в SLICE-00

ADR-001 реализуется в PR `Solution and Module Skeleton`.

Минимальный объём:

1. создать шесть embedded packages;
2. создать `Odyssey.Unity.Client` assembly;
3. создать exact `.asmdef` references;
4. создать `package.json` dependency graph;
5. создать pure .NET Core solution;
6. добавить по одному smoke type в каждый module;
7. добавить architecture validation script/test;
8. доказать, что Domain/Rules/Content/Application компилируются без Unity;
9. доказать, что запрещённая тестовая dependency ломает check;
10. удалить test violation перед merge.

Smoke types не должны становиться случайным production API. Они могут быть заменены Core primitives в следующем PR.

---

# 19. Критерии приёмки

ADR считается реализованным, когда выполнены все условия:

- [ ] существуют все семь production assemblies;
- [ ] их имена совпадают с ADR;
- [ ] `.asmdef` references совпадают с матрицей;
- [ ] package dependencies совпадают с матрицей;
- [ ] dependency graph не содержит циклов;
- [ ] Domain, Rules, Content и Application имеют `noEngineReferences`;
- [ ] Core solution собирается без Unity Editor;
- [ ] Unity project компилируется;
- [ ] Persistence не ссылается на Networking;
- [ ] Networking не ссылается на Persistence;
- [ ] Unity-specific namespace отсутствует в Core;
- [ ] architecture check запускается одной документированной командой;
- [ ] нарушение check блокирует CI;
- [ ] pull request содержит evidence выполненных проверок.

---

# 20. Трассировка

| Источник | Связь |
|---|---|
| Technical Development Baseline §11–14 | Уточняет архитектурный стиль, модули, структуру и enforcement |
| Roadmap Stage 1 §9.2–9.6 | Реализует обязательные границы `SLICE-00` |
| Test Strategy | Формирует architecture/build checks и evidence |
| Domain Model | Защищает доменную модель от infrastructure/UI dependencies |
| Persistence Contract | Оставляет Persistence реализацией Application ports |
| Networking Contract | Оставляет Networking host-authoritative adapter вокруг Application |
| Content Block System | Запрещает Content execution обходить Application и Domain invariants |

---

# 21. Нормативное действие

С момента принятия этого ADR:

- формулировка `limited contracts` в предварительной матрице Technical Development Baseline не применяется;
- действует точная матрица раздела 5 этого ADR;
- новый код и задачи Codex обязаны ссылаться на ADR-001 при создании или изменении assemblies;
- конфликтующий compile-time reference является архитектурным дефектом и блокирует merge.

---

**Конец документа**
