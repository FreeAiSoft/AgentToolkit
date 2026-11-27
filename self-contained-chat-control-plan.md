# План: самодостаточный и расширяемый чат тулкита (`AgentChatControl`)

> Документ-план + отчёт о реализации. Актуально для `AgentShared`
> (`C:\Users\Chris\source\repos\AgentToolkit`).
>
> Цель: тулкит-чат встраивается в любой WPF-хост одной строкой
> `<controls:AgentChatControl/>` и при этом остаётся расширяемым — хост может
> подменить runtime/сторадж/действия, добавить вкладки и секции настроек, не
> ломая дефолтное поведение.

---

## 1. Текущее состояние (что мешает «одной строке»)

- Нет единого фасада: хост вручную собирает `ChatModel` + `ChatAgentSession` +
  `StandaloneChatHost` + `SettingsModel` + VM и раскладывает всё по
  `DataContext`/`Host`/`Settings`/`AppDataDirectory` (см. `Agent.ChatHost\MainWindow.xaml.cs`).
- Два источника настроек: `ChatControl.Settings` — это `SettingsData`
  (снапшот для оверлея Permissions), а вкладка настроек работает через
  `SettingsModel`. Это разные объекты; риск расхождения зафиксирован в
  `toolkit-settings-persistence-plan.md`.
- VM настроек (`SettingsHostViewModel`) лежит в хосте, а по README-конвенции
  «тонкие WPF view models» — это per-project. Для drop-in такой адаптер должен
  быть частью тулкита.
- Точки расширения уже есть (`InsertHostSection`, `AddHostTab`, слоты,
  `IChatHost`, `IAgentRuntimeProvider`, `IChatHostActions`, `ISettingsStore`),
  но они разрознены и не собраны в один конфигурируемый объект.

---

## 2. Решения

### R1. Фасад

| Вариант | Решение |
|---|---|
| A. Composite `UserControl` (внутри `AgentTabControl`) | **Основной** |
| B. Невизуальный билдер | Запасной |

**Решение:** A. Имя фасада — **`AgentChatControl`** (в `Agent.Toolkit.Wpf.Controls`).

### R2. Дефолты (самодостаточность)

Фасад сам создаёт, если хост не передал:

| Что | Дефолт |
|---|---|
| `IChatHistoryStore` | `InMemoryChatHistoryStore` |
| `ISettingsStore` | `ToolkitSettingsStore(AppDataDirectory)` |
| `AppDataDirectory` | `%AppData%` (→ `%AppData%\AgentToolkit\toolkit.db`) |
| `IAgentRuntimeProvider` | `StubRuntimeProvider` |
| `IChatHostActions` | `NullHostActions.Instance` |
| `IChatHost` | внутренний `StandaloneChatHost` |
| `IAgentLogger` | `NoOpLogger.Instance` |
| `IWorkspaceProvider` | `null` → глобальный скоуп |

### R3. Единый источник настроек

| Вариант | Решение |
|---|---|
| **A. Чат и вкладка настроек делят ОДИН `SettingsModel`** | **Основной** |
| B. Промежуточная синхронизация `SettingsData` ↔ `SettingsModel` | Запасной |

**Решение:** A, но аддитивно: у `ChatControl` появляется новое свойство
`SettingsModel`; старый путь через `Settings` (`SettingsData`) сохраняется для
обратной совместимости. Permissions-оверлей читает/пишет из `SettingsModel`,
когда оно задано, иначе — из `Settings`.

### R4. VM настроек переезжает в тулкит

`SettingsHostViewModel` → `Agent.Toolkit.Wpf.Controls.SettingsViewModel`
(та же логика, ноль бизнес-логики). Файл в `Agent.ChatHost` удаляется.

### R5. Расширение — на существующих швах

Фасад форвардит уже существующие API:
`AddHostTab`, `InsertSettingsSection`, `InsertMicrophoneSection`,
`TrySelectTab`, `ISettingsStore`, `IAppDataDirectoryProvider` (через
`AppDataDirectory`), `IWorkspaceProvider`, `IAgentRuntimeProvider`,
`IChatHostActions`, `IChatHost`.

---

## 3. Целевая картина

```xml
<!-- минимальный drop-in -->
<controls:AgentChatControl x:Name="Chat"/>
```

```csharp
// расширенный сценарий
Chat.AppDataDirectory  = appDataDirectory;   // иначе %AppData% (→ %AppData%\AgentToolkit\toolkit.db)
Chat.WorkspaceProvider = workspaceProvider;  // иначе глобальный скоуп
Chat.RuntimeProvider   = runtimeProvider;    // свой AI-runtime
Chat.HostActions       = hostActions;        // Git/Roslyn/robot
Chat.AddHostTab(myTabDefinition, myTabContent);
Chat.InsertSettingsSection(0, mySettingsSection);
```

---

## 4. Дизайн по фазам

### Фаза 1. `SettingsViewModel` в тулките

Новый `Agent.Toolkit.Wpf\Controls\SettingsViewModel.cs`:

```csharp
public class SettingsViewModel : BaseViewModel
{
    public SettingsViewModel(SettingsModel model)
    {
        Settings = model;
        SetModel(model, null);
    }

    public SettingsModel Settings { get; }
}
```

Контракт DataContext `SettingsControl` остаётся прежним: `Settings` + `Cmd`
(`Cmd` наследуется из `BaseViewModel`; `SetModel` подключает
`SendCommand += model.SendCommand`).

Удаляется `Agent.ChatHost\SettingsHostViewModel.cs`.

### Фаза 2. `ChatControl` работает от `SettingsModel`

