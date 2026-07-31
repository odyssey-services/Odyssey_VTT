# Odyssey VTT — Technical Development Baseline

**Документ:** `TECHNICAL_DEVELOPMENT_BASELINE_Odyssey_VTT_v0.2.md`  
**Версия:** 0.2  
**Дата:** 1 августа 2026 года  
**Статус:** Approved baseline for M0 / M1  
**Область:** техническая исходная точка нового репозитория, движок, платформы, архитектурные границы, структура кода, тестирование, CI, лицензирование и правила разработки через Codex  
**Связанные этапы:** Roadmap Stage 0, Stage 1, `SLICE-00`, Milestones `M0` и `M1`

---

# 1. Назначение

Этот документ определяет обязательную техническую исходную точку для новой реализации Odyssey VTT.

Он отвечает на вопросы:

- где и как создаётся основной репозиторий;
- какая версия Unity и какой технологический стек используются;
- какие платформы поддерживает MVP;
- как разделяются Core, инфраструктура и Unity-клиент;
- какие зависимости между модулями разрешены;
- какие правила обязательны для сериализации, ошибок, команд, событий, логирования и тестов;
- как Codex получает задачи, изменяет код и доказывает готовность результата;
- какие проверки блокируют pull request;
- какие решения уже утверждены, а какие должны быть вынесены в отдельные ADR.

Документ не заменяет продуктовые и специализированные контракты. Он переводит их в однозначные правила создания репозитория и первого технического каркаса.

---

# 2. Источники истины и приоритет

## 2.1 Нормативные источники

При реализации применяются:

1. последнее явное решение владельца продукта;
2. `ACTIVE_DOCUMENTATION_BASELINE_Odyssey_VTT_v1.7.md`;
3. этот Technical Development Baseline;
4. утверждённый ADR для конкретного технического вопроса;
5. специализированный продуктовый контракт подсистемы;
6. Product Requirements;
7. MVP Scope;
8. Domain Model;
9. Project Vision;
10. Roadmap;
11. Test Strategy.

## 2.2 Разрешение конфликтов

- Product requirement определяет требуемое поведение.
- Специализированный контракт определяет модель конкретной подсистемы.
- Этот документ определяет технический способ организации разработки, если он не меняет продуктовый контракт.
- ADR может уточнить или заменить техническое решение этого документа, но обязан явно указать заменяемый раздел и последствия.
- Changelog, handoff и LegacyReference не являются источниками требований без прямого указания задачи.

## 2.3 Запрет неявного расширения

Codex и разработчики не могут самостоятельно:

- добавлять функции вне активного MVP;
- переносить архитектуру старого прототипа;
- менять продуктовые инварианты ради удобства реализации;
- добавлять внешние сервисы, зависимости или телеметрию без утверждённого решения;
- ослаблять host authority, redaction, idempotency, audit или persistence guarantees.

---

# 3. Утверждённые решения владельца продукта

| ID | Решение | Статус |
|---|---|---|
| TDB-DEC-001 | Проект создаётся полностью с нуля; production-код старого прототипа не наследуется | Approved |
| TDB-DEC-002 | MVP поддерживает Windows 10 и Windows 11 на PC | Approved |
| TDB-DEC-003 | Архитектура процесса — x86-64 | Approved |
| TDB-DEC-004 | Движок — Unity 6.3 LTS, ветка `6000.3` | Approved |
| TDB-DEC-005 | Exact Editor patch фиксируется в `ProjectVersion.txt` | Approved |
| TDB-DEC-006 | Render Pipeline — HDRP | Approved |
| TDB-DEC-007 | Основной runtime UI — UI Toolkit | Approved |
| TDB-DEC-008 | Единственный authoritative code repository — private GitHub repository `odyssey-services/Odyssey_VTT`; visibility не изменяется без отдельного решения владельца | Approved, supersedes v0.1 wording |
| TDB-DEC-009 | Система контроля версий — Git | Approved |
| TDB-DEC-010 | Крупные бинарные файлы управляются через Git LFS | Approved |
| TDB-DEC-011 | CI выполняется через GitHub Actions | Approved |
| TDB-DEC-012 | `main` защищена; изменения проходят через ветку и pull request | Approved |
| TDB-DEC-013 | Codex не объединяет pull request и не пишет напрямую в `main` | Approved |
| TDB-DEC-014 | Объединение выполняется только после проверки и одобрения владельца проекта | Approved |
| TDB-DEC-015 | Исходный код публикуется как All Rights Reserved | Approved |
| TDB-DEC-016 | Полная продуктовая документация не публикуется вместе с кодом | Approved |
| TDB-DEC-017 | Закрытая документация хранится в локальном Git-репозитории на основном диске | Approved |
| TDB-DEC-018 | Резервная копия закрытой документации хранится на переносном HDD | Approved |
| TDB-DEC-019 | Разрешены MIT, BSD, Apache 2.0 и Unity Companion License | Approved |
| TDB-DEC-020 | GPL, AGPL и зависимости с неясной лицензией запрещены без отдельного решения | Approved |
| TDB-DEC-021 | Codex не добавляет новую зависимость без разрешения задачи или ADR | Approved |
| TDB-DEC-022 | Основная цель производительности — 1920×1080, стабильные 60 FPS | Approved |
| TDB-DEC-023 | Минимально допустимый профиль — 1920×1080, 30 FPS | Approved |
| TDB-DEC-024 | Профили качества обязательны; тяжёлые HDRP-эффекты могут отключаться | Approved |
| TDB-DEC-025 | Основной JSON serializer — System.Text.Json | Approved with mandatory compatibility spike |
| TDB-DEC-026 | Campaign state не хранится как единый JSON; authoritative persistence остаётся SQLite согласно документу 05 | Approved |
| TDB-DEC-027 | Переносимый контейнер кампании — `.odcamp` с versioned manifest и отдельными assets | Approved |

---

# 4. Граница M0 и M1

## 4.1 Milestone M0 — Baseline Ready

M0 закрывается, когда:

