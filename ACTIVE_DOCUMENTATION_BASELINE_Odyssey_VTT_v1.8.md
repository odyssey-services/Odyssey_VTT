# Odyssey VTT — Active Documentation Baseline

**Документ:** `ACTIVE_DOCUMENTATION_BASELINE_Odyssey_VTT_v1.8.md`
**Версия:** 1.8
**Дата:** 10 августа 2026 года
**Статус:** Superseded by `ACTIVE_DOCUMENTATION_BASELINE_Odyssey_VTT_v1.9.md`

**Материальное изменение v1.8:** принят `docs/adr/ADR-009_Unity_Project_and_Build_Baseline_v1.1.md`, который supersedes ADR-009 v1.0 only for the Unity Editor/package baseline and pins Unity `6000.4.0f1 (8cf496087c8f)` with HDRP `com.unity.render-pipelines.high-definition` `17.4.0`. Registered `TECHNICAL_DEVELOPMENT_BASELINE_Odyssey_VTT_v0.3.md`; no MVP scope, repository privacy, or architecture boundary is changed.

**Материальное изменение v1.7:** зарегистрирован `TECHNICAL_DEVELOPMENT_BASELINE_Odyssey_VTT_v0.2.md`, который фиксирует решение владельца: `odyssey-services/Odyssey_VTT` остаётся Private и является единственным authoritative code repository. Private visibility не ослабляет запрет на private product documentation, secrets и archives в Git history; дальнейшие substantive changes проходят только через branch → pull request → owner review → owner merge.

**Материальное изменение v1.6:** добавлен исполнимый пакет `SLICE-00`: parent task `ODY-S00-000`, living ExecPlan, упорядоченный backlog из десяти задач и первая Ready-задача `ODY-S00-001 — Repository Foundation`. Delivery-группы Technical Baseline `PR-000–PR-005` сохранены по результатам, но широкие Core/CI группы разделены на review-safe task/PR units без изменения архитектуры или MVP. В комплекте оставлен только текущий Active Baseline; исторические версии реестра удалены как дубли источника истины.

**Материальное изменение v1.5:** добавлены `docs/tasks/TASK_TEMPLATE.md` и правила жизненного цикла task contracts. Шаблон задаёт обязательные Goal/Scope/Authorities/Acceptance/Validation/Compatibility/Security/Definition of Done/Completion Evidence поля, связывает отдельную задачу с `PLANS.md` и ExecPlan, запрещает фиктивные результаты проверок и не позволяет задаче переопределять MVP или ADR. `AGENTS.md` и `PLANS.md` синхронизированы с новым task workflow без изменения архитектурных или продуктовых решений.

**Материальное изменение v1.4:** добавлен корневой `PLANS.md` как обязательный operational contract для планирования работы Codex. Он разделяет brief plan и repository ExecPlan, задаёт триггеры, структуру живого плана, milestone/evidence rules, privacy constraints, change control, recovery/rollback и критерии завершения. `PLANS.md` не является источником продуктовых или архитектурных решений и не может переопределять Active Baseline, ADR или Technical Development Baseline.

**Материальное изменение v1.3:** добавлен корневой `AGENTS.md` как обязательный operational contract для Codex. Он фиксирует source routing, task planning triggers, module boundaries, command/event rules, serialization, Result/Error, composition, Clock/RNG, diagnostics, Unity baseline, dependency licensing, validation commands, Git/PR workflow и code review blockers. `AGENTS.md` является кратким исполнимым резюме и не может отменять или переопределять принятые ADR.

**Материальное изменение v1.2:** принят ADR-010 с process-scoped structured diagnostics runtime, typed EventCode/allowlist properties, локальными memory/rolling JSONL/emergency sinks, bounded queue/backpressure, CorrelationId/DiagnosticId, crash markers, rotation/retention, explicit diagnostic sessions, allowlisted diagnostic bundles и обязательной redaction данных классов Personal/HiddenGameplay/Secret до любого sink.


