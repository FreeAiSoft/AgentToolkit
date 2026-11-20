# План тестирования решения AgentShared (`AgentToolkit.slnx`)

## 1. Область и цель

Покрыть тестами всё решение, а не только свежие изменения. Цель — закрыть «серые
зоны» (код без тестов), превратить хрупкие guard-тесты в поведенческие и
зафиксировать регрессии в рантайм-логике, которые компилятор не ловит.

Проекты:
- `Agent.Core` — ядро (агент, инструменты, MCP, контекст, чекпоинты, сервисы, провайдеры, модели).
- `Agent.Models` — модели/VM (ChatModel, SettingsModel, теминг, toggle).
- `Agent.Mvvm` — MVVM-примитивы.
- `Agent.Toolkit.Wpf` — WPF-контролы (UI; логика выносится в тестируемые helper-классы).
- `PackTool` — упаковка/версионирование.

Тестовый проект: `Agent.Tests` (NUnit 3.14, net8.0).

---

## 2. Текущее состояние (сводка)

### 2.1 Покрыто хорошо
- `AgentRunner`, `AgentPromptBuilder`, `AgentResponseParser`, `DsmlNormalizer`,
  `ToolSchemaConverter`, `SubAgentRunner`.
- Контекст: `TokenEstimator`, `ContextCondenser`, `ContextAssembler`, `ContextBudget`,
  `ContextUsageCalculator`, `PromptCacheLossDetector`, `ContextUsageBinder`.
- Провайдеры: `AiProviderCatalog`, `AiProviderFactory`, `AiProviderId`,
  `AnthropicProvider`, `OpenAiCompatibleProvider`, `OllamaModelSource`, `StubAiProvider`.
- Чекпоинты: `CheckpointService`.
- Сервисы: `CommitMessageBuilder`, `CompilationErrorContextBuilder`,
  `ErrorListItemContextBuilder`, `ExceptionContextBuilder`, `FileLockDetector`,
  `LineEndings`, `MarkdownCodeBlockText`, `MarkdownToDocxConverter`, `MemoryBankService`,
  `MermaidHtmlBuilder`, `TextDiff`, `TextNormalizer`, `CSharpHighlighter`,
  `OpenAiWhisperSttService`, `PromptLibrary`, `PromptPaths`, `PromptSet`.
- Модели: `ChatModel`, `SettingsModel`, `TabDefinition`, `ThemeModeState`, `ColumnPacker`.

### 2.2 Покрыто хрупко (guard-тесты по тексту исходников)
- `ChatControlCopyGuardTests`, `NotificationBannerStyleGuardTests`,
  `ThemeHardcodeGuardTests`, `SettingsToastGuardTests` — проверяют
  `File.ReadAllText(...).Contains(...)`, ломаются от рефакторинга и не проверяют поведение.

### 2.3 Не покрыто вовсе (ключевые пробелы)
- **Весь модуль MCP** (`Agent.Core/Mcp`, 16 файлов) — ни одного теста.
- `ChatMessageMapper` (код сам заявляет о тестах, которых нет).
- `ToolRepetitionDetector`.
- `GitOutputParser`, `GitEnvironmentSanitizer`, `GitRunner`.
- `CheckpointDiffBuilder`, `CheckpointExcludePatterns`, модели `Checkpoint`,
  `CheckpointDiff`, `CheckpointSaveResult`, `CheckpointServiceOptions`, `InitResult`.
- `ReasoningEffortMap` (Apply-ветки), `ReadPathPolicy`, `ProviderTranscriptCodec`,
  `ExceptionFormatter`.
- `NativeToolCatalog`, `AgentToolRegistry`, `AgentToolEnvironment`, `AgentTool`,
  `AgentToolCatalogEntry`, `ToolApproval`.
- `AppDataPaths`, `BuildTestOutputFilter`, `AgentModeNames`, `HelpDocumentation`,
  `ImageDimensions`, `SettingsStore`, `WorkspaceIdentity/Resolver/LogSettings`.
- `Agent.Mvvm` целиком.
- Модели данных: `ChatMessage`, `ChatPayloads`, `PendingMessage`, `ChatAttachment`,
  `UiState`, `AgentPhase`, `AiModelInfo`, `AiProviderSpec/Capabilities/Protocol/SettingFlags`.
- `Agent.Models/AgentToolToggle`.
- WPF-конвертеры и большинство контролов.

---

## 3. План тестов по модулям

### P1 — критичные/полностью пустые

#### MCP-стек (`Agent.Core/Mcp`) — `McpTests.cs`
- `McpServerSettings.Parse/ToJson`: round-trip, bare-объект, пустой/битый JSON.
- `McpServerConfig`, `McpTransportType`, `McpNames`.
- `McpSchemaConverter.ToInformal`.
- `McpToolResultParser.ToText`, `McpToolResult`, `McpReadResults`.
- `McpClient` + in-memory `IMcpTransport`: initialize/list/call, ошибки JSON-RPC, dispose.
- `McpToolAdapter`, `McpContextTools.Create`.

#### `ChatMessageMapper` — `ChatMessageMapperTests.cs`
- `ToHistoryMessage`/`FromHistoryMessage` для всех `Kind`, round-trip, `SkipAnswerText`.
- `BuildTitle`: пусто/переносы/обрезка 60 + «…».
- Null-аргументы.

#### `ToolRepetitionDetector` — `ToolRepetitionDetectorTests.cs`
- Блокировка на `limit`, отключение при `0`, сброс счётчика, `BuildSignature`.

#### Git-сервисы — `GitServicesTests.cs`
- `GitOutputParser.SplitLines/ExtractChangedPath/FormatLoggedOutput`.
- `GitEnvironmentSanitizer.Sanitize/WithForcedConfig`.

### P2 — непокрытые сервисы
- `CheckpointDiffBuilder`, `CheckpointExcludePatterns`.
- `ReasoningEffortMap`, `ReadPathPolicy`, `ProviderTranscriptCodec`, `ExceptionFormatter`.
- `NativeToolCatalog`, `AgentToolRegistry`, `ToolApproval`.

### P3 — модели и MVVM-фундамент
- `Agent.Mvvm`: `RelayCommand`, `BaseEntity/Model/ViewModel`, `ViewMode`, `NotifyLevel`.
- Модели данных и `AgentToolToggle`.

### P4 — рефакторинг guard-тестов
- Вынести `FormatMessageForCopy` и `CreateAccentBackground` в тестируемые helper-классы,
  покрыть юнит-тестами; guard-проверки оставить только как smoke-защиту.

---

## 4. Definition of Done
- Каждый публичный метод покрыт минимум одним кейсом (happy path + граничный/null).
- Побочные эффекты (clipboard/файлы/сеть) — за seam-интерфейсами/fakes.
- Нет тестов, читающих исходники как текст (кроме явных smoke-guard'ов).
- `dotnet test` зелёный без внешних зависимостей (git/сеть — опционально).
