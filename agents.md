# agents.md — как работать с репозиторием AgentShared

Это каноничный источник общих частей для трёх хостов: RPAWriter2, VibeCoder, AI-RPA.
Здесь описаны типовые задачи: сборка, тесты, **поднятие версии → коммит → локальный пакет**,
и работа с темой оформления.

## Структура

- `Agent.Mvvm` — MVVM-база (netstandard2.0)
- `Agent.Core` — ядро агента (netstandard2.0)
- `Agent.Models` — общие модели (`ChatModel`, `SettingsModel`)
- `Agent.Toolkit.Wpf` — WPF-контролы (`ChatControl`, `SettingsControl`, …), net472
- `AgentToolkit` — пакет-обёртка: `Agent.Mvvm` + `Agent.Core` + `Agent.Models` → один `.nupkg`
- `AgentToolkit.Wpf` — пакет-обёртка: `Agent.Toolkit.Wpf` → отдельный `.nupkg`
- `Agent.Tests` — тесты (net8.0, NUnit)
- `feed/` — локальный NuGet-фид (папка `.nupkg`), gitignored
- `artifacts/` — выход CI, gitignored

## Сборка и тесты

```powershell
# Собрать отдельные проекты
dotnet build Agent.Toolkit.Wpf/Agent.Toolkit.Wpf.csproj
dotnet build Agent.Tests/Agent.Tests.csproj

# Прогнать все тесты
dotnet test Agent.Tests/Agent.Tests.csproj

# Только тесты темы
dotnet test Agent.Tests/Agent.Tests.csproj --filter "FullyQualifiedName~ThemeSettingsTests"
```

> Не собирай всё решение `AgentToolkit.slnx` целиком — в него включены и проекты хоста
> `RPAWriter2` (соседняя папка), которые могут быть в промежуточном состоянии.

## Release-цикл: версия → коммит → локальный пакет

### 1. Поднять версию

Версия живёт в одном месте — `Directory.Build.props`:

```xml
<PropertyGroup>
  <Version>1.15.0</Version>
</PropertyGroup>
```

Правило: фича / поведение → минор (1.15 → 1.16), фикс → патч (1.15.0 → 1.15.1).

### 2. Закоммитить

Стиль коммитов — русский, многострочный, по проектам (см. `git log`):

```
feat: каталог AI-провайдеров и переключение темы Dark/Light

Agent.Core: Добавить каталог провайдеров (…)
Agent.Models: Добавить ProviderId/ProviderIndex и ThemeIndex в SettingsModel
Agent.Toolkit.Wpf: Добавить смену темы (…)
Agent.Tests: Добавить тесты (…)
chore: Поднять версию до 1.15.0
```

```powershell
git add -A
git commit -m "…" -m "…"   # либо -F файл-с-сообщением
```

> Кириллица в `git commit -m` через PowerShell иногда портится (теряются первые
> заглавные буквы). Надёжнее писать сообщение в файл и коммитить через
> `git commit -F <file>`.

### 3. Создать локальный пакет

```powershell
.\pack.ps1
```