**Материальное изменение v1.1:** принят ADR-009 v1.0 с точным Unity `6000.3.20f1 (c9ba695d4f07)`, repository-owned HDRP/UI Toolkit/Input System baseline, package lock policy, Bootstrap/AppShell scenes, D3D12→D3D11 Windows graphics matrix, Low/Medium/High quality assets, Mono development и IL2CPP RC/Release profiles, automated build validation и Unity patch upgrade procedure.

**Материальное изменение v1.0:** принят ADR-008 с разделением host wall UTC, process monotonic time, campaign WorldClock и presentation time; custom Clock/Scheduler ports, durable deadline semantics, host-secret campaign RNG key epochs, HMAC-SHA-256 stream derivation v1, xoshiro256** v1, rejection mapping без modulo bias, retry/replay rules, non-secret RngProofData и обязательными .NET/Unity/IL2CPP contract vectors.

**Материальное изменение v0.9:** принят ADR-007 с независимыми Application/Build/Schema/Format/Contract/Protocol/Ruleset версиями, корневыми `version.json` и `config/compatibility.json`, каноническим BuildIdentity, release channels, immutable Git tags, artifact provenance/checksums и обязательным `SLICE-00` version scaffold. Также удалён случайный повтор описания ADR-006 в реестре authorities.

**Материальное изменение v0.8:** принят ADR-006 с single-source dual Unity/.NET compilation, Core bridge projects `netstandard2.1`, pinned .NET 10 LTS test host, NUnit/Unity Test Framework runners, test project taxonomy, source inventory parity, shared compatibility vectors и обязательным `SLICE-00` test scaffold.

**Материальное изменение v0.7:** принят ADR-005 с единственным production composition root в Unity Client, manual constructor injection, явными Process/Campaign/Session/Operation/Presentation scopes, typed runtime factories, Unity bootstrap lifecycle, resource ownership, отдельной test composition и запретом service locator/DI container по умолчанию.

**Материальное изменение v0.6:** принят ADR-004 с единым Application Result/Error contract, стабильными ErrorCode/SafeReasonCode, структурированным RetryDirective, разделением terminal CommandResult.Rejected и outer infrastructure failure, exception boundaries, validation details, localization и diagnostic redaction.

**Материальное изменение v0.5:** принят ADR-003 с разделением сериализационных профилей, явными versioned DTO, canonical JSON/fingerprint, immutable event payload, pure upcasters, SQLite JSON boundaries, `.odcamp` manifest rules и обязательной AOT/IL2CPP-проверкой.

**Материальное изменение v0.4:** принят ADR-002 с единой моделью Application Command и DomainEvent, канонической идемпотентностью через CommandId, atomic event batch, Pending continuation и compensation semantics.

**Материальное изменение v0.3:** принят ADR-001 с точным ациклическим графом модулей, ownership типов и обязательным автоматическим контролем зависимостей.

**Материальное изменение v0.2:** добавлен утверждённый технический baseline нового репозитория, Unity-стека, архитектурных границ, CI и правил Codex.

---

# 1. Назначение

Этот файл определяет единственный активный комплект документации для подготовки и выполнения задач разработки. Исторические handoff и changelog-файлы не являются источниками требований.

# 2. Порядок приоритета

При конфликте применяется порядок:

1. последнее явное решение владельца продукта;
2. этот active baseline;
3. принятый ADR для конкретного технического вопроса;
4. Technical Development Baseline для общей технической организации репозитория и Stage 0–1;
5. `AGENTS.md` как operational summary для Codex, не способный переопределять пункты 1–4;
6. `PLANS.md` как operational contract планирования и ведения ExecPlan, не способный переопределять пункты 1–5;
7. `docs/tasks/TASK_TEMPLATE.md` и заполненный task contract как operational contract одной единицы работы, не способные переопределять пункты 1–6;
8. специализированный контракт соответствующей подсистемы;
9. Product Requirements;
10. MVP Scope;
11. Domain Model;
12. Project Vision;
13. Roadmap;
14. Test Strategy;
15. changelog, handoff и LegacyReference — только история или техническое свидетельство.

Специализированный принятый ADR имеет приоритет над предварительной формулировкой Technical Development Baseline только в пределах явно указанной области решения. Во всех остальных вопросах Technical Development Baseline сохраняет силу.

# 3. Активные нормативные документы

