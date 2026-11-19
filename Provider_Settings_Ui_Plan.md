# План: вывод настройки AI-провайдера в интерфейс настроек (вкладка API)

> Документ-план. Код не правим — только фиксируем, что и в каком порядке делать.

## 0. Резюме

Провайдеры нейросетей уже реализованы в ядре (каталог, фабрика, модель настроек,
персистенция, тесты), но в интерфейсе их не видно: во вкладке «API» нет выбора
провайдера, а провайдер-специфичные настройки (в первую очередь Azure) не
существуют ни в модели, ни в провайдере. Делаем три вещи:

1. ComboBox «Provider» в начало секции API, связанный с `SettingsModel.ProviderIndex`.
2. Провайдер-специфичные поля: `AzureApiVersion` + `AzureDeploymentName` (для Azure
   OpenAI) и проброс их в `OpenAiCompatibleProvider` (query `api-version` + сегмент
   `deployments/{name}` + заголовок `api-key`).
3. Условная видимость строк секции API по метаданным каталога (скрыть API key для
   Ollama/LM Studio и показывать Azure-поля только для Azure).

Дизайн-принцип (по образцу Cline): не хардкодить панель на каждого провайдера,
а описывать «какие поля нужны» в метаданных `AiProviderSpec` и управлять видимостью
строк из этого описания.

## 1. Диагноз (факты, снято 19.11.2026)

### 1.1. Что уже есть в ядре (готово)

- **Каталог** — `Agent.Core/Providers/AiProviderCatalog.cs`: 10 встроенных провайдеров
  (Anthropic, OpenAI, Azure OpenAI, Ollama, LM Studio, OpenRouter, DeepSeek, Groq, xAI,
  OpenAI-compatible custom). У каждого `Id`, `DisplayName`, `Protocol`, `DefaultBaseUrl`,
  `DefaultModel`, `RequiresApiKey`, `Capabilities`, `ContextWindowSize`, `DocsUrl`.
- **Вспомогательные типы** — `AiProviderId.cs` (канонические id + `Normalize`),
  `AiProviderSpec.cs`, `AiProviderCapabilities.cs`, `AiProviderProtocol.cs`, `AiModelInfo.cs`.
- **Фабрика** — `Agent.Core/Services/AiProviderFactory.cs`: `Create(SettingsData)` маршрутизирует
  по `ProviderId` и строит `AnthropicProvider` / `OpenAiCompatibleProvider` / `StubAiProvider`.
- **Модель** — `Agent.Models/SettingsModel.cs`:
  - поле `_providerId` (строка 30);
  - `ProviderId` (строка ~637) — сеттер нормализует id и вызывает `ApplyProviderDefaults()`;
  - `ProviderDisplayName` (строка ~653) — имя из каталога;
  - `ProviderIndex` (строка ~663) — 0-based индекс по `AiProviderCatalog.All`;
  - `ApplyProviderDefaults()` (строка ~685) — подставляет `BaseUrl` / `Model` / `ContextWindowSize`
    выбранного провайдера;
  - `ProviderId` сохраняется/загружается в `SettingsData.ProviderId` (строки 170 и 314).
- **Провайдер** — `OpenAiCompatibleProvider` шлёт `POST {BaseUrl}/chat/completions` с
  `Authorization: Bearer`; Azure-специфики (`api-version`, `deployments/{name}`, заголовок
  `api-key`) нет.
- **Тесты** — `Agent.Tests/ProviderCatalogTests.cs`, `Agent.Tests/ProviderTests.cs` покрывают
  нормализацию id, каталог и маршрутизацию фабрики.

### 1.2. Чего нет в интерфейсе (пробел)

Вкладка API `Agent.Toolkit.Wpf/Controls/SettingsControl.xaml` (секция, строки ~117–148) сейчас:

- Row 0 — Base URL (`Settings.BaseUrl`);
- Row 1 — API key (`PasswordBox`, `ApiKeyBox`);
- Row 2 — Model (`Settings.Model`);
- далее Temperature, таймауты, tool calling, memory bank и т.д.

**ComboBox «Provider» отсутствует**, и биндинг `Settings.ProviderIndex` / `Settings.ProviderDisplayName`
не встречается ни в одном XAML тулкита. Поле API key показывается всегда, хотя Ollama/LM Studio
его не требуют.

### 1.3. Чего нет в модели/фабрике (провайдер-специфичные настройки)

