# ADR-009 — Unity Project and Build Baseline

**Документ:** `docs/adr/ADR-009_Unity_Project_and_Build_Baseline_v1.0.md`  
**ADR:** ADR-009  
**Версия:** 1.0  
**Дата:** 27 июля 2026 года  
**Статус:** Accepted  
**Область:** exact Unity Editor baseline, project creation, package policy, HDRP/UI Toolkit/Input System configuration, Unity assets and scenes, Player settings, Windows build profiles, graphics APIs, scripting backends, build automation, Unity CI contract, upgrade policy и `SLICE-00` Unity scaffold  
**Связанные этапы:** Roadmap Stage 1, `SLICE-00`, Milestone `M1`, PR-001 Unity Project Foundation, PR-005 CI and Windows Dev Build и последующие Unity Client slices  
**Базовые документы:** `TECHNICAL_DEVELOPMENT_BASELINE_Odyssey_VTT_v0.1.md`, `16_Test_Strategy_Odyssey_VTT_v0.1.md`, `17_Roadmap_Odyssey_VTT_v0.11.md`, `ADR-001_Module_Boundaries_and_Dependency_Direction_v1.0.md`, `ADR-003_Serialization_Strategy_v1.0.md`, `ADR-005_Dependency_Composition_v1.0.md`, `ADR-006_Test_Project_Structure_and_Dual_Unity_DotNet_Compilation_v1.0.md`, `ADR-007_Versioning_and_Build_Identity_v1.0.md`, `ADR-008_Deterministic_Clock_and_RNG_v1.0.md`

---

# 1. Решение

Odyssey VTT создаётся как один version-controlled Unity 6.3 LTS HDRP-проект с точным Editor patch, воспроизводимым package graph, явными Windows build profiles и автоматизированной проверкой всех критических Unity settings.

Обязательные решения:

1. Точный Unity Editor baseline — **Unity `6000.3.20f1`**, changeset `c9ba695d4f07`.
2. `ProjectSettings/ProjectVersion.txt` обязан содержать точный Editor version и revision; другой Editor не используется для обычной разработки или CI.
3. Целевая production-платформа MVP — **Windows Standalone x86-64**; UWP, Windows ARM64, Web и dedicated server не входят в baseline.
4. Unity Editor устанавливается с модулем **Windows Build Support (IL2CPP)**. Возможность Windows Mono build используется как быстрый development path, но не заменяет IL2CPP validation.
5. Проект первоначально создаётся официальным Unity HDRP template для выбранного Editor patch. После первого commit источником истины являются repository files, а не название или текущая версия Hub template.
6. Render Pipeline — HDRP. Переключение на URP/Built-in либо смешение pipeline assets запрещено без superseding ADR.
7. Runtime UI — UI Toolkit. uGUI не является вторым параллельным UI baseline.
8. Input — Unity Input System package; Active Input Handling устанавливается в `Input System Package (New)`, а legacy Input Manager не используется новым кодом.
9. Обязательные Unity packages фиксируются в `Packages/manifest.json` и `Packages/packages-lock.json`; floating versions, preview/experimental packages и неявные package upgrades запрещены.
10. Основной registry — официальный Unity registry. Custom scoped registry, Git package, tarball, local disk package или unsigned third-party package требуют отдельного утверждения и license/security review.
11. Core-код размещается в embedded packages `Packages/com.odyssey.*` согласно ADR-001/ADR-006. `Assets/` содержит Unity Client, scenes, UI и project-specific assets, но не дублирует Core source.
12. Asset Serialization — `Force Text`; Version Control Mode — `Visible Meta Files`; все `.meta` коммитятся.
13. Единственные стартовые runtime scenes Stage 1 — `Bootstrap.unity` и `AppShell.unity`.
14. `Bootstrap.unity` создаёт единственный AppRuntime/composition root и загружает `AppShell.unity`; случайные scene objects не строят параллельный service graph.
15. Enter Play Mode Options на M1 не отключают Domain Reload и Scene Reload. Оптимизация reload допускается позже только после lifecycle tests.
16. Windows Graphics API list фиксируется явно: Direct3D 12 первым, Direct3D 11 fallback вторым; Auto Graphics API выключен.
17. Vulkan не входит в обязательную Windows-матрицу MVP.
18. Hardware ray tracing, HDR display output, DLSS/FSR/XeSS, Streaming Virtual Texturing и dynamic resolution не являются baseline requirements и выключены либо не используются до отдельной measured task.
19. Color Space — Linear.
20. Обязательны три quality level: `Low`, `Medium`, `High`; default — `Medium` до появления hardware benchmark matrix.
21. Каждый quality level использует собственный HDRP Render Pipeline Asset и совместимый Quality/Volume configuration. Отключение визуальных функций не меняет authoritative state или доступность игровых данных.
22. Unity Physics/PhysX не является authority для правил, LoS, grid, movement validation или combat. Он может использоваться только как presentation/helper adapter, если результат не становится авторитетным без Application validation.
23. Build profiles:
    - `Development-Debug` — Windows x64, Mono, Development Build, Script Debugging, diagnostic UI;
    - `Development-Profile` — Windows x64, Mono, Development Build, Autoconnect Profiler/Deep Profiling только по явному запуску;
    - `Release-Candidate` — Windows x64, IL2CPP, production-like settings, non-development build;
    - `Release` — Windows x64, IL2CPP, clean tagged commit, production settings.
