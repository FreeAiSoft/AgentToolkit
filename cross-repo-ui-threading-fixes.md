# Точные правки: маршалинг в UI-поток и отзывчивость (AgentToolkit / RPAWriter2 / AI-RPA)

Статус: **задокументированные правки, код НЕ менялся**.
Целевая версия пакетов: **1.20** (итоговая сборка пакета `1.20.0.1` — см. раздел 4).

Документ покрывает три ранее выявленных дефекта:

1. `CollectionView` ошибку при изменении `ObservableCollection` из фонового потока (AI-RPA + защита на уровне AgentToolkit).
2. Фриз интерфейса во время потокового reasoning (визуализация reasoning в AgentToolkit + троттлинг в хосте AI-RPA).
3. Фриз интерфейса при выполнении инструментов (RPAWriter2 — чрезмерная работа на главном потоке VS).

Смежный документ: `C:\Users\Maxxx\source\repos\RPAWriter2\ui-freeze-during-tools-fix-plan.md` (диагностика и план фриза при инструментах).

---

## 1. AgentToolkit (репозиторий `AgentShared`)

### 1.1 `ChatMessagesControl` — разрешить синхронизацию коллекции с UI

Файл: `Agent.Toolkit.Wpf/Controls/ChatMessagesControl.xaml.cs`

Цель: вызвать `BindingOperations.EnableCollectionSynchronization` **до** создания `CollectionView`, чтобы WPF маршалил фоновые изменения через lock, а не бросал `NotSupportedException`.

**Добавить using:**
```csharp
using System.Windows.Data;
```

**Добавить поле:**
```csharp
private readonly object _syncRoot = new object();
```

**Было:**
```csharp
private void AttachView(ObservableCollection<ChatMessage> messages)
{
    _messages = messages;
    _view = CollectionViewSource.GetDefaultView(_messages);
    _view.Filter = FilterPredicate;
    ChatList.ItemsSource = _view;
}
```

**Стало:**
```csharp
private void AttachView(ObservableCollection<ChatMessage> messages)
{
    _messages = messages;
    BindingOperations.EnableCollectionSynchronization(_messages, _syncRoot);
    _view = CollectionViewSource.GetDefaultView(_messages);
    _view.Filter = FilterPredicate;
    ChatList.ItemsSource = _view;
}
```

---

### 1.2 `MessageQueueControl` — синхронизация коллекции + маршалинг обработчика

Файл: `Agent.Toolkit.Wpf/Controls/MessageQueueControl.xaml.cs`

**Добавить using:**
```csharp
using System.Windows.Data;
```

**Добавить поле:**
```csharp
private readonly object _syncRoot = new object();
```

**Было:**
```csharp
private void AttachCollection(ObservableCollection<PendingMessage> collection)
{
    _messages.CollectionChanged -= OnCollectionChanged;

    _messages = collection;
    QueueList.ItemsSource = _messages;
    _messages.CollectionChanged += OnCollectionChanged;
    Reindex();
    UpdateVisibility();
}

private void OnCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
{
    Reindex();
    UpdateVisibility();
    Changed?.Invoke(this, EventArgs.Empty);
}
```

**Стало:**
```csharp
private void AttachCollection(ObservableCollection<PendingMessage> collection)
{
    _messages.CollectionChanged -= OnCollectionChanged;

    _messages = collection;
    BindingOperations.EnableCollectionSynchronization(_messages, _syncRoot);
    QueueList.ItemsSource = _messages;
    _messages.CollectionChanged += OnCollectionChanged;
    Reindex();
    UpdateVisibility();
}

private void OnCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
{
    if (!Dispatcher.CheckAccess())
    {
        Dispatcher.BeginInvoke(new Action(() => OnCollectionChanged(sender, e)));
        return;
    }
    Reindex();
    UpdateVisibility();
    Changed?.Invoke(this, EventArgs.Empty);
}
```

Примечание: `OnCollectionChanged` дополнительно обёрнут в Dispatcher, потому что он трогает WPF-контролы (`QueuedLabel.Text`, `Visibility`), а `EnableCollectionSynchronization` защищает только `CollectionView`, но не произвольных подписчиков события.

