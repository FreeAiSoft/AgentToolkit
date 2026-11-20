# План: мигающий зелёный индикатор выполняющейся команды

## Цель

Пока команда (tool call) выполняется, в карточке команды в чате отображается
**мигающий зелёный огонёк** — в точности такой же, как зелёная точка активного
чата на плашке истории (`Theme.ActiveDot`, Ellipse 8×8). Огонёк находится **между
левой границей карточки и шевроном**. Как только команда выполнилась — огонёк
исчезает, а в карточку подставляется результат.

## Архитектурный принцип

Фича реализуется **целиком на уровне тулкита** (`Agent.Core` + `Agent.Toolkit.Wpf`)
и **не зависит от потребителя тулкита**. Потребитель (`RPA.Chat`, VSIX-хосты и т.д.)
не содержит и не добавляет никакой логики индикатора.

Сейчас жизненный цикл карточки команды находится снаружи тулкита: карточку создаёт
потребитель уже **после** выполнения инструмента. Чтобы тулкит сам показывал состояние
«выполняется», он должен **сам владеть карточками команд**. Для этого вводится шин
событий инструмента и трекер, живущий в Core, а WPF-слой только рендерит состояние.

## Слои изменений

| Слой | Проект | Что меняем |
|------|--------|-----------|
| Core | `Agent.Core` | `ToolExecutionBus`, уведомления из `AgentRunner`, `ChatMessage.IsExecuting`/`SetToolResult`, `ToolCallProgressTracker` |
| WPF | `Agent.Toolkit.Wpf` | подключение трекера в `ChatControl`, оверлей-Ellipse + анимации + контент 1px в `ChatMessageCard` |

---

## 1. Agent.Core — шин событий инструмента

Файл: `Agent.Core\AgentTools\ToolExecutionBus.cs` (новый).

Тулкит не должен зависеть от конкретного экземпляра `AgentRunner`, поэтому вводится
лёгкий статический шин, публикующий только нужные UI-факты:

```csharp
using System;

namespace Agent.Core.AgentTools
{
    public static class ToolExecutionBus
    {
        public static event EventHandler<AgentToolCall> Started;
        public static event EventHandler<AgentToolCall> Executed;

        internal static void RaiseStarted(AgentToolCall e) => Started?.Invoke(null, e);
        internal static void RaiseExecuted(AgentToolCall e) => Executed?.Invoke(null, e);
    }
}
```

---

## 2. Agent.Core — AgentRunner публикует старт/финиш

Файл: `Agent.Core\AgentTools\AgentRunner.cs`.

1. В `ExecuteNativeToolCallAsync` и `ExecuteJsonToolCallAsync` поднять `Started`
   **после** `RequestApprovalAsync` и **перед** `tool.ExecuteAsync(...)`.
2. Во всех местах, где поднимается instance-событие `ToolExecuted`, дополнительно
   уведомлять шин `ToolExecutionBus.RaiseExecuted(...)`.

Правила:
- **Blocked**: `Started` НЕ поднимается — сразу `Executed` с `Blocked = true`.
- **Rejected**: `Started` НЕ поднимается — сразу `Executed` с `Rejected = true`.
- Instance-событие `ToolExecuted` сохраняется для обратной совместимости.

---

## 3. Agent.Core.Models — состояние и обновление результата

Файл: `Agent.Core\Models\ChatMessage.cs`.

- `public bool IsExecuting` (INPC, default false).
- `public void SetToolResult(string result)` — обновляет `ToolCallPayload.Result` и
  уведомляет `ToolBodyText`/`ToolBodyBlocks`/`DisplayText`/`SingleLineDisplayText`/`Blocks`/`MessageTitle`.

`ChatMessageMapper` не трогаем: `IsExecuting` — runtime-состояние, не персистится.

---

## 4. Agent.Core — ToolCallProgressTracker

Файл: `Agent.Core\Services\ToolCallProgressTracker.cs` (новый, UI-free).