24. Код, работающий только в Mono, не считается готовым. IL2CPP Windows x64 smoke обязателен до закрытия `SLICE-00` и для каждого Release Candidate.
25. Managed code stripping на M1 фиксируется на консервативном уровне `Low`. Повышение уровня требует отдельного measured PR с serialization/AOT/linker tests.
26. Custom scripting define symbols применяются только к diagnostics/build capabilities и не могут менять правила, persisted contracts или network semantics.
27. Все builds создаются через versioned Editor build entry point и PowerShell scripts. Manual Build button не является release procedure.
28. Build script заново применяет и проверяет profile settings; он не зависит от последнего active build profile конкретного разработчика.
29. Build output создаётся только вне `Assets/`, `Packages/` и `ProjectSettings/` в `artifacts/` или переданном output path.
30. Каждый artifact получает BuildIdentity из ADR-007 и `build-identity.json`.
31. Unity Console compiler error, package restore/signature error, отсутствующий required scene/settings asset, package lock drift или failed smoke test делает build неуспешным.
32. GitHub Actions provider и Unity license activation implementation выбираются отдельной implementation task, но обязаны соблюдать contract этого ADR.
33. Patch upgrade `6000.3.x` выполняется отдельным PR после review release notes, clean reimport, package diff, all tests, Mono build и IL2CPP build.
34. Переход с `6000.3` на другую Unity minor/LTS line, смена render pipeline, release scripting backend либо обязательной Graphics API требует amendment или superseding ADR.
35. ADR-009 является нормативным authority по Unity project/build baseline и заменяет предварительные Unity/build-profile решения Technical Development Baseline в пределах этой области.

---

# 2. Контекст и проблема

Odyssey VTT начинается с нуля и будет в значительной степени развиваться через Codex. Без точного Unity baseline разные задачи могут незаметно создать несовместимые решения:

- открыть проект в разных patch-версиях Editor и массово переписать YAML assets;
- выбрать разные template/package versions;
- использовать Built-in/URP assets внутри HDRP-проекта;
- включить `Both` input handling и начать смешивать legacy Input Manager с Input System;
- положить Core source в `Assets`, нарушив dual compilation ADR-006;
- создать несколько bootstrap scenes и несколько runtime singletons;
- собирать dev и release через разные ручные настройки;
- считать Mono-прохождение доказательством IL2CPP-совместимости;
- случайно включить ray tracing или дорогостоящие HDRP features как функциональную зависимость;
- использовать активный local Build Profile как неявный источник CI settings;
- обновить package lock «заодно» с игровой задачей;
- добавить Unity Services, Netcode, Addressables или telemetry без решения владельца продукта;
- получить build, который нельзя связать с Git commit и versioned settings.

Нужен один contract, превращающий утверждённый стек Unity 6.3 LTS + HDRP + UI Toolkit в воспроизводимый repository scaffold и одинаковый build process для человека, Codex и CI.

---

# 3. Движущие факторы

Решение оптимизировано под:

1. Windows 10/11 x64 MVP.
2. Публичный GitHub repository.
3. Unity 6.3 LTS и длительную поддержку проекта.
4. HDRP при функциональной независимости от тяжёлой графики.
5. UI Toolkit как основной runtime UI.
6. Single-source Core packages и dual Unity/.NET compilation.
7. Быстрые Mono iterations и обязательную IL2CPP проверку.
8. Codex tasks с минимальным количеством скрытых Editor state.
9. Reproducible package restore.
10. Git-friendly text serialization.
11. Детерминированные tests и explicit bootstrap lifecycle.
12. Отсутствие прежнего production-кода и необходимость чистого стартового scaffold.
13. Возможность сменить отдельные settings через measured PR без переписывания всей архитектуры.
14. Отделение Unity presentation/runtime от authoritative Domain/Application.

---

# 4. Термины

## 4.1 Editor baseline

Точная пара `Unity version + changeset`, которой разрешено открывать, импортировать и собирать проект.

## 4.2 Repository project state

Закоммиченные `Assets`, `Packages`, `ProjectSettings` и связанные configuration files. После первого commit именно они являются источником истины, а не Unity Hub template.

## 4.3 Build profile

Версионируемая конфигурация platform/backend/development options, применяемая build automation. Не следует полагаться на UI-selected active profile.

## 4.4 Quality profile

Набор Quality/HDRP assets и settings, определяющих визуальное качество без изменения функциональной логики.

## 4.5 Unity package graph

Полный resolved graph из `manifest.json` и `packages-lock.json`, включая transitive dependencies.

## 4.6 Unity Client

Верхний presentation/composition module из ADR-001, зависящий от Unity API и собирающий adapters/application graph.

## 4.7 Build smoke

Минимальный автоматизированный запуск Player, подтверждающий startup до `Ready`, корректный BuildIdentity, отсутствие fatal errors и clean shutdown.

---

# 5. Точный Editor baseline

## 5.1 Принятая версия

```text
Unity Editor: 6000.3.20f1
Release line: Unity 6.3 LTS
Changeset: c9ba695d4f07
Release date: 16 July 2026
```

