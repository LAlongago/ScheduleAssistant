[CmdletBinding()]
param(
    [ValidateSet('framework-dependent', 'self-contained')]
    [string]$Mode = 'framework-dependent',

    [string]$OutputDirectory
)

$ErrorActionPreference = 'Stop'
$projectPath = (Resolve-Path (Join-Path $PSScriptRoot '..\ScheduleAssistant.SPIKE001.Notifications.csproj')).Path

if ([string]::IsNullOrWhiteSpace($OutputDirectory)) {
    $OutputDirectory = Join-Path (Split-Path $projectPath -Parent) "artifacts\publish\$Mode"
}

$OutputDirectory = [System.IO.Path]::GetFullPath($OutputDirectory)
New-Item -ItemType Directory -Force $OutputDirectory | Out-Null

$selfContained = if ($Mode -eq 'self-contained') { 'true' } else { 'false' }

Write-Host "Publishing $Mode to $OutputDirectory"
dotnet publish $projectPath `
    -c Release `
    -r win-x64 `
    --self-contained $selfContained `
    "-p:WindowsAppSDKSelfContained=$selfContained" `
    -p:Platform=x64 `
    --nologo `
    -o $OutputDirectory
if ($LASTEXITCODE -ne 0) {
    throw "dotnet publish failed with exit code $LASTEXITCODE."
}

$size = (Get-ChildItem -LiteralPath $OutputDirectory -File -Recurse |
    Measure-Object -Property Length -Sum).Sum
Write-Host ("Published bytes: {0:N0}" -f $size)
Write-Host "Executable: $(Join-Path $OutputDirectory 'ScheduleAssistant.SPIKE001.Notifications.exe')"
