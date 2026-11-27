# AgentShared

Canonical source of truth for the code shared by the three agent hosts:

- RPAWriter2 (`C:\Users\Maxxx\source\repos\RPAWriter2`)
- VibeCoder (`C:\Users\Maxxx\source\repos\VibeCoder`)
- AI-RPA (`C:\Users\Maxxx\Desktop\AI-RPA`)

Shared surface (UI-free, netstandard2.0):

- `Agent.Mvvm` - MVVM base (`BaseModel`, `BaseViewModel`, `IView`, `RelayCommand`, `ViewMode`, `NotifyLevel`)
- `Agent.Core` - agent core (tool framework, checkpoints, context, contracts, data models `ChatMessage`/`ChatPayloads`/..., prompt library, provider, markdown, memory bank)
- `Agent.Models` - shared product models (`ChatModel`, `SettingsModel`) and the `ContextUsageBinder` host helper
- `Agent.Toolkit.Wpf` - shared WPF chat + settings UI (`ChatControl`, `SettingsControl`,
  `ChatMessagesControl`, `MessageQueueControl`, `SegmentedToggle`, `TopOverflowWrapPanel`,
  converters, `AgentTheme`), net472

The three UI-free assemblies ship as a single NuGet package: **`AgentToolkit`**.
The WPF assembly ships as a separate package: **`AgentToolkit.Wpf`**.

Per-project (NOT shared): hosts, thin WPF view models, host services,
project-specific tool catalogs (registered into the shared `AgentToolRegistry`).

The RPAWriter2 tab models (`ChatModel`, `SettingsModel`, `LogAnalyzer`, `KnowledgeBaseModel`,
`SpecTabModel`) and RPA services (robot generation, Pix/Mwf/UiPath converters, production-log
analysis) live in `Agent.Rpa` inside the RPAWriter2 solution - they are coupled to RPA specifics
and are not shared with VibeCoder / AI-RPA.

## Chat & settings UI (`ChatControl` / `SettingsControl`)

`Agent.Toolkit.Wpf` provides the generic agent-chat and settings UI shared by the hosts.

- `ChatControl` owns the whole chat view (toolbar, history, message list, input box with
  attachments/mic/clear, Permissions/Docs overlays, pinned message, Cancel/Resume status, mode
  toggle, pending-message queue) and all its generic view logic. It is fed with a `ChatModel`
  as `DataContext`, an `IChatHost` host contract, a `SettingsData`, and an injected
  `IMicrophoneRecorder` / `ISpeechToTextService` via `AttachServices`.
- `IChatHost` is the host seam — the chat view delegates every RPA/VS/Git-specific operation to
  the host: `SendChatAsync`, `CancelAgentRun`, `ResumeAgentRunAsync`, `IsAgentRunning`,
  `OpenCommitControl`, `SendBuildErrors`, `OpenCreateRobot`, `RebuildAgentTools`, `StartNewChat`,
  `OpenConversation`, `OnConversationRemoved`, `ShowDocsIndex`, `CompactNowAsync`,
  `RequestMemoryUpdate`.
- Host chrome is injected into six named `ContentControl` slots: `VoiceErrorToastHost`,
  `OptionsWarningToastHost`, `CommitResultBarHost`, `CheckpointRestoreErrorToastHost`,
  `CommitEditorHost`, `CreateRobotHost` (plus `DocsTitleTextControl` / `DocsContentControl` for
  the Docs overlay).
- `SettingsControl` owns the generic settings view and exposes `InsertHostSection(int, SettingsSection)`
  plus `HostBannerSlot` / `HostToolbarSlot` for host-specific settings sections.

## Drop-in host (`AgentChatControl`)

For hosts that only need the whole chat (Chat + Settings tabs) without wiring models,
stores and the agent host by hand, `Agent.Toolkit.Wpf` ships a self-contained facade:

```xml
<controls:AgentChatControl x:Name="Chat"/>
```

It builds the `AgentTabControl` with the `Chat`/`Settings` tabs, an in-memory history store,
a SQLite settings store under `%AppData%\AgentToolkit\toolkit.db` and a standalone stub agent
host. Every seam is overridable (`AppDataDirectory`, `SettingsStore`, `HistoryStore`,
`WorkspaceProvider`, `RuntimeProvider`, `HostActions`, `Host`, `Logger`), and host content is
added through `AddHostTab(...)` and `InsertSettingsSection(...)`.

## Publishing

```
.\pack.ps1
```

builds the four assemblies and packs them into two packages in `feed\`:

- `AgentToolkit` (bundles `Agent.Mvvm` + `Agent.Core` + `Agent.Models`)
- `AgentToolkit.Wpf` (bundles `Agent.Toolkit.Wpf`)

The GitHub Actions workflow `.github\workflows\build.yml` builds the same packages and pushes
them to nuget.org (requires the `NUGET_API_KEY` secret).

Local consumers add the `feed\` folder as a NuGet package source and reference `AgentToolkit`:

```xml
<add key="AgentShared" value="C:\Users\Maxxx\source\repos\AgentShared\feed" />
```