На дату принятия ADR это последний опубликованный Unity 6.3 LTS patch, найденный в официальном Unity release archive.

## 5.2 ProjectVersion.txt

Ожидаемый contract:

```text
m_EditorVersion: 6000.3.20f1
m_EditorVersionWithRevision: 6000.3.20f1 (c9ba695d4f07)
```

CI и `scripts/verify-repository.ps1` сравнивают обе строки с approved configuration.

## 5.3 Установка Editor

Минимальные компоненты:

- Unity Editor Windows x64 `6000.3.20f1`;
- Windows Build Support (IL2CPP);
- требуемый Unity-bundled toolchain;
- Visual Studio/Build Tools C++ prerequisites, если их требует установленный IL2CPP module;
- Unity Hub допускается как installer, но не является частью build contract.

Документация, Android/iOS/UWP/Web modules и другие targets не обязательны.

## 5.4 Запрет version drift

Если проект открыт другой версией:

- изменения не коммитятся;
- YAML/ProjectSettings churn не принимается как «автоматическое обновление»;
- разработчик возвращается на approved Editor либо создаёт отдельную upgrade task.

---

# 6. Создание нового проекта

## 6.1 Initial template

PR-001 создаёт проект официальным HDRP template, совместимым с `6000.3.20f1`.

После создания:

1. удаляются sample/demo scenes и template tutorial assets;
2. удаляются ненужные Unity Services и collaboration packages;
3. создаётся Odyssey folder/package structure;
4. применяются settings этого ADR;
5. фиксируются `manifest.json`, `packages-lock.json`, `ProjectSettings` и `.meta`;
6. выполняется clean reopen без Console errors;
7. template больше не используется как способ восстановления проекта.

## 6.2 Запрет повторной генерации

Нельзя пересоздать проект из нового template и поверх него скопировать `Assets`. Восстановление выполняется clean checkout repository и package restore.

---

# 7. Физическая структура Unity project

```text
/
├─ Assets/
│  └─ Odyssey/
│     ├─ Client/
│     │  ├─ Runtime/
│     │  ├─ UI/
│     │  ├─ Scenes/
│     │  ├─ Settings/
│     │  ├─ Editor/
│     │  └─ Tests/
│     └─ SharedAssets/
├─ Packages/
│  ├─ manifest.json
│  ├─ packages-lock.json
│  ├─ com.odyssey.domain/
│  ├─ com.odyssey.rules/
│  ├─ com.odyssey.application/
│  ├─ com.odyssey.content/
│  ├─ com.odyssey.persistence/
│  └─ com.odyssey.networking/
├─ ProjectSettings/
├─ UserSettings/              # ignored
├─ Library/                   # ignored
├─ Logs/                      # ignored
└─ artifacts/                 # ignored except explicit evidence
```

`Assets/Odyssey/Client` не становится местом для Domain/Rules/Application implementation.

---

# 8. Package baseline

## 8.1 Обязательные package capabilities

Обязательны:

- High Definition Render Pipeline;
- Input System;
- Unity Test Framework;
- встроенные Unity modules, необходимые для Windows Player, audio, image loading и runtime UI;
- embedded Odyssey packages.

UI Toolkit является частью Unity platform/editor и не требует выбора альтернативной UI framework package.

## 8.2 Отложенные packages

Не входят в PR-001 без отдельной задачи:

- Addressables;
- Netcode for GameObjects;
- Unity Transport;
- Unity Gaming Services;
- Authentication/Lobby/Relay SDK;
- Cinemachine;
- Timeline;
- Entities/DOTS;
- Burst/Collections как прямая dependency Odyssey;
- VFX Graph, если он не является transitive/core HDRP dependency;
- Performance Test Framework;
- UI Toolkit Test Framework;
- crash reporting/analytics/ads/IAP;
- localization package;
- third-party DI/logging/serialization packages.

Пакет может появиться позже только в задаче соответствующего slice.

## 8.3 Package version source

Точные package versions определяются resolved graph официального Editor/template и фиксируются repository lock-файлом.

ADR не использует `@latest`. Для code review значимы конкретные изменения `manifest.json` и `packages-lock.json`.

## 8.4 Registry policy

Разрешены по умолчанию:

- Unity registry packages;
- embedded local packages `com.odyssey.*` из текущего repository.

Требуют отдельного approval:

- scoped registries;
- Git URLs;
- tarball URLs;
- package from disk;
- package fork;
- package без ясной лицензии;
- package с invalid signature.

Начиная с Unity 6.3 Package Manager проверяет цифровые подписи tarball packages; signature warning/error не игнорируется в CI.

## 8.5 Lock discipline

- оба package files коммитятся;
- lock-файл не regenerates в unrelated PR;
- package update — отдельный PR;
- package PR содержит reason, license review, release notes и test evidence;
- transitive package change объясняется;
- preview/experimental suffix блокируется repository check;
- Package Manager auto-resolve не является разрешением на merge.

---

# 9. Render Pipeline baseline

## 9.1 HDRP authority

HDRP является единственным render pipeline проекта.

`GraphicsSettings.defaultRenderPipeline` и quality overrides обязаны ссылаться на Odyssey-owned HDRP assets, а не на immutable package samples.

## 9.2 Settings assets

Минимальный набор:

```text
Assets/Odyssey/Client/Settings/Rendering/
├─ OdysseyHDRPGlobalSettings.asset
├─ OdysseyHDRP_Low.asset
├─ OdysseyHDRP_Medium.asset
├─ OdysseyHDRP_High.asset
├─ OdysseyVolume_Low.asset
├─ OdysseyVolume_Medium.asset
└─ OdysseyVolume_High.asset
```

Имена могут уточняться implementation task, но количество и ownership не меняются.

## 9.3 Quality profiles

| Profile | Назначение | Baseline |
|---|---|---|
| Low | minimum visual cost | дорогие эффекты выключены, UI/board читаемы |
| Medium | default | целевой баланс 1080p/60 |
| High | повышенное качество | более дорогие shadows/lighting/post-processing |

Точные shadow, AA, volumetric и post-processing values относятся к versioned settings assets и performance tasks, а не к игровым правилам.

## 9.4 Запрещённая функциональная зависимость

Нельзя делать функционально важными:

- bloom;
- volumetric fog;
- color grading;
- ray-traced effects;
- HDR monitor output;
- post-processing-only highlighting;
- частицу/свет без альтернативного UI indicator.

Grid, token state, Fog of War, selection, permissions и warnings должны оставаться понятными на Low.

## 9.5 Отложенные features

По умолчанию не включаются:

- hardware ray tracing;
- path tracing;
- DLSS/FSR/XeSS integration;
- HDR display support;
- Streaming Virtual Texturing;
- dynamic resolution;
- custom render pipeline fork.

Их добавление требует benchmark и отдельной задачи; ray tracing/custom pipeline требует ADR amendment.

---

# 10. Graphics API и Windows Player

## 10.1 Platform

```text
Target: Standalone Windows
Architecture: x86-64
Operating systems: Windows 10 / Windows 11
```

UWP и Windows ARM64 не являются fallback targets.

## 10.2 Graphics APIs

`Auto Graphics API for Windows` выключен.

Порядок:

1. Direct3D 12;
2. Direct3D 11.

Если D3D12 initialization/driver path не работает, Player может использовать D3D11 fallback. Vulkan не входит в required test matrix.

## 10.3 Color и display

- Color Space: Linear;
- default Player window — resizable desktop window;
- functional acceptance и performance runs используют 1920×1080;
- конкретный first-launch fullscreen/window preference относится к UX/settings task;
- HDR display output не требуется;
- display resolution не влияет на authoritative coordinates/rules.

## 10.4 GPU capability

HDRP требует современный GPU capability. Конкретная minimum GPU/VRAM model не фиксируется до benchmark matrix; build обязан корректно сообщать несовместимость, а не silently corrupt rendering.

---

# 11. UI Toolkit baseline

## 11.1 Основной UI

Runtime UI строится через:

- UXML — структура;
- USS — стили;
- C# Presenter/ViewModel/Controller — поведение;
- Application Commands/Queries — действия.

## 11.2 Panel settings

Project-owned PanelSettings и theme/style assets хранятся в:

```text
Assets/Odyssey/Client/Settings/UI/
```

Package sample assets не используются как mutable project settings.

## 11.3 Запреты

- бизнес-логика в `VisualElement` subclass запрещена;
- UI callbacks не обращаются к SQLite/transport напрямую;
- scene object не хранит authoritative campaign state;
- uGUI `Canvas`/`EventSystem` не создаётся как второй UI stack без отдельного исключения;
- IMGUI допускается только в Editor tooling/diagnostic fallback, не как runtime product UI.

## 11.4 UI Toolkit tests

Presenter/ViewModel проверяются pure .NET tests, а Unity integration — EditMode/PlayMode согласно ADR-006.

---

# 12. Input System baseline

## 12.1 Player setting

```text
Active Input Handling: Input System Package (New)
```

`Both` и legacy-only запрещены для нового проекта.

## 12.2 Input asset

Создаётся project-owned input actions asset:

```text
Assets/Odyssey/Client/Settings/Input/OdysseyInputActions.inputactions
```

Начальный shell может содержать только общие presentation actions:

- Point;
- PrimaryAction;
- SecondaryAction;
- Submit;
- Cancel;
- Navigate;
- Scroll;
- Pan;
- Zoom.

Они не являются Domain commands до явного mapping в presenter/application boundary.

## 12.3 Generated wrapper

Если включается C# wrapper generation, generated file должен быть детерминированным, находиться рядом с input asset и коммититься либо воспроизводимо создаваться repository script. Смешанная ручная правка generated wrapper запрещена.

## 12.4 Rebinding

Пользовательский rebind storage является versioned configuration и реализуется отдельной UX/config task. PR-001 не обязан реализовывать rebind UI.

---

# 13. Asset и Editor settings

Обязательные значения:

```text
Version Control Mode: Visible Meta Files
Asset Serialization Mode: Force Text
Enter Play Mode Options: disabled
Line endings in repository: LF
```

Правила:

- `.meta` создаётся Unity и не копируется случайно между assets;
- GUID не регенерируется ради «исправления» ссылки;
- `Library`, `Temp`, `Obj`, `Logs`, local builds, `UserSettings` игнорируются;
- `.unity`, `.prefab`, `.asset`, `.uxml`, `.uss`, `.inputactions`, `.asmdef` остаются обычными Git files, не Git LFS;
- large source art/audio follows Git LFS policy Technical Baseline;
- UnityYAMLMerge настраивается как merge tool, но конфликт scene/prefab не разрешается автоматическим выбором одной стороны без validation;
- mass reserialize выполняется отдельным PR;
- Editor cache/Accelerator является optional local optimization и не source of truth.

---

# 14. Scene baseline

## 14.1 Scenes

Минимум:

```text
Assets/Odyssey/Client/Scenes/Bootstrap.unity
Assets/Odyssey/Client/Scenes/AppShell.unity
```

## 14.2 Bootstrap scene

`Bootstrap.unity`:

- имеет build index 0;
- содержит один минимальный `AppRuntimeHost`;
- читает build/config bootstrap input;
- создаёт composition root ADR-005;
- запускает application runtime;
- загружает `AppShell.unity`;
- показывает fatal startup screen при неуспехе;
- выполняет idempotent shutdown.

Она не содержит campaign content, board, combat objects или демонстрационные assets.

## 14.3 AppShell scene

`AppShell.unity`:

- содержит presentation roots/UIDocument references;
- показывает application version/build identity;
- показывает health/status Developer Shell в development profiles;
- не создаёт persistence/network services;
- может выгружаться и загружаться повторно без потери process/campaign ownership.

## 14.4 Runtime root

Разрешён только один persistent runtime root. Любой второй bootstrap обнаруживается и завершается с diagnostic error согласно ADR-005.

Случайные `DontDestroyOnLoad` objects запрещены.

---

# 15. Assembly baseline

## 15.1 Unity Client assemblies

Минимум:

```text
Odyssey.Unity.Client.Runtime.asmdef
Odyssey.Unity.Client.Editor.asmdef
Odyssey.Tests.Unity.EditMode.asmdef
Odyssey.Tests.Unity.PlayMode.asmdef
```

Core assembly definitions следуют ADR-001/ADR-006.

## 15.2 Assembly properties

- explicit references;
- no cyclic references;
- Editor assembly имеет Editor platform constraint;
- test assemblies используют testAssembly flag;
- production assemblies не ссылаются на test assemblies;
- autoReferenced выключается там, где это предотвращает обход границ;
- unsafe code выключен;
- overrideReferences используется только при документированной необходимости.

## 15.3 Defines

Допустимые baseline defines:

```text
ODYSSEY_DEVELOPMENT
ODYSSEY_DIAGNOSTICS
ODYSSEY_PROFILING
ODYSSEY_RELEASE_CANDIDATE
ODYSSEY_RELEASE
```

Define может включать overlay, profiler markers или extra validation. Он не может менять:

- Domain rules;
- command/event payload;
- serialization contract;
- persistence semantics;
- permission behavior;
- network protocol result.

---

# 16. Scripting backend и AOT

## 16.1 Development

`Development-Debug` и `Development-Profile` используют Mono для быстрого iteration loop.

Это не освобождает от AOT-compatible design.

## 16.2 Release Candidate и Release

Используют:

```text
Scripting Backend: IL2CPP
Architecture: x86-64
Api Compatibility Level: .NET Standard
```

## 16.3 Managed stripping

M1 baseline — `Low`.

Повышение stripping level требует:

- source-generated serialization tests;
- linker report review;
- IL2CPP contract vectors;
- bootstrap/player smoke;
- explicit `link.xml` review, если он появляется.

Широкий `link.xml` с preserve-all для всего проекта запрещён как скрытие AOT проблем.

## 16.4 Reflection

Reflection в Core/runtime разрешена только если:

- не находится на критическом path;
- имеет IL2CPP test;
- не используется как неявная service discovery/serialization registry;
- preservation requirements явно описаны.

---

# 17. Build profiles

| Profile | Backend | Development | Diagnostics | Назначение |
|---|---|---:|---|---|
| Development-Debug | Mono | да | полный local | ежедневная разработка |
| Development-Profile | Mono | да | profiler markers | performance investigation |
| Release-Candidate | IL2CPP | нет | безопасный production-like | RC validation |
| Release | IL2CPP | нет | минимальный safe | tagged release |

## 17.1 Development-Debug

- script debugging включён;
- development console/overlay разрешён;
- extra assertions включены;
- build может быть dirty и маркируется ADR-007;
- не содержит production secrets.

## 17.2 Development-Profile

- profiler support включается build script;
- deep profiling по умолчанию выключен;
- visual quality representative, default Medium;
- profiler instrumentation не меняет authoritative result.

## 17.3 Release-Candidate

- clean commit предпочтителен и обязателен для publishable RC;
- IL2CPP;
- no development build;
- Low/Medium/High profiles включены;
- full smoke, persistence/serialization compatibility и clean install checks;
- build identity/channel = ReleaseCandidate.

## 17.4 Release

- только protected `main` tagged commit;
- IL2CPP;
- no debugging/development flags;
- no developer shell controls, cheats или private docs;
- signed artifact/code signing решается deployment ADR, но отсутствие code signing не меняет build identity;
- artifact immutable.

---

# 18. Build automation

## 18.1 Editor entry point

Создаётся versioned Editor class, например:

```text
Odyssey.Unity.Client.Editor.Build.OdysseyBuildEntryPoint
```

