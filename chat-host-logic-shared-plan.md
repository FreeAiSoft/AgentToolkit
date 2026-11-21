# План: вынести общую логику агентного цикла и управления чатом в shared-слой (тулкит)

> Статус: **только план, код не изменялся.**
> Решение принято: **вариант 1** — UI-free ядро `ChatAgentSession` в `Agent.Models`,
> базовый `ChatHostBase` в `Agent.Toolkit.Wpf`, `IChatHost` сохраняется как фасад
> (минимум ломки существующих хостов).

---

## 1. Проблема

Сейчас почти вся «механика» чата продублирована в каждом хосте:

- `StubChatHost` (`Agent.ChatHost\StubChatHost.cs`) — заглушка, у которой всё, кроме
  `SendChatAsync`, пустое; ответы захардкожены и не проходят через агентный цикл.
- Во внешних хостах (RPAWriter2, VibeCoder, AI-RPA) каждый реализует `IChatHost`
  самостоятельно: один и тот же код drain-очереди, запуска `AgentRunner`, маппинга
  стрима на `ChatMessage`, cancel/resume, компакции, `StartNewChat`/`OpenConversation`
  копируется из проекта в проект.

Смысл рефакторинга — **сместить ответственность**: общая логика должна жить в
shared-слое и быть написана один раз, а хосты должны реализовывать только то, что
действительно специфично (DTE/Git/Roslyn/роботы, сборка агентного окружения).

---

## 2. Что уже есть в shared-слое (переиспользуем)

| Компонент | Где | Что умеет |
|---|---|---|
| `ChatModel` | `Agent.Models\ChatModel.cs` | состояние чата и сценарии: `StartNewChat`, `OpenConversation`, `EnsureActiveConversation`, `Enqueue`/`DequeueNext`, `ApplyCompactedTranscript`, `Phase`, `HasResumableRun`, `RecordTokenUsage`, `SetContextWindow`, `SaveHistoryNow` |
| `AgentRunner` | `Agent.Core\AgentTools\AgentRunner.cs` | `RunAsync`/`ResumeAsync`/`CompactAsync`, `IProgress<ChatStreamDelta>`, `IProgress<AgentResumeSnapshot>`, события `ToolExecuted`/`ContextCompacted`/`CompactionStarted`/`CompactionFinished`/`RetryOccurred` |
| `ChatStreamAssembler` | `Agent.Core\Services\ChatStreamAssembler.cs` | уже реализован: превращает поток в стабильные сегменты reasoning/content (`Opened`/`Text`/`Closed`) |
| `ChatStreamPhase` / `ChatStreamDelta.Phase` | `Agent.Core\Models\Models.cs` | уже реализованы: `ReasoningBegin/Delta/End`, `ContentBegin/Delta/End`, `TurnEnd` |
| `AgentRunner.ReportTurnEnd` | `Agent.Core\AgentTools\AgentRunner.cs` | уже реализован: закрывает сегменты в конце вызова модели |
| `ChatMessage` / `ChatMessageKind` / `ChatAttachment` / `AgentPhase` | `Agent.Core\Models\*` | UI-free домен чата |
| `StubAiProvider` | `Agent.Core\Services\StubAiProvider.cs` | готовый провайдер-заглушка для smoke-хоста |

Вывод: инфраструктура стрима уже готова. Не хватает **общего класса-сессии**, который
свяжет `ChatModel + AgentRunner + ChatStreamAssembler` и заменит продублированный
host-код, а также **базового хоста** в WPF-тулките.

---

## 3. Целевая архитектура и разделение ответственности

| Операция | Сейчас | Станет |
|---|---|---|
| `SendChatAsync` (drain очереди → AgentRunner → assembler → ChatMessage → Phase/tokens) | каждый хост | **shared** `ChatAgentSession` |
| `CancelAgentRun` / `ResumeAgentRunAsync` / `IsAgentRunning` (CTS + `AgentResumeSnapshot`) | каждый хост | **shared** `ChatAgentSession` |
| `StartNewChat` / `OpenConversation` / `OnConversationRemoved` (сброс run + `ChatModel.*`) | каждый хост | **shared** `ChatAgentSession` |
| `CompactNowAsync` (`AgentRunner.CompactAsync` + `ApplyCompactedTranscript`) | каждый хост | **shared** `ChatAgentSession` |
| `ShowDocsIndex` (рендер индекса документации) | каждый хост | **shared**, WPF-тулкит (`ChatControl`) |
| `RequestMemoryUpdate` (вставить prompt в input) | каждый хост | **shared**, WPF-тулкит (`ChatControl`) |
| `OpenCommitControl` / `SendBuildErrors` / `OpenCreateRobot` / `RebuildAgentTools` | каждый хост | **остаётся у хоста** (DTE/Git/Roslyn/роботы) |
| Сборка агентного окружения (AI provider, registry, permissions, budget, prompt factory, approval gate) | каждый хост | **остаётся у хоста** через узкий контракт |

