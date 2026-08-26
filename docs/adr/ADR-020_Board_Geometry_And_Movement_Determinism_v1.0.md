# ADR-020 — Board Geometry and Movement Determinism

**Документ:** `docs/adr/ADR-020_Board_Geometry_And_Movement_Determinism_v1.0.md`
**ADR:** ADR-020
**Версия:** 1.0
**Дата:** 26 августа 2026 года
**Статус:** Accepted
**Область:** кросс-платформенная детерминированная арифметика координатной системы Board; конкретные формулы расстояния для Square/Hex/None grid; epsilon-конвенция для LOS/cover/wall intersection; подход к spatial-индексу для occupancy/obstacle/visibility запросов на MVP-масштабе — техническая implementation-ADR, которую `08_Scenes_And_Board_Odyssey_VTT_v0.5.md` §13.4 и §25.1 сама явно называет требуемой, не входящей в продуктовую документацию
**Связанные этапы:** Roadmap Этап 4 (`SLICE-03`), Milestone `M4`, backlog `ODY-S03-001`
**Базовые документы:** `08_Scenes_And_Board_Odyssey_VTT_v0.5.md` §6.1 (`WorldPosition`/finite-value правила), §7.7 (distance metrics), §13.4 (epsilon-конвенция — явно "implementation ADR"), §21.6/§25/BT-079 (restart-restore детерминизм), §25.1 (spatial index — явно "implementation ADR"), §25.4 (рендеринг — явно НЕ входит), §4.4 (Core geometry воспроизводима без Unity Physics — ключевое обоснование, почему детерминизм вообще требуется), `docs/adr/ADR-002_Command_and_Domain_Event_Model_v1.0.md` §26 (пример `board.token.move v1` — движение токена уже обычная авторитетная команда, не переоткрывается), `docs/adr/ADR-008_Deterministic_Clock_and_RNG_v1.0.md` (образец кросс-платформенного детерминизма для другой области — RNG/часы; тот же уровень строгости и versioned-constant стиль применяется здесь), `docs/adr/ADR-017_Snapshot_Delta_Reconnect_Model_v1.0.md` §1 (доставка board-дельт — обычные projection-дельты, не переоткрывается), `docs/tasks/SLICE-03_BACKLOG.md`

---

# 1. Решение

Odyssey VTT фиксирует единую детерминированную геометрическую модель Board Core — кросс-платформенную арифметику координатной системы, конкретные формулы расстояния для каждого поддерживаемого `GridType`, versioned epsilon-конвенцию для intersection-тестов и подход к spatial-индексу для occupancy/obstacle/visibility запросов — используя исключительно уже принятые механизмы (`ADR-002`'s командную модель, `ADR-017`'s доставку projection-дельт), не переопределяя их.

Обязательные решения:

1. **Кросс-платформенная арифметика**: вся авторитетная geometry-математика выполняется исключительно операциями IEEE-754 `System.Double` через `System.Math`, без `MathF`/`float`, без platform-specific fast-math/FMA-переупорядочивания, в фиксированном, документированном порядке операций для каждой формулы (раздел 4). Это даёт то же свойство, что `ADR-008` уже устанавливает для RNG (§32.1: "тот же алгоритм на любой целевой платформе даёт тот же результат") — не новый принцип, применение того же требования к другой области.
2. **Формулы расстояния**: три Square-метрики (`Euclidean` default, `ChebyshevDiagonalEqualsOne`, `AlternatingOneTwo`), hex-distance (cube-coordinate формула × `WorldUnitsPerCell`), и `GridType=None` (прямой Euclidean world distance) — зафиксированы точно, без метрик сверх названных `08_Scenes_And_Board` §7.7 (раздел 5).
3. **Epsilon-конвенция**: `GeometryEpsilonV1 = 1e-6` мировых единиц (метров), versioned constant по образцу `ADR-008`'s versioned алгоритмов; epsilon-tolerant orientation test (знаковая площадь/cross-product против epsilon-масштабированного порога) для endpoint touch, collinear overlap, line-along-wall, token-center-on-segment; граничный/неоднозначный случай классифицируется как **блокирующий** (fail-closed, согласуется с `08_Scenes_And_Board` §24.5/§24.6's уже принятым fail-closed принципом) (раздел 6).
4. **Spatial index**: uniform spatial hash, ключ — grid cell coordinate для `Square`/`Hex`, fixed-size world-unit bucket для `GridType=None`, versioned как `SpatialIndexV1` — не R-tree/quadtree, обоснование в разделе 7.
5. Этот ADR **не определяет**: полную схему `Scene`/`Board`/`SceneObject`/`Token` (implementation-задача, использующая эту ADR как основу, не содержание самой ADR); Unity-рендеринг-оптимизацию (`08_Scenes_And_Board` §25.4 — явно presentation-layer); сетевую доставку board-дельт (`ADR-017`, не переоткрывается); командную модель движения токена (`ADR-002`, не переоткрывается) — все явно отложены (раздел 8).