Текущая модель — только общие поля. Не хватает специфичных полей, которые реально
отличают провайдеров по wire-формату (таксономия сверена с Cline `ProviderConfig`):

- **Azure OpenAI** — нужны `AzureApiVersion` (query `api-version`) и `AzureDeploymentName`
  (сегмент `openai/deployments/{name}` + заголовок `api-key`). Сейчас Azure в каталоге
  с пустым `DefaultBaseUrl` и общим `OpenAiCompatible` — не работает.
- **(отложено)** AWS Bedrock (region/accessKey/secretKey/sessionToken/cross-region),
  Google Vertex/Gemini (projectId+region), `apiLine` (china/international) — требуют новых
  протоколов-хендлеров, вне объёма.
- **(отложено)** произвольные HTTP-заголовки (`HTTP-Referer`/`X-Title` для OpenRouter,
  `X-Api-Key` для кастомных эндпоинтов) — generic-расширение, отдельным шагом.

## 2. План работ

### 2.0. Дизайн-решение

Идём по прагматичному варианту (не полный data-driven рантайм-рендер, как в Cline/React,
а **метаданные в спеке + фиксированные строки XAML с условной видимостью**):

- Расширяем `AiProviderSpec` списком «провайдер-специфичных полей» (enum-флаги/дескрипторы).
- `SettingsControl` показывает/скрывает строки (API key, Azure-поля) через
  `Visibility`-биндинги к метаданным выбранного провайдера.
- Конкретные значения полей (`AzureApiVersion`, `AzureDeploymentName`) — обычные
  наблюдаемые свойства `SettingsModel` + `SettingsData` (персистенция).

Полный динамический рендер форм не делаем: у нас 2 протокола, XAML-сетка фиксированная —
это окупится только если появится 3-й протокол (Bedrock/Gemini).

### 2.1. Ядро: `SettingsData` — новые поля

`Agent.Core/Services/SettingsStore.cs` (класс `SettingsData`), рядом с `ProviderId`:

- `public string AzureApiVersion { get; set; } = "";` — query-параметр `api-version`.
- `public string AzureDeploymentName { get; set; } = "";` — deployment/engine Azure;
  при пустом значении использовать `Model`.

Дефолты версии вынести в каталог (`AiProviderSpec`), а не в `SettingsData`.

### 2.2. Каталог: `AiProviderSpec` — метаданные полей + Azure-дефолты

`Agent.Core/Providers/AiProviderSpec.cs` (+ `AiProviderCatalog.cs` для Azure):

- `public string DefaultAzureApiVersion { get; set; } = "";` (для Azure — актуальная
  стабильная версия, значение уточнить по доке на момент реализации).
- Новый тип `AiProviderSettingFlags` (флаги: `ApiKey`, `AzureApiVersion`,
  `AzureDeploymentName`, …) + свойство `Settings` — перечисление, какие поля показывать.
  Минимально можно обойтись уже существующим `RequiresApiKey` для скрытия API key,
  но флаги дают чистую расширяемость под будущих провайдеров.
- `ApplyProviderDefaults()` дополнительно подставляет `AzureApiVersion`/`AzureDeploymentName`
  (по `Settings`-флагам), не затирая введённые пользователем значения (см. R3).

### 2.3. Фабрика/провайдер: проброс Azure

`Agent.Core/Services/AiProviderFactory.cs` + `OpenAiCompatibleProvider`:

- Для `ProviderId == AzureOpenAi` собирать URL как
  `{BaseUrl}/openai/deployments/{deployment}/chat/completions?api-version={version}`
  (BaseUrl = `https://{resource}.openai.azure.com`), а авторизацию отправлять заголовком
  `api-key` (не `Authorization: Bearer`).
- Поля пробрасывать через `OpenAiOptions` (новые `AzureApiVersion`, `AzureDeploymentName`).

### 2.4. XAML — ComboBox «Provider» + условная видимость

`Agent.Toolkit.Wpf/Controls/SettingsControl.xaml`, секция API:

1. Добавить `<RowDefinition Height="Auto"/>` в начало и вставить пару
   `TextBlock "Provider"` + `ComboBox x:Name="ProviderCombo"` с
   `SelectedIndex="{Binding Settings.ProviderIndex, Mode=TwoWay}"`,
   `DisplayMemberPath="DisplayName"`.