Он принимает profile/output/build identity input, применяет settings, валидирует их и запускает build.

## 18.2 PowerShell entry points

```text
scripts/build-dev.ps1
scripts/build-release.ps1
scripts/test-unity.ps1
```

На M1 допускается общий internal build script с разными profile parameters.

## 18.3 Headless invocation

CI-compatible command использует exact Editor:

```text
Unity.exe \
  -batchmode \
  -nographics \
  -quit \
  -projectPath <repo-root> \
  -executeMethod <build-entry-point> \
  <profile/output arguments>
```

Exact CLI shape фиксируется implementation, но manual UI state не используется.

## 18.4 Validation before build

Build прекращается, если:

- Editor version mismatch;
- package restore/signature error;
- compile error;
- required asset/scene missing;
- unexpected active render pipeline;
- Graphics API order mismatch;
- backend/architecture mismatch;
- required BuildIdentity отсутствует;
- Release build dirty/not tagged;
- private documentation или secret попали в staging output;
- test-only assembly включается в Player.

## 18.5 Output

Рекомендуемый layout:

```text
artifacts/builds/<BuildId>/Windows-x64/
├─ OdysseyVTT.exe
├─ OdysseyVTT_Data/
├─ UnityPlayer.dll
├─ build-identity.json
├─ checksums.sha256
└─ build-report.json
```

Local paths могут отличаться, но artifact package сохраняет эту логическую структуру.

---

# 19. Build identity integration

Перед build:

1. читается `version.json`;
2. вычисляется BuildIdentity ADR-007;
3. generated runtime build info создаётся в temporary/generated area;
4. значение отображается в Developer/Status panel;
5. `build-identity.json` копируется рядом с Player;
6. generated secret/private data отсутствует;
7. после build temporary data очищается либо проверяется как deterministic generated file.

Unity `PlayerSettings.bundleVersion` является отражением ApplicationVersion, а не независимым source of truth.

---

# 20. Development и Player smoke

Минимальный smoke подтверждает:

1. Player process запускается;
2. `Bootstrap.unity` достигает startup phase `Ready`;
3. `AppShell` загружен;
4. BuildIdentity доступен;
5. HDRP pipeline активен;
6. UI Toolkit root отображается;
7. Input System и Cancel/Submit path работают;
8. fatal Console/Player log errors отсутствуют;
9. clean shutdown завершается без зависших background operations;
10. exit code/evidence сохраняется.

IL2CPP smoke обязателен до закрытия M1.

---

# 21. Unity tests

Согласно ADR-006:

- EditMode проверяет assemblies, assets, package/config consistency и Editor adapters;
- PlayMode проверяет bootstrap, scenes, UI Toolkit runtime, input и shutdown;
- Windows Player smoke проверяет реальный built artifact;
- pure .NET остаётся основным контуром Domain/Rules/Application.

Unity test не дублирует pure .NET test без проверки Unity integration risk.

---

# 22. Обязательные `SLICE-00` test cases

Минимальный набор:

| TestCaseId | Сценарий |
|---|---|
| `TST-UNI-001` | ProjectVersion соответствует `6000.3.20f1` и revision |
| `TST-UNI-002` | Package manifest/lock присутствуют и parseable |
| `TST-UNI-003` | Preview/experimental package отсутствует |
| `TST-UNI-004` | Custom scoped registry отсутствует |
| `TST-UNI-005` | Force Text и Visible Meta Files включены |
| `TST-UNI-006` | HDRP является current render pipeline |
| `TST-UNI-007` | Low/Medium/High имеют project-owned HDRP assets |
| `TST-UNI-008` | Medium является default quality |
| `TST-UNI-009` | Auto Graphics API Windows выключен |
| `TST-UNI-010` | D3D12 идёт перед D3D11 |
| `TST-UNI-011` | Ray tracing не является required/default feature |
| `TST-UNI-012` | Color Space = Linear |
| `TST-UNI-013` | Active Input Handling = New Input System only |
| `TST-UNI-014` | Bootstrap scene index 0 и AppShell присутствует |
| `TST-UNI-015` | Второй AppRuntime не создаётся |
| `TST-UNI-016` | Scene unload освобождает UI subscriptions |
| `TST-UNI-017` | Core assemblies не зависят от UnityEngine |
| `TST-UNI-018` | Test assemblies не попадают в Player |
| `TST-UNI-019` | Development-Debug Mono build проходит |
| `TST-UNI-020` | Release-Candidate IL2CPP x64 build проходит |
| `TST-UNI-021` | IL2CPP Player startup достигает Ready |
| `TST-UNI-022` | BuildIdentity в UI и sidecar совпадает |
| `TST-UNI-023` | Build script не зависит от active local profile |
| `TST-UNI-024` | Build output не создаётся внутри Assets/Packages |
| `TST-UNI-025` | Dirty Release build отклоняется |
| `TST-UNI-026` | Package lock drift обнаруживается |
| `TST-UNI-027` | Invalid package signature/restore failure блокирует build |
| `TST-UNI-028` | Low quality сохраняет читаемость grid/token/status UI |
| `TST-UNI-029` | Enter Play Mode reload settings соответствуют baseline |
| `TST-UNI-030` | Repeated startup/shutdown не оставляет persistent objects/tasks |

