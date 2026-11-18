# План: детекция потери входа в prompt-кэш нейронки

> Статус: согласован (v2). Код пока не менялся — это только документация плана.
> Версия плана: 2026-11-19. Целевая версия пакета: `1.16.2` → `1.17.0` (фича/поведение → минор).

## 1. Цель

В течение беседы детектировать, когда вход (input) «выпадает» из prompt-кэша провайдера
(нейронки), и сообщать двумя способами:

1. **Лог** — модель пишет в лог через внедрённый `IAgentLogger`
   (в RPAWriter2 реализация на NLog → chat-log файл).
2. **Оранжевое самоисчезающее предупреждение** на 10 секунд с крестиком закрытия —
   переносимый в `AgentToolkit.Wpf` контрол `NotificationBanner`.

Интеграция выполняется **только в RPAWriter2** (VibeCoder и AI-RPA не трогаем).

## 2. Что такое «кэш нейронки» и «потеря входа»

В коде уже есть модель prompt-cache провайдера:

- `TokenUsage.CacheHitTokens` / `CacheMissTokens` (`Agent.Core/Models/Models.cs`) — сколько
  входных токенов пришло из кэша провайдера и сколько мимо, на каждый запрос.
- `ChatConversation.AccumulateUsage` (`Agent.Core/Models/ChatHistory.cs`) на каждый запрос
  сохраняет `LastCacheHitTokens` / `LastCacheMissTokens` и накопительный `Usage`.
- `ProviderTranscriptCodec` хранит транскрипт, «чтобы кэш провайдера оставался тёплым».

«Потеря входа в кэш» = кэш провайдера «остыл»: предыдущий запрос получал входные токены из
кэша (`CacheHitTokens > 0`), а следующий — уже нет (`CacheHitTokens == 0`) либо hit резко
упал. Это сигнал к предупреждению.

Сигнал уже течёт по цепочке:

```
AnthropicProvider / OpenAiCompatibleProvider
  → событие TokenUsageUpdated (per-request)
  → ContextUsageBinder (Agent.Models, маршалинг в UI-поток)
  → ChatModel.RecordTokenUsage(usage)
  → ChatConversation.AccumulateUsage(usage)
```

Поэтому детектор встраивается в одну точку — `ChatModel.RecordTokenUsage`.

## 3. Правила детекции

Порог `DropRatio` задаётся **в процентах** (0..100), значение по умолчанию **0**.
`DropRatio` — **допустимое отклонение** (tolerable drop) cache-hit между запросами:
потеря детектируется, когда фактическое падение **превышает** порог.

`dropPercent = 100 * (prev - cur) / prev` — фактическое падение cache-hit, %.

- Потеря детектируется при: `previousCacheHitTokens > 0 && currentPromptTokens > 0`
  `&& dropPercent > DropRatio`.
- `DropRatio = 0` → допустимое отклонение 0 % → детектируется **любая** потеря.
- `DropRatio = 100` → допустима потеря 100 % контекста → потери не детектируются.
- `0` и `100` — обычные предельные значения, без специальной трактовки «выключено».

По виду потери (для разных сообщений):

- **Полная потеря**: `currentCacheHitTokens == 0` (падение 100 %).
- **Частичная потеря**: `currentCacheHitTokens > 0` (падение больше порога, но не до нуля).

Во всех остальных случаях событие не поднимается (в т.ч. для провайдеров, которые вообще
не отдают кэш-метрик — тогда `hit` всегда 0 и ложных срабатываний нет).

## 4. Сообщения (разные для полной и частичной потери)

Сообщение информативное: сколько потеряно **в процентах и абсолютно** (токены).

- **Полная потеря:**
  `Prompt cache lost: cache-hit input dropped from {prev:N0} to 0 tokens (lost {lost:N0} tokens, 100%).`