Этот ADR является нормативным authority по детерминированной geometry-математике Board Core — не полным контрактом `08_Scenes_And_Board_Odyssey_VTT_v0.5.md`, который остаётся источником доменной модели (Scene/Board/SceneObject/Token schema), использующей эту математику как основу.

---

# 2. Контекст и проблема

`08_Scenes_And_Board_Odyssey_VTT_v0.5.md` — продуктовый документ (3070 строк), фиксирующий доменную модель Board (Scene/Board/SceneObject/Token,層 layers, walls/doors/windows, fog of war, drawings, area effects) как host-authoritative, persistence-backed и permission-aware подсистему. Документ сам дважды явно называет две узкие технические области, которые **не** являются продуктовым решением, а требуют отдельной implementation ADR:

- §13.4 (Intersection convention): "Recommended: `GeometryEpsilon = versioned constant`. Exact epsilon is implementation ADR, not user-facing campaign data."
- §25.1 (Spatial index): "Host должен использовать spatial index для... Подходящий вариант: Uniform spatial hash / grid index or R-tree/quadtree. Конкретная структура — implementation ADR."

Кроме этих двух явно названных пунктов, `SLICE-03_BACKLOG.md` §5 (`ODY-S03-001`'s task boundary, зафиксированной в предыдущей задаче `ODY-S03-000`) расширяет объём этого ADR на смежные вопросы той же природы — детерминированную кросс-платформенную арифметику координатной системы (§6.1's `WorldPosition`/finite-value правила уже частично зафиксированы продуктовым документом, но не дают ADR-уровневой гарантии, что одна и та же geometry-операция на разных целевых платформах/compilation targets даёт побитово идентичный результат) и конкретные формулы расстояния (§7.7 называет метрики по имени — `Euclidean`/`ChebyshevDiagonalEqualsOne`/`AlternatingOneTwo`/hex-distance — но не даёт их точных математических определений).

Почему детерминизм вообще требуется, не только "разумное поведение": `08_Scenes_And_Board` §4.4 прямо фиксирует, что "Unity collider/raycast может ускорять preview, но authoritative result воспроизводится детерминированной geometry-библиотекой Core" — то есть Core geometry-библиотека обязана давать воспроизводимый результат **независимо** от Unity Physics/раскладки конкретного клиента. Это усиливается §21.6 ("Fog masks/regions и drawings must restore identically after restart") и тестовым требованием BT-079 ("Restart restore: Token positions, doors, fog, drawings и areas restore identically"). Поскольку `ADR-006` (Test Project Structure and Dual Unity/DotNet Compilation) уже устанавливает, что Core-библиотеки компилируются и тестируются в двух разных compilation targets (чистый .NET и Unity), а хост может исполняться на разных ОС/архитектурах, без ADR-уровневой гарантии кросс-платформенного детерминизма golden-vector тесты (тот же класс доказательства, что `ADR-008` уже использует для RNG) не могут быть написаны как надёжный acceptance-критерий будущей implementation-задачи.

`ADR-002` (Command and Domain Event Model), уже принятый (`SLICE-00`), уже содержит пример `board.token.move v1` как обычную авторитетную команду (§26) — подтверждает, что командная модель движения токена не переоткрывается этим ADR. `ADR-017` (Snapshot/Delta/Reconnect Model) уже фиксирует, что board-дельты — обычные projection-дельты (§1) — доставка не переоткрывается. Этот ADR закрывает именно ту узкую математическую область, которую оба уже принятых ADR оставили нерешённой: саму геометрию, на которой команды типа "подвинуть токен" основаны.