TestCaseId могут уточняться traceability matrix, но semantics сохраняются.

---

# 23. CI contract

Конкретная GitHub Action/Unity license provider отложены, но pipeline обязан:

1. использовать exact Unity version;
2. pin action by immutable commit SHA;
3. не выдавать license secrets fork PR;
4. не печатать license/token;
5. отличать skipped от passed;
6. сохранять Unity test results и Player logs;
7. создавать Windows artifact через тот же script, что локально;
8. выполнять Mono dev build на PR после появления license-enabled runner;
9. выполнять IL2CPP build на main/RC и в обязательном M1 validation;
10. не использовать cache как замену clean restore test.

До готовности licensed CI owner выполняет documented local Unity validation; это не может маркироваться как CI passed.

---

# 24. Package и Editor upgrade policy

## 24.1 Patch upgrade

Переход `6000.3.20f1 → 6000.3.x` требует отдельного PR:

1. ссылка на official release notes;
2. review known issues;
3. backup/branch point;
4. update ProjectVersion;
5. review manifest/lock diff;
6. clean deletion/reimport `Library` на validation machine;
7. pure .NET tests;
8. Unity EditMode/PlayMode;
9. Mono Windows build/smoke;
10. IL2CPP Windows build/smoke;
11. serialization/Clock/RNG cross-runtime vectors;
12. owner approval.

## 24.2 Minor/LTS upgrade

Переход на `6000.4`, `6000.5` или другую line требует superseding/amended ADR, даже если Unity называет её production-ready.

## 24.3 Package-only update

Graphics core packages не обновляются вручную на несовместимую line с approved Editor. Любой override package from disk требует ADR/explicit task.

## 24.4 Rollback

Upgrade PR сохраняет возможность полного rollback одним revert без migration persisted content, если изменение не было опубликовано. Если новая версия создала irreversible asset migrations, upgrade plan обязан содержать backup/rollback evidence.

---

# 25. Security и public repository

- Unity license data отсутствует в repository;
- machine identifiers и activation files отсутствуют;
- package registry credentials отсутствуют;
- private product documentation не встраивается в Resources/StreamingAssets/Player;
- sample/placeholder assets имеют разрешённую для публичного repository лицензию;
- build script проверяет staged files на known secret patterns;
- Editor.log/Player.log публикуются только после redaction ADR-010;
- third-party package notices обновляются вместе с dependency PR.

---

# 26. Performance baseline

ADR-009 не утверждает minimum hardware, но фиксирует test contract:

- primary test resolution: 1920×1080;
- target: stable 60 FPS на representative medium-level PC;
- minimum acceptable profile: 30 FPS;
- Low/Medium/High проверяются отдельно;
- Development-Profile не считается Release performance из-за instrumentation;
- CPU/GPU frame timing снимаются на built Player;
- Editor FPS не является release evidence;
- D3D11 и D3D12 compatibility фиксируются; full performance focus — primary D3D12 path;
- feature включается по измерению, не по предположению.

---

# 27. Codex rules

Codex обязан:

1. использовать exact Editor baseline;
2. не обновлять package graph без scope задачи;
3. не добавлять Unity Services/Addressables/Netcode/DI/telemetry скрыто;
4. не переключать Input Handling в `Both`;
5. не создавать uGUI fallback без approval;
6. не хранить Domain/Application code в Unity scene/component;
7. не создавать второй bootstrap/singleton graph;
8. не полагаться на вручную выбранный build profile;
9. запускать указанные build/test scripts;
10. честно указывать, если Unity/IL2CPP check не был выполнен;
11. не менять ProjectSettings массово unrelated task;
12. включать manifest/lock diff explanation;
13. не заявлять Mono-only behavior готовым;
14. не менять quality settings так, чтобы Low терял функциональную информацию;
15. не повышать Unity version самостоятельно.

---

# 28. Результат PR-001

PR-001 считается готовым, когда существует:

- Unity `6000.3.20f1` project;
- точный `ProjectVersion.txt`;
- HDRP project settings/assets;
- Low/Medium/High quality assets;
- UI Toolkit AppShell;
- Input System asset и New-only setting;
- Force Text/Visible Meta Files;
- Bootstrap/AppShell scenes;
- минимальные Client `.asmdef`;
- package manifest/lock без preview dependencies;
- Windows D3D12→D3D11 settings;
- Mono Development-Debug build;
- IL2CPP compatibility/build evidence либо отдельный PR-004/PR-005 task, но не позже M1 exit;
- repository verification tests;
- отсутствие Console errors при clean open.

---

# 29. Последствия

Положительные:

- одинаковый Editor и package graph;
- меньше случайных YAML/package diffs;
- build воспроизводим локально и в CI;
- раннее обнаружение IL2CPP/AOT проблем;
- UI/Input/Rendering не переизобретаются каждой задачей;
- Codex получает точный scaffold;
- quality/performance изменения отделены от игровых правил;
- upgrade можно review и rollback.

Стоимость:

- IL2CPP build медленнее;
- три HDRP quality assets требуют поддержки;
- patch upgrade становится формальной задачей;
- package changes требуют evidence;
- строгий bootstrap ограничивает быстрые scene-singleton prototypes;
- Unity license CI остаётся отдельной операционной задачей.

