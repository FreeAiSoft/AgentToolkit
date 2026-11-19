# План: переключение темы из хоста + починка темизации AgentToolkit + интеграция в RPAWriter2 / AiRpaStudio

> Документ-план. Код не правим — только фиксируем, что и в каком порядке делать.
> Актуализирует решение C1 из `AgentShared_Update_And_Integration_Plan.md` (там было решено
> «оставить свою тему, переключение из хоста — отдельная карточка»). Настоящий план и есть
> эта карточка: делаем тему AgentToolkit переключаемой из приложения-хоста, чиним её покрытие
> и доставляем обновление в оба хоста.

## 0. Резюме

Три задачи:

1. **Сделать тему AgentToolkit переключаемой из приложения-хоста.** Сейчас `AgentThemeManager`
   есть и умеет `Apply("Dark"/"Light")`, но хосты его **не вызывают** (ни в RPAWriter2, ни в
   AiRpaStudio нет ни одного вызова `AgentThemeManager`). В AiRpaStudio тема студии
   (`StudioTheme` + `Studio.*`) и тема чата (`ChatTheme.*`) переключаются своим механизмом, а
   встроенный контрол AgentToolkit (`ChatMessagesControl`) продолжает жить на дефолтной тёмной
   палитре `Theme.*`. Нужен единый хост-контракт с двумя режимами переключения (ручной/автоматический).
2. **Починить покрытие темы внутри контролов агента.** Часть контролов использует
   `{DynamicResource Theme.*}` и перекрашивается, но есть хардкод цветов и `Background="Black"`,
   которые при переключении не меняются. Перечислить и перевести на `Theme.*`.
3. **Интегрировать обновление в RPAWriter2 и AiRpaStudio**: поднять версию, собрать пакеты,
   поднять `PackageReference`, подключить переключение темы из хоста и мигрировать хост-контролы
   с `{StaticResource Dark.*}` на `{DynamicResource Theme.*}`.

---

## 1. Диагноз (факты, снято 19.11.2026)

### 1.1. Как устроена тема в AgentShared

- Палитры: `Agent.Toolkit.Wpf/Styles/Theme.Dark.xaml`, `Theme.Light.xaml` — нейтральные ключи
  `Theme.*` (`Theme.Window`, `Theme.Panel`, `Theme.Control`, `Theme.Text`, `Theme.Border`,
  `Theme.Accent`, `Theme.CodeBackground`, `Theme.Scroll*`, `Theme.Chat*`, `Theme.Card*`,
  `Theme.Context*` и т.д.).
- Менеджер: `Agent.Toolkit.Wpf/Theming/AgentThemeManager.cs` — пишет значения кистей в
  `Application.Current.Resources`, `DynamicResource`-ссылки перекрашивают UI на лету.
  Публичное API уже есть:
  - `Apply(string baseTheme)` / `Apply(bool light)` / `ApplyDark()` / `ApplyLight()`;
  - `EnsureInitialized()` (страховка, тёмная тема);
  - `Current` (строка "Dark"/"Light");
  - событие `ThemeChanged`.
- Стили: `Styles/AgentTheme.xaml` (неявные стили TextBlock/TextBox/PasswordBox/Button/TabControl/
  ComboBox/ListBox и т.д. — на `Theme.*`), `Styles/ChatResources.xaml` (карточки сообщений),
  `Styles/DarkScrollBars.xaml` (скроллбары). `AgentTheme.xaml` содержит legacy-шим `Dark.*`
  (только тёмные значения) для старых контролов хостов.
- Настройка: `Agent.Models.SettingsModel.BaseTheme` / `ThemeIndex` (0=Dark, 1=Light).
- Переключатель внутри тулкита: секция «Theme» в `SettingsControl.xaml` (ComboBox,
  `OnThemeSelectionChanged` → `ApplyLight`/`ApplyDark`), плюс применение при `DataContextChanged`.
- `EnsureInitialized()` вызывается в конструкторах `ChatControl`, `SettingsControl`,
  `AgentTabControl`.

### 1.2. Почему «мало что меняется» — инвентарь незатемизированных мест

Контролы, у которых **нет** `DynamicResource Theme.*` вовсе (не перекрашиваются) либо есть
хардкод:

| Файл (Agent.Toolkit.Wpf) | Проблема | Что сделать |
|---|---|---|
| `Controls/Chat/ToolCallMessageControl.xaml` | `Background="Black"` на ScrollViewer (строка 16) | `{DynamicResource Theme.CodeBackground}` |
| `Controls/Chat/ToolBlockedMessageControl.xaml` | `Background="Black"` на ScrollViewer (строка 16) | `{DynamicResource Theme.CodeBackground}` |
| `Controls/Chat/CheckpointMessageControl.xaml` | `#4CAF50` (стр. 20), `#64B5F6` (стр. 26) — хардкод | ввести ключи `Theme.*` либо переиспользовать `Theme.ChatLastActBorder`/`Theme.Highlight` |
| `Controls/ChatControl.xaml` | `#1A64B5F6` (стр. 72), градиент `#6E6E6E/#E9E9E9/#6E6E6E` (стр. 142–144), `#7AC88F` (стр. 460, «активный чат»), `#FF8C00` (стр. 484, «important») | ввести ключи `Theme.*` (заливка юзер-сообщения, шиммер, активная точка, важная метка) |
| `Controls/SettingsControl.xaml` | `#C8C8C8` (стр. 714, 729) | `{DynamicResource Theme.TextMuted}` / `Theme.Border` |
| `Controls/Notification/NotificationBanner.xaml` | весь цвет из DP `AccentBrush`, фон `Transparent` | добавить темо-зависимый фон (`Theme.Panel`) либо задокументировать как семантический |
| `Styles/AgentTheme.xaml` | legacy `Dark.*` (стр. 28–37) — только тёмные, `StaticResource` | оставить как шим для хостов, но тулкит не должен на них ссылаться |
| код-бихайнд: `Markdown/MarkdownRenderer.cs`, `Controls/MermaidDiagramControl.cs`, `Controls/Chat/ChatMessageCard.cs`, `Controls/ContextIndicator.xaml(.cs)`, `Controls/SegmentedToggle.xaml(.cs)`, `Controls/MessageQueueControl.xaml(.cs)` | возможен хардкод кистей/цветов вне `Theme.*` | аудит и перевод на `Theme.*` |

Справка: карточки сообщений (`ChatResources.xaml`) и шаблоны тела сообщения
(`ChatMessagesControl.xaml`: `TextMessageBlock`, `CodeMessageBlock`) уже на `Theme.*` — они
перекрашиваются. Проблема точечная, а не тотальная.

### 1.3. Текущие версии пакетов и точки входа хостов

Версии (снято из `.csproj`):

| Проект | `AgentToolkit` | `AgentToolkit.Wpf` |
|---|---|---|
| AgentShared `Directory.Build.props` | — (`Version 1.17.0`) | — |
| RPAWriter2 `RPAWriter2/RPAWriter2.csproj` | **1.17.0.1** | **1.17.0.1** |
| RPAWriter2 `Agent.Rpa/Agent.Rpa.csproj` | 1.15.0 | — |
| RPAWriter2 `RPAWriter.Tests/RPAWriter.Tests.csproj` | 1.15.0 | — |
| AI-RPA `RPA.Chat/RPA.Chat.csproj` | **1.17.0** | **1.17.0** |
| AI-RPA `RPA.Shared/RPA.Shared.csproj` | 1.17.0 | — |

**AiRpaStudio (AI-RPA, `C:\Users\Maxxx\Desktop\AI-RPA`):**
- Свой механизм темы: `RPA.Shared/Models/StudioTheme.cs` (Dark/Light),
  `AiRpaStudio/Helpers/StudioPalette.cs` + `StudioBrushes.cs` (`Studio.*`),
  `RPA.Chat/Themes/ChatTheme.Dark/Light.xaml` (`Dark.*`/`Chat.*`),
  `RPA.Chat/Themes/ChatStudioBridge.cs` (мост `Chat.* → Studio.*`).
- Переключатель в шапке студии: `ToggleButton.IsChecked` ← `ChatViewModel.IsDarkTheme`
  (`AiRpaStudio/Views/MainView.xaml.cs` стр. 171–176).
- Поток смены: `ChatViewModel.Theme` → `ThemeApplying` (`AppSettings.ApplyTheme`, пересобирает
  `Studio.*`) → `ThemeChanged` → `ChatPanel.ApplyTheme` (меняет свой словарь `ChatTheme.*` и
  перекрывает мостом). См. `MainView.xaml.cs` стр. 159–176, 244–250.
