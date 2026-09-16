# DEV-001 Build Validation Report

- Date: 2026-09-16
- Branch: `fix/dev-001-build-validation`
- Base: `origin/main` at `20ec16506b037468a71e61e871c03f05b5c33bac`
- Scope: compiler/analyzer repair and validation completion only

## Environment

| Item | Result |
|---|---|
| OS | Microsoft Windows 10.0.26200 |
| PowerShell | 7.6.5, Core, Win32NT |
| Git | 2.55.0.windows.3 |
| Configured SDK | .NET 10.0.100 in `global.json` |
| Installed .NET SDK | Not available: `dotnet` is not recognized |
| WPF target | `net10.0-windows`, with `EnableWindowsTargeting=true` |

## Historical CI evidence

The latest run before this fix was [GitHub Actions run 35082941357](https://github.com/LAlongago/ScheduleAssistant/actions/runs/35082941357), for commit `20ec16506b037468a71e61e871c03f05b5c33bac`. It failed during Release build before tests started:

- Application `AssemblyMarker.cs`: `System.Reflection.Assembly` returned from a `System.Type` property;
- the same type mismatch existed in Infrastructure; and
- `AssemblySmokeTests.DomainAssembly_ShouldBeLoadable` raised CA1707.

## Required command results

Commands were invoked from the repository root using PowerShell 7. The missing SDK prevented every .NET command from starting.

| Command | Exit code | Result |
|---|---:|---|
| `dotnet --info` | 1 | Blocked: `dotnet` command not found |
| `dotnet restore .\ScheduleAssistant.sln` | 1 | Blocked before restore |
| `dotnet build .\ScheduleAssistant.sln -c Release --no-restore` | 1 | Blocked before build |
| `dotnet test .\ScheduleAssistant.sln -c Release --no-build --no-restore` | 1 | Test host did not start; 0 discovered / 0 passed / 0 failed |
| `dotnet run --project .\src\ScheduleAssistant.Presentation\ScheduleAssistant.Presentation.csproj -c Release --no-build` | 1 | WPF process did not start |

These are blocked results, not successful build/test results.

## Static validation completed

- Project and configuration XML plus `global.json` parsed successfully.
- The four production project references still match the specified inward dependency direction.
- The targeted Assembly marker properties now return `System.Reflection.Assembly`.
- CA1707 suppression exists only under `tests/.editorconfig`; no global suppression was added.
- No database, migration, repository, business entity, ViewModel, or Windows integration implementation was added.
- `git diff --check` passed for the complete source, handoff, and report patch before the final documentation commit.

## Test statistics and WPF result

No test assembly was built or discovered because the .NET SDK is absent. No WPF executable was built or run; empty-window display, normal close, and process exit remain **待 Windows + .NET SDK 验证**.

## Required follow-up

On a Windows machine with the SDK selected by `global.json`, run:

```powershell
dotnet --info
dotnet restore .\ScheduleAssistant.sln
dotnet build .\ScheduleAssistant.sln -c Release --no-restore
dotnet test .\ScheduleAssistant.sln -c Release --no-build --no-restore
dotnet run --project .\src\ScheduleAssistant.Presentation\ScheduleAssistant.Presentation.csproj -c Release --no-build
```

Record the SDK version, exit codes, test totals, and WPF window/exit observation here before declaring DEV-001 a fully buildable, testable, Windows-startable baseline or beginning SPIKE-001/SPIKE-002 acceptance.