- **Частичная потеря:**
  `Prompt cache degraded: cache-hit input dropped from {prev:N0} to {cur:N0} tokens (lost {lost:N0} tokens, {pct:0}%).`

где `lost = prev - cur`, `pct = round(100 * lost / prev)`.

## 5. Где хранится состояние

Состояние детектора хранится **в объекте беседы** (`ChatConversation`), только в рантайме,
без персистенции и без сброса:

- `PreviousCacheHitTokens` (int, по умолчанию 0) — кэш-хит предыдущего запроса.
- `PreviousPromptTokens` (int, по умолчанию 0) — prompt предыдущего запроса.

Каждая беседа хранит своё состояние, поэтому при смене беседы (`StartNewChat` /
`OpenConversation` / `CloseActiveChat` / `ResetConversation`) **ничего сбрасывать не нужно**:
новая беседа просто стартует с `Previous* = 0`.

## 6. Изменения по проектам

### 6.1 `Agent.Core` (UI-free, netstandard2.0)

Новый файл `Agent.Core/Context/PromptCacheLossDetector.cs`:

- `public enum PromptCacheLossKind { None, Full, Partial }`.
- `public sealed class PromptCacheLossInfo` — payload события:
  `Kind`, `PreviousCacheHitTokens`, `CurrentCacheHitTokens`, `CurrentPromptTokens`,
  `CurrentCacheMissTokens`, `LostTokens` (абсолют), `LostPercent` (0..100), `Message`.
- `public static class PromptCacheLossDetector` со статическим методом
  `Detect(TokenUsage usage, int previousCacheHitTokens, int previousPromptTokens, double dropRatioPercent)`
  → возвращает `PromptCacheLossInfo` или `null`. Чистая логика, без состояния и I/O.

`Agent.Core/Models/ChatHistory.cs` (`ChatConversation`):

- добавить runtime-only свойства `PreviousCacheHitTokens` / `PreviousPromptTokens`
  (public, не персистятся — `ChatConversation` в хранилище не сериализуется, в БД пишутся
  только `ChatHistoryMessage`).

`Agent.Core/Services/SettingsStore.cs` (`SettingsData`):

- добавить `public double CacheLossDropRatioPercent { get; set; } = 0;`
  (проценты, 0..100 — допустимое отклонение cache-hit; по умолчанию 0).

`Agent.Core/Contracts/IAgentLogger.cs` (новый интерфейс логирования для модели):

- `void Info(string message)`, `void Warn(string message)`, `void Error(string message)`,
  `void Error(Exception exception, string message = null)`.

### 6.2 `Agent.Models`

`Agent.Models/ChatModel.cs`:

- конструктор получает опциональный `IAgentLogger logger = null` (по умолчанию no-op);
- `public event EventHandler<PromptCacheLossInfo> PromptCacheLost;`.
- `public double CacheLossDropRatioPercent { get; set; } = 0;` — хост зеркалит из
  `SettingsModel` (по аналогии с `SetContextWindow`).
- В `RecordTokenUsage`:
  1. прочитать `conversation.PreviousCacheHitTokens` / `conversation.PreviousPromptTokens`;
  2. `PromptCacheLossDetector.Detect(...)` с текущим `CacheLossDropRatioPercent`;
  3. `conversation.AccumulateUsage(usage)`;
  4. записать новые `Previous*` обратно в беседу;
  5. при `loss != null`: `_logger.Warn(loss.Message)` и поднять `PromptCacheLost`
     (лог пишет именно модель через `IAgentLogger`).

`Agent.Models/SettingsModel.cs`:

- поле `_cacheLossDropRatioPercent = 0`, свойство `CacheLossDropRatioPercent`;
- включить в `ToSettingsData()` и в загрузку/применение (`ApplySettings` / инициализация из
  `SettingsData`), рядом с `CacheHitPricePerMillion`.

### 6.3 `Agent.Toolkit.Wpf` (net472)

**Перенос тоста из RPAWriter2 (п. «используем существующий оранжевый тост — переносим в тулкит»):**