Принцип:

- `Agent.Models` остаётся UI-free и testable headless — поэтому ядро кладём туда, а не в WPF.
- `Agent.Toolkit.Wpf` добавляет только тонкий WPF-фасад `ChatHostBase`.
- `IChatHost` не ломаем: он остаётся фасадом, реализованным базовым классом.

---

## 4. Новые компоненты

### 4.1. Контракты в `Agent.Core\Contracts`

```csharp
/// <summary>
/// Фабрика агентного окружения, которую поставляет хост. Сессия не знает,
/// как собирается AgentRunner, какие инструменты/права/бюджет используются.
/// </summary>
public interface IAgentRuntimeProvider
{
    /// <summary>Создаёт готовый AgentRunner (provider + registry + permissions + budget + prompt factory + approval gate).</summary>
    AgentRunner CreateRunner();

    /// <summary>Бюджет контекста, используемый RunAsync/ResumeAsync/CompactAsync.</summary>
    ContextBudget Budget { get; }

    /// <summary>System-prompt, используемый при компакции.</summary>
    string CompactSystemPrompt { get; }
}

/// <summary>
/// Действия, которые действительно зависят от хоста (VS/DTE, Git, Roslyn, роботы).
/// Общая логика чата сюда не входит.
/// </summary>
public interface IChatHostActions
{
    void OpenCommitControl();
    void SendBuildErrors();
    void OpenCreateRobot();
    void RebuildAgentTools();

    // Scenario hooks (checkpoint / workspace scoping):
    void ResetCheckpointState();
    Task RehydrateCheckpointAsync(string conversationId);
    void OnConversationRemoved();
}
```

> Точный shape (`IAgentRuntimeProvider` vs. value-object `AgentRuntime`, один
> `IChatHostServices` вместо двух контрактов) фиксируется на этапе A до реализации.
> Здесь приведён рабочий вариант.

### 4.2. `Agent.Models\ChatAgentSession.cs` (новый, UI-free)

Владеет агентным циклом и «общей» частью сценариев:

```csharp
public sealed class ChatAgentSession
{
    public ChatAgentSession(
        ChatModel model,
        IAgentRuntimeProvider runtimeProvider,
        IChatHostActions hostActions,
        IAgentLogger logger = null);

    public bool IsRunning { get; }
    public Task SendAsync(string text, IReadOnlyList<ChatAttachment> attachments);
    public void Cancel();
    public Task ResumeAsync();
    public void RebuildRuntime();
    public void StartNewChat();
    public bool OpenConversation(string id);
    public void OnConversationRemoved();
    public Task CompactNowAsync();
}
```

Внутренности `SendAsync`:

1. `ChatModel.Enqueue(text, attachments)` → цикл `DequeueNext()` (drain очереди).
2. Конвертация видимого транскрипта в провайдерный формат:
   `ChatMessage` → `ChatMessageData`, `ChatAttachment` → `ChatImagePart`.
3. `AgentRunner.RunAsync(..., IProgress<ChatStreamDelta>, IProgress<AgentResumeSnapshot>)`.
4. `ChatStreamAssembler.OnDelta(delta)` в `IProgress<ChatStreamDelta>.Report`.
5. Подписка на `ChatStreamAssembler.SegmentChanged` и маппинг в `ChatMessage`:

| `SegmentChanged` | Действие |
|---|---|
| `Opened`, `Kind = Reasoning` | создать `ChatMessage` (`Kind = AgentText`, `IsThinking = true`); запомнить по `SegmentId` |
| `Text`, `Kind = Reasoning` | `message.Reasoning = e.Text` |
| `Closed`, `Kind = Reasoning` | `message.Reasoning = e.Text`; `IsThinking = false` |
| `Opened`, `Kind = Content` | создать `ChatMessage` (`Kind = AgentText`, `IsPartial = true`) |
| `Text`, `Kind = Content` | `message.Text = e.Text` |
| `Closed`, `Kind = Content` | `message.Text = e.Text`; `IsPartial = false` |