- создан единственный private authoritative code repository `odyssey-services/Odyssey_VTT`;
- закрытая продуктовая документация физически отделена от authoritative Git history;
- Unity Editor version зафиксирована;
- лицензия и политика зависимостей оформлены;
- секреты отсутствуют в репозитории;
- структура каталогов и обязательные скрипты созданы;
- новый проект открывается и собирается на чистом рабочем месте;
- создан реестр ADR;
- блокирующие противоречия документации отсутствуют.

## 4.2 Milestone M1 — Technical Skeleton

M1 закрывается, когда:

- завершён `SLICE-00`;
- существует минимальный Unity-клиент;
- чистые Core-модули тестируются без запуска Unity Editor;
- assembly boundaries и dependency direction технически проверяются;
- command/event contracts используются тестовой операцией;
- JSON round-trip и AOT/IL2CPP compatibility spike пройдены либо принято явное ADR-решение;
- Windows dev-build создаётся воспроизводимо;
- версия сборки видна в клиенте и логах;
- fast CI блокирует неправильный pull request.

---

# 5. Платформа и сборка

## 5.1 Target platform

```text
Operating systems: Windows 10 / Windows 11
Architecture: x86-64
Application type: Desktop standalone client
MVP distribution channel: Deferred
```

Точная минимальная сборка Windows 10 определяется build profile и матрицей тестирования перед release candidate. Она не должна угадываться в коде.

## 5.2 Graphics API

Baseline:

- DirectX 12 — основной графический API;
- DirectX 11 — совместимый fallback до завершения performance/compatibility matrix;
- Vulkan не является обязательной частью MVP;
- hardware ray tracing не входит в обязательный baseline;
- ray tracing выключен по умолчанию и не может быть необходим для функциональности продукта.

Финальная матрица Graphics API утверждается после теста HDRP-профилей на целевом оборудовании.

## 5.3 Build configurations

Минимальный набор:

```text
Development-Debug
Development-Profile
Release-Candidate
Release
```

### Development-Debug

- development build;
- расширенные assertions;
- diagnostic overlay;
- подробные локальные логи;
- без production secrets;
- допускается менее оптимизированный scripting backend для скорости итераций.

### Development-Profile

- profiler support;
- representative quality settings;
- performance markers;
- без developer cheats, меняющих доменное состояние без audit.

### Release-Candidate

- production-like settings;
- полный набор release checks;
- чистая установка;
- обновление с предыдущей поддерживаемой версией;
- проверка persistence, recovery и redaction.

### Release

- создаётся только из защищённой `main` по утверждённому tag;
- содержит build identity;
- проходит release quality report;
- не содержит debug-only tooling и тестовых секретов.

## 5.4 Scripting backend

Окончательный backend release-сборки фиксируется ADR после compatibility spike.

Baseline-направление:

- быстрые dev-build могут использовать Mono, если это сокращает время проверки;
- release candidate обязан дополнительно проверяться с IL2CPP x64;
- код, сериализация и reflection usage проектируются с учётом AOT;
- поведение, работающее только в Mono, не считается готовым.

---

# 6. Unity baseline

## 6.1 Editor

```text
Unity family: Unity 6
Approved branch: Unity 6.3 LTS
Version line: 6000.3.x
Exact patch: pinned in ProjectSettings/ProjectVersion.txt
```

Правила:

- exact patch выбирается при создании репозитория после smoke test;
- проект не открывается другой major/minor версией без отдельной upgrade-задачи;
- patch update допускается только отдельным pull request;
- patch update обязан пройти clean checkout, package restore, compile, tests и Windows build;
- Alpha, Beta и experimental Editor releases запрещены для основного проекта.

## 6.2 Package lock

- `Packages/manifest.json` коммитится;
- `Packages/packages-lock.json` коммитится;
- package versions не используют плавающие ranges;
- preview/experimental packages запрещены без ADR;
- package update выполняется отдельным pull request;
- изменение lock-файла без объяснения блокирует review.

## 6.3 HDRP

- проект создаётся из HDRP-compatible шаблона Unity 6.3 LTS;
- HDRP package version определяется выбранным Editor patch и фиксируется lock-файлом;
- качество строится через отдельные HDRP/Quality profiles;
- обязательны минимум Low, Medium, High;
- ray tracing не является required feature;
- визуальный эффект не может быть единственным носителем функционально важной информации;
- performance-heavy feature включается только после измерения.

## 6.4 UI Toolkit

UI Toolkit является основным runtime UI.

Обязательные правила:

- структура — UXML;
- стили — USS;
- поведение — C# presenter/view-model/controller слой;
- доменная логика не размещается в `VisualElement`, callbacks или MonoBehaviour;
- прямой доступ UI к SQLite, network transport и domain aggregates запрещён;
- UI получает prepared projection/view state;
- UI command вызывает Application layer;
- критические пользовательские действия имеют явный result/error feedback;
- UI Toolkit tests по возможности выполняются на ViewModel/Presenter уровне без сцены.

uGUI допускается только как локальное исключение, если UI Toolkit не поддерживает необходимый runtime-сценарий. Исключение требует ADR или явно утверждённой задачи и не может распространяться на весь интерфейс.

## 6.5 Input

- используется Unity Input System;
- legacy Input Manager не является основой нового проекта;
- input actions не содержат игровой логики;
- команды ввода переводятся в application intents;
- пользовательские переназначения клавиш проектируются как versioned configuration.

## 6.6 Unity asset settings

Обязательные настройки:

```text
Version Control Mode: Visible Meta Files
Asset Serialization: Force Text
Line endings: LF in repository
```

- `.meta` всегда коммитятся вместе с asset;
- потеря или ручное восстановление `.meta` без проверки запрещены;
- сцены и prefab не редактируются массово несвязанной задачей;
- Unity YAML merge используется для разрешённых scene/prefab merges;
- `Library`, `Temp`, `Logs`, `Obj`, local builds и user settings не коммитятся.

---

# 7. C# и .NET baseline

## 7.1 API compatibility

Baseline — `.NET Standard 2.1` для максимально предсказуемого пересечения Unity и чистых Core-проектов.

- C# compiler version определяется pinned Unity Editor и pinned .NET SDK для Core tests;
- ручной `LangVersion=preview` запрещён;
- platform-specific API не используется в Domain, Rules и Application;
- Windows path, registry и shell API допускаются только в platform adapter;
- переносимые данные используют `/`-нормализованные относительные пути или URI-like identifiers, а не абсолютные Windows paths.