- Новые файлы `Agent.Toolkit.Wpf/Controls/Notification/NotificationKind.cs` и
  `NotificationBanner.xaml` / `NotificationBanner.xaml.cs`, namespace
  `Agent.Toolkit.Wpf.Controls`.
- Контент копируется из `RPAWriter2/Controls/NotificationBanner.*` с единственным изменением:
  убрать `using Agent.Rpa.*` (они не используются) и сменить namespace. Поведение сохраняется:
  - `Kind = Warning` → оранжевый акцент `0xFF8C00`;
  - `AutoHideSeconds` (DispatcherTimer) — для нас **10**;
  - кнопка «✕» (`ShowClose = true`) → `CloseRequested`.

**Настройка в разделе настроек:**

- `Agent.Toolkit.Wpf/Controls/SettingsControl.xaml`: добавить поле в секцию **API**, рядом с
  ценами кэша (`CacheHitPricePerMillion`, ~строка 351), привязать к
  `Settings.CacheLossDropRatioPercent` (TwoWay, `UpdateSourceTrigger=PropertyChanged`), подпись
  типа `Cache loss tolerable drop, %`, tooltip: `Допустимое падение кэша, % (0 — любая потеря; 100 — потеря всего контекста)`.

**Поверхность для хоста (не обязательно, но удобно):**

- `Agent.Toolkit.Wpf/Controls/ChatControl.xaml.cs`: подписаться в `OnDataContextChanged` на
  `ChatModel.PromptCacheLost` и пере-поднять `public event EventHandler<PromptCacheLossInfo>
  PromptCacheLost` (по образцу существующего `VoiceError`). Логирование в шаред-слое не
  добавляем — модель остаётся чистой, лог пишет хост.

### 6.4 `Agent.Tests` (TDD, NUnit)

Новый `Agent.Tests/PromptCacheLossDetectorTests.cs` (чистый детектор):

- первый запрос / `usage == null` → `null`;
- полная потеря (`hit>0 → hit==0`, при `DropRatio < 100`) → `Kind=Full`, `LostTokens`,
  `LostPercent=100`, `Message`;
- `DropRatio = 0` → любая потеря (даже минимальная) даёт событие;
- `DropRatio = 100` → потери не детектируются (в т.ч. полная потеря);
- кэш тёплый (`hit>0 → hit>0` без падения) → `null`;
- провайдер без кэша (hit всегда 0) → `null`;
- частичная потеря при `dropPercent > DropRatio` → `Kind=Partial`, корректные `%` и абсолют;
- граничные значения `LostPercent`/округление.

Новый `Agent.Tests/ChatModelCacheLossTests.cs` (в стиле `ContextUsageBinderTests`,
`FakeChatHistoryStore`):

- два `RecordTokenUsage` (тёплый → холодный) поднимают `PromptCacheLost` ровно один раз с
  корректным payload;
- `RecordTokenUsage(null)` не падает и не поднимает событие;
- смена беседы (`StartNewChat` / `OpenConversation` / `CloseActiveChat`) **не сбрасывает**
  детектор, но новая беседа стартует с `Previous*=0` → нет ложного события.

### 6.5 RPAWriter2 (единственный хост интеграции)

- Переключить локальный `RPAWriter2.Controls.NotificationBanner`/`NotificationKind` на общий
  `Agent.Toolkit.Wpf.Controls.NotificationBanner`/`NotificationKind` (обновить `xmlns`/using;
  локальные файлы удалить).
- Реализовать `IAgentLogger` на базе NLog (пишет в chat-log файл) и инжектить в `ChatModel`
  при создании — логирование выполняет модель.
- Подписаться на `ChatModel.PromptCacheLost` (или на `ChatControl.PromptCacheLost`) и
  показать оранжевый тост: `NotificationBanner { Kind = Warning, Message = loss.Message,
  AutoHideSeconds = 10, ShowClose = true }`.
