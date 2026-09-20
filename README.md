# ScheduleAssistant

ScheduleAssistant is a local-first Windows desktop schedule and deadline assistant. The product and architecture baseline is [`ScheduleAssistant_V1_Development_Specification.md`](ScheduleAssistant_V1_Development_Specification.md); it is the single source of truth for all later task packages.

## Current implementation baseline

The repository contains the layered engineering baseline and the integrated DEV-041/DEV-042 core UI:

- layered Domain, Application, Infrastructure, and WPF Presentation projects;
- xUnit test projects, including dependency-direction tests;
- centralized SDK, compiler, analyzer, and NuGet package-version settings;
- a WPF startup window wired through one Host/DI composition root;
- ADR and task-handoff templates; and
- a Windows GitHub Actions workflow for restore, Release build, and tests.

The Presentation composition root resolves one `TaskUseCases` instance as both `ITaskUseCases` and
`ITaskQueries`, and one `InProcessEventBus` as `IApplicationEventPublisher`. SQLite initialization is
gated once before query pages run.

## Current running interface (INTEGRATION-008 + DEV-043 + DEV-044 + DEV-045)

The WPF application starts with a single three-part shell: a fixed left navigation rail, a header with
the current page title/date controls/search/new-task entry points, and the active page content region.
Today and Upcoming Deadlines use real SQLite-backed queries and shared loading/empty/error/ready states.

Available now:

- create and edit ordinary tasks, including validation, plan times, Deadline, reminder choice, location,
  details, materials, notes, dirty-form cancellation, optimistic-conflict reload, and explicit DST handling;
- a simplified-Chinese Windows font stack led by Microsoft YaHei UI, flat selection controls, four-digit
  time pickers, equal-width editor sections, and collapsed optional Deadline/content sections with independent
  expansion; validation feedback appears only after an invalid save attempt, and discard confirmation follows
  the application theme without local-timezone or optional-expansion hint text. Editor scrolling stays available
  without a visible scrollbar changing the two-column layout width;
- immutable migration `002_seed_default_categories.sql` with six idempotent default categories;
- Today grouping for overdue, planned-past, today plans, today-only Deadlines, and completed tasks;
- cached Deadline countdowns, upcoming-page range filtering capped at 7 days (24 hours/3 days/7 days), task completion and
  cancellation, and event-driven local refresh of Today and Upcoming Deadlines.

Still intentionally placeholder-only:

- Week and Month calendar pages;
- All Tasks and search filtering;
- attachment import/open/remove and recurrence editing;
- notifications, tray, startup, desktop mode, backup, and other later Windows integration work.

The placeholder pages do not query or write fake task data. Light and alternate Dark resource dictionaries
centralize color, typography, spacing, radius, border, focus, and state tokens.

## INTEGRATION-001 state

The repository also contains two isolated, non-production prototypes under `spikes/`:

- `spikes/SPIKE-001/` — Windows App SDK local-notification and activation experiment;
- `spikes/SPIKE-002/` — WorkerW/Progman desktop-host and `WidgetFallback` experiment.

Neither prototype is part of `ScheduleAssistant.sln`, the formal `src/`/`tests/` project graph, or the root CI build. Build them separately using the commands in their README files. Their historical build evidence and unverified Windows behavior are recorded in `docs/test-reports/` and must not be treated as formal adapter acceptance. The DEV-010 semantic draft is [`docs/specifications/DEV-010-domain-contracts.md`](docs/specifications/DEV-010-domain-contracts.md); it does not authorize implementation by itself.

## Prerequisites

- .NET SDK 10.0.100 or a compatible later feature band selected by `global.json`;
- Windows 10/11 x64 to run the WPF application and Windows-specific smoke checks;
- PowerShell 7 for the repository commands below.

The WPF project sets `EnableWindowsTargeting` so a compatible SDK can perform a compile on a non-Windows build agent. Running the application, testing Windows App SDK notifications, tray behavior, Registry startup, single-instance IPC, and desktop-host behavior still requires Windows.

If the SDK is installed in a user-local directory instead of the system installation location, configure it for the current PowerShell session before running the commands below. This does not modify system `PATH`, the registry, or other SDK installations:

```powershell
$dotnetRoot = 'E:\Dev\Tools\ScheduleAssistantDotnet'
$env:PATH = "$dotnetRoot;$env:PATH"
$env:DOTNET_ROOT = $dotnetRoot
$env:DOTNET_CLI_HOME = Join-Path $env:TEMP 'ScheduleAssistantDotnetCliHome'
$env:NUGET_PACKAGES = Join-Path $env:TEMP 'ScheduleAssistantNuGetPackages'
$env:APPDATA = Join-Path $env:TEMP 'ScheduleAssistantAppData'
Set-Location 'E:\Dev\Personal\Todo_list'
dotnet --info
```

The `DOTNET_CLI_HOME`, `NUGET_PACKAGES`, and `APPDATA` overrides are useful on locked-down Windows profiles where the default user tool or NuGet directories are not writable. Omit them when the normal user profile is accessible.

## Build and test

Run from the repository root:

```powershell
dotnet restore .\ScheduleAssistant.sln
dotnet build .\ScheduleAssistant.sln -c Release --no-restore
dotnet test .\ScheduleAssistant.sln -c Release --no-build --no-restore
```

To start the WPF application on Windows:

```powershell
dotnet run --project .\src\ScheduleAssistant.Presentation\ScheduleAssistant.Presentation.csproj -c Release
```

On first host startup the application creates its local SQLite data directory and applies the immutable
migrations. The managed database contains task/category/query foundations; attachments, recurrence and
Windows integration remain outside this integrated core UI.

## Repository layout

```text
src/
  ScheduleAssistant.Domain/          Pure domain boundary
  ScheduleAssistant.Application/    Use cases and ports boundary
  ScheduleAssistant.Infrastructure/ SQLite/Windows/filesystem adapters later
  ScheduleAssistant.Presentation/    WPF/MVVM composition root and views
tests/
  ScheduleAssistant.Domain.Tests/
  ScheduleAssistant.Application.Tests/
  ScheduleAssistant.Infrastructure.Tests/
  ScheduleAssistant.Architecture.Tests/
docs/
  adr/                               Architecture decision records
  handoffs/                          DEV task delivery records
  specifications/                    Specification index
  test-reports/                      Reproducible verification output
```

## Dependency direction

Production project references follow the specification:

```text
Presentation -> Application -> Domain
Presentation -> Infrastructure -> Application -> Domain
Infrastructure -> Domain
```

The Presentation-to-Infrastructure edge is restricted to composition registration. ViewModels must depend on Application ports, never on concrete repositories or Windows APIs. `ScheduleAssistant.Architecture.Tests` guards the project-level assembly direction.

Package versions are declared only in [`Directory.Packages.props`](Directory.Packages.props). Do not add a version to an individual project file or upgrade a package as a side effect of another task.

## Collaboration

Before changing code, read the specification, this README, [`AGENTS.md`](AGENTS.md), and the relevant ADR. Work on one numbered task at a time, keep changes inside its declared scope, and record the result in `docs/handoffs/DEV-xxx.md`. Use the task input and handoff templates as the minimum contract for future Codex sessions.
