# План: персист настроек тулкита (SQLite, per-workspace)

> Документ-план. Актуально для `AgentShared`
> (`C:\Users\Maxxx\source\repos\AgentShared`).
>
> Цель: тулкит, подключённый в приложение как контрол чата, умеет сам
> персистить свои настройки — создаёт свою подпапку внутри папки данных
> приложения, создаёт базу для персиста и хранит настройки с учётом разных
> воркспейсов агента. История сообщений в этой итерации **не реализуется**,
> но схема БД закладывается так, чтобы добавить её без переделки.
>
> Интеграция в AiRpaStudio в этой итерации **не выполняется** (вынесена в
> отдельный опциональный этап).

---

## 1. Текущее состояние (важно для «не поломать»)

- `ChatControl` (тулкит) сам настройки не хранит: хост обязан передать
  `Settings` (`SettingsData`) и, опционально, `SettingsPersistence`
  (`ISettingsStore`).
- Если `SettingsPersistence == null`, оверлей Permissions в
  `ChatControl.OnPermissionChanged` падает на статический
  `Agent.Core.Services.SettingsStore`, который пишет JSON в
  `%AppData%\RPAWriter2\<version>\settings.json` — это легаси-фолбэк.
- Вкладка Settings тулкита (`SettingsControl`) работает через
  `Agent.Models.SettingsModel`, которому хост сам передаёт `ISettingsStore`.
- `Agent.Core` уже имеет инфраструктуру воркспейсов: `AppDataPaths`
  (папки `workspaces\<id>`), `WorkspaceIdentity`, `WorkspaceResolver`,
  `WorkspaceInfo`, контракт `IWorkspaceProvider`; а
  `IChatHistoryStore.WorkspaceId` уже умеет скоупить историю.
- SQLite в тулките сейчас **нет**.

Вывод: делаем **аддитивно**. Существующие механизмы (host-injected store и
легаси-фолбэк) остаются; новый дефолтный персист включается только там, где
хост этого попросит.

---

## 2. Решения

### R1. Бэкенд хранения настроек

| Вариант | Плюсы | Минусы | Решение |
|---|---|---|---|
| **A. SQLite** (`Microsoft.Data.Sqlite` + `SQLitePCLRaw.bundle_e_sqlite3`) | Настоящая «база»; транзакции; одна БД потом примет историю; легко расширять таблицами | Новый нативный бинарь в net472/net48-хостах; надо проверить упаковку в `AgentToolkit` nupkg | **Основной** |
| **B. JSON-файлы** по скоупам | Ноль новых зависимостей; максимум совместимости | Не «база»; историю потом всё равно надо переводить на БД | Запасной |
| **C. Гибрид** (сейчас JSON, БД — позже под историю) | Минимальный риск сейчас | Откладывает требование «база для персиста» | Запасной |

**Решение:** A, но с обязательным спайком на упаковку (Шаг 0). Если нативный
SQLite не упакуется чисто в `AgentToolkit.nupkg` для net472/net48 — откатываемся
на B/C без смены публичных контрактов (`ISettingsStore` остаётся тем же).

### R2. Как тулкит узнаёт папку данных приложения

