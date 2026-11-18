# PackTool

Консольная утилита (net8.0), которая собирает весь тулкит AgentShared и пакует его
в локальный NuGet-фид `feed\`.

## Что делает

1. Читает версии из `feed\*.nupkg` и берёт **максимальную**.
2. Инкрементирует хвостовой компонент:
   - `1.7.2` → `1.7.2.1`
   - `1.4.5.6` → `1.4.5.7`
   - `1.16.2.0` → `1.16.2.1`
3. Пересобирает 4 проекта в `Release`:
   - `Agent.Mvvm`
   - `Agent.Core`
   - `Agent.Models`
   - `Agent.Toolkit.Wpf`
4. Пакует два пакета в `feed\`:
   - `AgentToolkit` (`Agent.Mvvm` + `Agent.Core` + `Agent.Models`)
   - `AgentToolkit.Wpf` (`Agent.Toolkit.Wpf`)

## Как запускается

Автоматически — через событие сборки: при **Release**-сборке проекта `Agent.Core`
выполняется post-build-цель `PackToolkitToFeed`, которая запускает `PackTool`.

Вручную:

```powershell
dotnet run --project PackTool/PackTool.csproj -c Release -- "C:\Users\Maxxx\source\repos\AgentShared"
```

Аргумент после `--` — путь к корню репозитория (по умолчанию — текущая папка).

## Нюанс версии

Счётчик стартует с `1`, а не с `0`: NuGet схлопывает нулевой 4-й компонент
(`1.7.2.0` хранится как `1.7.2`), поэтому трёхчастная версия инкрементируется
сразу в `1.7.2.1`.

## Защита от рекурсии

При внутренней сборке `Agent.Core` утилита передаёт `-p:AgentSkipReleasePack=true`,
чтобы post-build-событие не срабатывало повторно. То же свойство используют
`pack.ps1` и CI.
