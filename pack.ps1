# Builds the three shared assemblies, then packs them into the single combined
# AgentToolkit package and publishes it to the local feed (a folder of .nupkg files).
# Usage:  .\pack.ps1
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$feed = Join-Path $root 'feed'
New-Item -ItemType Directory -Force -Path $feed | Out-Null

$projects = @(
    'Agent.Mvvm\Agent.Mvvm.csproj'
    'Agent.Core\Agent.Core.csproj'
    'Agent.Models\Agent.Models.csproj'
)

foreach ($p in $projects) {
    $csproj = Join-Path $root $p
    Write-Host "Building $csproj ..."
    dotnet build $csproj -c Release --nologo
    if ($LASTEXITCODE -ne 0) { throw "dotnet build failed for $p" }
}

Write-Host "Packing AgentToolkit ..."
$toolkit = Join-Path $root 'AgentToolkit\AgentToolkit.csproj'
dotnet pack $toolkit -c Release -o $feed --nologo
if ($LASTEXITCODE -ne 0) { throw "dotnet pack failed for AgentToolkit" }

Write-Host "Package published to $feed"