## 7.2 Code style

- единый `.editorconfig` в корне;
- namespace соответствует модулю и функциональной области;
- один публичный top-level type на файл, кроме tightly coupled records/enums;
- публичные контракты имеют XML documentation, если назначение не очевидно;
- warnings в Core projects рассматриваются как errors, кроме явно зафиксированных исключений;
- `dynamic` запрещён в Core без ADR;
- `unsafe` запрещён без ADR и отдельного security/performance обоснования;
- reflection не используется в критическом runtime path без AOT-теста;
- глобальное mutable state и service locator запрещены.

## 7.3 Nullability

- nullable reference annotations включаются в чистых .NET Core projects;
- Unity assemblies используют тот же policy после подтверждения compiler compatibility;
- `null` не используется как неявный success/error/result state;
- optional data выражаются через nullable value, explicit option contract или отдельный state type;
- публичная десериализация обязана валидировать обязательные поля.

---

# 8. Репозиторий, права и открытость

## 8.1 Основной репозиторий

- private GitHub repository `odyssey-services/Odyssey_VTT`;
- единственный authoritative code repository;
- `main` — единственная release-bearing branch;
- разработка — короткоживущие branches;
- прямые push в `main` запрещены;
- force push в `main` запрещён;
- deletion protection для `main` включена;
- merge возможен только после required checks и owner approval.

## 8.2 Лицензия

В корне размещается `LICENSE` с формулировкой All Rights Reserved.

Минимальный смысл:

- проект не получает open-source license;
- правообладатель сохраняет исключительные права по умолчанию;
- private visibility не предоставляет дополнительных прав на код; доступ к repository не является лицензией;
- дополнительные права на использование, изменение, распространение, продажу или создание производных продуктов вне разрешённой функциональности GitHub не предоставляются без отдельного письменного разрешения;
- copyright notice присутствует в `README.md` и `LICENSE`;
- финальная формулировка `LICENSE` должна быть проверена перед публикацией и не считается юридической консультацией этого технического документа.

## 8.3 Внешние contributions

До появления отдельной политики:

- внешние pull requests не принимаются автоматически;
- `CONTRIBUTING.md` сообщает, что contribution требует предварительного письменного согласования;
- issue не считается разрешением использовать закрытые материалы;
- принятие внешнего кода возможно только после проверки происхождения и передачи необходимых прав.

## 8.4 Git LFS

Git LFS обязателен для крупных и плохо diff-able binary assets.

Начальный набор кандидатов:

```text
*.psd
*.psb
*.blend
*.fbx
*.obj
*.wav
*.mp3
*.ogg
*.exr
*.hdr
*.tif
*.tiff
*.7z
*.zip
```

Правила:

- `.meta`, `.json`, `.uxml`, `.uss`, `.cs`, `.md`, `.asset`, `.prefab`, `.unity` не отправляются в LFS только из-за расширения;
- дополнительные patterns добавляются осознанно;
- случайное добавление крупного binary в Git history исправляется до merge;
- LFS pointer integrity проверяется CI.

---

# 9. Гибридная документация

## 9.1 Repository-safe часть

В code repository могут находиться:

```text
README.md
LICENSE
THIRD_PARTY_NOTICES.md
CONTRIBUTING.md
SECURITY.md
AGENTS.md
PLANS.md
TECHNICAL_DEVELOPMENT_BASELINE_Odyssey_VTT_v0.2.md
docs/adr/**
docs/architecture/**
docs/build/**
docs/testing/**
docs/tasks/templates/**
```

## 9.2 Закрытая часть

Отдельно хранятся:

- полный Product Vision;
- Product Requirements;
- MVP Scope;
- Domain Model и специализированные продуктовые контракты;
- внутренняя Roadmap;
- risk register и decision log с непубличными сведениями;
- коммерческие планы;
- незавершённые решения;
- закрытые тестовые данные.

## 9.3 Локальное хранение

- закрытая документация — отдельный локальный Git repository на основном диске;
- remote для него не обязателен;
- резервная копия — переносной HDD;
- backup выполняется после material changes и перед новым milestone;
- минимум раз в квартал проверяется восстановление копии;
- рекомендуется шифрование диска или контейнера;
- off-site/cloud backup не входит в утверждённый baseline и остаётся рекомендацией.

## 9.4 Передача контекста Codex

Codex получает только task-specific bundle:

- Requirement IDs;
- необходимые выдержки;
- acceptance criteria;
- ограничения;
- разрешённые файлы;
- test expectations.

Task bundle:

- не коммитится в authoritative code repository, если содержит закрытый продуктовый текст;
- не копируется целиком в pull request;
- не попадает в CI artifacts;
- после задачи может быть удалён;
- в issue или pull request заменяется безопасным кратким описанием и IDs.

---

# 10. Политика сторонних зависимостей

## 10.1 Разрешённые лицензии

По умолчанию допускаются:

- MIT;
- BSD-2-Clause;
- BSD-3-Clause;
- Apache License 2.0;
- Unity Companion License для совместимых Unity packages.

## 10.2 Запрещённые без отдельного решения

- GPL;
- AGPL;
- SSPL;
- source-available лицензии с ограничениями использования;
- пакеты без лицензии;
- пакеты с неясным авторством;
- binary-only dependency без проверяемого происхождения;
- abandoned dependency для критического persistence/network/security path.

LGPL и иные copyleft-варианты требуют отдельной юридической и технической проверки.

## 10.3 Процедура добавления

Каждая зависимость требует:

1. конкретной необходимости;
2. проверки лицензии;
3. проверки maintenance status;
4. проверки security advisories;
5. анализа транзитивных зависимостей;
6. оценки AOT/IL2CPP и Unity compatibility;
7. записи в `THIRD_PARTY_NOTICES.md`;
8. pin версии;
9. теста удаления или замены для критического vendor lock-in.

Codex не добавляет dependency, GitHub Action или downloadable tool, если это не разрешено задачей либо ADR.

## 10.4 GitHub Actions

- actions pin-ятся минимум на immutable commit SHA для security-sensitive workflows;
- permissions задаются явно и минимально;
- fork pull requests не получают secrets;
- workflow не выполняет непроверенный code с write token;
- secrets выводить в log запрещено.