---

### 1.3 `ChatControl` — синхронизация `Attachments` и `History`

Файл: `Agent.Toolkit.Wpf/Controls/ChatControl.xaml.cs`

`Messages` и `Pending` уже синхронизируются внутри `ChatMessagesControl` / `MessageQueueControl`. Нужно покрыть ещё две коллекции, которые биндятся напрямую через `ItemsSource`.

**Добавить using:**
```csharp
using System.Windows.Data;
```

**Добавить поле:**
```csharp
private readonly object _syncRoot = new object();
```

**Было (в `OnDataContextChanged`):**
```csharp
// Share the model-owned collections with the list controls.
ChatMessages.Messages = model.Messages;
MessageQueue.Messages = model.Pending;
ChatMessages.Filter = message => !ReferenceEquals(message, model.PinnedMessage);
AttachmentsList.ItemsSource = model.Attachments;
HistoryList.ItemsSource = model.History;
```

**Стало:**
```csharp
// Share the model-owned collections with the list controls.
ChatMessages.Messages = model.Messages;
MessageQueue.Messages = model.Pending;
ChatMessages.Filter = message => !ReferenceEquals(message, model.PinnedMessage);

// Register cross-thread synchronization BEFORE the views are created, so the
// CollectionView built by ItemsSource allows background mutations.
BindingOperations.EnableCollectionSynchronization(model.Attachments, _syncRoot);
BindingOperations.EnableCollectionSynchronization(model.History, _syncRoot);
AttachmentsList.ItemsSource = model.Attachments;
HistoryList.ItemsSource = model.History;
```

---

### 1.4 Лёгкий рендер reasoning во время стриминга

Цель: пока идёт стриминг (`IsThinking == true`), рендерить reasoning в однострочном `TextBox` без переноса строк. Перенос включается после завершения. Это убирает дорогой re-layout обёрнутого текста на каждый токен.

Файл: `Agent.Toolkit.Wpf/Controls/Chat/AgentTextMessageControl.xaml`

**Было:**
```xml
<TextBox Text="{Binding ReasoningDisplayText, Mode=OneWay}" TextWrapping="Wrap"
         Foreground="{DynamicResource Theme.TextMuted}" FontStyle="Italic"
         IsReadOnly="True" Background="Transparent" BorderThickness="0" Padding="0"/>
```

**Стало:**
```xml
<TextBox Text="{Binding ReasoningDisplayText, Mode=OneWay}"
         Foreground="{DynamicResource Theme.TextMuted}" FontStyle="Italic"
         IsReadOnly="True" Background="Transparent" BorderThickness="0" Padding="0">
    <TextBox.Style>
        <Style TargetType="TextBox">
            <Setter Property="TextWrapping" Value="Wrap"/>
            <Style.Triggers>
                <DataTrigger Binding="{Binding IsThinking}" Value="True">
                    <Setter Property="TextWrapping" Value="NoWrap"/>
                </DataTrigger>
            </Style.Triggers>
        </Style>
    </TextBox.Style>
</TextBox>
```

Аналогично заменить reasoning `TextBox` в:
- `Agent.Toolkit.Wpf/Controls/Chat/ResultAnswerPlanControl.xaml`
- `Agent.Toolkit.Wpf/Controls/Chat/ResultAnswerActControl.xaml`

---

### 1.5 Снизить видимый «хвост» reasoning

Файл: `Agent.Core/Models/ChatMessage.cs`

**Было:**
```csharp
public const int ReasoningStreamTailLength = 8000;
```

**Стало:**
```csharp
public const int ReasoningStreamTailLength = 2000;
```

Примечание: значение 8000 всё ещё заставляет WPF пересчитывать большой текст на каждый апдейт. 2000 символов в однострочном режиме (п. 1.4) достаточно для визуального прогресса и дёшево для layout.

---

## 2. RPAWriter2

### 2.1 `read_files` — вынести файловую работу с UI-потока

Файл: `RPAWriter2/Services/AgentTools/AgentToolFactory.cs`, метод `ReadFilesAsync`.

Принцип: на главном потоке читать **только** буферы открытых редакторов; чтение с диска, форматирование и обрезку выполнять в `Task.Run`.