| Вариант | Описание | Решение |
|---|---|---|
| **A. Контракт `IAppDataDirectoryProvider`** | Хост отдаёт свой корень данных; тулкит добавляет `AgentToolkit\`. Тестируемо, без статики | **Основной** |
| B. Просто `Func<string>` | Меньше типов, хуже читается/тестируется | Допустимо |
| C. Продлить статический `AppDataPaths` | Меньше новых контрактов, но `AppDataPaths` завязан на `RPAWriter2` | Не рекомендуется |

**Решение:** A. Плюс у `ChatControl` появляется свойство-ярлык
`AppDataDirectory` (и `WorkspaceProvider`), чтобы хост мог включить дефолт одной
строкой.

### R3. Скоупинг по воркспейсам

| Вариант | Описание | Решение |
|---|---|---|
| **A. БД-на-воркспейс** | `<appData>\AgentToolkit\workspaces\<workspaceId>\toolkit.db`; без воркспейса — `<appData>\AgentToolkit\toolkit.db` | **Основной** |
| B. Одна БД + колонка `workspace_id` | Один файл, глобальные запросы | Сложнее миграция/конкурентность |
| C. JSON-на-воркспейс | Как A, но `settings.json` | Только если выбран R1=B |

**Решение:** A. Совпадает с уже существующим паттерном
`AppDataPaths.ResolveWorkspace`, простая изоляция и удаление. Дефолт **без**
`IWorkspaceProvider` = глобальная БД (ровно «как раньше», только БД вместо JSON).
Как только хост передаёт `IWorkspaceProvider` — настройки становятся
воркспейс-скоупленными.

### R4. Обратная совместимость (критично)

Приоритет выбора стора в `ChatControl`:

1. `SettingsPersistence` (host-injected) — **как сейчас, не трогаем**.
2. Новый дефолт: `AppDataDirectory` задан → `ToolkitSettingsStore`
   (БД, воркспейс-скоуп).
3. Ничего не задано → старый статический
   `Agent.Core.Services.SettingsStore` (легаси-фолбэк, **поведение не меняется**).

Это гарантирует, что RPAWriter2, VibeCoder и текущий AI-RPA продолжат работать
без изменений, пока сами не перейдут на дефолт.

---

## 3. Целевая структура папок и схема БД

```
<appDataRoot>                        <- отдаёт хост (напр. корень данных приложения)
└── AgentToolkit\                    <- создаёт тулкит
    ├── toolkit.db                   <- глобальные настройки (нет воркспейса)
    └── workspaces\
        └── <workspaceId>\           <- SHA-1 от пути решения (WorkspaceIdentity)
            └── toolkit.db           <- настройки конкретного воркспейса
```

Схема (сейчас заполняется только настройками, история — резерв):

```sql
PRAGMA user_version = 1;

CREATE TABLE IF NOT EXISTS settings (
    key             TEXT PRIMARY KEY,
    value_json      TEXT NOT NULL,
    updated_at_utc  TEXT NOT NULL
);