```text
00_Project_Vision_Odyssey_VTT_v0.11.md
01_Product_Requirements_Odyssey_VTT_v0.14.md
02_MVP_Scope_Odyssey_VTT_v0.10.md
03_Domain_Model_Odyssey_VTT_v0.25.md
04_Odyssey_Rules_Engine_Odyssey_VTT_v0.6.md
05_Persistence_Odyssey_VTT_v0.8.md
06_Networking_and_Session_Sync_Odyssey_VTT_v0.8.md
07_Permissions_Odyssey_VTT_v0.7.md
08_Scenes_And_Board_Odyssey_VTT_v0.5.md
09_Dice_And_Game_Log_Odyssey_VTT_v0.3.md
10_Characters_And_Progression_Odyssey_VTT_v0.2.md
11_Content_Block_System_Odyssey_VTT_v0.1.md
12_Combat_And_Actions_Odyssey_VTT_v0.1.md
13_Audio_System_Odyssey_VTT_v0.3.md
15_Legacy_Prototype_Reference_Odyssey_VTT_v0.1.md
16_Test_Strategy_Odyssey_VTT_v0.1.md
17_Roadmap_Odyssey_VTT_v0.11.md
TECHNICAL_DEVELOPMENT_BASELINE_Odyssey_VTT_v0.3.md
AGENTS.md
PLANS.md
docs/tasks/TASK_TEMPLATE.md
docs/tasks/SLICE-00_BACKLOG.md
docs/tasks/active/ODY-S00-000_SLICE_00_Technical_Skeleton.md
docs/tasks/active/ODY-S00-007_Serialization_and_AOT_Compatibility_Spike.md
docs/plans/active/ODY-S00-000_SLICE_00_Technical_Skeleton.md
docs/adr/ADR-001_Module_Boundaries_and_Dependency_Direction_v1.0.md
docs/adr/ADR-002_Command_and_Domain_Event_Model_v1.0.md
docs/adr/ADR-003_Serialization_Strategy_v1.0.md
docs/adr/ADR-004_Result_and_Error_Model_v1.0.md
docs/adr/ADR-005_Dependency_Composition_v1.0.md
docs/adr/ADR-006_Test_Project_Structure_and_Dual_Unity_DotNet_Compilation_v1.0.md
docs/adr/ADR-007_Versioning_and_Build_Identity_v1.0.md
docs/adr/ADR-008_Deterministic_Clock_and_RNG_v1.0.md
docs/adr/ADR-009_Unity_Project_and_Build_Baseline_v1.1.md
docs/adr/ADR-010_Logging_Diagnostics_and_Redaction_v1.0.md
```

# 4. Активные технические authorities

`TECHNICAL_DEVELOPMENT_BASELINE_Odyssey_VTT_v0.3.md` является обязательным техническим authority для private authoritative repository `odyssey-services/Odyssey_VTT`, Unity 6.4 Update release HDRP/UI Toolkit проекта, CI и работы Codex. Он не заменяет продуктовые требования и не может расширять MVP.

`AGENTS.md` является обязательным operational contract authoritative code repository для Codex и code review. Он автоматически применяется ко всему репозиторию, маршрутизирует агента к активным ADR и задаёт обязательные проверки, ограничения scope, Git/PR workflow и Definition of Done. При любом расхождении приоритет сохраняют этот Active Baseline, принятый ADR и Technical Development Baseline; `AGENTS.md` должен быть синхронизирован с ними, а не заменять их.

`PLANS.md` является обязательным operational contract для планирования сложных и многоэтапных задач Codex. Он определяет, когда достаточно brief plan, когда требуется repository ExecPlan, где хранить активные и завершённые планы, как фиксировать milestones, progress, decisions, discoveries, validation evidence, recovery/rollback и blockers. Он не предоставляет разрешение на изменение scope или архитектуры и должен применяться совместно с `AGENTS.md`, Active Baseline и соответствующими ADR.

