# ADR-006 — Test Project Structure and Dual Unity/.NET Compilation

**Документ:** `docs/adr/ADR-006_Test_Project_Structure_and_Dual_Unity_DotNet_Compilation_v1.0.md`  
**ADR:** ADR-006  
**Версия:** 1.0  
**Дата:** 27 июля 2026 года  
**Статус:** Accepted  
**Область:** test solution, Unity Test Framework, pure .NET test host, single-source dual compilation, `.asmdef`/`.csproj` parity, test assemblies, TestKit, contract vectors, architecture checks, test categories, CI suites, evidence и `SLICE-00` test scaffold  
**Связанные этапы:** Roadmap Stage 1, `SLICE-00`, Milestone `M1`  
**Базовые документы:** `TECHNICAL_DEVELOPMENT_BASELINE_Odyssey_VTT_v0.1.md`, `16_Test_Strategy_Odyssey_VTT_v0.1.md`, `ADR-001_Module_Boundaries_and_Dependency_Direction_v1.0.md`, `ADR-002_Command_and_Domain_Event_Model_v1.0.md`, `ADR-003_Serialization_Strategy_v1.0.md`, `ADR-004_Result_and_Error_Model_v1.0.md`, `ADR-005_Dependency_Composition_v1.0.md`

---

# 1. Решение

Odyssey VTT использует **двухконтурную тестовую архитектуру с одним production source tree**:

1. Чистые production-модули компилируются:
   - Unity через pinned `.asmdef` и package dependencies;
   - pure .NET solution через explicit SDK-style `.csproj` includes тех же физических `.cs`-файлов.
2. Production source не копируется, не синхронизируется генератором и не поддерживается в двух вариантах.
3. Symlink/junction не является частью обязательного build path; репозиторий должен работать после обычного Git checkout на Windows.
4. Unity-generated `.csproj` не является нормативным build contract и не коммитится как источник pure .NET solution.
5. Production bridge projects для Core target `netstandard2.1`.
6. Pure .NET test host использует pinned .NET 10 LTS SDK через корневой `global.json`; test projects target `net10.0`.
7. Версии .NET SDK, NUnit, test adapter, coverage collector и Unity Test Framework всегда pinned; floating ranges запрещены.
8. Pure .NET test framework — NUnit. Unity tests используют NUnit API, предоставляемый pinned Unity Test Framework package.
9. Test source между .NET runner и Unity runner **не переиспользуется целиком**. Общими могут быть только framework-neutral fixtures, builders, deterministic vectors и test contracts.
10. Один и тот же critical Core behavior проверяется преимущественно в pure .NET. Unity test не дублирует формульную/доменную проверку без причины.
11. Unity EditMode используется для assembly/package/editor integration, serialization compatibility и Unity adapters, не требующих реального frame lifecycle.
12. Unity PlayMode используется для bootstrap, scene lifecycle, UI Toolkit runtime, input, asset loading и frame-dependent поведения.
13. Windows player smoke является отдельной build/runtime проверкой и не подменяется PlayMode test.
14. Production assembly никогда не ссылается на test assembly, TestKit или test framework.
15. `InternalsVisibleTo` допускается только точечно к зарегистрированной test assembly того же module; broad friend access запрещён.
16. Architecture tests автоматически проверяют одновременно `.asmdef`, `package.json`, SDK projects и фактический source inventory.
17. Любой production `.cs`-файл, предназначенный для dual compilation, обязан входить ровно в один соответствующий Unity runtime assembly и ровно в один соответствующий .NET bridge project.
18. Расхождение compile set, public API или результата shared compatibility vector между Unity и .NET считается defect и блокирует merge.
19. Conditional compilation в Core запрещена по умолчанию. Исключения разрешены только в зарегистрированном compatibility shim с тестом обоих контуров.
20. Test execution не зависит от локального порядка, реального времени, глобального random, текущей директории, пользовательского профиля или уже запущенного Unity Editor.
21. Тесты параллельны только при доказанной изоляции; ресурсоёмкие/Unity/persistence tests получают явную non-parallel policy.
22. Flaky test не может быть silently retried до зелёного состояния. Повтор допускается только как диагностическое свидетельство согласно Test Strategy.
23. `SLICE-00` создаёт минимальный рабочий набор: Core bridge projects, Unit, Domain, Contracts, Architecture, Unity EditMode smoke, Unity PlayMode bootstrap smoke, scripts и CI gates.
24. Конкретное добавление Persistence, Networking, End-to-End, Performance и Security test projects выполняется при достижении соответствующего vertical slice, а не заранее пустыми placeholders.
25. Изменение target frameworks, test framework, source-sharing strategy или границы Unity/.NET runner требует нового ADR.

Этот ADR является нормативным authority по test project structure и dual compilation. Он уточняет раздел 21 Technical Development Baseline и технически реализует Test Strategy без изменения продуктового поведения.