Трекер подписывается на `ToolExecutionBus` и управляет карточками команд:
- `Started` → создать `ChatMessage { Kind=ToolCall, IsExecuting=true, Result="" }`.
- `Executed` после `Started` → обновить ту же карточку (`SetToolResult` + `IsExecuting=false`).
- `Executed` без `Started` (blocked/rejected) → создать финальную карточку.
- `spawn_agent` ведёт счётчик глубины субагента: пока глубина > 0, вложенные инструменты субагента игнорируются (у субагента своя Subagent-карточка); `ask_question` игнорируется (у него своя карточка).
- `Detach` гасит зависшую точку.

---

## 5. Agent.Toolkit.Wpf

### 5.1. ChatControl владеет трекером

Файл: `Agent.Toolkit.Wpf\Controls\ChatControl.xaml.cs`, `OnDataContextChanged`:
создавать `ToolCallProgressTracker(() => model.Messages, a => Dispatcher.BeginInvoke(a))`
и `Attach()`; при отключении старого `ChatModel` — `Detach()`.

### 5.2. Огонёк

Файлы: `Agent.Toolkit.Wpf\Styles\ChatResources.xaml` (шаблон `ChatMessageCard`) +
`Agent.Toolkit.Wpf\Controls\Chat\ChatMessageCard.cs` (code-behind анимации).

- Огонёк — **оверлей**, не отдельная колонка: лежит в колонке шеврона
  (`Grid.Column="0"`, `HorizontalAlignment="Left"`, 6×6, `Margin="-7,0,0,0"`).
  Отрицательный margin даёт нулевой/отрицательный вклад в ширину `Auto`-колонки,
  поэтому шеврон **не сдвигается**; точка занимает зазор в 1px между левой границей
  карточки и шевроном.
- `Fill=Theme.ActiveDot`, `RenderTransformOrigin=0.5,0.5` + `ScaleTransform`.
- Анимации в code-behind:
  - плавное появление 180 мс (`Opacity 0→1`, `ScaleX/Y 0.5→1`);
  - пульс яркостью и размером (`Opacity 1→0.15`, `ScaleX/Y 1→0.7`, 550 мс,
    `AutoReverse`, `RepeatBehavior=Forever`), стартует после появления;
  - плавное исчезновение 150 мс (`Opacity current→0`, затем `Collapsed`).
- **Контент контролов сообщений приподнят на 1px**: у `ContentPresenter` `Body`
  верхний отступ `Margin="0,6,0,0"` → `Margin="0,5,0,0"` (все per-kind контролы
  рендерят свой контент внутри этого `ContentPresenter`).



---

## 6. Тесты

### 6.1. Модель — `ChatMessage` (`ModelsDataTests.cs`)
- `IsExecuting_DefaultsFalse`
- `IsExecuting_RaisesPropertyChanged_OnChange_NotOnSameValue`
- `SetToolResult_UpdatesResultAndNotifiesToolBodyBlocks`
- `SetToolResult_NoPayload_DoesNotThrow`

### 6.2. Runner + шин (`AgentToolsTests.cs`)
- `RunAsync_ExecutedTool_RaisesStartedBeforeExecuted`
- `RunAsync_BlockedTool_RaisesExecuted_NotStarted`
- `RunAsync_RejectedTool_RaisesExecuted_NotStarted`

### 6.3. Трекер (`ToolCallProgressTrackerTests.cs`, новый)
- `Started_CreatesExecutingCard`
- `ExecutedAfterStarted_UpdatesSameCard`
- `ExecutedWithoutStarted_CreatesFinalCard`
- `SpecialTools_AreIgnored`
- `SubagentTools_AreIgnored`
- `Detach_StopsTrackingAndClearsActiveCard`

### 6.4. Персистентность (`ChatMessageMapperTests.cs`)
- `ToolCall_RuntimeIsExecuting_IsNotPersisted`

### 6.5. WPF (smoke, вручную)

---

## 7. Порядок работ (red → green → refactor)

1. Модель: тесты 6.1 + 6.4 → `IsExecuting` + `SetToolResult`.
2. Шин + Runner: тесты 6.2 → `ToolExecutionBus` + уведомления.
3. Трекер: тесты 6.3 → `ToolCallProgressTracker`.
4. WPF: трекер в `ChatControl` + Ellipse/blink.
5. Smoke.

---

## Не меняем

- `Theme.Dark.xaml` / `Theme.Light.xaml` — используем `Theme.ActiveDot`.
- `ChatMessageMapper`.
- Потребитель тулкита.
