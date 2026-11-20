# AgentToolkit.Wpf

Shared WPF controls and host helpers for the agent hosts
(RPAWriter2, VibeCoder, AI-RPA).

This is the only AgentShared assembly that references WPF. It stays on
`net472` so the VSIX hosts (RPAWriter2, VibeCoder) and the net48 studio
(AI-RPA) can all consume it.

> This package bundles the prebuilt `Agent.Toolkit.Wpf.dll`. The UI-free
> shared code (`Agent.Core`, `Agent.Models`, `Agent.Mvvm`) ships as the
> separate [AgentToolkit](https://www.nuget.org/packages/AgentToolkit)
> package and must be installed alongside this one.

## Requirements

- .NET Framework 4.7.2 (`net472`)
- WPF (`PresentationCore`, `PresentationFramework`, `WindowsBase`, `System.Xaml`)
- The `AgentToolkit` package (provides `Agent.Core`, `Agent.Models`, `Agent.Mvvm`)

## Installation

```xml
<PackageReference Include="AgentToolkit.Wpf" Version="1.19.1" />
<PackageReference Include="AgentToolkit" Version="1.19.1" />
```

Or add your local `feed\` folder as a NuGet source and install from there.

## What's included

### Chat UI

- `ChatControl` — the whole agent-chat view: toolbar, history, message list,
  input box with attachments / microphone / clear, Permissions and Docs
  overlays, pinned message, Cancel/Resume status, mode toggle and the
  pending-message queue.
- `ChatMessagesControl` — renders the chat transcript.
- `MessageQueueControl` — pending-message queue (host feeds and steers it).
- Message cards for every transcript entry kind: user, agent text, error,
  system notice, tool call, blocked tool, subagent, question, approval,
  checkpoint, compaction, mode switch, diff edits, result answers and more.

### Settings UI

- `SettingsControl` — generic settings panel: save button, section navigation
  and the built-in sections (API, Native tools, General, Sound). Host-specific
  sections are injected through `InsertHostSection`.

### Controls & infrastructure

- `SegmentedToggle` / `SegmentedToggleItem` — segmented on/off toggle.
- `NotificationBanner` — transient in-app toast/banner.
- `ContextIndicator` — context-window usage indicator.
- `TopOverflowWrapPanel` — wrap panel that overflows from the top.
- `AgentTabControl` — themed tab control.
- Converters (`MarkdownToElementConverter`, `MermaidCodeToElementConverter`,
  `MaterialIconToGeometryConverter`, `EnumMatchConverter` and others).
- Markdown and Mermaid diagram rendering (Mermaid.js is bundled as an embedded
  resource and rendered through WebView2).
- `AgentTheme` + dark/light palettes (`Theme.Dark.xaml`, `Theme.Light.xaml`).

### Theming

- `AgentThemeManager` — applies the shared dark/light palette at runtime.
  Toolkit controls reference theme colors through `DynamicResource` keys
  (`Theme.Text`, `Theme.Window`, ...), so switching palettes re-colors the
  already-open UI without recreating windows.

## Quick start

### Chat

1. Instantiate `ChatControl` (or place it in your view) and set a `ChatModel`
   as its `DataContext`.
2. Implement `IChatHost` for your host-specific operations (agent loop,
   commits, build errors, robot creation, workspace scoping) and assign it to
   `ChatControl.Host`.
3. Optionally call `AttachServices(mic, stt)` to enable voice input, and inject
   your host chrome into the named `ContentControl` slots
   (`VoiceErrorToastHost`, `CommitResultBarHost`, `CommitEditorHost`,
   `CreateRobotHost`, ...).

```csharp
var chat = new ChatControl
{
    DataContext = myChatModel,
    Host = myChatHost,
    Settings = settingsData
};
chat.AttachServices(microphoneRecorder, speechToTextService);
```

### Settings

`SettingsControl` expects a `DataContext` exposing a `Settings`
(`Agent.Models.SettingsModel`) property and a `Cmd` command dispatcher.
Host-specific sections are added with `InsertHostSection(index, section)`.

### Theme

```csharp
// Let the host own the theme:
AgentThemeManager.SetMode(AgentThemeMode.Auto);
AgentThemeManager.Apply("Dark"); // or "Light"
```

## Dependencies

Declared runtime dependencies of the bundled assembly:

- [Markdig](https://www.nuget.org/packages/Markdig) (markdown rendering)
- [Material.Icons](https://www.nuget.org/packages/Material.Icons) (icon glyphs)
- [Microsoft.Web.WebView2](https://www.nuget.org/packages/Microsoft.Web.WebView2) (Mermaid diagrams)

## License

MIT