**Было (суть):** после `SwitchToMainThreadAsync` весь цикл (диск + буфер + форматирование) выполнялся на главном потоке.

**Стало:**
```csharp
private async Task<string> ReadFilesAsync(JObject args, CancellationToken ct)
{
    var entries = ParseReadFilesEntries(args["files"]);
    if (entries.Count == 0)
        return "Missing or invalid \"files\" argument. Expected an array of {path, start_line?, end_line?} objects (or a string / array of strings).";

    // 1) Background: validate + resolve paths.
    var resolved = new List<(string Path, string Error, int? Start, int? End)>();
    foreach (var entry in entries)
    {
        var p = ResolvePath(entry.Path ?? "");
        if (string.IsNullOrWhiteSpace(p))
        {
            resolved.Add((null, "Error reading a files entry: missing \"path\".", entry.StartLine, entry.EndLine));
            continue;
        }
        if (!IsReadAllowed(p))
        {
            resolved.Add((null, "Error reading " + p + ": access denied — path is outside the allowed directories (solution / knowledge base / robots / skills / additional read folders) or matches a protected file name.", entry.StartLine, entry.EndLine));
            continue;
        }
        resolved.Add((p, null, entry.StartLine, entry.EndLine));
    }

    // 2) UI thread: read ONLY open editor buffers.
    var buffers = new Dictionary<string, (string Text, string Source)>(StringComparer.OrdinalIgnoreCase);
    await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(ct);
    var dte = _getDte();
    foreach (var r in resolved)
    {
        if (r.Error != null || r.Path == null)
            continue;

        var doc = dte != null ? VsDocumentService.FindOpenDocument(dte, r.Path) : null;
        if (doc == null)
            continue;

        await VsDocumentService.WaitForDocumentReadyAsync(doc).ConfigureAwait(true);
        var buffer = VsDocumentService.ReadDocumentText(doc);
        if (buffer != null)
            buffers[r.Path] = (buffer, "editor buffer");
    }

    // 3) Background: disk reads + formatting/truncation.
    return await Task.Run(() =>
    {
        var sb = new StringBuilder();
        foreach (var r in resolved)
        {
            if (r.Error != null)
            {
                sb.AppendLine(r.Error);
                continue;
            }

            string text;
            string source;
            if (buffers.TryGetValue(r.Path!, out var b))
            {
                text = b.Text;
                source = b.Source;
            }
            else
            {
                var diskText = ReadFromDiskOrNull(r.Path!, out var diskError);
                if (diskError != null)
                {
                    sb.AppendLine("Error reading " + r.Path + ": " + diskError);
                    continue;
                }
                text = diskText!;
                source = "disk";
            }

            sb.Append(FormatReadFilesResult(r.Path!, source, text, r.Start, r.End));
        }
        return sb.ToString();
    }, ct).ConfigureAwait(false);
}
```

---

### 2.2 `read_errors` — ограничить обход Error List на UI-потоке

Файл: `RPAWriter2/Services/BuildFeedbackService.cs`, метод `GetCompilationErrorsAsync`.

**Добавить константу в класс:**
```csharp
private const int MaxErrorItems = 1000;
```

**Было:**
```csharp
var result = new List<BuildError>();
for (var i = 1; i <= errorItems.Count; i++)
{
    ...
}
```

**Стало:**
```csharp
var result = new List<BuildError>();
var count = Math.Min(errorItems.Count, MaxErrorItems);
for (var i = 1; i <= count; i++)
{
    ...
}
```

(форматирование уже выполняется в фоне в `AgentToolFactory.ReadErrorsAsync` через `ConfigureAwait(false)`; здесь ограничивается только UI-обход.)

---

### 2.3 `read_output` — читать только «хвост» Output window

Файл: `RPAWriter2/Services/BuildFeedbackService.cs`, метод `ReadPaneText`.

**Добавить константу в класс:**
```csharp
private const int OutputTailLines = 1000;
```