Эта стоимость принята ради снижения архитектурного и release риска.

---

# 30. Отклонённые альтернативы

## 30.1 «Любой Unity 6.3 patch»

Отклонено: serialized assets и packages могут отличаться между patch versions.

## 30.2 Unity 6.5 Supported release

Отклонено: проект уже утвердил 6.3 LTS; переход не даёт достаточной выгоды для стартового scaffold и сокращает стабильность baseline.

## 30.3 URP вместо HDRP

Отклонено решением владельца продукта.

## 30.4 Built-in Render Pipeline

Отклонено решением владельца продукта и несовместимо с HDRP asset strategy.

## 30.5 Один Release build на Mono

Отклонено: не проверяет AOT/IL2CPP contract и не соответствует production baseline.

## 30.6 IL2CPP для каждой локальной итерации

Отклонено: слишком медленно; Mono разрешён как fast development path.

## 30.7 Auto Graphics API

Отклонено: не даёт контролировать primary/fallback API и усложняет evidence.

## 30.8 Только DirectX 12

Не принято на M1: D3D11 fallback снижает риск несовместимых drivers, пока не утверждена hardware matrix.

## 30.9 `Both` input systems

Отклонено: сохраняет legacy path и допускает разные input semantics.

## 30.10 Service locator через scene objects/ScriptableObject

Отклонено ADR-005.

## 30.11 Addressables с первого commit

Отложено: пока нет подтверждённой потребности и physical content-package decision.

## 30.12 Автоматическое обновление packages

Отклонено: package update должен быть reviewable intentional change.

## 30.13 Одна HDRP quality asset для всех профилей

Отклонено: не даёт безопасно отключать тяжёлые features на Low.

## 30.14 Domain Reload disabled по умолчанию

Отклонено для M1: может скрывать static/lifecycle leaks; сначала correctness.

---

# 31. Не входит в ADR-009

- конкретный GitHub Actions Unity provider;
- способ приобретения/активации Unity license;
- installer и auto-update;
- Steam/GitHub Releases distribution;
- production code signing certificate;
- crash reporting/telemetry;
- final minimum/recommended hardware;
- final HDRP numeric quality values;
- localization tooling;
- Addressables;
- network/relay SDK;
- SQLite provider;
- `.odcamp` physical archive implementation;
- asset content pipeline DCC tools;
- final UI/UX visual design;
- hardware ray tracing roadmap.

---

# 32. Связь с предыдущими ADR

- ADR-001 определяет, какие assemblies/modules может содержать Unity Client.
- ADR-003 требует System.Text.Json AOT/IL2CPP compatibility.
- ADR-005 определяет AppRuntime/composition root и scene lifecycle.
- ADR-006 определяет Unity/.NET test split и source parity.
- ADR-007 поставляет BuildIdentity, channels и artifact naming.
- ADR-008 запрещает использовать Unity Time/Random в authority.

ADR-009 не изменяет эти решения, а задаёт физическую Unity/build среду их выполнения.

---

# 33. Связь с ADR-010

ADR-010 обязан дополнительно определить:

- обработку Unity Console/Player log;
- fatal startup diagnostics;
- redaction Editor/Player/build logs;
- diagnostic bundle;
- uncaught exception handling;
- build/smoke diagnostic codes.

ADR-009 фиксирует только точки интеграции.

---

# 34. Traceability

| Решение | Источник |
|---|---|
| Unity 6.3 LTS | решение владельца продукта, Technical Baseline |
| Exact patch pin | Technical Baseline, ADR-007 |
| HDRP | решение владельца продукта |
| UI Toolkit | решение владельца продукта |
| Input System | Technical Baseline |
| Windows 10/11 x64 | решение владельца продукта, MVP Scope |
| Core embedded packages | ADR-001, ADR-006 |
| One bootstrap runtime | ADR-005 |
| Mono dev + IL2CPP RC | Technical Baseline, ADR-003/006 |
| D3D12 + D3D11 fallback | Technical Baseline |
| Low/Medium/High | performance decision владельца продукта |
| Package lock | Technical Baseline, public dependency policy |
| BuildIdentity | ADR-007 |
| No Unity Time/Random authority | ADR-008 |

---

# 35. Внешние основания

На дату принятия использованы официальные Unity sources:

- Unity `6000.3.20f1` release page и changeset;
- Unity 6.3 LTS Manual;
- Unity documentation по UI Toolkit runtime;
- Unity documentation по Input System;
- Unity documentation по активному Render Pipeline Asset;
- Unity documentation по Package Manager signatures начиная с Unity 6.3;
- Unity documentation по build pipeline и Windows/IL2CPP modules.

Current/latest online pages не заменяют pinned 6000.3 documentation и repository lock files.

---

# 36. Вступление в силу

С даты принятия:

- Unity baseline проекта — `6000.3.20f1 (c9ba695d4f07)`;
- PR-001 обязан следовать этому ADR;
- другой Editor/package baseline не может быть выбран Codex самостоятельно;
- Unity project settings/build profiles проверяются автоматически;
- Mono является development optimization, а IL2CPP — обязательным release/M1 evidence;
- изменение решения требует ADR-009 amendment либо нового superseding ADR.

---

**Конец документа**
