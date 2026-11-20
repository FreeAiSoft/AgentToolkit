# План: исключить ask_question у субагента (spawn_agent) на уровне тулкита

## Проблема

Субагент (`spawn_agent`) вместо обычного текстового ответа может выводить
интерактивную карточку-вопрос (`ask_question`). Нужно, чтобы субагент писал
ответ просто в чат.

## Причина

`SubAgentRunner.RunAsync` создаёт вложенный `AgentRunner` и передаёт ему тот же
`AgentToolRegistry`, что и у родителя. Поскольку в реестре зарегистрирован
`ask_question`, субагент может его вызвать и показать вопрос.

## Решение (уровень тулкита)

1. Добавлен метод `AgentToolRegistry.Without(string toolName)` — возвращает копию
   реестра без указанного инструмента (регистронезависимо) с сохранением
   disabled-флагов.
2. `SubAgentRunner.RunAsync` перед созданием `AgentRunner` строит
   `subRegistry = registry.Without("ask_question")` и передаёт его вложенному
   циклу вместо исходного реестра.

Таким образом субагент физически не может вызвать `ask_question` (инструмент
отсутствует в реестре и не попадает в tool definitions).

## Изменённые файлы

- `Agent.Core/AgentTools/AgentToolRegistry.cs` — метод `Without(...)`.
- `Agent.Core/AgentTools/SubAgentRunner.cs` — фильтрация `ask_question`.
- `Agent.Tests/SubAgentRunnerTests.cs` — тест `RunAsync_ExcludesAskQuestionFromSubAgentTools`.

## Эффект

- Основной агент по-прежнему может задавать уточняющие вопросы через `ask_question`.
- Субагент не получает `ask_question` и возвращает обычный текстовый отчёт,
  который попадает в чат как результат `spawn_agent`.
- Остальные инструменты, режим и права у субагента сохраняются.

## Проверка

1. Собрать решение (`dotnet build Agent.Tests -c Release`).
2. Запустить тесты субагента:
   `dotnet test Agent.Tests -c Release --filter FullyQualifiedName~SubAgentRunnerTests`.

## Статус

- [x] Правка внесена в `AgentToolRegistry.cs` и `SubAgentRunner.cs`.
- [x] Добавлен тест, исключающий `ask_question` из инструментов субагента.
- [x] Сборка `Agent.Tests` (Release) — `Build succeeded`.
- [x] Тесты `SubAgentRunnerTests` — `Test run: PASSED (4 passed)`.