---

# 2. Контекст и проблема

Odyssey VTT разрабатывается в Unity, но его критические правила, команды, события, permissions, serialization contracts и application orchestration должны проверяться быстро и детерминированно без запуска Unity Editor.

Одновременно чистый .NET test solution создаёт риск ложной уверенности, если:

- .NET tests компилируют копию source, отличающуюся от Unity package;
- Unity-generated project files используются как нестабильный источник;
- Unity и .NET компиляторы видят разные файлы или defines;
- тест проходит на .NET, но соответствующий source не включён в `.asmdef`;
- Core случайно вызывает API, отсутствующее в Unity profile;
- один runner использует другой JSON contract или fixture;
- тесты общей логики существуют только в PlayMode и становятся медленными;
- Unity test дублирует сотни Core tests и расходится с ними;
- test utilities попадают в player build;
- package reference graph и `.csproj` graph постепенно перестают совпадать;
- Codex создаёт новый test project или framework для каждой подсистемы.

Нужен один обязательный способ:

- физически хранить production code;
- компилировать его двумя toolchains;
- размещать разные классы тестов;
- делить test fixtures без копирования поведения;
- запускать быстрый PR gate;
- доказывать parity Unity и .NET;
- собирать одинаковые диагностические артефакты.

---

# 3. Движущие факторы

Решение оптимизировано под:

1. Быстрый feedback для Codex и разработчиков.
2. Проверку Domain/Rules/Application без Unity Editor.
3. Реальную Unity compile validation того же source.
4. Windows-first репозиторий без symlink-зависимости.
5. Unity 6.3 LTS, `.NET Standard 2.1` и IL2CPP constraints.
6. Минимум сторонних зависимостей.
7. Ациклический module graph ADR-001.
8. Детерминированные command/event и serialization contracts ADR-002/003.
9. Единый Result/Error contract ADR-004.
10. Отдельную test composition ADR-005.
11. Прослеживаемость TestCaseId и evidence из Test Strategy.
12. Постепенное добавление дорогих test layers по vertical slices.
13. Защиту публичного репозитория от случайного включения закрытых данных.
14. Возможность воспроизвести CI локально одной documented command.

---

# 4. Термины

## 4.1 Production source tree

Единственные физические `.cs`-файлы production module, расположенные в embedded Unity package или `Assets/Odyssey/Client` для Unity Client.

## 4.2 Unity runtime assembly

Assembly, создаваемая Unity из `.asmdef`, предназначенная для Editor/Player compile.

## 4.3 .NET bridge project

SDK-style `.csproj`, который явно включает production source существующего Unity package без копирования файлов и собирает его против утверждённого target framework.

Bridge project не является отдельной реализацией module.

## 4.4 Test host

Runtime и tooling, запускающие тесты:

- .NET test host;
- Unity EditMode runner;
- Unity PlayMode runner;
- Windows player smoke harness.

## 4.5 TestKit

Framework-neutral test-only source: builders, fakes, deterministic fixtures, vector readers и custom metadata. TestKit не является production module.

## 4.6 Compatibility vector

Версионированный input/expected-output набор, который независимо исполняется в .NET и Unity runners для доказательства одинакового поведения контракта.

## 4.7 Source inventory

Машинно проверяемый список production source, включённого в Unity assembly и соответствующий .NET bridge project.

## 4.8 TestCaseId

Стабильный идентификатор обязательного сценария, связывающий requirement, task, test, suite и evidence.

---

# 5. Обязательная структура репозитория

Базовая структура тестового контура:

```text
/
├─ Packages/
│  ├─ com.odyssey.domain/
│  │  ├─ Runtime/
│  │  ├─ Tests/                   # только если module требует Unity-specific test
│  │  ├─ Odyssey.Domain.asmdef
│  │  └─ package.json
│  ├─ com.odyssey.rules/
│  ├─ com.odyssey.content/
│  ├─ com.odyssey.application/
│  ├─ com.odyssey.persistence/
│  └─ com.odyssey.networking/
├─ Assets/Odyssey/Client/
│  ├─ Runtime/
│  ├─ Editor/
│  └─ Tests/
│     ├─ EditMode/
│     └─ PlayMode/
├─ DotNet/
│  ├─ Odyssey.Core.sln
│  ├─ Projects/
│  │  ├─ Odyssey.Domain.csproj
│  │  ├─ Odyssey.Rules.csproj
│  │  ├─ Odyssey.Content.csproj
│  │  └─ Odyssey.Application.csproj
│  └─ Tests/
│     ├─ Odyssey.Tests.Unit/
│     ├─ Odyssey.Tests.Domain/
│     ├─ Odyssey.Tests.Contracts/
│     └─ Odyssey.Tests.Architecture/
├─ Tests/
│  ├─ TestKit/
│  │  ├─ Builders/
│  │  ├─ Fakes/
│  │  ├─ Determinism/
│  │  └─ Metadata/
│  ├─ Contracts/
│  │  ├─ Vectors/
│  │  └─ Schemas/
│  ├─ Fixtures/
│  └─ TestEvidence/
├─ scripts/
│  ├─ restore.ps1
│  ├─ test-fast.ps1
│  ├─ test-all.ps1
│  ├─ test-unity.ps1
│  └─ verify-test-structure.ps1
├─ global.json
└─ Directory.Build.props
```