- Зеркалить `SettingsModel.CacheLossDropRatioPercent` → `ChatModel.CacheLossDropRatioPercent`
  при старте и при сохранении настроек.

## 7. TDD — обязательное правило

Порядок: **red → green → refactor**.

1. Сначала пишутся падающие тесты `PromptCacheLossDetectorTests` + `ChatModelCacheLossTests`.
2. Минимальная реализация `PromptCacheLossDetector` + `ChatConversation`-поля + `ChatModel`.
3. Рефакторинг (сообщения, пороги, payload).

Каждая новая возможность покрыта юнит-тестами в `Agent.Tests`.

## 8. Release-цикл

1. `Directory.Build.props`: `1.16.2` → `1.17.0`.
2. Коммит в русском многострочном стиле:

```
feat: детекция потери prompt-кэша и оранжевое предупреждение

Agent.Core: добавить PromptCacheLossDetector, IAgentLogger и CacheLossDropRatioPercent в SettingsData
Agent.Models: детекция в ChatModel.RecordTokenUsage, состояние в ChatConversation, настройка в SettingsModel
Agent.Toolkit.Wpf: перенести NotificationBanner/NotificationKind, поле настройки в SettingsControl
Agent.Tests: тесты детектора и ChatModel
chore: поднять версию до 1.17.0
```

3. `.\pack.ps1` — собрать и запаковать `AgentToolkit` + `AgentToolkit.Wpf` в `feed\`.

## 9. Не делаем сейчас / ограничения

- Состояние детектора — **только рантайм**, в персистенцию не пишем.
- Интеграция — **только RPAWriter2**; VibeCoder и AI-RPA не трогаем.
- Логирование выполняет сама модель через внедрённый `IAgentLogger`; хост лишь
  предоставляет реализацию (NLog → chat-log файл).

## 10. Последовательность выполнения задач

1. **TDD-red**: написать падающие тесты `PromptCacheLossDetectorTests`
   (правила детекции + сообщения) и `ChatModelCacheLossTests`
   (событие + логирование через `IAgentLogger`).
2. `Agent.Core`: добавить `IAgentLogger`.
3. `Agent.Core`: добавить `PromptCacheLossDetector` + `PromptCacheLossInfo` +
   `PromptCacheLossKind`.
4. `Agent.Core`: добавить `ChatConversation.PreviousCacheHitTokens` /
   `PreviousPromptTokens` (runtime-only).
5. `Agent.Core`: добавить `SettingsData.CacheLossDropRatioPercent`.
6. `Agent.Models`: `SettingsModel.CacheLossDropRatioPercent` (поле, `ToSettingsData`,
   загрузка/применение).
7. `Agent.Models`: `ChatModel` — конструктор с `IAgentLogger`, `PromptCacheLost`,
   `CacheLossDropRatioPercent`, логика в `RecordTokenUsage`.
8. **TDD-green**: `dotnet test Agent.Tests/Agent.Tests.csproj`.
9. `Agent.Toolkit.Wpf`: перенести `NotificationBanner`/`NotificationKind` из RPAWriter2
   (убрать `using Agent.Rpa.*`, сменить namespace).
10. `Agent.Toolkit.Wpf`: поле настройки + tooltip в `SettingsControl.xaml` (секция API).
11. `Agent.Toolkit.Wpf`: (опц.) пере-подъём `PromptCacheLost` в `ChatControl`.
12. **TDD-refactor**: прогнать все тесты, прибрать код.
13. `RPAWriter2`: реализовать `IAgentLogger` (NLog), инжектить в `ChatModel`, подписаться на
    `PromptCacheLost` → оранжевый тост 10 с, переключиться на общий `NotificationBanner`,
    зеркалить настройку.
14. Release: поднять версию `1.16.2` → `1.17.0`, коммит по `agents.md`, `.\pack.ps1`.