-- зарезервировано на следующую волну (сейчас НЕ создаём):
-- conversations(id, title, created_at_utc, updated_at_utc, is_important,
--                provider_transcript_json, usage_json)
-- messages(conversation_id, seq, kind, text, reasoning, mode, tool_name,
--          options_json, checkpoint_hash, timestamp_utc,
--          PRIMARY KEY(conversation_id, seq))
```

Настройки храним одной строкой `key='settings'`,
`value_json = SettingsData` (Newtonsoft). `user_version` — версия схемы для
будущих миграций.

---

## 4. Пошаговый план (только AgentShared)

### Шаг 0. Спайк упаковки SQLite (gate)

- Добавить `Microsoft.Data.Sqlite` в `Agent.Core` и в обёртку `AgentToolkit`.
- Собрать `pack.ps1`, поставить локальный пакет в тестовый net472/net48
  потребитель, убедиться, что нативный `e_sqlite3` попадает и БД
  открывается/пишется.
- **Критерий:** открытие/запись SQLite работает на net472 (WPF-хост) и на net8
  (тесты). Если нет — фиксируем R1=B/C.

### Шаг 1. TDD: контракт и пути (red → green)

- Добавить `IAppDataDirectoryProvider` в
  `Agent.Core\Contracts\UiServicesContracts.cs`:
  `string GetAppDataDirectory()`.
- Добавить `Agent.Core\Services\ToolkitStoragePaths.cs`:
  - `ToolkitDirectory(appDataDirectory)`;
  - `WorkspacesRoot(appDataDirectory)`;
  - `WorkspaceDirectory(appDataDirectory, workspaceId)`;
  - `SettingsDatabase(appDataDirectory, workspaceId)` (пустой id → глобальная
    `toolkit.db`).
- Тесты `Agent.Tests\ToolkitStoragePathsTests.cs`: путь с воркспейсом, без
  воркспейса, с пробелами/спецсимволами в `appDataDirectory`.

### Шаг 2. TDD: `ToolkitSettingsStore`

- Новый `Agent.Core\Services\ToolkitSettingsStore.cs : ISettingsStore`:
  - ctor
    `(string appDataDirectory, IWorkspaceProvider workspaceProvider = null, string legacySettingsFile = null)`;
  - `Load()`: определяет текущий `workspaceId` (из `IWorkspaceProvider`,
    fallback `""`), `Directory.CreateDirectory`, открывает БД, создаёт схему,
    читает `key='settings'`; при пустой БД и наличии legacy JSON — импортирует
    один раз; `SettingsData.Normalize`; возвращает.
  - `Save()`: `Normalize` + `UPSERT` в `settings`.
  - Все IO-ошибки проглатываются (как в существующих сторах).
- Тесты `Agent.Tests\SqliteSettingsStoreTests.cs`:
  - roundtrip load/save;
  - дефолты при пустой БД;
  - изоляция двух воркспейсов (fake `IWorkspaceProvider`);
  - глобальный скоуп при `workspaceProvider == null`;
  - миграция из legacy `settings.json`;
  - папка и `toolkit.db` создаются лениво при первом `Load`/`Save`.

### Шаг 3. Подключить дефолт в `ChatControl` (аддитивно)

- В `Agent.Toolkit.Wpf\Controls\ChatControl.xaml.cs`:
  - `public string AppDataDirectory { get; set; }`;
  - `public IWorkspaceProvider WorkspaceProvider { get; set; }`;
  - лениво создавать `ToolkitSettingsStore`, **только если**
    `SettingsPersistence == null && !string.IsNullOrWhiteSpace(AppDataDirectory)`;
  - оставить текущий fallback `SettingsStore.Save(...)` для случая «ничего не
    задано» — без изменений.
- Не менять сигнатуру/поведение для хостов, которые уже передают
  `SettingsPersistence`.

### Шаг 4. Зависимости и версия

- `Agent.Core\Agent.Core.csproj` + `AgentToolkit\AgentToolkit.csproj`:
  добавить `Microsoft.Data.Sqlite` 8.0.x (совместимую с netstandard2.0).
- `Directory.Build.props`: поднять минор версии (фича).

### Шаг 5. Прогон

- `dotnet test Agent.Tests/Agent.Tests.csproj` — зелёный.
- `.\pack.ps1` — пакеты собираются, `AgentToolkit.nupkg` содержит
  SQLite-зависимости и натив (проверка из Шага 0).

---

## 5. Отложено (вне этой итерации)

- Интеграция в AiRpaStudio / `RPA.Chat` (переключение `ChatHostView` на новый
  дефолт, `StudioWorkspaceProvider`, миграция `chat_settings.json`).
- Хранение истории сообщений в SQLite (таблицы `conversations`/`messages`).
- Голосовой ввод.
- Миграция RPAWriter2/VibeCoder на новый дефолт (их поведение не меняем).

---

## 6. Риски и меры

| Риск | Мера |
|---|---|
| Нативный SQLite не упаковывается в net472-хостах | Шаг 0 — gate; запасной JSON-бэкенд без смены контрактов |
| Сломать легаси-фолбэк `SettingsStore` (RPAWriter2/VibeCoder) | Приоритет R4: host store > новый дефолт > старый статик; старый файл/путь не трогаем |
| Расхождение `ChatControl.Settings` и `SettingsModel` | Явно синхронизировать источник; покрыть стражами |
| Миграция дважды затирает пользовательские настройки | Импорт только если строка `settings` в БД отсутствует; legacy-файл не удаляется |
| Пер-воркспейс настройки удивляют пользователей | Дефолт без `IWorkspaceProvider` = глобально; воркспейс-скоуп включается только передачей провайдера |

---

## 7. Порядок коммитов (рекомендуемый)

1. `test: ToolkitStoragePaths + ToolkitSettingsStore (red)` — шаги 1–2.
2. `feat(core): IAppDataDirectoryProvider + ToolkitStoragePaths + ToolkitSettingsStore (SQLite)` — green.
3. `feat(wpf): ChatControl — дефолтный персист через AppDataDirectory` — шаг 3.
4. `chore: Microsoft.Data.Sqlite в Agent.Core/AgentToolkit, версия X.Y.0` — шаг 4.