Точные дополнительные проекты появляются только с соответствующим scope.

---

# 6. Single-source dual compilation

## 6.1 Единственный source

Для `Domain`, `Rules`, `Content` и `Application` production source хранится только под:

```text
Packages/com.odyssey.<module>/Runtime/**/*.cs
```

.NET bridge project включает эти файлы через explicit relative glob/include.

Запрещено:

- копировать source в `DotNet/Projects`;
- генерировать вторую production copy;
- вручную синхронизировать два каталога;
- использовать post-build copy как нормальный workflow;
- хранить альтернативную `.NET` реализацию того же module;
- включать generated Unity `.csproj` в `Odyssey.Core.sln` как authority.

## 6.2 Windows portability

Build path не требует:

- administrator privileges;
- Developer Mode;
- symbolic links;
- junctions;
- нестандартной file system;
- ручной настройки IDE.

Обычный Git checkout должен быть достаточен после выполнения `scripts/bootstrap.ps1`.

## 6.3 Production target framework

Bridge projects `Domain`, `Rules`, `Content` и `Application` target:

```text
netstandard2.1
```

Это intentionally restrictive contract: Core не использует API только потому, что оно доступно test host на более новом .NET.

## 6.4 Test host framework

Pure .NET tests target:

```text
net10.0
```

Корневой `global.json` pin-ит exact feature band SDK. Roll-forward policy задаётся явно и не допускает silent переход на новый major SDK.

Переход test host на другой major .NET не меняет production target автоматически и требует отдельной validation task; изменение принципа требует ADR.

## 6.5 Language version

`LangVersion` не устанавливается в `latest` или `preview`.

Он pin-ится на значение, подтверждённое pinned Unity compiler и pure .NET compile spike. Код, собирающийся только новым Roslyn, но не Unity, запрещён.

## 6.6 Source inventory parity

`verify-test-structure` обязан доказать:

1. Каждый production file Core входит в соответствующий `.asmdef` compile set.
2. Каждый dual-compiled production file входит в соответствующий `.csproj`.
3. Один file не включён в два production bridge projects.
4. `Editor`, `Tests`, `obj`, `bin` и generated folders исключены.
5. `.csproj` не включает source соседнего module в обход ProjectReference.
6. Package dependencies, `.asmdef` references и `.csproj` ProjectReferences соответствуют ADR-001.
7. Нет orphan production source, который не компилируется ни одним обязательным контуром.

---

# 7. Conditional compilation policy

## 7.1 Default prohibition

В dual-compiled Core запрещены platform forks вида:

```text
#if UNITY_EDITOR
#if UNITY_STANDALONE
#if NET10_0
#if !UNITY
```

если они меняют business behavior или public contract.

## 7.2 Допустимый compatibility shim

Исключение возможно только если одновременно:

- файл расположен в явно зарегистрированном `Compatibility/`;
- проблема документирована;
- public behavior одинаков;
- существуют тесты обоих branches;
- architecture allowlist содержит точный file и reason;
- нет доступного framework-neutral решения.

Compatibility shim не может содержать rules, permission decisions, event creation или persistence policy.

## 7.3 Unity defines

Unity defines разрешены в Unity Client и provider-specific Unity adapter assemblies, которые не входят в pure .NET Core bridge.

---

# 8. Test frameworks и package policy

## 8.1 Pure .NET

Pure .NET projects используют:

- NUnit как test framework;
- официальный .NET test SDK;
- NUnit test adapter;
- pinned coverage collector, когда coverage gate будет активирован.

Exact package versions фиксируются central package/version configuration и lock/restore evidence. Floating versions запрещены.

## 8.2 Unity

Unity tests используют pinned `com.unity.test-framework` из `Packages/manifest.json` и `packages-lock.json`.

Нельзя вручную добавлять другую NUnit runtime в Unity project, если она конфликтует с Unity Test Framework.

## 8.3 Не шарить test implementation

Даже при общем NUnit API один test method не обязан компилироваться двумя runners.

Общими разрешено делать:

- fixture data;
- builders;
- fakes без framework dependency;
- compatibility vectors;
- expected snapshots;
- TestCaseId metadata contract;
- deterministic scenario descriptions.

Не следует шарить:

- runner-specific attributes;
- Unity coroutine tests;
- filesystem paths test host;
- assertion wrappers, скрывающие различия runners;
- setup/teardown lifecycle, зависящий от runner.