**Было:**
```csharp
private static string ReadPaneText(OutputWindowPane pane)
{
    ThreadHelper.ThrowIfNotOnUIThread();

    if (pane?.TextDocument == null)
        return string.Empty;

    try
    {
        var editPoint = pane.TextDocument.StartPoint.CreateEditPoint();
        return editPoint.GetText(pane.TextDocument.EndPoint) ?? string.Empty;
    }
    catch
    {
        return string.Empty;
    }
}
```

**Стало:**
```csharp
private static string ReadPaneText(OutputWindowPane pane)
{
    ThreadHelper.ThrowIfNotOnUIThread();

    if (pane?.TextDocument == null)
        return string.Empty;

    try
    {
        var doc = pane.TextDocument;
        var start = doc.StartPoint.CreateEditPoint();
        var from = doc.EndPoint.CreateEditPoint();
        from.LineUp(OutputTailLines);
        if (from.LessThan(start))
            from = start;
        return from.GetText(doc.EndPoint) ?? string.Empty;
    }
    catch
    {
        return string.Empty;
    }
}
```

---

### 2.4 `editor` — минимальный UI-доступ, тяжёлое на фоне

Файл: `RPAWriter2/Services/AgentTools/AgentToolFactory.cs`, метод `EditorAsync`.

**Было:** после `SwitchToMainThreadAsync` на главном потоке выполнялись чтение диска, вычисление нового содержимого, нормализация окончаний строк и запись на диск.

**Стало:**
```csharp
private async Task<string> EditorAsync(JObject args, CancellationToken ct)
{
    var p = ResolvePath(args["path"]?.Value<string>() ?? "");
    if (string.IsNullOrWhiteSpace(p)) return "Missing \"path\" argument.";

    var newText = args["new_text"]?.Value<string>() ?? "";
    var oldText = args["old_text"]?.Value<string>();
    int? insertLine = null;
    var insertToken = args["insert_line"];
    if (insertToken != null && insertToken.Type != JTokenType.Null)
    {
        if (insertToken.Type == JTokenType.Integer) insertLine = insertToken.Value<int>();
        else if (int.TryParse(insertToken.Value<string>(), out var parsed)) insertLine = parsed;
    }

    // 1) UI thread: locate + read the open editor buffer only.
    await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(ct);
    var dte = _getDte();
    var openDoc = dte != null ? VsDocumentService.FindOpenDocument(dte, p) : null;
    string buffer = null;
    if (openDoc != null)
    {
        var ready = await VsDocumentService.WaitForDocumentReadyAsync(openDoc).ConfigureAwait(true);
        buffer = ready ? VsDocumentService.ReadDocumentText(openDoc) : null;
    }

    // 2) Background: disk fallback + content computation + line-ending normalization.
    var edit = await Task.Run(() =>
    {
        var current = buffer ?? ReadDiskText(p);
        var newContent = ApplyEdit(current, oldText, insertLine, newText, out var error);
        if (error != null)
            return (null, error);

        var normalized = LineEndings.Normalize(newContent, LineEndings.Detect(current));
        return (normalized, null);
    }, ct).ConfigureAwait(false);

    if (edit.error != null)
        return edit.error;

    // 3) UI thread: write to the open buffer; otherwise write to disk on background.
    if (openDoc != null && dte != null)
    {
        await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(ct);
        var error = VsDocumentService.ReplaceDocumentText(dte, openDoc, edit.newContent);
        if (error != null) return error;
        try { openDoc.Save(); }
        catch (Exception ex) { return "Edited in the editor but failed to save: " + ex.Message; }
        return "Edited in editor and saved: " + p;
    }

    return await Task.Run(() =>
    {
        try
        {
            var dir = Path.GetDirectoryName(p);
            if (!string.IsNullOrWhiteSpace(dir)) Directory.CreateDirectory(dir);
            LineEndings.WriteAllText(p, edit.newContent);
            return "File written: " + p;
        }
        catch (Exception ex)
        {
            return "Failed to write file: " + ex.Message;
        }
    }, ct).ConfigureAwait(false);
}

private static (string newContent, string error) ApplyEdit(
    string current, string oldText, int? insertLine, string newText)
{
    if (insertLine.HasValue)
        return (InsertAtLine(current, insertLine.Value, newText), null);

    if (oldText != null)
    {
        var idx = current.IndexOf(oldText, StringComparison.Ordinal);
        if (idx < 0)
            return (null, "old_text was not found in the file (no edit was made).");
        var second = current.IndexOf(oldText, idx + oldText.Length, StringComparison.Ordinal);
        if (second >= 0)
            return (null, "old_text is not unique in the file; make it more specific and retry.");
        return (current.Substring(0, idx) + newText + current.Substring(idx + oldText.Length), null);
    }

    return (newText, null);
}
```