`docs/tasks/TASK_TEMPLATE.md` является обязательным operational contract для формулирования отдельной единицы работы. Заполненный task contract фиксирует цель, обоснование, authorities, проверенное исходное состояние, scope/non-goals, ограничения, ожидаемое поведение, deliverables, acceptance criteria, tests/validation, compatibility/migration/rollback, dependencies/licensing, security/privacy, planning mode, versioning impact, Definition of Done и фактические completion evidence. Задача не может предоставлять исключение из Active Baseline, ADR, Technical Development Baseline, `AGENTS.md` или `PLANS.md`; при необходимости исключения требуется решение владельца или новый ADR.

`docs/tasks/SLICE-00_BACKLOG.md` является активным operational backlog Stage 1. Он раскладывает принятый результат `SLICE-00` на десять зависимых задач, фиксирует exit criteria, non-goals и границы delivery-групп. Backlog не заменяет child task contracts и не может изменять Technical Baseline или ADR.

`docs/tasks/active/ODY-S00-000_SLICE_00_Technical_Skeleton.md` является parent task contract всего технического среза. Он определяет итог M1, общие acceptance criteria и связывает дочерние задачи с одним ExecPlan.

`docs/plans/active/ODY-S00-000_SLICE_00_Technical_Skeleton.md` является living ExecPlan для исполнения `SLICE-00`. Он ведёт milestones, progress, decisions, deviations, evidence, recovery и blockers; до начала разработки имеет статус Draft и не является доказательством выполнения кода или тестов.

`docs/tasks/active/ODY-S00-007_Serialization_and_AOT_Compatibility_Spike.md` is the current active task for Serialization and AOT Compatibility Spike. It is limited to ADR-003 System.Text.Json boundary DTOs, centralized serializer profiles, source-generated contexts, canonical UTF-8 JSON, parser limits, command fingerprint material, synthetic event payload hash evidence, pure upcaster spike, DiagnosticJson/LogEventV1 serialization evidence, spike-level `.odcamp` manifest fixture, and focused serialization/AOT compatibility proof; it explicitly forbids SQLite/Persistence runtime, Networking runtime, gameplay, full `.odcamp` import/export, BuildIdentity generation, GitHub Actions, ODY-S00-009 Windows Development-Debug artifact claims, Unity package/version baseline changes, and new serializer dependencies.

`docs/adr/ADR-001_Module_Boundaries_and_Dependency_Direction_v1.0.md` является принятым authority по module ownership, compile-time dependency graph, ports/adapters и assembly enforcement. Его точная матрица заменяет предварительные формулировки `limited contracts` в Technical Development Baseline.

`docs/adr/ADR-002_Command_and_Domain_Event_Model_v1.0.md` является принятым authority по command lifecycle, CommandId/idempotency, command results, DomainEvent envelopes, event batches, causality, Pending continuation, compensation и transactional publication. Его точные contracts заменяют предварительные command/event envelopes Technical Development Baseline и уточняют соответствующие разделы Domain, Persistence и Networking.

`docs/adr/ADR-003_Serialization_Strategy_v1.0.md` является принятым authority по JSON profiles, explicit versioned DTO, stable contract type/version, canonical serialization, command fingerprint, event payload hashes/upcasting, SQLite JSON boundaries, `.odcamp` manifests, parser limits и AOT/IL2CPP compatibility. Его правила заменяют любые неявные serializer defaults и запрещают прямую сериализацию Domain objects.

`docs/adr/ADR-004_Result_and_Error_Model_v1.0.md` является принятым authority по Application Result/Error, ErrorCategory, ErrorCode, SafeReasonCode, localization keys, RetryDirective, ValidationDetail, exception boundaries, diagnostic references и mappings между Domain/Application/Infrastructure/Transport/UI. Он уточняет CommandResult из ADR-002: terminal `Rejected` является авторитетным outcome внутри outer Success, а отсутствие durable terminal outcome выражается outer Result Failure.

`docs/adr/ADR-005_Dependency_Composition_v1.0.md` является принятым authority по production composition root, constructor injection, runtime profiles, Process/Campaign/Session/Operation/Presentation lifetimes, typed factories, Unity bootstrap/scene initialization, startup/shutdown, resource ownership, test composition и запрету service locator/DI container по умолчанию. Он заменяет предварительные правила раздела Dependency Composition Technical Development Baseline и применяется совместно с module graph ADR-001.