## 8.4 Mocking

На `SLICE-00` mocking framework не добавляется.

Используются:

- hand-written fakes;
- spies с явным captured state;
- deterministic clocks/RNG;
- in-memory ports;
- builders.

Добавление mocking library подчиняется third-party dependency policy и требует обоснования в задаче.

---

# 9. Test project taxonomy

## 9.1 `Odyssey.Tests.Unit`

Назначение:

- value objects;
- pure functions;
- parsing/validation helpers;
- Result/Error mappings;
- маленькие isolated classes.

Ограничения:

- без Unity;
- без SQLite;
- без network;
- без реального clock/random;
- по умолчанию `Fast`.

## 9.2 `Odyssey.Tests.Domain`

Назначение:

- DomainScenario;
- Rules Engine;
- command decision behavior;
- invariant validation;
- deterministic event batches;
- compensation semantics.

Ограничения:

- in-memory composition;
- без filesystem/network/Unity;
- scenario name отражает состояние и ожидаемый outcome.

## 9.3 `Odyssey.Tests.Contracts`

Назначение:

- command/event DTO;
- canonical JSON;
- fingerprint vectors;
- ErrorCode/SafeReasonCode;
- schema versions;
- upcasters;
- redaction DTO;
- public contract snapshots.

Контрактный test обязан явно указывать, является ли изменение совместимым или требует version bump.

## 9.4 `Odyssey.Tests.Architecture`

Назначение:

- ADR-001 dependency graph;
- package/asmdef/csproj parity;
- forbidden namespace/API checks;
- absence of Unity references in Core;
- absence of production → test references;
- source inventory parity;
- service locator prohibition из ADR-005;
- test metadata/catalog validation.

Architecture test не зависит от Unity Editor для анализа repository files.

## 9.5 Future projects

Создаются по необходимости:

```text
Odyssey.Tests.Persistence
Odyssey.Tests.Networking
Odyssey.Tests.EndToEnd
Odyssey.Tests.Performance
Odyssey.Tests.Security
```

Пустой project «на будущее» не создаётся только ради структуры.

---

# 10. Unity test assemblies

## 10.1 EditMode

Обязательная assembly:

```text
Odyssey.Tests.Unity.EditMode
```

Назначение:

- Unity package integration;
- `.asmdef` resolution;
- Unity-side serialization vectors;
- source-generated serializer context availability;
- Unity adapters без frame lifecycle;
- editor import/configuration checks;
- composition graph smoke, если scene не требуется.

EditMode не используется как замена pure .NET Domain tests.

## 10.2 PlayMode

Обязательная assembly:

```text
Odyssey.Tests.Unity.PlayMode
```

Назначение:

- bootstrap scene;
- startup/shutdown lifecycle;
- duplicate runtime host protection;
- scene load/unload;
- UI Toolkit runtime binding;
- input integration;
- frame/coroutine behavior;
- asset lifetime;
- cancellation при destruction/unload.

## 10.3 Assembly rules

Unity test assemblies:

- имеют explicit references;
- помечены как test assemblies;
- не входят в player build;
- не auto-reference production indiscriminately;
- не становятся dependency production assembly;
- разделяют EditMode и PlayMode;
- не используют закрытую продуктовую документацию как runtime asset.

## 10.4 Package-owned Unity tests

Module-specific Unity test допускается внутри embedded package, только если тест проверяет Unity/package integration самого module.

Чистый Domain/Rules test не переносится в package Unity Tests без причины.

---

# 11. TestKit и shared fixtures

## 11.1 Test-only boundary

TestKit компилируется только test projects/assemblies и никогда не входит в player.

Рекомендуемые test-only assemblies/projects:

```text
Odyssey.Testing.Contracts
Odyssey.Testing.Builders
Odyssey.Testing.Fakes
```

На `SLICE-00` допускается один небольшой `Odyssey.Testing` project, если его ответственность остаётся test-only и ациклической.

## 11.2 Builders

Builder:

- имеет безопасные явные defaults;
- позволяет переопределить значимые параметры;
- не скрывает важное state transition;
- не создаёт случайные данные без переданного seed;
- не обращается к real clock/filesystem/network.

## 11.3 Fakes

Fake:

- реализует один утверждённый port;
- хранит наблюдаемое состояние явно;
- позволяет fault injection;
- не повторяет production business logic;
- не является universal fake database/service provider.

## 11.4 Shared fixtures

Shared fixture не может содержать пользовательскую кампанию, закрытую документацию или лицензированный asset.

Все публичные repository fixtures:

- синтетические;
- минимальные;
- лицензированно безопасные;
- детерминированные;
- пригодные для CI logs/artifacts.

---

# 12. Compatibility vectors и parity

## 12.1 Обязательные vector families

На соответствующих этапах создаются:

```text
command-fingerprint-v1
canonical-json-v1
event-payload-hash-v1
result-error-mapping-v1
schema-upcast-v1
permission-redaction-v1
deterministic-rules-v1
```

## 12.2 Формат

Vector имеет:

- stable vector id;
- contract type/version;
- input;
- expected normalized output;
- expected hash/error;
- source requirement/test case;
- комментарий только для человека, не влияющий на computation.

## 12.3 Dual execution

Критический vector family исполняется:

- pure .NET contract test;
- Unity EditMode compatibility test;
- при необходимости IL2CPP/player smoke.

Результаты должны быть byte-for-byte/semantically identical согласно контракту ADR-003.

## 12.4 Golden update

Codex не обновляет expected vectors/snapshots автоматически только для получения зелёного теста.

Изменение golden data требует:

- объяснения причины;
- ссылки на requirement/ADR/version bump;
- human review diff;
- успешной проверки старой и новой совместимости, где она обязательна.

---

# 13. Test metadata и прослеживаемость

## 13.1 TestCaseId

Обязательный test получает stable `TestCaseId` через test-only metadata attribute или эквивалентный машиночитаемый catalog entry.

Формат:

```text
TC-<AREA>-<NNN>
```

Примеры:

```text
TC-ARCH-001
TC-CMD-004
TC-SER-012
TC-COMP-006
```

## 13.2 Связь

Минимальная цепочка:

```text
RequirementId / ADR section
→ TaskId
→ TestCaseId
→ Test project/assembly
→ CI suite
→ Evidence artifact
```

## 13.3 Catalog validation

CI проверяет:

- уникальность TestCaseId;
- отсутствие duplicate ownership;
- наличие test для обязательного catalog entry;
- отсутствие ссылки на несуществующий requirement/task;
- соответствие TestCaseId naming policy;
- отсутствие закрытого текста requirement в публичном test catalog.

---

# 14. Категории и suites

Утверждённые категории:

```text
Fast
Standard
Slow
RequiresUnity
RequiresPlayMode
RequiresPlayer
RequiresFileSystem
RequiresNetwork
Persistence
Networking
Performance
Security
Recovery
FlakyQuarantined
Manual
```

Правила:

1. Категория не заменяет уровень теста.
2. `Fast` не использует Unity, real network и uncontrolled filesystem.
3. `RequiresUnity` выполняется только Unity runner.
4. `FlakyQuarantined` не входит в required-green suite и создаёт tracked defect.
5. `Manual` не маскирует автоматизируемый release gate.
6. Test без категории получает default suite по project policy, а не silently пропускается.

---

# 15. Parallelism и изоляция

## 15.1 Pure .NET

Pure tests могут выполняться параллельно, если:

- нет shared mutable static state;
- нет общего file path/database;
- clock/RNG/IDs injected;
- environment не модифицируется глобально;
- culture/timezone задаются test-local или suite fixture с serial policy.

## 15.2 Non-parallel

Явно serial/non-parallel выполняются:

- tests, меняющие process-wide culture/environment;
- SQLite tests с общим file/database;
- Unity tests;
- player smoke;
- tests с exclusive external resource.

## 15.3 Temporary resources

Каждый test получает уникальный temporary directory/database name из deterministic test identity, а cleanup выполняется даже при failure.

Неочищенный temp artifact сохраняется только как объявленное diagnostic evidence.

---

# 16. Determinism requirements

Тест не использует напрямую:

- `DateTime.Now`/`UtcNow`;
- `Guid.NewGuid()` для значимого expected state;
- process-global random;
- `Task.Delay` как доказательство business timeout;
- arbitrary sleep;
- порядок файловой системы;
- текущую locale/timezone;
- текущую working directory;
- локальный user profile.

Используются:

- injected clock;
- seeded RNG;
- deterministic ID source;
- virtual scheduler/time;
- explicit culture;
- sorted canonical collections;
- bounded async wait с diagnostics только на integration boundary.

ADR-008 уточнит production Clock/RNG contracts; до него test scaffold использует минимальные временные interfaces, не расширяющие продуктовую модель.

---

# 17. CI execution model

## 17.1 Fast Core gate

`scripts/test-fast.ps1` выполняет минимум:

1. repository/test structure validation;
2. `dotnet restore` locked/pinned dependencies;
3. Core bridge compile;
4. Unit tests;
5. Domain tests;
6. Contract tests;
7. Architecture tests;
8. test result export;
9. failure evidence collection.

Fast gate обязан запускаться локально без Unity Editor.

## 17.2 Unity gate

`scripts/test-unity.ps1` выполняет:

1. Unity batchmode compile;
2. EditMode tests;
3. PlayMode smoke tests;
4. Unity test result export;
5. Editor log retention;
6. cleanup/exit-code verification.

Отсутствие Unity license/activation не превращается в success.

## 17.3 Pull request required checks

Для `SLICE-00` merge блокируют:

```text
VerifyRepository
VerifyTestStructure
CoreCompile
CoreFastTests
UnityCompile
UnityEditModeSmoke
UnityPlayModeBootstrapSmoke
```

Windows player build smoke становится required не позднее PR-005 из Technical Development Baseline.

## 17.4 Full suite

`scripts/test-all.ps1` агрегирует доступные Standard/Unity/infrastructure suites текущего milestone. Отсутствующий будущий project не считается success-симуляцией и не создаётся fake placeholder result.

## 17.5 No hidden local path

CI и local scripts вызывают одни и те же underlying commands. IDE-only run configuration не является release evidence.

---

# 18. Test result и evidence

Каждый runner сохраняет машиночитаемый результат и human-readable summary.

При failure сохраняются применимые:

- test result XML/TRX;
- stdout/stderr;
- Unity Editor log;
- build log;
- TestCaseId;
- seed;
- virtual timestamp;
- command/event envelopes;
- normalized contract vector diff;
- state snapshot;
- screenshot для Unity/UI;
- build identity;
- Unity version;
- .NET SDK version;
- package lock hash.

Артефакты:

- не содержат секреты;
- не содержат скрытые GM payload без redaction;
- не содержат локальную полную документацию;
- имеют ограниченный retention;
- связываются с commit SHA и CI run.

---

# 19. Flaky и retry policy

1. Первый failure всегда остаётся видимым.
2. CI может выполнить один диагностический rerun только для утверждённой категории.
3. Rerun success не превращает check в clean success автоматически.
4. Flaky test получает defect, owner и deadline.
5. Quarantine требует явного reason и не может скрывать release-critical scenario.
6. Увеличение timeout без анализа не считается исправлением.
7. Random seed и scheduling evidence сохраняются.
8. Codex не удаляет/игнорирует test из-за нестабильности без отдельного scope задачи.

---

# 20. Coverage policy

На `SLICE-00` coverage собирается для Core как diagnostic metric, но общий процент не является единственным quality gate.

Обязательны:

- coverage report создаётся воспроизводимо;
- generated code исключается обоснованным rule;
- test code не учитывается как production coverage;
- падение coverage на изменённом critical module отображается в PR;
- критический requirement не считается покрытым только из-за line coverage;
- branch/contract/invariant tests важнее формального процента.

Числовые thresholds утверждаются после появления устойчивого Core baseline и отдельной task, без изменения этого ADR, если не меняется принцип.

---

# 21. `InternalsVisibleTo` и test seams

`InternalsVisibleTo` разрешён только когда:

- internal behavior нельзя разумно проверить через public contract;
- friend assembly имеет exact зарегистрированное имя;
- доступ нужен test project того же module;
- открытие internal не обходится для integration между production modules;
- reason задокументирован в project file или architecture allowlist.

Запрещено:

- делать всё public ради tests;
- broad wildcard friend access;
- давать Application test assembly доступ к Persistence internals без ownership;
- использовать test seam как production service locator;
- добавлять conditional public API только в test build.

---

# 22. Architecture enforcement

`Odyssey.Tests.Architecture` и `verify-test-structure` проверяют минимум:

1. Exact production assembly names ADR-001.
2. Разрешённый module dependency graph.
3. `.asmdef` explicit references.
4. `package.json` dependency graph.
5. `.csproj` ProjectReferences.
6. Source inventory parity.
7. Core source без `UnityEngine`/Unity package references.
8. Production без test framework/TestKit references.
9. Test assemblies не входят в player build.
10. Editor source не входит runtime assembly.
11. Нет cyclic project/assembly references.
12. Нет `latest`/floating package versions.
13. Нет generated Unity `.csproj` как solution dependency.
14. Нет forbidden conditional compilation в Core.
15. Нет duplicate TestCaseId.
16. Нет пустых future test projects, создающих false readiness.
17. Нет запрещённых service locator patterns из ADR-005.
18. Pure .NET Core solution компилируется.

Нарушение блокирует merge с указанием file, rule и remediation.

---

# 23. `SLICE-00` обязательный scaffold

До закрытия M1 должны существовать:

## 23.1 Toolchain

- pinned Unity 6.3 LTS patch;
- pinned `com.unity.test-framework`;
- pinned .NET 10 LTS SDK через `global.json`;
- pinned NUnit/test SDK/adapter packages;
- `Directory.Build.props`;
- deterministic/locked restore policy.

## 23.2 Production bridge projects

```text
Odyssey.Domain
Odyssey.Rules
Odyssey.Content
Odyssey.Application
```

Они компилируют те же physical files, что embedded Unity packages.

## 23.3 Pure .NET test projects

```text
Odyssey.Tests.Unit
Odyssey.Tests.Domain
Odyssey.Tests.Contracts
Odyssey.Tests.Architecture
```

## 23.4 Unity tests