---

### 2.5 Roslyn — резолвить workspace на главном потоке один раз

Файл: `RPAWriter2/Services/RoslynWorkspaceService.cs`

**Было:** `Workspace`-getter лениво вызывал `Package.GetGlobalService(...)` (может быть вызван с фонового потока).

**Стало:**
```csharp
public sealed class RoslynWorkspaceService
{
    private static readonly object Gate = new object();
    private static VisualStudioWorkspace _workspace;
    private static bool _resolved;

    /// <summary>
    /// Resolves the singleton VisualStudioWorkspace once, on the VS UI thread.
    /// Idempotent and safe to call from any thread via JoinableTaskFactory.
    /// </summary>
    public async Task EnsureResolvedAsync(CancellationToken cancellationToken = default)
    {
        if (_resolved)
            return;

        await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);

        if (_resolved)
            return;

        try
        {
            var componentModel = Package.GetGlobalService(typeof(SComponentModel)) as IComponentModel;
            _workspace = componentModel?.GetService<VisualStudioWorkspace>();
        }
        catch
        {
            _workspace = null;
        }
        _resolved = true;
    }

    private VisualStudioWorkspace Workspace => _workspace;

    public Task<Document?> GetDocumentAsync(string fullPath, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(fullPath) || _workspace == null)
            return Task.FromResult<Document?>(null);

        var solution = _workspace.CurrentSolution;
        var documentIds = solution.GetDocumentIdsWithFilePath(fullPath);
        if (documentIds.Length == 0)
            return Task.FromResult<Document?>(null);

        return Task.FromResult(solution.GetDocument(documentIds[0]));
    }

    public Solution GetSolution()
    {
        return _workspace?.CurrentSolution;
    }
}
```

**Сопутствующая правка:** в `AgentToolFactory.cs` перед первым использованием `_roslyn` добавить `await _roslyn.EnsureResolvedAsync(ct);` в методы:
- `CodeOutlineAsync`
- `FindSymbolUsagesAsync`
- `GetInheritanceAsync`
- `GetDiagnosticsAsync`

Пример (в `CodeOutlineAsync`, сразу после проверки пути):
```csharp
await _roslyn.EnsureResolvedAsync(ct);
```

---

## 3. AI-RPA (`RPA.Chat`)

### 3.1 `StudioChatHost` — захват Dispatcher и маршалинг добавления сообщений

Файл: `RPA.Chat/Services/StudioChatHost.cs`

**Добавить using:**
```csharp
using System.Windows;
using System.Windows.Threading;
```

**Добавить поле и инициализацию в конструкторе:**
```csharp
private readonly Dispatcher _dispatcher;

public StudioChatHost(ChatModel model, SettingsData settings, ISettingsStore settingsPersistence = null)
{
    Model = model ?? throw new ArgumentNullException(nameof(model));
    Settings = settings ?? new SettingsData();
    SettingsPersistence = settingsPersistence;

    _dispatcher = Application.Current?.Dispatcher ?? Dispatcher.CurrentDispatcher;

    RpaAgentOptions.Load();
    _contextUsageBinder = new ContextUsageBinder(Model, SynchronizationContext.Current);
    _buildFeedback = new MsBuildFeedbackService(GetSolutionDirectory);
    RecreateAiProvider();
    RebuildAgentTools();
}
```

**Добавить helper (рядом с appenders):**
```csharp
private void RunOnUi(Action action)
{
    if (_dispatcher == null || _dispatcher.CheckAccess())
    {
        action();
        return;
    }
    _dispatcher.BeginInvoke(action);
}
```

**Было:**
```csharp
private void AppendChatMessage(ChatMessage message)
{
    Model.AppendMessage(message);
    SaveHistoryNow();
}
```