---

# 11. Архитектурный стиль

## 11.1 Основной принцип

Odyssey использует модульную layered/hexagonal architecture:

```text
Domain and Rules
        ↑
Application
        ↑
Ports / Contracts
        ↑
Persistence, Networking, Platform adapters
        ↑
Unity Client composition root
```

Стрелка означает направление допустимой зависимости от внешнего слоя к внутреннему. Внутренний слой не знает о внешнем.

## 11.2 Обязательные модули

```text
Odyssey.Domain
Odyssey.Rules
Odyssey.Application
Odyssey.Content
Odyssey.Persistence
Odyssey.Networking
Odyssey.Unity.Client
Odyssey.Tests.*
```

Дополнительные modules создаются только при доказанной ответственности и без циклических зависимостей.

## 11.3 Odyssey.Domain

Содержит:

- identities и value objects;
- aggregates;
- domain events;
- domain invariants;
- permissions vocabulary без infrastructure resolution;
- campaign/session-independent model primitives;
- stable domain error codes.

Запрещено:

- `UnityEngine`;
- SQLite;
- network transport;
- file system;
- wall-clock access;
- random globals;
- UI types;
- direct JSON/database annotations, связывающие domain с provider.

## 11.4 Odyssey.Rules

Содержит:

- детерминированные формулы;
- expression evaluation;
- roll and calculation trace;
- ruleset execution contracts;
- target and outcome calculation;
- deterministic validation.

Зависит только от Domain и approved BCL abstractions.

## 11.5 Odyssey.Application

Содержит:

- commands and handlers;
- query/use-case orchestration;
- validation pipeline;
- transaction boundaries;
- idempotency handling;
- authorization calls;
- event publication;
- ports для persistence, networking, clock, RNG и diagnostics;
- mapping stable errors to application results.

Application не знает о Unity widgets, SQLite implementation и transport implementation.

## 11.6 Odyssey.Content

Содержит:

- Content Block contracts;
- package definitions;
- validation and dependency graph;
- publication model;
- safe execution inputs;
- import/export DTOs.

Runtime content execution не может обходить Application transaction и Domain invariants.

## 11.7 Odyssey.Persistence

Реализует Application persistence ports:

- SQLite campaign store;
- transaction and outbox;
- snapshots;
- migrations;
- backups;
- `.odcamp` staging/import/export;
- content-addressed asset storage.

Persistence не содержит игровой логики.

## 11.8 Odyssey.Networking

Реализует Application networking/session ports:

- transport abstraction;
- host-authoritative command path;
- projection/redaction delivery;
- reconnect and ordering;
- network message serialization;
- session diagnostics.

Networking не принимает авторитетные игровые решения вместо Application/Domain.

## 11.9 Odyssey.Unity.Client

Содержит:

- Unity bootstrap;
- composition root;
- UI Toolkit views;
- presenters/view models;
- scene lifecycle;
- platform adapters;
- input mapping;
- audio/graphics integration;
- developer diagnostics UI.

Unity Client не становится доменной моделью и не хранит authoritative campaign state в MonoBehaviour/ScriptableObject.

---

# 12. Матрица зависимостей

`✓` — разрешено, `—` — запрещено как compile-time dependency.

| From / To | Domain | Rules | Application | Content | Persistence | Networking | Unity Client |
|---|---:|---:|---:|---:|---:|---:|---:|
| Domain | — | — | — | — | — | — | — |
| Rules | ✓ | — | — | — | — | — | — |
| Application | ✓ | ✓ | — | allowed contracts | — | — | — |
| Content | ✓ | ✓ | limited contracts | — | — | — | — |
| Persistence | ✓ | limited | ✓ | ✓ | — | — | — |
| Networking | ✓ | limited | ✓ | limited DTOs | — | — | — |
| Unity Client | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | — |

Уточнение `limited` фиксируется ADR и assembly definitions. Любая обратная или циклическая зависимость блокирует merge.

---

# 13. Физическая структура репозитория

Рекомендуемая исходная структура:

```text
/
├─ Assets/
│  └─ Odyssey/
│     ├─ Client/
│     │  ├─ Runtime/
│     │  ├─ UI/
│     │  ├─ Scenes/
│     │  ├─ Settings/
│     │  └─ Tests/
│     └─ SharedAssets/
├─ Packages/
│  ├─ com.odyssey.domain/
│  ├─ com.odyssey.rules/
│  ├─ com.odyssey.application/
│  ├─ com.odyssey.content/
│  ├─ com.odyssey.persistence/
│  └─ com.odyssey.networking/
├─ ProjectSettings/
├─ UserSettings/                  # ignored
├─ DotNet/
│  ├─ Odyssey.Core.sln
│  ├─ Projects/
│  └─ Tests/
├─ Tests/
│  ├─ Contracts/
│  ├─ Fixtures/
│  └─ TestEvidence/
├─ docs/
│  ├─ adr/
│  ├─ architecture/
│  ├─ build/
│  ├─ testing/
│  └─ tasks/templates/
├─ scripts/
├─ .github/
│  ├─ workflows/
│  ├─ ISSUE_TEMPLATE/
│  └─ PULL_REQUEST_TEMPLATE.md
├─ .editorconfig
├─ .gitattributes
├─ .gitignore
├─ AGENTS.md
├─ PLANS.md
├─ LICENSE
├─ README.md
├─ SECURITY.md
└─ THIRD_PARTY_NOTICES.md
```

## 13.1 Embedded packages

Core modules размещаются как embedded Unity packages, чтобы:

- иметь отдельные assembly definitions;
- фиксировать dependency direction;
- отделить Core от scenes/assets;
- переиспользовать те же `.cs` files в pure .NET test projects;
- не копировать код между Unity и test solution.

## 13.2 Pure .NET solution

`DotNet/Odyssey.Core.sln` компилирует тот же source code Domain/Rules/Application через explicit project includes.

Правила:

- source не дублируется;
- conditional compilation минимальна;
- Core code не должен требовать Unity defines;
- расхождение поведения Unity и .NET build считается defect;
- pinned .NET SDK фиксируется через `global.json` после scaffold spike.

