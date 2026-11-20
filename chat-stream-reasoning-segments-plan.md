# План: отдельные reasoning-блоки в стриме чата (варианты 2 + 3)

> Статус: **только план, код не изменялся.**
> Цель: исправить баг «чат добавляется только в первый reasoning» — reasoning
> должен накапливаться в каждом сегменте хода, как в Cline.

## 1. Проблема

Сейчас стрим отдаёт только «сырые» дельты без границ сегментов:

- `ChatStreamDelta { ReasoningDelta, ContentDelta }` (`Agent.Core/Models/Models.cs`)
- провайдеры шлют только куски текста:
  - `AnthropicProvider.ReadSseStreamAsync` — `thinking_delta` → `ReasoningDelta`,
    `text_delta` → `ContentDelta`;
  - `OpenAiCompatibleProvider.ReadSseStreamAsync` — `reasoning_content` →
    `ReasoningDelta`, `content` → `ContentDelta`.

`AgentRunner.RunLoopAsync` вызывает модель несколько раз за один ход
(reasoning → tool → reasoning → tool → final). Так как в потоке нет ни
начала/конца блока, ни стабильного id сегмента, принимающая сторона
(хост, реализующий `IChatHost` и подписанный на `IProgress<ChatStreamDelta>`)
не может надёжно понять, где заканчивается один reasoning и начинается
следующий. По симптому видно, что хост держит одну ссылку на «текущее»
reasoning-сообщение и не сбрасывает её между сегментами — все последующие
дельты дописываются в первый блок.

## 2. Как это решено в Cline

Ориентир — `MessageTranslatorState` (VSCode-путь `message-translator.ts`):

1. Для reasoning есть отдельное состояние: `streamingReasoningTs`
   (стабильный id блока) и `streamingReasoningText` (накопленный текст).
2. `content_start(reasoning)` → `getStreamingReasoningTs()` создаёт id при
   первом дельте; все следующие дельты обновляют **тот же** id
   (`partial: true`).
3. `content_end(reasoning)` → `clearStreamingReasoning()` возвращает id,
   финализирует блок (`partial: false`) и **сбрасывает** `streamingReasoningTs`
   и текст.
4. `reset()` вызывается на `iteration_start` и сбрасывает все
   streaming-указатели.

Итог: каждый новый reasoning-сегмент получает **новый** id → новое сообщение
в чате, а не дописывается в первый блок.

## 3. Выбранное решение

Совмещаем оба варианта:

- **Вариант 2** — явные фазы `Begin/Delta/End` в потоке (`ChatStreamDelta`).
- **Вариант 3** — накопитель/ассемблер `ChatStreamAssembler` в `Agent.Core`,
  который превращает фазы в стабильные, индемпотентные операции над
  сегментами и сбрасывается на границе итерации.

Границы сегментов определяют провайдеры (там, где они известны явно —
Anthropic `content_block_start/stop`) и `AgentRunner` (сигнал конца одного
вызова модели внутри цикла). Хост перестаёт «угадывать» границы и просто
применяет операции ассемблера к `ChatMessage`.

## 4. Новый контракт данных

### 4.1. `ChatStreamPhase`

Добавить в `Agent.Core/Models/Models.cs` рядом с `ChatStreamDelta`:

```csharp
public enum ChatStreamPhase
{
    /// <summary>Legacy-дельта без явной фазы (обратная совместимость).</summary>
    Delta = 0,

    ReasoningBegin,
    ReasoningDelta,
    ReasoningEnd,

    ContentBegin,
    ContentDelta,
    ContentEnd,

    /// <summary>Конец одного вызова модели внутри цикла; ассемблер закрывает
    /// незакрытые сегменты и сбрасывает streaming-указатели (аналог Cline reset()).</summary>
    TurnEnd
}
```

### 4.2. `ChatStreamDelta`

Расширить существующий класс (аддитивно, без ломки старых потребителей):