**Стало:**
```csharp
private void AppendChatMessage(ChatMessage message)
{
    RunOnUi(() =>
    {
        Model.AppendMessage(message);
        SaveHistoryNow();
    });
}
```

Эффект: `Model.Messages`, `History` (через `SaveHistoryNow` → `RefreshHistory`) и `INotifyPropertyChanged` больше не мутируются из фонового потока; это устраняет `CollectionView`-ошибку.

---

### 3.2 `StudioChatHost.SendChatAsync` — маршалинг мутаций после `await` + троттлинг reasoning

Файл: `RPA.Chat/Services/StudioChatHost.cs`, метод `SendChatAsync`.

**Было (фрагмент после запуска):**
```csharp
try
{
    var progress = new Progress<ChatStreamDelta>(delta =>
    {
        if (!string.IsNullOrEmpty(delta.ReasoningDelta))
            thinking.Reasoning += delta.ReasoningDelta;
    });
    var result = await _agentRunner.RunAsync(history, promptText, images, Model.Mode, BuildContextBudget(), cts.Token, progress);

    thinking.IsThinking = false;
    thinking.IsPartial = false;
    if (string.IsNullOrWhiteSpace(thinking.Reasoning) && !string.IsNullOrWhiteSpace(result.Reasoning))
        thinking.Reasoning = result.Reasoning;

    if (thinking.HasReasoning) thinking.Text = string.Empty;
    else Model.Messages.Remove(thinking);

    if (!string.IsNullOrWhiteSpace(result.FinalText))
    {
        AppendChatMessage(new ChatMessage { ... });
    }
}
catch (OperationCanceledException)
{
    thinking.IsThinking = false;
    if (Model.Messages.Contains(thinking)) AppendChat("Request cancelled.");
}
catch (Exception ex)
{
    thinking.IsThinking = false;
    AppendExtensionError(ex);
    ChatLog.Error(ex, "Agent chat request failed.");
}
```

**Стало:**
```csharp
try
{
    // Reasoning buffered and flushed on a timer (like RPAWriter2) so a stream of
    // per-token deltas does not re-layout the TextBox on every chunk.
    var reasoningBuffer = new StringBuilder();
    var reasoningTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(150) };
    reasoningTimer.Tick += (_, __) =>
    {
        thinking.Reasoning = reasoningBuffer.ToString();
        reasoningTimer.Stop();
    };

    var progress = new Progress<ChatStreamDelta>(delta =>
    {
        if (!string.IsNullOrEmpty(delta.ReasoningDelta))
        {
            reasoningBuffer.Append(delta.ReasoningDelta);
            if (!reasoningTimer.IsEnabled)
                reasoningTimer.Start();
        }
    });

    var result = await _agentRunner.RunAsync(history, promptText, images, Model.Mode, BuildContextBudget(), cts.Token, progress);

    reasoningTimer.Stop();
    RunOnUi(() =>
    {
        thinking.Reasoning = reasoningBuffer.ToString();
        thinking.IsThinking = false;
        thinking.IsPartial = false;

        if (string.IsNullOrWhiteSpace(thinking.Reasoning) && !string.IsNullOrWhiteSpace(result.Reasoning))
            thinking.Reasoning = result.Reasoning;

        if (thinking.HasReasoning)
            thinking.Text = string.Empty;
        else
            Model.Messages.Remove(thinking);

        if (!string.IsNullOrWhiteSpace(result.FinalText))
        {
            AppendChatMessage(new ChatMessage
            {
                Kind = Model.Mode == AgentMode.Act ? ChatMessageKind.ResultAnswerAct : ChatMessageKind.ResultAnswerPlan,
                Mode = modeName,
                Text = result.FinalText
            });
        }
    });
}
catch (OperationCanceledException)
{
    RunOnUi(() =>
    {
        thinking.IsThinking = false;
        if (Model.Messages.Contains(thinking))
            AppendChat("Request cancelled.");
    });
}
catch (Exception ex)
{
    RunOnUi(() => thinking.IsThinking = false);
    AppendExtensionError(ex);
    ChatLog.Error(ex, "Agent chat request failed.");
}
```