---

# 3. Термины

## 3.1 `WorldPosition`/`WorldVector`/`WorldRect` (уже принято, не переопределяется)

`08_Scenes_And_Board` §6.1's уже зафиксированные value types (`double X, Y`; finite-only; `NaN`/`Infinity` отклоняются). Этот ADR фиксирует **арифметические гарантии** поверх них (раздел 4), не изменяет саму структуру.

## 3.2 `GeometryEpsilonV1`

Versioned численная константа для epsilon-tolerant сравнений (раздел 6). Версионирование по образцу `ADR-008`'s `StreamDerivationV1`/`PRNGV1` — изменение значения требует новой версии (`GeometryEpsilonV2`), не молчаливой правки константы.

## 3.3 `DistanceMetric`

Одно из значений `08_Scenes_And_Board` §7.7's уже названного набора (`Euclidean`, `ChebyshevDiagonalEqualsOne`, `AlternatingOneTwo` для Square; implicit hex-distance для Hex; implicit Euclidean для `None`) — этот ADR даёт каждому точную формулу (раздел 5), не вводит новых значений.

## 3.4 `SpatialIndexV1`

Versioned конкретная структура данных (раздел 7) для occupancy/obstacle/visible-object/area-target/cover-candidate запросов, удовлетворяющая `08_Scenes_And_Board` §25.1's требованию.

## 3.5 `GridCoordinate`

