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

## Переключение темы (Dark/Light)

### Как это устроено

- Палитры: `Agent.Toolkit.Wpf/Styles/Theme.Dark.xaml` и `Theme.Light.xaml` — все цвета
  с нейтральными ключами `Theme.*`.
- Менеджер: `Agent.Toolkit.Wpf/Theming/AgentThemeManager.cs` — пишет значения кистей
  в `Application.Current.Resources`, поэтому `DynamicResource`-ссылки перекрашивают UI на лету.
- Контролы ссылаются на цвета через `{DynamicResource Theme.*}` (не `StaticResource`).
- Настройка: `SettingsModel.ThemeIndex` (0 = Dark, 1 = Light) и секция «Theme» в `SettingsControl`.

### Точка входа для хоста

Хост должен применить сохранённую тему при старте:

```csharp
AgentThemeManager.Apply(SettingsStore.Load().BaseTheme);
```

Без этого по умолчанию будет тёмная (`EnsureInitialized` в `ChatControl`/`SettingsControl` страхует).

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