**Было (блок `finally`):**
```csharp
finally
{
    _chatCts = null;
    cts.Dispose();
    Model.StopStatus();
    SaveHistoryNow();
    Model.PersistUiState(MainTab.Chat);
}
```

**Стало:**
```csharp
finally
{
    _chatCts = null;
    cts.Dispose();
    RunOnUi(() =>
    {
        Model.StopStatus();
        SaveHistoryNow();
        Model.PersistUiState(MainTab.Chat);
    });
}
```

---

## 4. Версия и упаковка (цель 1.20)

### 4.1 Объявление версии

Файл: `C:\Users\Maxxx\source\repos\AgentShared\Directory.Build.props`

```xml
<Version>1.20.0</Version>
```

(текущее значение `1.19.1`).

### 4.2 Итоговая версия пакетов

Канонический путь через `PackTool`:

```powershell
cd C:\Users\Maxxx\source\repos\AgentShared
dotnet run --project PackTool\PackTool.csproj -c Release -- .
```

`VersionBumper` инкрементирует trailing-компоненту, поэтому при объявленной `1.20.0` результатом будет:

- `AgentToolkit.1.20.0.1.nupkg`
- `AgentToolkit.Wpf.1.20.0.1.nupkg`

### 4.3 Обновление потребителей

| Проект | Файл | Действие |
|---|---|---|
| RPAWriter2 (VSIX) | `RPAWriter2/RPAWriter2.csproj` | `AgentToolkit` + `AgentToolkit.Wpf` → `1.20.0.1` |
| RPAWriter2 — RPA-слой | `RPAWriter2/Agent.Rpa/Agent.Rpa.csproj` | `AgentToolkit` → `1.20.0.1` |
| RPAWriter2 — тесты | `RPAWriter2/RPAWriter.Tests/RPAWriter.Tests.csproj` | `AgentToolkit` → `1.20.0.1` |
| AI-RPA — чат | `C:\Users\Maxxx\Desktop\AI-RPA\RPA.Chat\RPA.Chat.csproj` | `AgentToolkit` + `AgentToolkit.Wpf` → `1.20.0.1` |
| AI-RPA — общий слой | `C:\Users\Maxxx\Desktop\AI-RPA\RPA.Shared\RPA.Shared.csproj` | `AgentToolkit` → `1.20.0.1` |
| AI-RPA — студия | `C:\Users\Maxxx\Desktop\AI-RPA\AiRpaStudio\AiRpaStudio.csproj` | `HintPath` `..\packages\AgentToolkit.1.19.1.3\...` → `1.20.0.1`; `AgentToolkit.Wpf.1.19.1.3` → `1.20.0.1`; распаковать новые `.nupkg` в `..\packages\` |
| VibeCoder (опционально) | `VibeCoder/VibeCoder/VibeCoder.csproj`, `VibeCoder/VibeCoder.Core/VibeCoder.Core.csproj` | `AgentToolkit`/`AgentToolkit.Wpf` → `1.20.0.1` |
| smoke (опционально) | `AgentShared/smoke/smoke.csproj` | `AgentToolkit` → `1.20.0.1` |

---

## 5. Порядок применения и проверка

1. Применить правки AgentToolkit (раздел 1).
2. Применить правки RPAWriter2 (раздел 2).
3. Применить правки AI-RPA (раздел 3).
4. Поднять версию до `1.20.0`, упаковать `1.20.0.1` (раздел 4).
5. Обновить `PackageReference`/`HintPath` у потребителей (таблица в 4.3).
6. Пересобрать `RPAWriter2` и `AI-RPA`.

Критерии приёмки:

- В AI-RPA при выполнении инструментов нет ошибки `CollectionView`; чат обновляется на UI-потоке.
- В RPAWriter2 Cancel / steering / TextBox отзывчивы во время `read_files`, `read_output`, `read_errors`, `editor`, Roslyn-инструментов.
- Reasoning-стриминг не замораживает UI (нет поштучного re-layout); после завершения reasoning показывается с переносами.
- Существующие тесты (`RPAWriter.Tests`, `Agent.Tests`) проходят.