2. Сдвинуть `Grid.Row` всех строк секции на +1 (механическая правка, проверить сборкой XAML).
3. Добавить две строки для Azure (`Azure API version`, `Azure deployment name`) и привязать
   их `Visibility` к флагу Azure (конвертер bool→Visibility, как уже используется для других
   строк), по умолчанию `Collapsed`.
4. Привязать `Visibility` строки API key к `RequiresApiKey` выбранного провайдера
   (скрывать для Ollama/LM Studio). Реализация — через computed-свойства
   `SettingsModel` (`IsApiKeyVisible`, `IsAzureVisible`), чтобы биндинги остались простыми.

### 2.5. Code-behind

`SettingsControl.xaml.cs`: в конструкторе `ProviderCombo.ItemsSource = AiProviderCatalog.All;`
(`using Agent.Core.Providers;`). Всё остальное — через биндинги к `SettingsModel`.

### 2.6. TDD (red → green → refactor)

1. **Round-trip индекса**: `SettingsModel.ProviderIndex = i` меняет `ProviderId` на
   `AiProviderCatalog.All[i].Id` для всех позиций; пустой/неизвестный `ProviderId`
   нормализуется в `AiProviderId.OpenAi`.
2. **`ApplyProviderDefaults()`**: Ollama → `BaseUrl=http://localhost:11434/v1`,
   `Model=llama3.2`, `ContextWindowSize=8192`; Anthropic → `https://api.anthropic.com`,
   `claude-sonnet-4-5`; Azure → подставляет `AzureApiVersion`/`AzureDeploymentName` из спеков.
3. **Провайдер-специфичные флаги**: `IsApiKeyVisible` = false для Ollama/LM Studio,
   `IsAzureVisible` = true только для Azure.
4. **Azure URL/заголовок**: юнит-тест `OpenAiCompatibleProvider` (через `FakeHttpHandler`):
   для Azure запрос уходит на `.../openai/deployments/{name}/chat/completions?api-version=…`
   и содержит заголовок `api-key`; для обычного OpenAI — `{base}/chat/completions` + `Bearer`.
5. Прогнать существующие провайдерные тесты — не сломать зелёный набор.

### 2.7. Проверка

- `dotnet build Agent.Toolkit.Wpf/Agent.Toolkit.Wpf.csproj`
- `dotnet test Agent.Tests/Agent.Tests.csproj --filter "FullyQualifiedName~Provider"`
- (по желанию) `dotnet test Agent.Tests/Agent.Tests.csproj`

## 3. Критерии готовности

- Во вкладке «API» есть ComboBox «Provider» со всеми провайдерами из `AiProviderCatalog.All`.
- Выбор провайдера меняет `Settings.ProviderId` и применяет дефолты (`BaseUrl`/`Model`/
  `ContextWindowSize`, для Azure — `AzureApiVersion`/`AzureDeploymentName`).
- Строка API key скрыта для Ollama/LM Studio; Azure-поля видны только для Azure.
- Azure-запросы собирают правильный URL/заголовки (покрыто юнит-тестом).
- `ProviderId` и новые поля сохраняются/загружаются через `SettingsData`.
- Все провайдерные юнит-тесты зелёные; сборка тулкита без ошибок.

## 4. Риски и открытые решения

- **R1. Сдвиг строк секции API.** Много `Grid.Row`, легко ошибиться. Решение: аккуратный
  сдвиг + сборка XAML; опечатка проявится кривым лейаутом/`XamlParseException`.
- **R2. Источник списка провайдеров.** Заполняем `ItemsSource` в code-behind (каталог
  статический). Если провайдеры станут настраиваемыми — переносим в DataContext хоста.
- **R3. UX дефолтов.** `ApplyProviderDefaults()` перезаписывает `BaseUrl`/`Model` при смене
  провайдера. **Открытое решение:** оставить как есть (дефолты каталога — источник истины)
  либо не перезаписывать поля, уже заполненные пользователем. Для новых Azure-полей
  предлагаю НЕ затирать пользовательский ввод, а подставлять только при пустом значении.
- **R4. Объём «провайдер-специфики».** Сейчас добавляем только Azure (единственный реальный
  wire-отличие среди 10 провайдеров). Bedrock/Gemini/Vertex/`apiLine`/кастомные заголовки —
  отдельными задачами вместе с новыми протоколами-хендлерами.
- **R5. Точный дефолт `api-version` Azure** — зафиксировать актуальную стабильную версию на
  момент реализации и вынести в `AiProviderSpec.DefaultAzureApiVersion`.