---

# 14. Assembly definitions и architecture enforcement

Для каждого Unity module создаётся `.asmdef`.

Обязательные свойства:

- explicit references;
- auto referenced отключается там, где это усиливает границы;
- editor-only code находится в Editor assembly;
- test assembly отделена;
- platform constraints задаются явно;
- cyclic references запрещены.

CI выполняет architecture check:

- Domain assembly не ссылается на Unity и infrastructure;
- Rules не ссылается на Unity/Persistence/Networking;
- Application не ссылается на concrete adapters;
- Unity-specific namespaces отсутствуют в Core source;
- запрещённые project/assembly references блокируют pull request.

---

# 15. Core technical primitives

`SLICE-00` обязан создать минимум следующие contracts.

## 15.1 Typed IDs

- IDs не передаются как случайные строки;
- каждый identity type имеет отдельный тип;
- сериализация стабильна;
- parsing валидирует формат;
- default/empty identity запрещена там, где объект уже создан.

Минимальные кандидаты:

```text
CampaignId
SessionId
UserId
MembershipId
CharacterId
SceneId
BoardId
TokenId
CommandId
EventId
TransactionId
CorrelationId
IdempotencyKey
AssetId
ContentDefinitionId
ContentVersionId
```

## 15.2 Version types

Отдельные типы:

```text
ApplicationVersion
BuildVersion
SchemaVersion
CampaignFormatVersion
RulesetVersion
ContentPackageVersion
NetworkProtocolVersion
```

Версии разных областей не взаимозаменяемы.

## 15.3 Result/Error

Каждая application operation возвращает explicit result.

Error содержит минимум:

```text
Code
Category
SafeMessage
CorrelationId
Retryability
ValidationDetails[]
DiagnosticContextRef?    # только для разрешённой диагностики
```

- exception не используется как обычный expected result;
- публичное сообщение не раскрывает секреты;
- stable error code тестируется;
- stack trace не показывается обычному пользователю.

## 15.4 Command envelope

Минимум:

```text
CommandId
CommandType
ActorId
CampaignId
SessionId?
ExpectedRevision?
IdempotencyKey
CorrelationId
IssuedAt
PayloadVersion
Payload
```

Command handler:

- валидирует actor and permission;
- проверяет expected revision;
- применяет idempotency semantics;
- использует injected clock/RNG;
- создаёт DomainEvents;
- коммитит атомарно через Application transaction boundary.

## 15.5 Event envelope

Минимум:

```text
EventId
EventType
AggregateId
AggregateRevision
TransactionId
CorrelationId
OccurredAt
PayloadVersion
Payload
AudienceClassification
```

Event не удаляется для сокрытия истории; correction/compensation оформляются отдельным событием.

## 15.6 Clock

- wall clock доступен через `IClock`;
- тесты используют virtual/fixed clock;
- monotonic duration не вычисляется через local wall time;
- timezone presentation отделена от stored UTC instant.

## 15.7 RNG

- randomness доступна через explicit abstraction;
- seed фиксируется в test evidence;
- Rules Engine не вызывает global random API;
- authoritative roll создаётся только host-authoritative path;
- duplicate command не создаёт новый roll.

## 15.8 Revision and idempotency

- aggregate/session revision типизирована;
- stale command возвращает stable conflict result;
- повтор того же idempotency key возвращает исходный result либо deterministic duplicate status;
- idempotency state хранится в той же authoritative transaction boundary, когда это требуется контрактом.

---

# 16. Serialization baseline

## 16.1 Роль System.Text.Json

System.Text.Json является основным JSON serializer для:

- manifest files;
- settings;
- command/event DTOs;
- contract fixtures;
- safe diagnostic metadata;
- versioned polymorphic payloads там, где это разрешено контрактом.

Он не означает, что вся кампания хранится одним JSON-файлом.

## 16.2 Authoritative persistence

Согласно `05_Persistence_Odyssey_VTT_v0.8.md`:

- authoritative campaign state хранится в SQLite;
- assets хранятся отдельно по относительным paths/content hash;
- JSON применяется для versioned payloads и manifests;
- `.odcamp` является переносимым архивным контейнером, а не live database format;
- абсолютные исходные пути не становятся рабочей зависимостью.

## 16.3 DTO boundary

- domain aggregates напрямую не сериализуются как публичный file/network contract;
- используются explicit versioned DTO;
- property names считаются contract;
- enum сериализуется стабильным утверждённым представлением;
- неизвестное поле обрабатывается по documented compatibility policy;
- отсутствующее обязательное поле блокирует import/message acceptance;
- converters находятся в Serialization layer, не в Domain.

## 16.4 Polymorphism

- произвольная десериализация CLR type names запрещена;
- `$type` с assembly-qualified names запрещён;
- subtype выбирается через allowlisted discriminator;
- неизвестный discriminator безопасно отклоняется или сохраняется как unsupported payload согласно контракту;
- recursion/depth/size limits обязательны для недоверенного input.

## 16.5 AOT/IL2CPP compatibility spike

До production use обязательна проверка System.Text.Json в pinned Unity 6.3 patch:

1. Mono Windows dev-build;
2. IL2CPP Windows x64 build;
3. round-trip typed IDs и version types;
4. custom converters;
5. polymorphic allowlist;
6. invalid/untrusted payload limits;
7. trimming/AOT behavior;
8. performance на representative payload;
9. no reflection-only runtime failure.

Если spike не проходит, ADR-003 обязан предложить замену или адаптацию. Замена serializer требует явного обновления этого baseline либо утверждения владельца продукта. Молчаливый переход на другой JSON library запрещён.

## 16.6 Schema versions и migrations

- каждый persisted/public contract имеет schema version;
- version отсутствует только для immutable trivial format, утверждённого ADR;
- migration выполняется explicit runner;
- destructive migration требует backup;
- неизвестная более новая версия не открывается на запись;
- round-trip не считается migration test.

---

# 17. Persistence boundary до Stage 2

В M1 создаются только interfaces и test doubles, необходимые для границ.

Не выбираются без ADR:

- конкретный SQLite package/driver;
- connection pooling strategy;
- WAL tuning;
- encryption implementation;
- backup container implementation;
- final ZIP/ZIP64 library.