```csharp
public class ChatStreamDelta
{
    /// <summary>Фаза события. По умолчанию Delta — legacy-поведение.</summary>
    public ChatStreamPhase Phase { get; set; } = ChatStreamPhase.Delta;

    /// <summary>Текст события: дельта для *Delta, накопленный/финальный текст для *End.</summary>
    public string Text { get; set; }

    /// <summary>Стабильный id сегмента. Ассемблер майнит его на *Begin,
    /// если провайдер не передал свой.</summary>
    public string SegmentId { get; set; }

    // ---- legacy-поля (остаются для обратной совместимости) ----
    /// <summary>Incremental reasoning text (legacy).</summary>
    public string ReasoningDelta { get; set; }

    /// <summary>Incremental answer content text (legacy).</summary>
    public string ContentDelta { get; set; }
}
```

Правила заполнения:

- В фазе `ReasoningDelta`/`ContentDelta` заполняются **и** `Text`, **и**
  соответствующий legacy-поле (`ReasoningDelta`/`ContentDelta`) — чтобы старые
  хосты продолжали работать как раньше.
- В фазах `*Begin`/`*End` legacy-поля пустые; старые хосты такие события
  просто игнорируют (они проверяют только `ReasoningDelta`/`ContentDelta`).
- `TurnEnd` не несёт текста и не виден старым хостам.

## 5. Новый компонент: `ChatStreamAssembler`

Файл: `Agent.Core/Services/ChatStreamAssembler.cs`.

### 5.1. Типы событий

```csharp
public enum ChatStreamSegmentKind { Reasoning, Content }

public enum ChatStreamSegmentEventKind { Opened, Text, Closed }

public sealed class ChatStreamSegmentEventArgs : EventArgs
{
    public string SegmentId { get; set; }
    public ChatStreamSegmentKind Kind { get; set; }
    public ChatStreamSegmentEventKind Event { get; set; }

    /// <summary>Накопленный текст (Event == Text) или финальный текст (Event == Closed).</summary>
    public string Text { get; set; }
}
```

### 5.2. API ассемблера

```csharp
public sealed class ChatStreamAssembler
{
    /// <summary>Применить одно событие потока (фазу или legacy-дельту).</summary>
    public void OnDelta(ChatStreamDelta delta);

    /// <summary>Событие, на которое подписывается хост для обновления ChatMessage.</summary>
    public event EventHandler<ChatStreamSegmentEventArgs> SegmentChanged;
}
```

### 5.3. Семантика

| Вход | Поведение |
| --- | --- |
| `ReasoningBegin` | Майнит/принимает `SegmentId`, открывает reasoning-сегмент, шлёт `Opened`. |
| `ReasoningDelta` | Добавляет `Text` к накопленному reasoning, шлёт `Text` с накопленным текстом. |
| `ReasoningEnd` | Финализирует reasoning (текст = `Text` или накопленный), шлёт `Closed`, закрывает сегмент. |
| `ContentBegin` / `ContentDelta` / `ContentEnd` | Аналогично для content. |
| `Delta` (legacy) | Graceful degradation: `ReasoningDelta` → авто-open reasoning + append; `ContentDelta` → авто-close открытого reasoning, авто-open content + append. |
| `TurnEnd` | Закрывает все открытые сегменты (`Closed`), затем сбрасывает указатели (аналог `reset()` в Cline). |

Инварианты:

- Один открытый reasoning-сегмент и один открытый content-сегмент максимум
  одновременно.
- `SegmentId` уникален в рамках экземпляра ассемблера; майнится на `*Begin`.
- Повторный `*Begin` без `*End` для того же `Kind` сначала закрывает предыдущий
  сегмент (защита от «провайдер забыл End»).
- Ассемблер **не** знает про `ChatMessage`/WPF — он UI-agnostic и тестируется
  без UI. Маппинг на `ChatMessage` остаётся в хосте.

## 6. Изменения в `AnthropicProvider`

Файл: `Agent.Core/Services/AnthropicProvider.cs`, метод `ReadSseStreamAsync`.

1. Добавить локальный словарь типов блоков по `index`:
   `Dictionary<int, string> contentBlockTypes` (`"text"`, `"thinking"`,
   `"tool_use"`).