```text
Odyssey.Tests.Unity.EditMode
Odyssey.Tests.Unity.PlayMode
```

Минимальные smoke scenarios:

- EditMode видит и загружает обязательные assemblies;
- EditMode исполняет один shared serialization/contract vector;
- PlayMode запускает bootstrap scene;
- создаётся ровно один AppRuntime;
- runtime достигает Ready;
- shutdown выполняется без leaked exception;
- повторная загрузка не создаёт второй runtime host.

## 23.5 Scripts

```text
scripts/restore.ps1
scripts/test-fast.ps1
scripts/test-unity.ps1
scripts/test-all.ps1
scripts/verify-test-structure.ps1
```

## 23.6 CI

PR обязан доказать:

- Core compile;
- Fast tests;
- architecture parity;
- Unity compile;
- EditMode smoke;
- PlayMode bootstrap smoke.

---

# 24. Обязательные тестовые сценарии ADR-006

Минимальный набор:

1. `TC-TEST-001` — каждый Core production file включён в соответствующий `.asmdef` и `.csproj`.
2. `TC-TEST-002` — duplicate production source include отклоняется.
3. `TC-TEST-003` — orphan Core source отклоняется.
4. `TC-TEST-004` — Domain bridge target равен `netstandard2.1`.
5. `TC-TEST-005` — test host использует pinned SDK из `global.json`.
6. `TC-TEST-006` — floating NuGet/package version отклоняется.
7. `TC-TEST-007` — Unity-generated `.csproj` не входит в normative solution.
8. `TC-TEST-008` — production project не ссылается на TestKit/test framework.
9. `TC-TEST-009` — test assembly не входит в player build.
10. `TC-TEST-010` — forbidden Unity namespace в Core отклоняется.
11. `TC-TEST-011` — forbidden platform conditional в Core отклоняется.
12. `TC-TEST-012` — approved compatibility shim имеет tests обоих branches.
13. `TC-TEST-013` — duplicate TestCaseId отклоняется.
14. `TC-TEST-014` — .NET и Unity canonical JSON vector совпадают.
15. `TC-TEST-015` — .NET и Unity command fingerprint vector совпадают.
16. `TC-TEST-016` — deterministic DomainScenario повторяется с тем же результатом.
17. `TC-TEST-017` — test не использует real clock/global RNG в запрещённой зоне.
18. `TC-TEST-018` — bootstrap PlayMode создаёт один AppRuntime.
19. `TC-TEST-019` — shutdown PlayMode освобождает runtime и subscriptions.
20. `TC-TEST-020` — Unity batchmode failure возвращает non-zero exit code.
21. `TC-TEST-021` — missing Unity activation не даёт false-green result.
22. `TC-TEST-022` — Fast script запускается из clean checkout после bootstrap.
23. `TC-TEST-023` — CI artifacts не содержат secret/private documentation marker.
24. `TC-TEST-024` — rerun не скрывает первоначальный failure.
25. `TC-TEST-025` — empty future test project/placeholder result отклоняется.
26. `TC-TEST-026` — package/asmdef/csproj dependency graphs совпадают с ADR-001.
27. `TC-TEST-027` — test temporary resources уникальны и очищаются.
28. `TC-TEST-028` — public contract golden update требует явного diff/evidence.

TestCaseId могут быть расширены, но их семантика не переиспользуется.

---

# 25. Запрещённые реализации

Запрещено:

- две копии production source;
- symlink-only build;
- reliance на Unity-generated solution;
- test framework package с floating version;
- `LangVersion=latest`/`preview` без отдельного решения;
- Core API, доступное только test host;
- domain tests только в PlayMode;
- test source, попадающий в player;
- production reference на `NUnit`, TestKit или test assembly;
- blanket `InternalsVisibleTo`;
- shared mutable global fixture;
- arbitrary sleeps;
- real random/clock в deterministic tests;
- silently ignored test;
- auto-accept snapshots;
- automatic retry-until-green;
- пустой green placeholder вместо отсутствующего suite;
- test fixture из закрытой пользовательской кампании;
- копирование внутренней продуктовой документации в CI evidence;
- создание нового test framework без ADR/dependency review.

---

# 26. Последствия

## 26.1 Положительные

- Core tests запускаются быстро без Unity.
- Unity всё равно компилирует тот же production source.
- Нет риска расхождения копий.
- Архитектурные границы проверяются машиночитаемо.
- Codex получает однозначное место для каждого test.
- Contract vectors доказывают parity runners.
- Unity tests остаются сфокусированными на Unity-specific behavior.
- CI можно воспроизвести локально.
- TestKit не загрязняет production.
- Публичный repository не зависит от закрытых данных.

## 26.2 Отрицательные

- Нужно поддерживать `.asmdef`, `package.json` и `.csproj` синхронно.
- Появляется собственный verification script.
- Два runners увеличивают CI setup.
- Unity tests требуют лицензии/activation infrastructure.
- Некоторые compatibility проблемы проявятся только в Unity/IL2CPP.
- Framework-neutral fixtures требуют дисциплины.