Разрешено создать минимальный in-memory adapter для `SLICE-00`, если он:

- не объявляется production persistence;
- соблюдает transaction/idempotency contract;
- не формирует ложный file format;
- заменяется реальным adapter в Stage 2.

---

# 18. Dependency composition

## 18.1 Default

M1 использует explicit manual composition root.

- dependency injection container не добавляется;
- constructor injection — основной способ;
- optional dependency не извлекается через global locator;
- lifetime виден в composition root;
- test composition создаётся отдельно.

DI framework может быть добавлен только ADR с доказанной необходимостью.

## 18.2 Unity bootstrap

Bootstrap scene:

- создаёт composition root;
- загружает app configuration;
- создаёт adapters;
- запускает Application shell;
- открывает minimal developer navigation;
- показывает build version и health/status;
- корректно завершает resources на shutdown.

Business operation не должна зависеть от порядка `Awake` случайных MonoBehaviour.

---

# 19. UI application boundary

Baseline pattern:

```text
UI Toolkit View
↕
Presenter / ViewModel
↕
Application Commands and Queries
↕
Domain / Rules through Application
```

Обязательные правила:

- view state immutable или controlled observable model;
- presenter не выполняет SQL/network IO напрямую;
- UI optimistic update разрешён только при documented reconciliation;
- command pending/success/error states различимы;
- permission check в UI не заменяет authoritative permission check;
- скрытые данные не передаются в view model запрещённого пользователя;
- redaction выполняется до presentation layer.

---

# 20. Logging, diagnostics и секреты

## 20.1 Logging abstraction

Создаётся собственный минимальный `IOdysseyLogger` contract либо аналогичный port без обязательной внешней logging library.

Log event содержит:

```text
TimestampUtc
Level
EventCode
CorrelationId
BuildVersion
Subsystem
SafeProperties
```

## 20.2 Запрещённые данные

В обычные логи не попадают:

- passwords;
- access/refresh tokens;
- private keys;
- owner key material;
- private chat plaintext;
- GM-only hidden payload для unauthorized client;
- полные absolute user paths без необходимости;
- закрытый task bundle;
- содержимое secret environment variables.

## 20.3 Diagnostic bundle

- создаётся только явным действием пользователя или approved support flow;
- имеет allowlist contents;
- redacts secrets;
- содержит build/version/system summary;
- не включает закрытую документацию;
- не включает private chat plaintext.

## 20.4 Authoritative repository security

- secrets хранятся только в local secret store или GitHub Actions Secrets;
- `.env`, credentials, certificates и user configs ignored;
- sample configs содержат placeholders;
- secret scan обязателен на PR;
- обнаруженный secret считается incident: его удаление из последнего commit недостаточно, secret отзывается и history очищается при необходимости.

---

# 21. Тестовая архитектура

Этот baseline наследует `16_Test_Strategy_Odyssey_VTT_v0.1.md`.

## 21.1 Pure .NET tests

Без Unity запускаются:

- Unit;
- DomainScenario;
- Contract для Core/public DTO;
- architecture tests;
- deterministic clock/RNG tests;
- serialization tests;
- idempotency tests;
- error code tests.

## 21.2 Unity tests

Unity Test Framework используется для:

- assembly/package integration;
- scene/bootstrap lifecycle;
- UI Toolkit runtime behavior;
- input integration;
- asset loading;
- platform-specific adapters;
- Windows build smoke.

## 21.3 Test projects

```text
Odyssey.Tests.Unit
Odyssey.Tests.Domain
Odyssey.Tests.Contracts
Odyssey.Tests.Architecture
Odyssey.Tests.Persistence
Odyssey.Tests.Networking
Odyssey.Tests.Unity.EditMode
Odyssey.Tests.Unity.PlayMode
Odyssey.Tests.EndToEnd
Odyssey.Tests.Performance
Odyssey.Tests.Security
```

Создаются по мере этапов; `SLICE-00` обязан создать минимум Unit, Domain, Contracts, Architecture и Unity smoke.

## 21.4 Naming and evidence

Каждый обязательный test имеет стабильный TestCaseId и связь:

```text
RequirementId
→ TaskId
→ TestCaseId
→ CI suite
→ Evidence
```

При failure сохраняются применимые артефакты:

- logs;
- seed;
- virtual time;
- commands/events;
- state snapshot;
- screenshot;
- build version.

---

# 22. CI/CD baseline

## 22.1 Pull request pipeline

Каждый pull request запускает минимум:

```text
RepositoryPolicyCheck
LicenseAndDependencyCheck
Formatting
StaticAnalysis
SecretScan
DotNetCompile
FastUnit
DomainScenario
Contract
ArchitectureCheck
SerializationRoundTrip
UnityCompile
UnityEditModeSmoke
WindowsDevelopmentBuild
RequirementTraceabilityCheck
```

На первых scaffold pull requests допустимо вводить checks поэтапно, но M1 не закрывается без полного набора.

## 22.2 Merge blockers

PR нельзя объединить, если:

- compile failed;
- required test failed;
- architecture boundary нарушена;
- secret обнаружен;
- dependency добавлена без approval;
- license неизвестна;
- lock-файл изменён без объяснения;
- public contract изменён без version/test/documentation;
- mandatory requirement не имеет test/evidence;
- Windows dev-build не создан;
- owner approval отсутствует.

## 22.3 Main and scheduled pipelines

После merge в `main`:

- повторяется trusted build;
- сохраняется artifact;
- выполняется более полный test suite;
- формируется build identity.

Nightly по мере появления подсистем:

- Standard tests;
- Unity PlayMode;
- Integration;
- Persistence recovery;
- Networking;
- End-to-End;
- security/dependency scan;
- performance trend;
- soak tests.

## 22.4 Unity CI licensing

Способ автоматической активации Unity Editor и конкретные GitHub Actions фиксируются отдельной implementation task/ADR.

Требования:

- license secret не попадает в logs;
- fork PR не получает license secret;
- action version pinned;
- отсутствие Unity license не даёт false-green result;
- локальная команда воспроизводит основную проверку без зависимости от GitHub UI.

## 22.5 Artifact retention

