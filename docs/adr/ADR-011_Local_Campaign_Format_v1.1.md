# ADR-011 — Local Campaign Format

**Документ:** `docs/adr/ADR-011_Local_Campaign_Format_v1.1.md`  
**ADR:** ADR-011  
**Версия:** 1.1  
**Дата:** 24 августа 2026 года  
**Статус:** Accepted  
**Supersedes:** `docs/adr/ADR-011_Local_Campaign_Format_v1.0.md` только в части §12.1 (выбор конкретной SQLite provider-библиотеки). Все остальные решения v1.0 (физическая структура папки кампании, `.odcamp` container, `manifest.json`, независимые version dimensions, SQLite runtime profile/PRAGMA-профиль, принцип построения базовой схемы данных, доменные идентификаторы) остаются без изменений и в силе.  
**Область:** закрытие открытого вопроса `ADR-011` v1.0 §12.1 — выбор конкретной .NET SQLite provider-библиотеки  
**Связанные этапы:** Roadmap Этап 2 (`SLICE-01`), Milestone `M2`, backlog `ODY-S01-005`  
**Базовые документы:** `docs/adr/ADR-011_Local_Campaign_Format_v1.0.md` §12.1, `docs/tasks/completed/ODY-S01-005_SP-02_Persistence_Reliability_Report.md` §3, `THIRD_PARTY_NOTICES.md`

---

# 1. Решение

`ADR-011` v1.0 §12.1 оставляла выбор конкретной .NET SQLite provider-библиотеки открытым до результатов `SP-02` (`ODY-S01-005`). Product owner принял отчёт `SP-02` и его рекомендацию (раздел 3 отчёта) as-is и явно одобрил закрытие этого вопроса этим amendment в той же итерации, не откладывая его отдельным шагом.

**Нормативное решение:** конкретной .NET SQLite provider-библиотекой для Odyssey VTT является **`Microsoft.Data.Sqlite`**, с обязательным транзитивным пином `SQLitePCLRaw.bundle_e_sqlite3` версии **`>= 3.0.3`**.

Пин версии `SQLitePCLRaw.bundle_e_sqlite3` обязателен: версия `2.1.x`, которую `Microsoft.Data.Sqlite` 9.0.x подтягивает транзитивно по умолчанию, отмечена NuGet audit как содержащая known high-severity уязвимость (`GHSA-2m69-gcr7-jv3q`), обнаруженную при подготовке `SP-02` и зафиксированную в `THIRD_PARTY_NOTICES.md`. Версия `3.0.3` и выше эту уязвимость не содержит.

---

# 2. Источник решения

Это решение принято на основании эмпирической рекомендации `SP-02`, зафиксированной в `docs/tasks/completed/ODY-S01-005_SP-02_Persistence_Reliability_Report.md` §3. Ключевое обоснование дословно из отчёта, не изобретается заново:

> «The reliability properties this spike measured are SQLite-engine-level properties, not .NET-wrapper-level properties. WAL crash recovery, the Backup API's atomicity characteristics, and `integrity_check`'s corruption detection are all implemented inside the native `sqlite3` C library. Every mainstream .NET SQLite wrapper (`Microsoft.Data.Sqlite`, `System.Data.SQLite`, `sqlite-net`) ultimately calls into the same native engine via `SQLitePCLRaw` or an equivalent P/Invoke layer. [...] The wrapper choice is consequently better decided on API ergonomics, maintenance, and licensing than on reliability [...]»
>
> «License and maintenance: `Microsoft.Data.Sqlite` is MIT-licensed and maintained by the .NET/EF Core team as part of the officially supported .NET data-access family, consistent with this repository's existing MIT-only third-party approvals [...]»
>
> «API ergonomics observed directly while building the harness: `SqliteConnection.BackupDatabase` gives direct, low-ceremony access to the SQLite Backup API [...], and standard `ADO.NET`-shaped connection/command/transaction types integrate cleanly with the transactional patterns `ADR-012` §5 and `ADR-013` §6 already specify [...] — no friction was encountered implementing any of the six scenarios' exact PRAGMA/transaction/backup requirements.»

Этот ADR не вводит нового обоснования сверх того, что уже дал отчёт `SP-02` — он лишь придаёт этой рекомендации нормативную силу.

---

# 3. Не изменяется этим amendment

- Физическая структура папки кампании, `.odcamp` container, `manifest.json` (`ADR-011` v1.0 разделы 4–5) — без изменений.
- Независимые version dimensions (`CampaignFormatVersion`, `DatabaseSchemaVersion`, `RulesetVersion`) (`ADR-011` v1.0 раздел 6) — без изменений.
- SQLite runtime profile / обязательный PRAGMA-профиль (`ADR-011` v1.0 §7.1: `journal_mode = WAL`, `foreign_keys = ON`, `synchronous = FULL`, `busy_timeout = 5000`) — без изменений; именно этот профиль был эмпирически проверен `SP-02` против выбранной библиотеки и подтверждён рабочим (отчёт `SP-02` разделы 2.1–2.6).
- Принцип построения базовой схемы данных и доменные идентификаторы (`ADR-011` v1.0 разделы 8–9) — без изменений.
- Открытый вопрос `ADR-011` v1.0 §12.2 (`CampaignPublicId`) — не затрагивается этим amendment, остаётся `[OPEN]`.

---

# 4. Нормативное действие

`ADR-011` v1.0 остаётся историческим контекстом и продолжает действовать во всём, что не перечислено в разделе 1 этого документа как закрытое. Active work must use `ADR-011` v1.1 в части выбора SQLite provider-библиотеки.

С даты принятия:

- implementation-задачи, реализующие доступ к `campaign.db`, обязаны использовать `Microsoft.Data.Sqlite` с `SQLitePCLRaw.bundle_e_sqlite3 >= 3.0.3`, не альтернативную библиотеку и не непинованную транзитивную версию;
- `ADR-011` v1.0 §12.1 считается закрытым этим amendment, не остаётся отдельным открытым вопросом далее;
- этот выбор не переопределяет и не ослабляет PRAGMA-профиль или любое другое решение `ADR-011` v1.0, перечисленное в разделе 3 этого документа как неизменное;
- изменение этого решения (например, при появлении новых находок надёжности) требует нового amendment этого ADR, не молчаливого отклонения в реализации.

---

**Конец документа**
