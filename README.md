# AgentShared

Canonical source of truth for the code shared by the three agent hosts:

- RPAWriter2 (`C:\Users\Maxxx\source\repos\RPAWriter2`)
- VibeCoder (`C:\Users\Maxxx\source\repos\VibeCoder`)
- AI-RPA (`C:\Users\Maxxx\Desktop\AI-RPA`)

Shared surface (UI-free, netstandard2.0):

- `Agent.Mvvm` - MVVM base (`BaseModel`, `BaseViewModel`, `IView`, `RelayCommand`, `ViewMode`, `NotifyLevel`)
- `Agent.Core` - agent core (tool framework, checkpoints, context, contracts, data models `ChatMessage`/`ChatPayloads`/..., prompt library, provider, markdown, memory bank)
- `Agent.Models` - shared product models (`ChatModel`, `SettingsModel`) and the `ContextUsageBinder` host helper
- `Agent.Toolkit.Wpf` - shared WPF controls (`ContextIndicator`), net472

The three UI-free assemblies ship as a single NuGet package: **`AgentToolkit`**.
The WPF assembly ships as a separate package: **`AgentToolkit.Wpf`**.

Per-project (NOT shared): hosts, thin WPF view models, host services,
project-specific tool catalogs (registered into the shared `AgentToolRegistry`).

The RPAWriter2 tab models (`ChatModel`, `SettingsModel`, `LogAnalyzer`, `KnowledgeBaseModel`,
`SpecTabModel`) and RPA services (robot generation, Pix/Mwf/UiPath converters, production-log
analysis) live in `Agent.Rpa` inside the RPAWriter2 solution - they are coupled to RPA specifics
and are not shared with VibeCoder / AI-RPA.

## Publishing

```
.\pack.ps1
```

builds the three assemblies and packs them into a single `AgentToolkit` package in `feed\`.
The GitHub Actions workflow `.github\workflows\build.yml` builds the same package and pushes
it to nuget.org (requires the `NUGET_API_KEY` secret).

Local consumers add the `feed\` folder as a NuGet package source and reference `AgentToolkit`:

```xml
<add key="AgentShared" value="C:\Users\Maxxx\source\repos\AgentShared\feed" />
```