- Новое свойство `public SettingsModel SettingsModel { get; set; }`.
- `OnPermissionsClick` читает чекбоксы из `SettingsModel ?? Settings`.
- `OnPermissionChanged` пишет в `SettingsModel ?? Settings`; при
  `SettingsModel != null` сохраняет через `SettingsModel.SaveSettingsCommand()`
  (один персист, поднимается `SettingsSaved`), иначе — прежний путь
  (`SettingsPersistence` → `SaveSettings()` → legacy).
- После изменения по-прежнему `Host?.RebuildAgentTools()`.

### Фаза 3. UI-free фабрика + фасад

#### 3.1. `Agent.Models` (netstandard2.0, тестируемо из net8.0)

- `ToolkitChatConfiguration` — конфиг (см. R2).
- `ToolkitChatContext` — результат сборки (модели + стораджи + runtime/actions).
- `ToolkitHostFactory.Build(config)` — резолвит дефолты, создаёт
  `SettingsModel`/`ChatModel`, вызывает их `Run()`, возвращает контекст.
- `ToolkitHostFactory.DefaultAppDataDirectory` = `%AppData%` (store appends `AgentToolkit`).

#### 3.2. `StandaloneChatHost` принимает кастомный runtime

Добавляется опциональный параметр `IAgentRuntimeProvider runtimeProvider = null`
(дефолт — `StubRuntimeProvider`). Аддитивно, существующие вызовы не меняются.

#### 3.3. `AgentChatControl` (WPF)

XAML: `<controls:AgentTabControl x:Name="Tabs"/>`.

Публичная поверхность:

```csharp
// конфигурация (до первой сборки)
public string AppDataDirectory { get; set; }
public ISettingsStore SettingsStore { get; set; }
public IChatHistoryStore HistoryStore { get; set; }
public IUiStateStore UiStateStore { get; set; }
public IWorkspaceProvider WorkspaceProvider { get; set; }
public IAgentRuntimeProvider RuntimeProvider { get; set; }
public IChatHostActions HostActions { get; set; }
public IChatHost Host { get; set; }
public IAgentLogger Logger { get; set; }

// доступ к внутренностям
public AgentTabControl TabControl { get; }
public ChatControl ChatPanel { get; }
public SettingsControl SettingsPanel { get; }
public ChatModel Model { get; }
public SettingsModel SettingsModel { get; }

// расширения
public TabItem AddHostTab(TabDefinition definition, object content);
public void InsertSettingsSection(int index, SettingsControl.SettingsSection section);
public void InsertMicrophoneSection(int index);
public bool TrySelectTab(string key);

// события-ретрансляторы
public event EventHandler<string> VoiceError;
public event EventHandler<PromptCacheLossInfo> PromptCacheLost;
```

Жизненный цикл: сборка ленивая, идемпотентная, на `Loaded`/первом `Run()`:

```
ResolveLogger → ResolveAppDataDirectory → Build (ToolkitHostFactory)
→ Host ?? StandaloneChatHost(chat, logger, actions, runtime)
→ Wire ChatPanel (DataContext=chat, Host, SettingsModel, AppDataDirectory, Logger)
→ Wire SettingsPanel (DataContext = new SettingsViewModel(settings))
→ ChatPanel.EnsureChatReadyToReceive()
→ Tabs.TrySelectTab(CoreTabs.Chat.Key)
```

### Фаза 4. Дефолтный персист

`ToolkitHostFactory` резолвит `AppDataDirectory` (если не задан) в
`%AppData%`, затем `ToolkitSettingsStore` пишет
`<root>\AgentToolkit\toolkit.db`. Существующий legacy-фолбэк
`SettingsStore.Save` не трогается — он остаётся в `ChatControl` для хостов,
которые не передают ни `SettingsStore`, ни `AppDataDirectory`.

### Фаза 5. Демонстрационный host + документация

`Agent.ChatHost\MainWindow` переводится на один `<controls:AgentChatControl/>`
(с примером расширения). Обновляется `README.md`.

### Фаза 6. Тесты и версия

- net8.0 тесты фабрики (`ToolkitHostFactoryTests`): дефолты, override,
  `toolkit.db`, идемпотентность, workspace-скоуп.
- Текст-гарды фасада (`AgentChatControlGuardTests`), как существующий
  `SettingsToastGuardTests` (net8.0 не ссылается на net472-WPF).
- `Directory.Build.props`: поднять минор версии.

---

## 5. Риски и меры

| Риск | Мера |
|---|---|
| Сломать существующие хосты (RPAWriter2/VibeCoder/AI-RPA) | Всё аддитивно; сигнатуры `ChatControl`/`SettingsControl`/`IChatHost` не меняются; legacy-фолбэк сохраняется |
| Расхождение Permissions ↔ Settings | R3-A — один `SettingsModel`; тесты синхронизации |
| `BaseViewModel` в WPF-сборке (конвенция per-project VM) | Осознанное исключение ради drop-in; VM остаётся тонким адаптером |
| net8.0-тесты не видят net472-WPF | Логика вынесена в UI-free `Agent.Models`; фасад покрыт текст-гардами + сборкой net472 |
| Двойная инициализация фасада | `_initialized`-гейт; тест идемпотентности |

---

## 6. Порядок коммитов

1. `feat(wpf): SettingsViewModel в тулкит + удаление SettingsHostViewModel`.
2. `refactor(wpf): ChatControl работает от SettingsModel`.
3. `feat(models): ToolkitHostFactory — UI-free сборка чата/настроек`.
4. `feat(wpf): AgentChatControl — самодостаточный фасад с расширениями`.
5. `refactor(chathost): MainWindow на AgentChatControl`.
6. `docs + test + chore: README, тесты, версия`.