2. В `case "content_block_start"`:
   - запомнить `contentBlockTypes[index] = block.type`;
   - для `thinking` → `ReasoningBegin`;
   - для `text` → `ContentBegin`.
   - `tool_use` — как сейчас, только в `toolBlocks`.
3. В `case "content_block_delta"`:
   - `thinking_delta` → `ReasoningDelta` (`Text = thinking`, legacy
     `ReasoningDelta = thinking`);
   - `text_delta` → `ContentDelta` (`Text = text`, legacy `ContentDelta = text`);
   - `input_json_delta` — без изменений.
4. Добавить `case "content_block_stop"`:
   - взять тип по `index` из `contentBlockTypes`;
   - `thinking` → `ReasoningEnd` (финальный текст — накопленный reasoning);
   - `text` → `ContentEnd` (финальный текст — накопленный content);
   - удалить запись из `contentBlockTypes`.
5. После цикла чтения (stream ended / `message_stop`) закрыть оставшиеся
   открытые `text`/`thinking` блоки (`*End`), чтобы не оставлять висячих
   сегментов.

Накопление в `StringBuilder reasoning`/`content` и итоговый `ChatResponse`
**не меняются** — меняется только то, что репортится в `progress`.

## 7. Изменения в `OpenAiCompatibleProvider`

Файл: `Agent.Core/Services/OpenAiCompatibleProvider.cs`, метод `ReadSseStreamAsync`
(SSE-ветка и non-SSE fallback).

У OpenAI-совместимых нет явных `content_block_start/stop`, поэтому границы
определяем по смене канала и по завершению стрима:

1. Локальная переменная `currentChannel` (`null` / `reasoning` / `content`).
2. На каждом чанке:
   - `reasoningDelta != ""` и `currentChannel != reasoning` →
     `ReasoningBegin` (новый сегмент), `currentChannel = reasoning`;
   - затем `ReasoningDelta` (`Text = reasoningDelta`, legacy
     `ReasoningDelta = reasoningDelta`).
   - `contentDelta != ""` и `currentChannel != content` →
     сначала `ReasoningEnd` (если `currentChannel == reasoning`), затем
     `ContentBegin`, `currentChannel = content`;
   - затем `ContentDelta` (`Text = contentDelta`, legacy
     `ContentDelta = contentDelta`).
3. При `[DONE]` / завершении чтения:
   - `ReasoningEnd` (если `currentChannel == reasoning`);
   - `ContentEnd` (если `currentChannel == content`).
4. Non-SSE fallback (обычный JSON-ответ):
   - `ReasoningBegin` + `ReasoningDelta` (полный `r`) + `ReasoningEnd`, если
     `r` не пуст;
   - `ContentBegin` + `ContentDelta` (полный `c`) + `ContentEnd`, если `c`
     не пуст.

`RecordUsage`, накопление `reasoning`/`content` и `ChatResponse` не меняются.

## 8. Изменения в `AgentRunner`

Файл: `Agent.Core/AgentTools/AgentRunner.cs`.

### 8.1. Сигнал конца вызова модели

В `RunLoopAsync` сразу после
`var response = await CallWithRecoveryAsync(...)` добавить
`ReportTurnEnd(progress)`:

```csharp
private static void ReportTurnEnd(IProgress<ChatStreamDelta> progress)
    => progress?.Report(new ChatStreamDelta { Phase = ChatStreamPhase.TurnEnd });
```

Это закрывает reasoning/content одного вызова модели до выполнения
инструментов и до следующего вызова модели (аналог `reset()` в Cline на
`iteration_start`).

### 8.2. Non-streaming fallback

В `CallModelAsync` заменить текущее
`progress.Report(new ChatStreamDelta { ReasoningDelta = response.Reasoning });`
на фазы:

- если `response.Reasoning` не пуст:
  `ReasoningBegin` → `ReasoningDelta` (полный текст, legacy `ReasoningDelta`) →
  `ReasoningEnd`;
- если `response.Answer` не пуст:
  `ContentBegin` → `ContentDelta` (полный текст, legacy `ContentDelta`) →
  `ContentEnd`.