- `RPA.Chat/Views/ChatPanel.xaml` хостит **AgentToolkit `ChatMessagesControl`** (стр. 571 и 1083),
  который живёт на `Theme.*`. Но смена темы студии **не** вызывает `AgentThemeManager` — значит
  встроенный список сообщений и карточки остаются тёмными. Это главный разрыв.

**RPAWriter2 (`C:\Users\Maxxx\source\repos\RPAWriter2`, VSIX-расширение):**
- Нет `App.xaml.cs` и нет хост-переключателя темы; тема тулкита дефолтно тёмная через
  `EnsureInitialized()` в конструкторах тулкит-контролов.
- Хостит `AgentTabControl` (владеет Chat/Settings), `SettingsPanel.InsertHostSection(...)`
  (`MainWindowControl.xaml.cs` стр. 1183–1220) — то есть переключатель «Theme» уже доступен
  внутри `SettingsControl`, но хост-контролы его не слушают.
- Хост-контролы (`CommitEditorControl`, `CreateRobotControl`, `ControlsGalleryControl`,
  `KnowledgeBaseControl`, `PackagesControl`, `PixMwfMappingControl`, `PixRobotConversionControl`,
  `RpaSettingsSection`, `SpecTabControl`, `SystemActivitiesSelectionWindow`, `VsSettingsSection`,
  `WfSnippetsWindow`, `ConverterToolWindowControl`, `MainWindowControl.xaml`) массово используют
  `{StaticResource Dark.*}` — при переключении темы они **не** перекрашиваются (шим только тёмный).
- Часть кода-бихайнда тянет `(Brush)FindResource("Dark.Text")`
  (`MainWindowControl.xaml.cs` стр. 1198, 2713).

## 2. Задача 1 — хост-контракт переключения темы (AgentShared)

Цель: приложение-хост одним вызовом переключает тему и AgentToolkit, и своих контролов, при этом
источник истины один (не разъезжается с `SettingsControl`).

### 2.1. Что добавить в AgentShared

1. **Задокументировать/стабилизировать хост-API** `AgentThemeManager`:
   - `Apply(string name)` — уже есть, принимает `"Dark"`/`"Light"` без учёта регистра;
   - `Current` — уже есть;
   - `ThemeChanged` — уже есть;
   - убедиться, что `Apply` безопасен при `Application.Current == null` и при повторном вызове
     с той же темой (сейчас `WritePalette` не шлёт событие, если `_current == name` — это ок).
2. **Два режима переключения в ядре (`AgentThemeManager`).** Вводим перечисление
   `AgentThemeMode { Manual, Auto }` и свойство `Mode`/метод `SetMode` у `AgentThemeManager`:
   - **`Manual` (ручной)** — темой владеет только тулкит: переключение идёт исключительно через
     ComboBox «Theme» в `SettingsControl` (`SettingsModel.BaseTheme` + `Apply`). Вызовы
     `AgentThemeManager.Apply(...)` от хоста игнорируются (хост не трогает тему тулкита).
   - **`Auto` (автоматический)** — темой владеет хост: хост применяет свою настройку темы через
     `AgentThemeManager.Apply(...)`; `SettingsControl` только отражает текущую тему (ComboBox
     синхронизируется с `SettingsModel.BaseTheme`, который заполняет хост) и **не** применяет её
     при `DataContextChanged`.
   Дефолт — `Auto` для AiRpaStudio (тема студии = источник истины) и `Manual` для автономного
   запуска тулкита; конкретный дефолт/переключение — зафиксировать в коде + тестом.
3. **Палитра — штатная (решение R3).** Хосты принимают штатную палитру `Theme.*` тулкита,
   `ThemeBridge` (точное совпадение с `Studio.*`) не делаем. Хосты просто зовут
   `Apply("Dark"/"Light")`; цветовое расхождение с собственной палитрой студии — приемлемо.

### 2.2. Контракт (как будет выглядеть у хоста)

```csharp
// старт: хост выбирает, кто владеет темой тулкита
AgentThemeManager.SetMode(AgentThemeMode.Auto);           // Auto — применяется настройка хоста
AgentThemeManager.Apply(SettingsStore.Load().BaseTheme);  // "Dark" | "Light"

// переключение (в обработчике переключателя темы хоста) — действует только в режиме Auto
AgentThemeManager.Apply(light ? "Light" : "Dark");
```