Целочисленная (для Square: `(col, row)`; для Hex: axial `(q, r)`) производная координата, вычисляемая детерминированным округлением из `WorldPosition` (§6.1's "deterministic rounding выполняется только при переходе к grid coordinate" — этот ADR фиксирует точное правило округления, раздел 4.3).

---

# 4. Координатная система и детерминированная арифметика

**Явное решение (отвечает на явный вопрос задачи)**: кросс-платформенная гарантия строится на уже данном IEEE-754 `double`-контракте .NET, не на новом численном формате.

## 4.1 Почему `System.Double`/`System.Math` достаточно

.NET Common Language Infrastructure гарантирует IEEE-754 double-precision арифметику для `System.Double` на всех поддерживаемых runtime-таргетах (чистый .NET, Unity Mono, Unity IL2CPP) — тот же контракт, на который уже безусловно полагается `ADR-008`'s детерминированный RNG (не вводится новый numeric contract, используется тот же самый). Побитовая воспроизводимость одной и той же последовательности IEEE-754 операций в одном и том же порядке гарантирована спецификацией независимо от целевой платформы/архитектуры — при условии, что порядок операций фиксирован и не варьируется компилятором (раздел 4.2).

## 4.2 Запрещённые источники недетерминизма

Core geometry-библиотека (`BoardGeometryService`, `GridCoordinateService`, `MovementPathValidator`, `ObstacleIntersectionService`, `LineOfSightService`, `CoverSuggestionService`, `AreaIntersectionService` — `08_Scenes_And_Board` §4.4) обязана:

- использовать исключительно `double`/`System.Math` API — никогда `float`/`MathF`, `Unity.Mathematics` типы или `UnityEngine.Vector2/3` как источник авторитетного результата (Unity collider/raycast остаётся client-side preview-ускорением, `08_Scenes_And_Board` §4.4, не переопределяется);
- фиксировать порядок суммирования/умножения в многошаговых формулах (раздел 5) — не полагаться на порядок, зависящий от компилятора/JIT-оптимизации (`AggressiveOptimization`/vectorized reduction, если он меняет порядок сложения чисел с плавающей точкой, запрещён для авторитетных geometry-вычислений);
- не использовать параллельные/недетерминированно упорядоченные reduce-операции (`Parallel.For`, `PLINQ`) для накопления geometry-результата, где порядок влияет на округление;
- не читать `DateTime.Now`/machine-specific culture-formatting как часть вычисления (уже общий принцип `ADR-008` §11 — здесь распространяется на geometry).

## 4.3 Детерминированное округление WorldPosition → GridCoordinate

`08_Scenes_And_Board` §6.1 уже требует, что "deterministic rounding выполняется только при переходе к grid coordinate", не давая точного правила. Этот ADR фиксирует: округление **floor** относительно `GridSettings.Origin` (`floor((WorldPosition.X - Origin.X) / WorldUnitsPerCell)`, аналогично по Y) для Square; аналогичное floor-based преобразование в axial-координаты для Hex (раздел 5.2) — не "round half away from zero" и не banker's rounding, поскольку floor даёт однозначную, не зависящую от знака смещения границу клетки, необходимую для BOARD-INV-008's canonical center snap и для consistent occupancy-запросов.

---

# 5. Формулы расстояния

**Явное решение (отвечает на явный вопрос задачи)**: точные формулы для каждой метрики, названной `08_Scenes_And_Board` §7.7, не изобретённые сверх неё.

## 5.1 Square grid — три метрики

Даны две `WorldPosition`-точки `A`, `B`; `dx = B.X - A.X`, `dy = B.Y - A.Y`; `c = WorldUnitsPerCell`; для diagonal-метрик точки сначала переводятся в `GridCoordinate` (раздел 4.3) — `dCellX = ColB - ColA`, `dCellY = RowB - RowA`.

| Метрика | Формула | Обоснование |
|---|---|---|
| `Euclidean` (default) | `sqrt(dx² + dy²)` — прямое world-distance, без перехода в grid coordinate | Не требует cell-квантования; естественная метрика для произвольного пути; default по `08_Scenes_And_Board` §7.7. |
| `ChebyshevDiagonalEqualsOne` | `max(abs(dCellX), abs(dCellY)) × c` на каждый сегмент, накопленное по `Segments[]` | Классическое "diagonal = orthogonal" правило (D&D 5e-style); Chebyshev distance — стандартная формула для этого правила. |
| `AlternatingOneTwo` | На каждый **шаг** (единичное перемещение на одну клетку) внутри сегмента: если шаг диагональный — стоимость чередуется `1,2,1,2,...` (в клетках) начиная с `1` для первого диагонального шага пути, если ортогональный — стоимость `1`; итоговая стоимость сегмента/пути в клетках × `c` | Классическое "5-10-5-10" alternating diagonal правило (D&D 3.x/Pathfinder-style); чередование ведётся по счётчику диагональных шагов **вдоль всего `MovementPath`** (не сбрасывается на каждом новом `Segment`), чтобы результат не зависел от того, как GM визуально разбил путь на waypoints. |

Для всех трёх метрик итоговое `TotalDistance` — сумма по `Segments[]` в порядке следования (раздел 4.2's фиксированный порядок суммирования), передаётся Rules Engine в метрах (`08_Scenes_And_Board` §7.7, не переопределяется).

## 5.2 Hex grid

`08_Scenes_And_Board` §7.7: "Для Hex используется hex distance, умноженная на `WorldUnitsPerCell`." Этот ADR фиксирует точную формулу: axial-координаты `(q, r)` (детерминированно вычисленные из `WorldPosition` по ориентации `HexFlatTop`/`HexPointyTop`, раздел 4.3), cube-distance:

```text
s = -q - r
hexDistance(A, B) = (abs(qA - qB) + abs(rA - rB) + abs(sA - sB)) / 2
```

Итоговое расстояние — `hexDistance × WorldUnitsPerCell`, накопленное по сегментам как в разделе 5.1. Формула идентична для `HexFlatTop` и `HexPointyTop` — ориентация влияет только на axial-to-world преобразование (раздел 4.3), не на саму distance-формулу, что даёт единственную реализацию `hexDistance`, переиспользуемую для обоих ориентаций.

## 5.3 `GridType = None`

`08_Scenes_And_Board` §7.7: "Для GridType=None используется Euclidean world distance." Этот ADR подтверждает: идентична формуле `Euclidean` раздела 5.1, без перехода в `GridCoordinate` (`None` не имеет клеточного quantization, `08_Scenes_And_Board` §7.1).

---

# 6. Epsilon-конвенция

**Явное решение (отвечает на явный вопрос задачи)**: единая versioned константа, epsilon-tolerant orientation test, fail-closed граничный случай.

## 6.1 `GeometryEpsilonV1` — значение и обоснование

```text
GeometryEpsilonV1 = 1e-6 (мировые единицы, метры)
```

Обоснование величины: `WorldUnitsPerCell` типично в диапазоне `0.5`–`10` метров (`08_Scenes_And_Board` §6.2's пример `1`, `1.5`, `2`, `5`); `double` даёт ~15–17 значащих десятичных цифр точности. `1e-6` метра (1 микрометр) на много порядков меньше любой геймплейно значимой величины (позиции, размеры клеток), но на несколько порядков больше типичной накопленной ошибки округления IEEE-754 double-арифметики после разумного числа (десятки) последовательных операций при координатах разумного игрового масштаба (до ~`10^4`–`10^5` метров абсолютной величины поля боя) — граница, которую implementation-задача обязана покрыть golden-vector тестом (раздел 11, пункт 2).

Версионирование как `GeometryEpsilonV1` (не безымянная константа) — по образцу `ADR-008`'s `StreamDerivationV1`/`PRNGV1`: изменение значения в будущем требует `GeometryEpsilonV2` и явного amendment/superseding ADR, не молчаливой правки в implementation-задаче.

## 6.2 Epsilon-tolerant orientation test

Для всех перечисленных `08_Scenes_And_Board` §13.4 случаев (endpoint touch, collinear overlap, line exactly along wall, token center exactly on segment) — единый примитив: знаковая площадь треугольника (2D cross-product) `orientation(P, Q, R) = (Q.X-P.X)(R.Y-P.Y) - (Q.Y-P.Y)(R.X-P.X)`, классифицируемая как `Left`/`Right`/`Collinear` через сравнение `abs(orientation) < GeometryEpsilonV1 × scaleFactor` для `Collinear`, где `scaleFactor` — нормализующий множитель по величине входных координат (предотвращает ложную классификацию `Collinear` для геометрически далёких точек и ложную классификацию не-`Collinear` для очень коротких сегментов). Один и тот же примитив используется во всех geometry-сервисах раздела 4.2's списка — не отдельная epsilon-логика на сервис.

## 6.3 Fail-closed граничный случай

Когда orientation-тест классифицирует случай как `Collinear`/touching (эпсилон-граница) в контексте LOS/cover/movement-obstacle intersection — результат трактуется как **блокирующий** (пересечение есть), не как "нет пересечения". Обоснование: согласуется с `08_Scenes_And_Board` §24.5's ("Invalid geometry отклоняется до Commit") и §24.6's ("Host не отправляет непроверенную более широкую projection. Fail closed") уже принятым принципом — неоднозначная geometry-граница не должна расширять visibility/movement сверх того, что однозначно доказано.

---

# 7. Spatial index

**Явное решение (отвечает на явный вопрос задачи)**: uniform spatial hash, не R-tree/quadtree.

## 7.1 Выбор: `SpatialIndexV1` = uniform spatial hash

Ключ индекса:

- `Square`/`Hex`: `GridCoordinate` (раздел 4.3) объекта/токена — сущности с footprint размером `WidthCells × HeightCells` (`08_Scenes_And_Board` §11.3) индексируются по всем занимаемым клеткам;
- `GridType = None`: fixed-size world-unit bucket (сторона бакета = разумная константа порядка типичного query-радиуса, например `VisionSourceComponent.RangeWorldUnits`'s типичного диапазона — implementation-задача фиксирует конкретное число как tuning-параметр, не архитектурное решение).

## 7.2 Обоснование против R-tree/quadtree

MVP-масштаб (`08_Scenes_And_Board` §25: до 200 токенов на активной Scene, единственная Board на Scene, `BOARD-INV-008`'s уже принятое требование, что токены snap к центрам клеток) делает uniform hash структурно проще и достаточно быстрым: O(1) средняя вставка/удаление/point-query по ключу клетки, O(k) диапазонный запрос по числу пересекаемых клеток — без балансировки дерева, без амортизированной сложности перестроения при частых movement-обновлениях (что происходит на каждый `MoveToken`). R-tree/quadtree даёт преимущество при сильно неравномерном распределении объектов произвольного (не clamped к клеткам) размера и очень большом масштабе — ни одно из условий не выполняется для MVP по данным `08_Scenes_And_Board` §25. Выбор не заперт навсегда: `SpatialIndexV1` — версионированное имя, позволяющее `SpatialIndexV2` (например, R-tree) как post-MVP-задачу при доказанной необходимости, без переоткрытия этого ADR.

## 7.3 Область применения индекса

Единый `SpatialIndexV1` используется для всех перечисленных `08_Scenes_And_Board` §25.1 типов запросов — token occupancy, obstacle intersection (индексация wall segment bounding boxes, §13.2/§25.2's уже рекомендованный кэш), visible object queries, area target detection, cover candidates — не отдельная структура на тип запроса. Cache-инвалидация ключей индекса привязана к `SceneObjectRevision`/`BoardRevision` (`08_Scenes_And_Board` §21.3, §25.2's "Cache keys обязательно включают соответствующие revisions" — уже принятый принцип, не переопределяется).

---

# 8. Не входит в ADR-020

Явно исключено из объёма этого ADR:

- **Полная схема `Scene`/`Board`/`SceneObject`/`Token`** (`08_Scenes_And_Board` §4) — implementation-задача, использующая эту ADR как математическую основу, не содержание самой ADR.
- **Unity-рендеринг-оптимизация** (`08_Scenes_And_Board` §25.4 — chunked grid rendering, object pooling, batched line rendering, culling) — явно presentation-layer tuning, не архитектурно-блокирующий вопрос.
- **Сетевая доставка board-дельт** — уже `ADR-017`, не переоткрывается.
- **Командная модель движения токена** (validation pipeline §12.4, revision-checks, permission-checks) — уже `ADR-002`, не переоткрывается; этот ADR фиксирует только геометрический шаг (§12.4 пункты 7–9) внутри уже существующего pipeline.
- **Circular footprint rasterization rule** (`08_Scenes_And_Board` `OPEN-BOARD-005`) — явно помечено как non-blocking open item продуктовым документом, не входит в это ADR; implementation-задача может использовать conservative preset masks до отдельного будущего уточнения.
- **Fog physical representation** (polygon vs tile/mask, `OPEN-BOARD-004`) — отдельный non-blocking open item, не геометрия movement/intersection.
- **Технический спайк** — `SLICE-03_BACKLOG.md` §3 уже обосновала: геометрия — детерминированная математика, проверяемая golden-vector тестами будущей implementation-задачи, не требующая эмпирического спайка.
- **Production-реализация** (реальный код `BoardGeometryService`/`GridCoordinateService`/`MovementPathValidator`/etc., unit-тесты) — future implementation-задача.

---

# 9. Соответствие module boundaries (`ADR-001`) и уже принятым `ADR-002`/`ADR-008`/`ADR-017`

Этот ADR не вводит новый код и не переопределяет ни одну уже принятую границу:

- Core geometry-сервисы (`08_Scenes_And_Board` §4.4's список) остаются чистым C# без зависимости от `UnityEngine`/Unity Physics как источника авторитетной истины — согласуется с `ADR-001`'s module boundary (`Odyssey.Domain`/`Odyssey.Rules` не зависят от Unity).
- Командная модель движения токена (`board.token.move v1`, `ADR-002` §26) не изменяется — этот ADR фиксирует только внутренний геометрический шаг validation pipeline (`08_Scenes_And_Board` §12.4 пункты 7–9), не структуру команды/события/pipeline.
- Кросс-платформенный детерминизм строится на том же IEEE-754 `double`-контракте, на который уже полагается `ADR-008`'s RNG — не вводит параллельный numeric contract.
- Доставка board-дельт клиенту остаётся `ADR-017`'s `ProjectionDeltaBatch`/`Operations[]` — этот ADR не добавляет новый тип операции и не меняет протокол доставки.

---

# 10. Правила для Codex

Codex обязан:

1. Реализовывать всю авторитетную geometry-математику исключительно через `System.Double`/`System.Math` — никогда `float`/`MathF`/`Unity.Mathematics`/`UnityEngine.Vector2/3` как источник авторитетного результата.
2. Фиксировать порядок операций многошаговых формул (раздел 4.2) идентично для всех целевых compilation targets — не полагаться на компилятор/JIT-специфичный порядок суммирования с плавающей точкой.
3. Реализовывать округление `WorldPosition → GridCoordinate` исключительно через floor-правило раздела 4.3 — не round-half-away-from-zero, не banker's rounding.
4. Реализовывать ровно три Square-метрики и hex-distance/None-Euclidean раздела 5 точно по данным формулам — не изобретать дополнительные метрики сверх названных `08_Scenes_And_Board` §7.7.
5. Использовать единый epsilon-tolerant orientation-примитив раздела 6.2 во всех geometry-сервисах — не отдельную epsilon-логику на сервис; неоднозначный/граничный случай классифицировать как блокирующий (раздел 6.3), не как "нет пересечения".
6. Реализовывать `SpatialIndexV1` как uniform spatial hash (раздел 7) — не R-tree/quadtree без отдельного будущего ADR-amendment, обоснованного измеренной необходимостью.
7. Не реализовывать под этим ADR: доменную схему Scene/Board/SceneObject/Token, Unity-рендеринг-оптимизацию, сетевую доставку дельт, командную модель движения — все явно отложены (раздел 8).

---

# 11. Definition of Done для будущей implementation-задачи

Implementation-задача, реализующая эту модель, обязана — до открытия своего Draft PR с production-кодом — доказать (тестами):

1. Идентичный входной набор geometry-операций даёт побитово идентичный `double`-результат при запуске в чистом .NET и в Unity Mono/IL2CPP compilation target (golden-vector тест, доказывающий кросс-платформенный детерминизм раздела 4).
2. Каждая из трёх Square-метрик, hex-distance и None-Euclidean формула раздела 5 покрыта golden-vector тестом с заранее вычисленным ожидаемым результатом (не сравнением реализации самой с собой).
3. `GeometryEpsilonV1`'s epsilon-tolerant orientation-тест корректно классифицирует endpoint touch, collinear overlap, line-along-wall и token-center-on-segment как блокирующий случай (раздел 6.3), покрыто отдельными тестами на каждый из четырёх сценариев `08_Scenes_And_Board` §13.4.
4. `SpatialIndexV1` даёт идентичный набор результатов occupancy/obstacle/visible-object/area-target/cover-candidate запросов по сравнению с наивным полным перебором (brute-force reference implementation) на тестовом наборе объектов — доказывает корректность индекса, не только производительность.
5. Restart-restore (`08_Scenes_And_Board` BT-079) даёт идентичные token positions/fog/geometry после сериализации-десериализации через `ADR-012`'s snapshot-механизм — доказывает, что детерминизм раздела 4 сохраняется через persistence-границу, не только в рамках одного процесса.
6. Ни одна метрика/epsilon/spatial-index реализация не вводит зависимость от `UnityEngine`/Unity Physics как источника авторитетного результата — доказано тестом, что Core geometry-сборка компилируется и проходит тесты в чистом .NET без ссылки на `UnityEngine`.

---

# 12. Рассмотренные альтернативы

## 12.1 `float`/`Unity.Mathematics` для geometry-вычислений

Отклонено: single-precision даёт меньшую точность и не гарантирует того же уровня кросс-платформенной побитовой воспроизводимости, которую `ADR-008` уже требует для RNG; `Unity.Mathematics` вводит зависимость Core-библиотеки от Unity-специфичного пакета, что нарушает `08_Scenes_And_Board` §4.4's уже принятый принцип ("Core сервисы не зависят от Unity Physics как источника истины") и `ADR-001`'s module boundary.

## 12.2 Round-half-away-from-zero вместо floor для WorldPosition → GridCoordinate

Отклонено: floor даёт единственную, не зависящую от знака координаты границу клетки (клетка `[n, n+1)` при любом знаке `n`), что проще версионировать и тестировать golden-vector'ами, чем round-half-away-from-zero, которое даёт разные точки округления слева/справа от нуля координатной системы и усложняет симметрию occupancy-запросов около `Origin`.

## 12.3 R-tree/quadtree как `SpatialIndexV1`

Отклонено (раздел 7.2): избыточная сложность для MVP-масштаба (до 200 токенов, snap-to-cell модель), не даёт измеримого преимущества над uniform hash на этом масштабе; оставлено как возможный `SpatialIndexV2` post-MVP при доказанной необходимости.

## 12.4 Отдельная epsilon-константа на каждый geometry-сервис (LOS, cover, wall intersection — разные значения)

Отклонено: единая `GeometryEpsilonV1` (раздел 6.1) проще версионировать, тестировать и обосновывать одним golden-vector набором; расхождение epsilon между сервисами создало бы риск несогласованных решений (например, LOS считает точки collinear, а cover — нет) для геометрически идентичного случая без продуктового обоснования такого расхождения.

## 12.5 Технический спайк для измерения производительности spatial-index на реальном железе

Отклонено, обоснование уже дано `SLICE-03_BACKLOG.md` §3: выбор структуры данных (раздел 7) — архитектурное решение, проверяемое golden-vector/brute-force-reference тестами (Definition of Done пункт 4), не требующее эмпирического измерения на реальном оборудовании; производительность как таковая (`08_Scenes_And_Board` §25.4) — presentation-layer вопрос, явно вне объёма этого ADR.

---

# 13. Трассировка

ADR реализует и уточняет:

- `08_Scenes_And_Board_Odyssey_VTT_v0.5.md` §6.1 (`WorldPosition`/finite-value — арифметическая гарантия поверх, не переопределение структуры), §7.7 (distance metrics — точные формулы), §13.4 (epsilon-конвенция — явно "implementation ADR"), §21.6/BT-079 (restart-restore детерминизм), §25.1 (spatial index — явно "implementation ADR"), §4.4 (Core geometry-воспроизводимость независимо от Unity Physics — ключевое обоснование раздела 2);
- `17_Roadmap_Odyssey_VTT_v0.11.md` §12.2 (prerequisite documents, не даёт литерального ADR-списка — обоснование количества дано `ODY-S03-000`);
- `docs/adr/ADR-002_Command_and_Domain_Event_Model_v1.0.md` §26 (`board.token.move v1` пример — командная модель не переоткрывается);
- `docs/adr/ADR-008_Deterministic_Clock_and_RNG_v1.0.md` (versioned-constant стиль, кросс-платформенный IEEE-754 double-контракт — тот же уровень строгости применён здесь);
- `docs/adr/ADR-017_Snapshot_Delta_Reconnect_Model_v1.0.md` §1 (доставка board-дельт — обычные projection-дельты, не переоткрывается);
- `docs/adr/ADR-001_Module_Boundaries_and_Dependency_Direction_v1.0.md` (Core не зависит от Unity — подтверждено, не изменено).

Связанные будущие задачи (`docs/tasks/SLICE-03_BACKLOG.md`):

```text
ODY-S03-002  ADR: Extended Audience and Selected-Participant Visibility (независимая от этой задачи)
(будущая, не зарезервированная в этой ревизии backlog) production-реализация этой ADR — BoardGeometryService/GridCoordinateService/MovementPathValidator/etc. и их golden-vector тесты
(будущая, не зарезервированная в этой ревизии backlog) SLICE-03 vertical slice implementation backlog — использует эту ADR как основу для движения токена/LOS/cover
```

---

# 14. Нормативное действие

Принято как ADR этой задачи (`ODY-S03-001`) без ожидания технического спайка — обоснование: этот ADR фиксирует детерминированную математическую модель (арифметика, формулы расстояния, epsilon-конвенция, spatial-index подход), полностью проверяемую golden-vector/brute-force-reference тестами будущей implementation-задачи, не требующую эмпирических данных с реального оборудования или внешней среды (`SLICE-03_BACKLOG.md` §3's уже данное обоснование "no spike required").

С даты принятия (`Accepted`):

- ни одна implementation-задача `SLICE-03`/будущих слайсов не вводит альтернативную distance-метрику, epsilon-константу или spatial-index структуру под этим ADR без amendment/superseding ADR;
- будущая implementation-задача обязана переиспользовать `ADR-002`'s командную модель и `ADR-017`'s протокол доставки дельт для движения токена/geometry-изменений — не вводить параллельные механизмы;
- изменение зафиксированных здесь значений (например, `GeometryEpsilonV1 → V2`, `SpatialIndexV1 → V2`) требует amendment этого ADR или нового superseding ADR, не молчаливого изменения в реализации.

---

**Конец документа**