`TurnEnd` после вызова уже ставится в `RunLoopAsync` (п. 8.1).

### 8.3. Замечание

`AgentRunner` не должен сам создавать `ChatMessage`; он остаётся UI-agnostic
и только эмитит фазы через `IProgress<ChatStreamDelta>`. Ассемблер живёт на
стороне потребителя (хоста), как и сейчас живёт логика применения дельт.

## 9. Интеграция хоста (потребитель `IProgress<ChatStreamDelta>`)

Хост (реализация `IChatHost`, внешний по отношению к этому решению):

1. Создаёт **один** `ChatStreamAssembler` на ход агента (на вызов
   `RunAsync`/`ResumeAsync`).
2. В колбэке `IProgress<ChatStreamDelta>.Report(delta)` вызывает
   `assembler.OnDelta(delta)`.
3. Подписывается на `assembler.SegmentChanged` и маппит события на
   `ChatMessage`:

| `Event` | Действие |
| --- | --- |
| `Opened`, `Kind = Reasoning` | Создать `ChatMessage` (`Kind = AgentText`, `IsThinking = true`); запомнить по `SegmentId`. |
| `Text`, `Kind = Reasoning` | `message.Reasoning = e.Text` (накопленный текст; `ReasoningDisplayText` сам показывает хвост). |
| `Closed`, `Kind = Reasoning` | `message.Reasoning = e.Text`; `IsThinking = false`. |
| `Opened`, `Kind = Content` | Создать `ChatMessage` (`Kind = AgentText`, `IsPartial = true`). |
| `Text`, `Kind = Content` | `message.Text = e.Text` (накопленный текст). |
| `Closed`, `Kind = Content` | `message.Text = e.Text`; `IsPartial = false`. |

Ключевой эффект: новый `SegmentId` на каждом `ReasoningBegin` гарантирует
отдельное `ChatMessage` на каждый reasoning-сегмент хода; старый баг
«дописывается в первый reasoning» исчезает.

> Хост-код находится вне текущего решения, поэтому эта часть плана — контракт
> для владельца хоста. Все библиотечные изменения (п. 4–8) готовятся здесь и
> покрываются тестами, чтобы хосту осталась только тривиальная подписка.

## 10. Обратная совместимость

- `Phase` по умолчанию `Delta`, поэтому любые старые отправители продолжают
  работать.
- Провайдеры параллельно заполняют legacy `ReasoningDelta`/`ContentDelta` —
  старые хосты видят ровно прежний поток дельт (с тем же багом, пока не
  перейдут на ассемблер).
- Новые `*Begin`/`*End`/`TurnEnd` старыми хостами игнорируются (в них нет
  legacy-текста).
- `ChatStreamAssembler` дополнительно умеет переваривать чистые legacy-дельты
  (`Phase = Delta`), поэтому хост может включить ассемблер до обновления
  провайдеров и получить улучшение (границы по эвристике), а после обновления —
  точные границы по фазам.

## 11. План тестов

### 11.1. Новый файл `Agent.Tests/ChatStreamAssemblerTests.cs`

- `ReasoningBegin_Delta_End_ProducesOneClosedSegment` — один сегмент, `Opened`
  → `Text`(накопление) → `Closed`.
- `TwoReasoningSegments_ProduceDifferentSegmentIds` — два `ReasoningBegin`
  подряд → разные `SegmentId`, нет склейки.
- `ContentBegin_AfterReasoning_ClosesReasoningFirst` — переход reasoning →
  content закрывает reasoning.
- `TurnEnd_ClosesDanglingSegments` — открытый reasoning без `End` закрывается
  по `TurnEnd`.
- `LegacyDelta_StillAccumulates` — `Phase = Delta` с
  `ReasoningDelta`/`ContentDelta` работает через авто-open/close.

### 11.2. `Agent.Tests/ProviderTests.cs` (расширить)

- Anthropic: SSE с `content_block_start(thinking)` + `thinking_delta` +
  `content_block_stop` → ассемблер видит `ReasoningBegin` → `ReasoningDelta` →
  `ReasoningEnd`.