6. Обновление `ChatModel`: `Phase`, `HasResumableRun`, `AppendMessage`, `RecordTokenUsage`.

`Cancel`/`ResumeAsync` — `CancellationTokenSource` + `AgentResumeSnapshot`.
`StartNewChat` — `Cancel` → `ChatModel.StartNewChat()` → `hostActions.ResetCheckpointState()`.
`OpenConversation` — `Cancel` → `ChatModel.OpenConversation(id)` → `hostActions.RehydrateCheckpointAsync(id)`.
`OnConversationRemoved` — сброс `AgentResumeSnapshot` → `hostActions.OnConversationRemoved()`.
`CompactNowAsync` — `AgentRunner.CompactAsync` → `ChatModel.ApplyCompactedTranscript`.

### 4.3. `Agent.Toolkit.Wpf\Controls\ChatHostBase.cs` (новый)

Абстрактный фасад, реализующий `IChatHost`:

```csharp
public abstract class ChatHostBase : IChatHost
{
    protected ChatHostBase(ChatModel model, IAgentRuntimeProvider runtimeProvider, IAgentLogger logger = null);

    protected ChatAgentSession Session { get; }

    /// <summary>Хост наследует и реализует только узкий набор действий.</summary>
    protected abstract IChatHostActions HostActions { get; }

    // IChatHost: SendChatAsync/CancelAgentRun/ResumeAgentRunAsync/IsAgentRunning/
    //            StartNewChat/OpenConversation/OnConversationRemoved/CompactNowAsync -> Session
    //            OpenCommitControl/SendBuildErrors/OpenCreateRobot/RebuildAgentTools -> HostActions
}
```

### 4.4. `ChatControl` берёт на себя UI-операции

- `ShowDocsIndex()` — рендерит индекс документации в `DocsContentControl`
  через существующий `MarkdownRenderer` (источник — `HelpDocumentation` либо
  предоставленный хостом текст).
- `RequestMemoryUpdate()` — вставляет memory-bank prompt в `InputBox` через
  существующий `SetInputText`.
- Обработчики `OnDocsClick` / `OnDocsHome` / `OnUpdateMemory` вызывают эти методы
  напрямую, а не `Host.*`.

Обратная совместимость: `ShowDocsIndex` / `RequestMemoryUpdate` **остаются** в
`IChatHost`, но `ChatHostBase` реализует их как делегирование во `View` (или no-op).
Это убирает необходимость ломать хосты в один момент.

---

## 5. Поэтапный план

### Этап A — `Agent.Core` и `Agent.Models`

1. Добавить `IAgentRuntimeProvider` и `IChatHostActions` в `Agent.Core\Contracts`.
2. Реализовать `ChatAgentSession` в `Agent.Models`.
3. Перенести конвертацию `ChatMessage → ChatMessageData` и маппинг
   `ChatStreamAssembler → ChatMessage` из хостов в `ChatAgentSession`.
4. Добавить юнит-тесты `ChatAgentSessionTests` в `Agent.Tests`
   (fake `IAgentRuntimeProvider` / `IChatHostActions` / `IChatHistoryStore`):
   - Phase, HasResumableRun;
   - drain очереди;
   - cancel / resume;
   - компакция.

### Этап B — `Agent.Toolkit.Wpf`

5. Добавить `ChatHostBase`.
6. В `ChatControl` реализовать локальные `ShowDocsIndex` / `RequestMemoryUpdate`,
   обновить обработчики и XML-доки.
7. Обновить документацию `IChatHost`: пометить `ShowDocsIndex` / `RequestMemoryUpdate`
   как legacy (контракт сохранить).

### Этап C — `Agent.ChatHost` (standalone smoke-host)

8. Удалить `StubChatHost.cs`.
9. Добавить `StandaloneRuntimeProvider` — собирает `AgentRunner` на базе
   `Agent.Core.Services.StubAiProvider` + пустой `AgentToolRegistry` +
   дефолтные permissions/budget (сохраняет «эхо-режим», но уже через полный
   агентный цикл тулкита).
10. Добавить no-op `StandaloneHostActions`.
11. В `MainWindow.xaml.cs` заменить `new StubChatHost(model, logger)` на
    `ChatModel + StandaloneChatHost : ChatHostBase + ChatControl`.