В режиме `Manual` хост **не** вызывает `Apply` — темой владеет только ComboBox «Theme» в
`SettingsControl`, а собственная тема хоста (`Studio.*` в AiRpaStudio, миграция `Theme.*` в
RPAWriter2) переключается отдельно и на тулкит не влияет.

---

## 3. Задача 2 — починка покрытия темы (AgentShared)

Механическая замена хардкода на `{DynamicResource Theme.*}` (таблица в §1.2). Порядок:

1. **Добавить недостающие ключи в обе палитры** (`Theme.Dark.xaml` и `Theme.Light.xaml`) для
   новых семантик:
   - `Theme.CodeBackground` — уже есть (использовать для `Background="Black"`);
   - для `ChatControl`: `Theme.ChatUserBackground`/`Theme.ChatUserBorder` (уже есть — см.
     `Theme.ChatUserBorder`/`Theme.ChatUserBackground`), `Theme.Shimmer*` (градиент-заглушка),
     `Theme.ActiveDot` (#7AC88F), `Theme.ImportantMark` (#FF8C00);
   - для `CheckpointMessageControl`: `Theme.CheckpointHash` (#4CAF50) и `Theme.CheckpointState`
     (#64B5F6) — либо переиспользовать существующие `Theme.ChatLastActBorder`/`Theme.Highlight`.
2. **Заменить хардкод** в файлах из §1.2 на `{DynamicResource Theme.*}`.
3. **Аудит код-бихайнда** на `new SolidColorBrush(...)` / `Brushes.*` / `Color.FromRgb(...)` вне
   `AgentThemeManager` — перевести на чтение `Theme.*` через `FindResource`/`TryFindResource`
   (динамически, а не `StaticResource`).
4. **`NotificationBanner`**: дать темо-зависимый фон и оставить `AccentBrush` только для
   семантики (ошибка/предупреждение), чтобы текст/рамка не исчезали на светлой теме.
5. **Legacy `Dark.*` в `AgentTheme.xaml`**: оставить (обратная совместимость для хостов), но
   проверить, что ни один контрол тулкита на них не ссылается; при миграции хостов (§4) ключи
   можно будет со временем удалить.

## 4. Задача 3 — интеграция в RPAWriter2 и AiRpaStudio

### 4.0. Выпуск пакета (общий шаг)

1. Поднять версию в `Directory.Build.props`: `1.17.0` → **1.18.0** (фича/поведение → минор).
2. `dotnet build Agent.Tests/Agent.Tests.csproj` + `dotnet test` → зелёные.
3. `.\pack.ps1` → `feed\AgentToolkit.1.18.0.nupkg` и `AgentToolkit.Wpf.1.18.0.nupkg`.
4. Доставить пакеты в фиды хостов (по `nuget.config` каждого хоста):
   - AI-RPA: `nuget_local\` (или замапить `AgentShared\feed`) — см. план A1;
   - RPAWriter2: его `nuget.config`/`.nuget-offline` — положить те же `.nupkg`.

### 4.1. AiRpaStudio (AI-RPA)

1. Поднять `PackageReference` до **1.18.0** в `RPA.Chat/RPA.Chat.csproj` и
   `RPA.Shared/RPA.Shared.csproj`.
2. Подключить переключение темы тулкита в `AiRpaStudio/Views/MainView.xaml.cs`:
   - в потоке смены темы (`ThemeApplying`/`ThemeChanged`, стр. 159–176 и 244–250) рядом с
     `Services.AppSettings.ApplyTheme(...)` добавить `AgentThemeManager.Apply("Dark"/"Light")`
     по значению `StudioTheme`;
   - на старте применить сохранённую тему (`AppSettings.ApplyTheme(theme)` уже вызывается в
     стр. 166 — туда же добавить `AgentThemeManager.Apply`);
   - убедиться, что `ChatMessagesControl` (и `SettingsControl`, если добавится) перекрашиваются.
3. Порядок важен: сначала пересобрать `Studio.*` (`ThemeApplying`), затем `AgentThemeManager.Apply`
   и `ChatPanel`-мост — иначе чат/карточки отстают на один переключатель (тот же принцип, что уже
   зафиксирован в комментарии `MainView.xaml.cs` стр. 160–162).
4. Убедиться, что `ChatStudioBridge` не конфликтует с `Theme.*` (разные наборы ключей —
   конфликта быть не должно, но проверить визуально).

### 4.2. RPAWriter2 (VSIX)

1. Поднять `PackageReference` до **1.18.0** в `RPAWriter2/RPAWriter2.csproj` (и, при необходимости,
   `Agent.Rpa/Agent.Rpa.csproj`, `RPAWriter.Tests/RPAWriter.Tests.csproj`).
2. **Мигрировать хост-контролы с `{StaticResource Dark.*}` на `{DynamicResource Theme.*}`**
   (таблица в §1.3): `MainWindowControl.xaml`, `ConverterToolWindowControl.xaml` и 12 контролов в
   `RPAWriter2/Controls/`. Механическая замена `Dark.X → Theme.X` по таблице соответствий:
   `Dark.Window→Theme.Window`, `Dark.Panel→Theme.Panel`, `Dark.Control→Theme.Control`,
   `Dark.ControlHover→Theme.ControlHover`, `Dark.ControlPressed→Theme.ControlPressed`,
   `Dark.Border→Theme.Border`, `Dark.Text→Theme.Text`, `Dark.ErrorText→Theme.ErrorText`,
   `Dark.Accent→Theme.Accent`. `Dark.SubtleText/MutedText/Primary` (если встречаются) →
   `Theme.TextSecondary`/`Theme.TextMuted`/`Theme.Text`.
3. **Код-бихайнд**: заменить `(Brush)FindResource("Dark.Text")` на
   `(Brush)FindResource("Theme.Text")` (или `TryFindResource`) в `MainWindowControl.xaml.cs`
   (стр. 1198, 2713).
4. **Точка переключения темы из хоста (решение R4: два режима).** В ядре (`AgentThemeManager`)
   вводятся режимы `Manual`/`Auto` (§2.1.2), поэтому в RPAWriter2:
   - **`Manual`** — темой владеет только `SettingsControl` (ComboBox «Theme» уже встроен); после
     миграции `Dark.* → Theme.*` вся панель перекрашивается от этого ComboBox. Хост ничего не
     вызывает.
   - **`Auto`** — хост применяет свою настройку темы через `AgentThemeManager.Apply(...)` и
     персистит выбор в своих настройках (VSIX, единого App.xaml нет — точка старта в инициализации
     тул-окна `MainWindowControl`).
   Режим выбирается конфигурацией хоста (настройка/флаг), дефолт зафиксировать при реализации.
5. Убедиться, что после миграции нет «висячих» `StaticResource Dark.*` в контролах тулкита и
   что legacy-шим `Dark.*` в `AgentTheme.xaml` больше не нужен хост-контролам (можно оставить до
   следующего цикла).

## 5. Порядок работ (фазы)

1. **Фаза 0 — контрольная точка.** Собрать и прогнать тесты AgentShared
   (`dotnet build Agent.Tests/Agent.Tests.csproj`, `dotnet test`) и оба хоста — зафиксировать
   исходное зелёное состояние.
2. **Фаза 1 (Задача 1).** Хост-API `AgentThemeManager`: добавить режимы `Manual`/`Auto`
   (`AgentThemeMode`), стабилизировать поведение с `SettingsControl`, задокументировать контракт.
   Покрыть тестами (§6).
3. **Фаза 2 (Задача 2).** Починка покрытия: добавить недостающие `Theme.*` ключи в обе палитры,
   заменить хардкод и `Background="Black"`, аудит код-бихайнда. Покрыть тестами (§6).
4. **Фаза 3 — выпуск.** Версия 1.18.0 → `pack.ps1` → разложить `.nupkg` по фидам хостов.
5. **Фаза 4 (Задача 3, AiRpaStudio).** Поднять версию, подключить `AgentThemeManager.Apply` в
   поток темы студии; визуальная проверка переключения Dark/Light.
6. **Фаза 5 (Задача 3, RPAWriter2).** Поднять версию, мигрировать `Dark.*→Theme.*`, подключить
   точку переключения; визуальная проверка.
7. **Фаза 6 — приёмка.** Полные сборки/тесты обоих хостов, визуальная проверка обеих тем на
   чате, настройках и хост-контролах.

---

## 6. TDD и тесты (обязательное правило red → green → refactor)

1. **Тесты `SettingsModel`/нормализации** (продолжить `Agent.Tests/ThemeSettingsTests.cs`):
   - `ThemeIndex`/`BaseTheme` двусторонне (0/1 ↔ Dark/Light), регистронезависимость, дефолт Dark.
2. **Тесты `AgentThemeManager`** (отдельный фикстур; где нужен `Application.Current`, тестировать
   через STA-хелпер или абстрагировать запись в `ResourceDictionary`):
   - `Apply("Light")` выставляет `Current == "Light"` и кладёт светлую кисть `Theme.Window`;
   - повторный `Apply("Light")` не шлёт `ThemeChanged` повторно;
   - `Apply("Dark")` после `Apply("Light")` шлёт `ThemeChanged` один раз;
   - `EnsureInitialized` не перезаписывает уже применённую хостом тему;
   - в режиме `Manual` вызов `Apply(...)` от хоста игнорируется (тему меняет только `SettingsControl`);
   - в режиме `Auto` `Apply(...)` от хоста применяет тему, а `SettingsControl` при
     `DataContextChanged` не перебивает её.
3. **Тесты покрытия темы (Задача 2):** новый guard-тест, который парсит `.xaml` контролов и
   падает, если найден хардкод `#[0-9A-Fa-f]{6,8}` или `Background="Black"` вне
   `Theme.Dark/Light.xaml`/легаси-шима. Это регрессионный страж, чтобы хардкод не возвращался.
4. **Интеграционные (по факту, в хостах):** убедиться, что при переключении меняются фон окна,
   текст, карточки сообщений, скроллбары, ComboBox/TabControl — снять контрольный список.

---

## 7. Критерии готовности

- В ядре есть режимы `Manual`/`Auto`: в `Auto` хост одним вызовом `AgentThemeManager.Apply(...)`
  переключает тему и `SettingsControl` его не перебивает; в `Manual` темой владеет только
  ComboBox «Theme» в `SettingsControl`, а `Apply(...)` от хоста игнорируется.
- В контролах AgentToolkit нет хардкода цветов и `Background="Black"` вне палитр; guard-тест
  зелёный.
- AgentShared: `dotnet test` зелёный; версия `Directory.Build.props` = 1.18.0; пакеты в `feed\`.
- AiRpaStudio: `PackageReference` 1.18.0; переключатель темы студии одновременно меняет и
  `Studio.*`, и `Theme.*` (карточки чата перекрашиваются); старт применяет сохранённую тему.
- RPAWriter2: `PackageReference` 1.18.0; хост-контролы на `{DynamicResource Theme.*}` (нет
  `StaticResource Dark.*` в проде); переключение темы меняет всю панель.
- Оба хоста собираются с 0 ошибок, тесты зелёные, число тестов не меньше контрольной точки.

---

## 8. Риски и открытые решения

- **R1. Два источника истины темы.** В AiRpaStudio тема уже живёт в `ChatViewModel.Theme`
  (`StudioTheme`) + `AppSettings`. Нельзя заводить второй источник в `SettingsModel.BaseTheme`
  тулкита — хост должен остаться единственным владельцем и просто транслировать значение в
  `AgentThemeManager`. **Решение:** хост-владелец, тулкит — ведомый.
- **R2. `SettingsControl` автоприменение.** Может перебить хост при `DataContextChanged`.
  **Решение:** два режима в §2.1.2 (в `Auto` темой владеет хост, `SettingsControl` не перебивает).
- **R3. Совпадение палитр — решено.** Принимаем штатную палитру `Theme.*` тулкита, `ThemeBridge`
  не делаем (§2.1.3).
- **R4. RPAWriter2 — VSIX без App.xaml — решено.** В ядре два режима переключения
  (`Manual`/`Auto`): `Manual` — только ComboBox «Theme» в `SettingsControl`; `Auto` — применяется
  настройка хост-приложения (§2.1.2, §4.2.4).
- **R5. Legacy `Dark.*`.** После миграции хостов шим становится не нужен, но удалять его сразу
  рискованно (могут остаться незамеченные ссылки). **Решение:** мигрировать → прогон → удалять
  шим отдельным шагом.
- **R6. `Material.Icons`/`Material.Icons.WPF`.** Разные пакеты иконок в хостах — не связано с
  темой напрямую, но проверить, что иконки тоже используют `Theme.Text`, а не хардкод.