Скрипт собирает 4 проекта в `Release` и пакует два пакета в `feed\`:

- `AgentToolkit.1.15.0.nupkg`
- `AgentToolkit.Wpf.1.15.0.nupkg`

Локальный фид уже прописан в `nuget.config` (источник `AgentShared` → `.\feed`),
поэтому консюмеры, добавляющие папку `feed\` как источник пакетов, сразу видят свежие версии.

### Публикация в nuget.org

CI `.github/workflows/build.yml` собирает те же пакеты и пушит их в nuget.org
при пуше тега `v*` (нужен секрет `NUGET_API_KEY`). Локально для nuget.org ничего не нужно.


### Автопак при Release-сборке Agent.Core

При Release-сборке проекта `Agent.Core` срабатывает post-build-событие, которое запускает
консольную утилиту `PackTool` и собирает весь тулкит в `feed\`:

- версия берётся как максимум из уже лежащих в `feed\` `.nupkg`;
- хвостовой компонент инкрементируется: `1.7.2` → `1.7.2.1`, `1.4.5.6` → `1.4.5.7`
  (счётчик стартует с 1 — нулевой 4-й компонент NuGet схлопывает в трёхчастную версию);
- собираются `Agent.Mvvm`, `Agent.Core`, `Agent.Models`, `Agent.Toolkit.Wpf` и пакуются
  `AgentToolkit` + `AgentToolkit.Wpf`.

Чтобы не зациклиться, внутренние сборки и `pack.ps1`/CI используют свойство
`-p:AgentSkipReleasePack=true`.


## Переключение темы (Dark/Light)

### Как это устроено

- Палитры: `Agent.Toolkit.Wpf/Styles/Theme.Dark.xaml` и `Theme.Light.xaml` — все цвета
  с нейтральными ключами `Theme.*`.
- Менеджер: `Agent.Toolkit.Wpf/Theming/AgentThemeManager.cs` — пишет значения кистей
  в `Application.Current.Resources`, поэтому `DynamicResource`-ссылки перекрашивают UI на лету.
- Контролы ссылаются на цвета через `{DynamicResource Theme.*}` (не `StaticResource`).
- Настройка: `SettingsModel.ThemeIndex` (0 = Dark, 1 = Light) и `SettingsModel.ThemeModeIndex`
  (0 = Manual, 1 = Auto). На вкладке «Theme» в `SettingsControl` — ComboBox «Theme source»
  (AgentToolkit / Host application) и ComboBox «Theme» (Dark / Light).

### Хост-контракт: два режима владения темой

У `AgentThemeManager` есть перечисление `AgentThemeMode` и метод `SetMode`, который выбирает,
кто владеет темой тулкита:

- **`Manual` (дефолт)** — темой владеет только тулкит: переключение идёт через ComboBox «Theme»
  в `SettingsControl` (`SettingsModel.BaseTheme` + `ApplyFromSettings`). Вызовы
  `AgentThemeManager.Apply(...)` от хоста игнорируются.
- **`Auto`** — темой владеет хост: он применяет свою настройку через `AgentThemeManager.Apply(...)`,
  а `SettingsControl` только отражает текущую тему и не перебивает её при `DataContextChanged`.

Хост, который сам управляет темой, при старте выбирает `Auto` и применяет сохранённую тему:

```csharp
using Agent.Models;               // AgentThemeMode
using Agent.Toolkit.Wpf.Theming;  // AgentThemeManager

AgentThemeManager.SetMode(AgentThemeMode.Auto);
AgentThemeManager.Apply(SettingsStore.Load().BaseTheme); // "Dark" | "Light"

// переключение в обработчике тумблера темы хоста (действует только в режиме Auto):
AgentThemeManager.Apply(light ? "Light" : "Dark");
```

В режиме `Manual` хост **не** вызывает `Apply` — темой владеет только ComboBox «Theme» в
`SettingsControl`. Без явного `SetMode` по умолчанию действует `Manual` (совместимо с автономным
запуском тулкита); тёмную тему при этом страхует `EnsureInitialized` в `ChatControl`/`AgentTabControl`.

Логика режимов и детект смены вынесены в UI-свободный `Agent.Models.ThemeModeState` (netstandard2.0),
поэтому контракт покрыт юнит-тестами без WPF `Application`.

Режим также выбирается пользователем на вкладке «Theme» в `SettingsControl` (ComboBox «Theme source»,
связан с `SettingsModel.ThemeModeIndex`, значение персистится в `SettingsData.ThemeMode`). При загрузке
`SettingsControl` применяет сохранённый режим через `AgentThemeManager.SetMode`, а в режиме `Auto`
отключает ComboBox «Theme», т.к. темой владеет хост-приложение.

### Добавить новую тему

1. Создай `Styles/Theme.<Name>.xaml` с тем же набором ключей `Theme.*`.
2. Зарегистрируй палитру в `AgentThemeManager` (поле + `LoadPalette` + ветка в `Apply`).
3. Добавь пункт в `SettingsModel.ThemeIndex`/`BaseTheme` и ComboBox в `SettingsControl`.

### Обратная совместимость

Ключи `Dark.*` и `Material.Accent` оставлены в `AgentTheme.xaml` как legacy-шим — старые
контролы хоста, ссылающиеся на `{StaticResource Dark.*}`, продолжают работать. Новый код
должен использовать `Theme.*`.

## TDD — обязательное правило
- Любая разработка идёт только через тестирование (red → green → refactor).
- Сначала пишется падающий тест, затем минимальная реализация, затем рефакторинг.
- Каждая новая возможность должна быть покрыта юнит-тестами в проекте 