### Этап D — внешние хосты (AI-RPA, RPAWriter2, VibeCoder)

12. Заменить их собственные реализации `IChatHost` на наследование `ChatHostBase`.
13. Оставить только `IAgentRuntimeProvider` и `IChatHostActions`.
14. Для AI-RPA обновить `AgentToolkit_Wpf_Chat_Integration_Plan.md` и стражи
    `AiRpaStudio.Tests`; удалить перенесённую в тулкит логику из
    `StudioChatHost` / `ChatViewModel`.

### Этап E — документация и упаковка

15. Обновить `README.md`, `AgentToolkit.Wpf/README.md`
    (новые `ChatAgentSession`, `ChatHostBase`, контракты).
16. `pack.ps1` и `.github/workflows/build.yml` не менять: `Agent.Models` и
    `Agent.Toolkit.Wpf` уже собираются до `pack`, новый код попадёт в пакеты
    `AgentToolkit` и `AgentToolkit.Wpf` автоматически.

### Этап F — верификация

17. `dotnet build AgentToolkit.slnx` (0 ошибок).
18. `dotnet test Agent.Tests` (новые тесты сессии зелёные).
19. Запуск `Agent.ChatHost` — чат отвечает через `ChatAgentSession` + `StubAiProvider`.
20. Для AI-RPA: `MSBuild AiRpa.sln /p:Configuration=Release` + прогон `AiRpaStudio.Tests`.

---

## 6. Рекомендуемый порядок коммитов

1. `feat(core/models): IAgentRuntimeProvider + IChatHostActions + ChatAgentSession (+тесты)`
2. `feat(toolkit.wpf): ChatHostBase + локальные ShowDocsIndex/RequestMemoryUpdate в ChatControl`
3. `refactor(chathost): перевести standalone-хост на ChatHostBase/StubAiProvider, удалить StubChatHost`
4. `refactor(hosts): миграция AI-RPA/RPAWriter2/VibeCoder на ChatHostBase` (в соответствующих репозиториях)
5. `docs: обновить README и планы интеграции`

---

## 7. Чек-лист верификации

- [ ] `ChatAgentSession` покрыт юнит-тестами headless (Agent.Tests).
- [ ] `ChatHostBase` реализует `IChatHost`, общие методы делегируют в `ChatAgentSession`.
- [ ] `StubChatHost` удалён; `Agent.ChatHost` работает через `ChatHostBase` + `StubAiProvider`.
- [ ] `ChatControl` сам рендерит docs index и вставляет memory prompt (без `Host.*`).
- [ ] `IChatHost` сохранён как фасад (обратная совместимость).
- [ ] Сборка `AgentToolkit.slnx` — 0 ошибок.
- [ ] Прогон тестов зелёный.
- [ ] Smoke-запуск `Agent.ChatHost` проходит end-to-end.

---

## 8. Риски

| Риск | Меры |
|---|---|
| Хосты ломаются при переносе логики | `IChatHost` остаётся фасадом; перенос по этапам, каждый этап компилируется |
| `ChatAgentSession` тянет слишком много ответственности | разделение: сессия — только общий цикл, хост — `IAgentRuntimeProvider`/`IChatHostActions` |
| Маппинг стрима на `ChatMessage` теряет edge-case'ы | переносим таблицу из `chat-stream-reasoning-segments-plan.md` §9 как есть; тесты на сегменты |
| `ToolCallProgressTracker` дублируется | решение: подписку на tool-события держит сессия, карточки пишет через модель; трекер — кандидат на перенос в shared |
| WPF-зависимости протекают в `Agent.Models` | ядро в `Agent.Models` не ссылается на WPF; WPF-фасад изолирован в `Agent.Toolkit.Wpf` |

---

## 9. Зафиксированные решения

- **Вариант архитектуры:** ядро `ChatAgentSession` в `Agent.Models` (UI-free) +
  `ChatHostBase` в `Agent.Toolkit.Wpf`; `IChatHost` сохраняется как фасад.
- **`ShowDocsIndex` / `RequestMemoryUpdate`** остаются в `IChatHost` как legacy,
  но `ChatControl` реализует их сам.
- **`StubChatHost`** удаляется, заменяется `ChatHostBase` + `StubAiProvider`.
- **CI/упаковка** не меняются: новые типы попадают в существующие пакеты.
