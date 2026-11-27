# Agent.ChatHost.Extended

Расширенный пример WPF-хоста на базе `Agent.ChatHost` (тонкое приложение). Единственное
текущее расширение — настоящий логгер на NLog, внедрённый через `IAgentLogger`.

## Отличие от тонкого хоста

- `Agent.ChatHost` — подключает `AgentChatControl` с дефолтами и не передаёт логгер
  (внутри используется no-op fallback `NoOpLogger`).
- `Agent.ChatHost.Extended` — создаёт `NLogAgentLogger : IAgentLogger` и передаёт его
  в `Chat.Logger`, поэтому `ChatModel`, `SettingsModel` и все хранилища пишут в NLog.
- `WpfLog` (внутренний логгер общих WPF-контролов, работающий через `System.Diagnostics.Trace`)
  перенаправляется в тот же NLog-пайплайн через `NLogTraceListener`.

## Куда пишутся логи

Основное место (для чтения агентом в Plan mode):

```
<repo-root>\logs\agent-chat-host-extended.log
```

Корень репозитория определяется поиском `AgentToolkit.slnx` вверх от каталога сборки.
Решение (корень репозитория) находится внутри read-scope режима Plan, поэтому агент может
прочитать лог напрямую:

- `read_files` по пути `logs/agent-chat-host-extended.log`;
- либо инструмент `analyze_logs` с тем же путём.

Если маркер решения не найден (опубликованная/установленная копия), лог пишется в fallback:

```
%LocalAppData%\AgentChatHost.Extended\logs\agent-chat-host-extended.log
```

Папка `logs/` уже входит в `.gitignore`, так что файлы логов не загрязняют `git status`.

## Запуск

```powershell
dotnet run --project Agent.ChatHost.Extended/Agent.ChatHost.Extended.csproj
```