## 26.3 Принятый компромисс

Дополнительная сложность build/test scaffold принимается, потому что она значительно дешевле постоянного запуска всей критической логики через Unity и риска расхождения двух реализаций Core.

---

# 27. Отклонённые альтернативы

## 27.1 Только Unity Test Framework

Отклонено из-за медленного feedback, зависимости от Editor/license и плохой изоляции Core.

## 27.2 Только pure .NET tests

Отклонено: не доказывает Unity assembly/package, scene, UI, input, serialization/AOT и player integration.

## 27.3 Копировать source в DotNet

Отклонено из-за guaranteed drift и двойной review surface.

## 27.4 Использовать Unity-generated `.csproj`

Отклонено: generated files нестабильны и не являются контролируемым cross-toolchain contract.

## 27.5 Symlink shared source

Отклонено как обязательный path из-за Windows permissions/developer mode и проблем checkout/CI. Symlink может использоваться локальным экспериментом, но не repository contract.

## 27.6 Один test source для обоих runners

Отклонено как default: различия lifecycle/attributes/packages создают хрупкую абстракцию. Общими остаются vectors и fixtures, а не runner behavior.

## 27.7 xUnit/MSTest для pure .NET

Не выбраны, чтобы сократить количество assertion/test mental models рядом с NUnit-based Unity Test Framework. Решение не означает совместное использование runner package.

## 27.8 Multi-target production projects `netstandard2.1;net10.0`

Не выбрано как default: production contract должен проверяться против самого ограничительного утверждённого profile. Test host может ссылаться на `netstandard2.1` assembly.

---

# 28. Правила для Codex

При создании или изменении test Codex обязан:

1. Определить правильный runner и project до изменения.
2. Не создавать второй production source.
3. Не менять target framework/package version без scope задачи.
4. Не добавлять Unity dependency в Core ради test.
5. Не добавлять mocking/test library самостоятельно.
6. Использовать TestCaseId для обязательного сценария.
7. Добавить/обновить traceability entry.
8. Запустить `test-fast` для Core changes.
9. Запустить применимый Unity suite для Unity/package changes.
10. Сохранить failure evidence и не скрывать первый failure retry-ем.
11. Не обновлять golden/snapshot без документированной причины.
12. Не ослаблять architecture rule ради прохождения задачи.
13. Не помещать закрытый requirement text в public tests/logs.
14. В PR перечислить runners, команды и результаты.

---

# 29. Definition of Done ADR-006

ADR считается внедрённым, когда:

- [ ] существует pinned `global.json` с .NET 10 LTS SDK;
- [ ] production bridge projects target `netstandard2.1`;
- [ ] Core source физически не дублируется;
- [ ] `.asmdef`, package и `.csproj` graphs соответствуют ADR-001;
- [ ] существуют Unit, Domain, Contracts и Architecture test projects;
- [ ] существуют Unity EditMode и PlayMode test assemblies;
- [ ] `test-fast` работает без Unity;
- [ ] `test-unity` работает batchmode;
- [ ] source inventory parity проверяется автоматически;
- [ ] shared compatibility vectors выполняются минимум .NET и Unity EditMode;
- [ ] test metadata уникальна и валидируется;
- [ ] production build не содержит test assemblies/TestKit;
- [ ] PlayMode bootstrap smoke проходит;
- [ ] required PR checks настроены;
- [ ] документация запуска доступна из публичного репозитория;
- [ ] clean checkout может воспроизвести test path без ручной IDE-конфигурации.

---

# 30. Влияние на связанные документы

- `TECHNICAL_DEVELOPMENT_BASELINE` сохраняет версию: ADR уточняет уже зарегистрированный вопрос, не меняя утверждённый продуктовый scope.
- `16_Test_Strategy` сохраняет версию: его уровни и quality gates не меняются; ADR выбирает техническую реализацию оставленных open implementation details.
- `ADR-001` сохраняет силу: ADR-006 реализует его module/assembly graph в test solution.
- `ADR-003` используется для shared serialization vectors.
- `ADR-005` используется для separate test composition и Unity bootstrap smoke.
- Active Documentation Baseline повышается и регистрирует ADR-006.

---

# 31. Следующие решения

После принятия ADR-006 по плану оформляются:

1. ADR-007 — Versioning and Build Identity.
2. ADR-008 — Deterministic Clock and RNG.
3. ADR-009 — Unity Build Profiles, Graphics API and Scripting Backend.
4. ADR-010 — Logging and Diagnostics Baseline.

Exact SDK feature band, NuGet package versions и Unity Test Framework package version фиксируются в PR-002/lock files после clean scaffold validation без нового ADR, если не меняются принципы этого документа.

---

**Конец документа**