`docs/adr/ADR-006_Test_Project_Structure_and_Dual_Unity_DotNet_Compilation_v1.0.md` является принятым authority по test project structure, single-source dual compilation, `netstandard2.1` Core bridge projects, pinned .NET 10 LTS test host, NUnit/Unity Test Framework runners, EditMode/PlayMode границам, TestKit, source inventory parity, compatibility vectors, test metadata и обязательному `SLICE-00` test scaffold. Он уточняет раздел Test Architecture Technical Development Baseline и реализует Test Strategy без изменения её quality gates.

`docs/adr/ADR-007_Versioning_and_Build_Identity_v1.0.md` является принятым authority по ApplicationVersion, BuildIdentity, build channels, Git tags, artifact naming/provenance, независимым schema/format/contract/protocol/ruleset версиям, support ranges, version sources of truth и `SLICE-00` version scaffold. Он заменяет предварительный раздел Versioning and Build Identity Technical Development Baseline и уточняет compatibility/version fields Persistence, Networking, Serialization и Test Strategy.


`docs/adr/ADR-008_Deterministic_Clock_and_RNG_v1.0.md` является принятым authority по host wall UTC, monotonic duration, WorldClock separation, scheduler/deadline semantics, authoritative RNG stream ownership, campaign RNG key epochs, HMAC-SHA-256 derivation v1, xoshiro256** v1, unbiased integer mapping, retry/replay behavior, RngProofData и Clock/RNG contract vectors. Он заменяет предварительные Clock/RNG разделы Technical Development Baseline и уточняет Rules Engine, Dice/Log, Persistence, Networking и Test Strategy.

`docs/adr/ADR-009_Unity_Project_and_Build_Baseline_v1.1.md` является принятым authority по exact Unity Editor `6000.4.0f1`, repository project creation, package graph/lock/signatures, HDRP quality assets, UI Toolkit и Input System settings, Bootstrap/AppShell scenes, Windows D3D12→D3D11 configuration, Mono/IL2CPP build profiles, automated build entry point, Player smoke и Unity patch upgrade policy. Он заменяет предварительные Unity project/build-profile разделы Technical Development Baseline и применяется совместно с ADR-005/006/007/008.

`docs/adr/ADR-010_Logging_Diagnostics_and_Redaction_v1.0.md` является принятым authority по structured log schema, EventCode registry, safe property allowlist, data classification, redaction до sink, CorrelationId/DiagnosticId propagation, exception capture, bounded queue/backpressure, local sinks, rotation/retention, crash markers, diagnostic sessions, diagnostic bundles и запрету remote telemetry в MVP. Он заменяет предварительный раздел Logging/Diagnostics Technical Development Baseline и применяется совместно с ADR-004/005/007/008/009.

# 5. Отменённые и зарезервированные элементы

- Документ 14 (`14_YouTube_Music_Integration`) отменён. Номер 14 не переиспользуется.
- `MVP-PKG-16` отменён и зарезервирован. Он не блокирует выпуск.
- `PR-AUD-010–PR-AUD-016` и `PR-AUD-024–PR-AUD-025` отменены и зарезервированы.
- `UC-007` отменён и зарезервирован.
- YouTube, YouTube Music, URL-import, downloader tools, OAuth музыкальных сервисов и provider adapters не входят в MVP.

# 6. Актуальная Audio граница

Audio MVP принимает подготовленные локальные MP3, OGG и WAV из файловой системы GM. Обязательны `SingleFile`, `MultipleFiles` и рекурсивный `FolderTree`; каждый ImportBatch допускает частичный успех и формирует полный отчёт. Импорт копирует assets внутрь кампании, дедуплицирует их по content hash и не сохраняет абсолютный исходный путь как рабочую зависимость.

# 7. Ненормативные файлы

Следующие типы файлов не должны использоваться Codex как источник требований:

- `DOCUMENTATION_ALIGNMENT_CHANGELOG_*`;
- `Odyssey_VTT_New_Chat_Handoff_*`;
- `LegacyReference/*`, пока конкретная карточка не указана в задаче;
- templates и TestEvidence placeholders.

Они могут использоваться только для истории, объяснения происхождения решения или формы артефакта.

---

**Конец документа**