- PR artifacts хранятся ограниченный срок;
- release candidate artifacts хранятся до замены или explicit cleanup;
- artifacts не содержат secrets или private documentation;
- build artifact именуется по version + commit.

---

# 23. Git workflow

## 23.1 Branch model

Используется trunk-based workflow с короткоживущими branches.

Рекомендуемые имена:

```text
feat/ODV-123-short-name
fix/ODV-124-short-name
docs/ODV-125-short-name
chore/ODV-126-short-name
spike/ODV-127-short-name
```

## 23.2 Pull request

PR содержит:

- TaskId;
- цель;
- in-scope/out-of-scope;
- linked Requirement IDs;
- архитектурные последствия;
- изменённые contracts;
- tests и команды;
- screenshots/evidence, если применимо;
- dependency/license declaration;
- known limitations.

## 23.3 Merge strategy

Baseline — squash merge после owner approval.

- итоговый commit имеет TaskId;
- незавершённые WIP commits не засоряют `main`;
- merge commit/rebase strategy может быть изменена repository policy без изменения product contract;
- Codex не нажимает merge.

## 23.4 Commit hygiene

- один PR решает одну согласованную задачу;
- unrelated refactor запрещён;
- generated files коммитятся только если Unity/package workflow требует их в repository;
- массовое форматирование отдельным PR;
- binary change объясняется;
- private requirement text не попадает в commit message.

---

# 24. Обязательные scripts

В корне `scripts/` создаются PowerShell entry points для Windows:

```text
scripts/bootstrap.ps1
scripts/restore.ps1
scripts/format.ps1
scripts/verify-format.ps1
scripts/test-fast.ps1
scripts/test-all.ps1
scripts/test-unity.ps1
scripts/build-dev.ps1
scripts/build-release.ps1
scripts/verify-docs.ps1
scripts/verify-repository.ps1
```

Требования:

- scripts fail with non-zero exit code;
- не требуют ручных кликов для CI path;
- проверяют prerequisites и выдают понятную ошибку;
- не скачивают mutable executable без checksum/pin;
- не печатают secrets;
- одинаковая команда используется локально и в CI;
- paths вычисляются относительно repository root.

На M0 допускается, что часть scripts вызывает минимальные placeholders, но M1 требует работающих `format`, `test-fast`, `build-dev`, `verify-docs` и `verify-repository`.

---

# 25. Versioning и build identity

## 25.1 Application version

До MVP используется SemVer-compatible pre-1.0 versioning:

```text
0.MINOR.PATCH
```

- MINOR — новый вертикальный срез или material capability;
- PATCH — исправления без изменения публичного contract;
- breaking contract change до 1.0 повышает MINOR и migration/compatibility notes.

## 25.2 Build identity

Каждая сборка содержит:

```text
ApplicationVersion
BuildNumber
GitCommitSha
GitBranchOrTag
BuildTimestampUtc
UnityVersion
BuildConfiguration
SchemaVersion
NetworkProtocolVersion
```

Build identity видна:

- в developer/status panel;
- в логах;
- в diagnostic bundle;
- в release quality report.

## 25.3 Tags

- release tag создаётся только после owner approval;
- tag соответствует application version;
- tag не перемещается;
- release notes ссылаются на completed tasks и migrations.

---

# 26. Performance baseline

## 26.1 Цели

```text
Primary target: 1920×1080, stable 60 FPS
Minimum acceptable profile: 1920×1080, 30 FPS
Higher resolutions: supported without universal 60 FPS guarantee
```

## 26.2 Quality profiles

Минимум:

- Low;
- Medium;
- High.

Каждый profile определяет:

- HDRP asset;
- shadows;
- lighting/post-processing;
- anti-aliasing;
- texture quality;
- effects density;
- optional expensive features.

## 26.3 Functional independence

Отключение тяжёлых визуальных эффектов не может:

- скрывать game state;
- делать сетку/токены нечитаемыми;
- ломать Fog of War;
- менять правила;
- препятствовать управлению.

## 26.4 Hardware baseline

Конкретные minimum/recommended CPU, GPU, RAM и VRAM не фиксируются без benchmark.

До конца Stage 1 создаётся hardware/performance test plan. До release candidate утверждается фактическая матрица Windows 10/11 и GPU/API.

---

# 27. Правила работы Codex

## 27.1 Постоянный контекст

В публичном repository создаётся `AGENTS.md`, который повторяет кратко обязательные правила:

- source-of-truth hierarchy;
- module boundaries;
- prohibited dependencies;
- command/event/idempotency rules;
- no hidden data leakage;
- no unapproved dependencies;
- required scripts/tests;
- no merge to `main`;
- no scope expansion;
- no private documentation in Git.

`PLANS.md` определяет формат планов для крупных изменений.

## 27.2 Task contract

Каждая задача Codex содержит:

```text
Task ID
Roadmap Stage
Vertical Slice
Requirement IDs
Goal
Source of truth excerpts
Context
Constraints
In scope
Out of scope
Expected behavior
Acceptance criteria
Required tests
Validation commands
Done when
Evidence
```

## 27.3 План до изменения

Codex обязан сначала сформировать или обновить план, если задача:

- затрагивает более одного модуля;
- меняет public contract;
- добавляет migration;
- добавляет dependency;
- затрагивает security/permissions/redaction;
- изменяет persistence/network format;
- занимает несколько логических шагов.

## 27.4 Ограничение изменения файлов

Task явно перечисляет разрешённые области. Codex:

- не выполняет unrelated cleanup;
- не переписывает архитектуру без ADR;
- не изменяет generated/binary assets без необходимости;
- не обновляет Unity/packages «заодно»;
- не меняет product docs в публичном repository, если они закрыты.

## 27.5 Завершение задачи

Перед handoff Codex обязан:

1. проверить diff;
2. удалить accidental files;
3. выполнить required commands;
4. сообщить failed/skipped checks;
5. перечислить изменения;
6. перечислить риски и ограничения;
7. открыть pull request;
8. не выполнять merge.

False claim о пройденном тесте недопустим. Если проверка не запускалась, это указывается явно.

---

# 28. ADR register

До закрытия M1 необходимы:

| ADR | Тема | Требуемый момент |
|---|---|---|
| ADR-001 | Module boundaries and dependency direction | До создания assemblies |
| ADR-002 | Command and Domain Event model | До Core primitives |
| ADR-003 | Serialization strategy and System.Text.Json compatibility | До persistent/public DTO production use |
| ADR-004 | Result and Error model | До первых handlers |
| ADR-005 | Dependency composition | До bootstrap implementation |
| ADR-006 | Test project structure and dual Unity/.NET compilation | До test scaffold |
| ADR-007 | Versioning and build identity | До первого CI artifact |
| ADR-008 | Deterministic Clock and RNG | До Rules/roll tests |
| ADR-009 | Unity build profiles, graphics API and scripting backend | До Release-Candidate pipeline |
| ADR-010 | Logging, diagnostics and redaction | До diagnostic overlay |

До Stage 2 дополнительно:

| ADR | Тема |
|---|---|
| ADR-011 | SQLite provider and transaction implementation |
| ADR-012 | Campaign folder and `.odcamp` physical format |
| ADR-013 | Snapshot, journal and recovery implementation |
| ADR-014 | Migration runner |
| ADR-015 | Owner key local storage baseline |

---

# 29. Отложенные решения

Следующие вопросы не блокируют создание `SLICE-00`:

- конкретный SQLite driver;
- network transport/relay SDK;
- account provider;
- E2EE library;
- distribution channel: GitHub Releases, Steam или иной;
- installer/update framework;
- crash reporting/telemetry;
- Addressables adoption;
- final minimum hardware;
- production code signing certificate;
- localization tooling;
- exact `.odcamp` archive implementation;
- final Unity CI action/provider.

Codex не имеет права выбрать их как скрытую часть другой задачи.

---

# 30. Первый пакет разработки

## PR-000 — Repository Policy and Documentation

Результат:

- `README.md`;
- `LICENSE` All Rights Reserved;
- `CONTRIBUTING.md`;
- `SECURITY.md`;
- `THIRD_PARTY_NOTICES.md`;
- `AGENTS.md` skeleton;
- `PLANS.md`;
- этот baseline;
- ADR index;
- `.gitignore`, `.gitattributes`, `.editorconfig`.

## PR-001 — Unity Project Foundation

Результат:

- Unity 6.3 LTS HDRP project;
- exact patch pinned;
- package lock;
- UI Toolkit and Input System baseline;
- Force Text / Visible Meta Files;
- bootstrap scene;
- build version placeholder;
- clean open without errors.

## PR-002 — Module and Test Skeleton

Результат:

- embedded packages;
- assembly definitions;
- pure .NET solution;
- unit/domain/contract/architecture test projects;
- dependency rule test;
- scripts `restore`, `format`, `test-fast`.

## PR-003 — Core Primitives

Результат:

- typed IDs;
- versions;
- Result/Error;
- clock/RNG;
- command/event envelopes;
- revision/idempotency contracts;
- deterministic tests.

## PR-004 — Serialization Spike

Результат:

- System.Text.Json contract DTOs;
- converters;
- Mono round-trip;
- IL2CPP x64 round-trip;
- invalid payload tests;
- ADR-003 conclusion.

## PR-005 — CI and Windows Dev Build

Результат:

- protected required checks;
- fast pipeline;
- Unity compile/test;
- Windows dev-build;
- artifact retention;
- build identity;
- minimal smoke test.

После этих PR `SLICE-00` может быть принят при выполнении всех exit criteria Roadmap и Test Strategy.

---

# 31. Definition of Done этого baseline

Документ считается внедрённым, когда:

1. он находится в публичном code repository;
2. `ACTIVE_DOCUMENTATION_BASELINE` указывает его как технический authority;
3. решения владельца продукта не противоречат repository settings;
4. Unity 6.3 LTS patch закреплён;
5. `main` защищена;
6. All Rights Reserved license опубликована;
7. dependency policy опубликована;
8. private documentation отсутствует в Git history;
9. scripts contract создан;
10. ADR index создан;
11. `AGENTS.md` ссылается на этот baseline;
12. первый scaffold PR следует структуре раздела 30.

---

# 32. Checklist для review

## Repository

- [ ] Публичный GitHub repository создан.
- [ ] `main` защищена.
- [ ] Direct push и force push запрещены.
- [ ] Owner approval required.
- [ ] Git LFS настроен.
- [ ] All Rights Reserved license присутствует.
- [ ] Contribution policy присутствует.

## Unity

- [ ] Unity 6.3 LTS exact patch закреплён.
- [ ] HDRP project открывается без ошибок.
- [ ] UI Toolkit используется как основной UI.
- [ ] Input System включён.
- [ ] Force Text и Visible Meta Files включены.
- [ ] Package lock committed.

## Architecture

- [ ] Core modules не зависят от Unity.
- [ ] Assembly definitions созданы.
- [ ] Architecture tests блокируют запрещённые references.
- [ ] Manual composition root создан.
- [ ] Global service locator отсутствует.

## Quality

- [ ] Pure .NET tests запускаются без Unity.
- [ ] Unity smoke test запускается.
- [ ] Windows dev-build создаётся.
- [ ] System.Text.Json spike запланирован/выполнен.
- [ ] Required CI checks блокируют merge.
- [ ] Build identity доступна.

## Security and documentation

- [ ] Secret scan включён.
- [ ] Private docs отсутствуют в public Git.
- [ ] Task bundles не сохраняются в artifacts.
- [ ] Dependencies имеют разрешённые лицензии.
- [ ] `THIRD_PARTY_NOTICES.md` актуален.
- [ ] Резервная копия закрытой документации проверена.

---

# 33. Внешние технические ориентиры

При создании репозитория используются соответствующие pinned-версии официальной документации:

- Unity 6.3 LTS release and support documentation;
- Unity Manual for version 6000.3;
- Unity .NET profile support documentation;
- Unity UI Toolkit runtime documentation;
- Unity HDRP and Windows compatibility documentation;
- Unity Test Framework documentation;
- GitHub branch protection, Actions and secret security documentation;
- Git LFS documentation;
- Microsoft System.Text.Json documentation для выбранной совместимой package version.

Онлайн-документ «current» не заменяет документацию pinned Unity version.

---

**Конец документа**