- Anthropic: два thinking-блока → два разных сегмента.
- OpenAI-compatible: `reasoning_content` затем `content` → корректные фазы и
  переход; `[DONE]` закрывает открытые сегменты.
- Non-SSE fallback: полный reasoning/content отдаётся как `Begin→Delta→End`.

### 11.3. `Agent.Tests/AgentToolsTests.cs` (расширить)

- Обновить/дополнить `RunAsync_TwoTurns_StreamsReasoningPerTurn`: проверить,
  что после `TurnEnd` между итерациями получаются **два** закрытых
  reasoning-сегмента (`ReasoningEnd` для каждого хода), а не одна склейка.
- Добавить сценарий с инструментом: reasoning → tool → reasoning → final даёт
  два reasoning-сегмента.

### 11.4. Регрессия

- Существующие тесты провайдеров, `AgentRunner`, `ChatMessageMapper`,
  `ModelsDataTests` должны проходить без изменений.
- Собрать `Agent.Tests` в Release и прогнать:
  `dotnet test Agent.Tests -c Release`.

## 12. Список изменяемых/добавляемых файлов

Изменяются:

- `Agent.Core/Models/Models.cs` — `ChatStreamPhase`, расширение `ChatStreamDelta`.
- `Agent.Core/Services/AnthropicProvider.cs` — фазы по `content_block_start/delta/stop`.
- `Agent.Core/Services/OpenAiCompatibleProvider.cs` — фазы по смене канала и `[DONE]`.
- `Agent.Core/AgentTools/AgentRunner.cs` — `ReportTurnEnd` и фазовый non-streaming fallback.

Добавляются:

- `Agent.Core/Services/ChatStreamAssembler.cs` — ассемблер + типы
  `ChatStreamSegmentKind`, `ChatStreamSegmentEventKind`,
  `ChatStreamSegmentEventArgs`.
- `Agent.Tests/ChatStreamAssemblerTests.cs` — тесты ассемблера.

Не трогаются (вне библиотеки):

- код хоста (реализация `IChatHost`) — только контрактная интеграция по п. 9.

## 13. Критерии приёмки

1. Каждый reasoning-сегмент хода отображается отдельным блоком/сообщением;
   последующие reasoning не дописываются в первый.
2. Переход reasoning → content закрывает reasoning-блок.
3. Вызовы инструментов между итерациями не склеивают reasoning соседних
   вызовов модели.
4. Non-streaming fallback отдаёт те же фазы, что и streaming.
5. Старые потребители legacy-полей продолжают работать (обратная
   совместимость).
6. `Agent.Tests` в Release: сборка успешна, все тесты проходят.

## 14. Порядок работ

1. `Models.cs`: `ChatStreamPhase` + расширить `ChatStreamDelta`.
2. `ChatStreamAssembler.cs` + тесты ассемблера (без провайдеров — на
   синтетических дельтах).
3. `AnthropicProvider` → фазы + тесты.
4. `OpenAiCompatibleProvider` → фазы + тесты.
5. `AgentRunner` → `TurnEnd` + фазовый fallback + тесты цикла.
6. Прогнать полный `dotnet test Agent.Tests -c Release`.
7. Передать владельцу хоста контракт п. 9 для миграции на `ChatStreamAssembler`.

## 15. Риски / открытые вопросы

- Точная сигнатура `ChatStreamAssembler` (один event с `EventKind` vs. три
  события) — финализируется при реализации; в плане выбран один event
  `SegmentChanged` для простоты.
- У Anthropic extended thinking может быть несколько thinking-блоков — это
  учтено (по одному сегменту на `content_block_start(thinking)`).
- У OpenAI-совместимых границы reasoning/content выводятся по смене канала;
  если конкретный провайдер чередует каналы, ассемблер корректно закроет/откроет
  сегменты (инвариант «максимум один открытый сегмент каждого Kind»).
- `TurnEnd` эмитится на каждый вызов модели; если в будущем появятся параллельные
  вызовы модели в одном ходе, понадобится `TurnId`/`SegmentId` с явным скоупом.
