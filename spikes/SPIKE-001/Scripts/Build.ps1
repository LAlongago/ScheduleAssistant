[CmdletBinding()]
param(
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Release'
)

$ErrorActionPreference = 'Stop'
$projectPath = (Resolve-Path (Join-Path $PSScriptRoot '..\ScheduleAssistant.SPIKE001.Notifications.csproj')).Path

Write-Host "Restoring $projectPath"
dotnet restore $projectPath --nologo
if ($LASTEXITCODE -ne 0) {
    throw "dotnet restore failed with exit code $LASTEXITCODE."
}

Write-Host "Building $Configuration x64"
dotnet build $projectPath -c $Configuration -p:Platform=x64 --no-restore --nologo
if ($LASTEXITCODE -ne 0) {
    throw "dotnet build failed with exit code $LASTEXITCODE."
}
