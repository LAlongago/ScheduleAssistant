# ScheduleAssistant Engineering Instructions

## Baseline and task discipline

`ScheduleAssistant_V1_Development_Specification.md` is the only product, architecture, data, and collaboration baseline. Before editing code, read it completely, then read `README.md` and the ADRs relevant to the task. Every change must name a task ID, state its allowed files/directories, and state what is intentionally out of scope.

Do not expand a task because a nearby improvement looks convenient. If the specification is contradictory or a public contract must change outside the task, stop and record the conflict for the integration owner instead of choosing an interpretation silently.

DEV-001 establishes the skeleton only. It must not grow business entities, use cases, ViewModels, SQL, migrations, database tables, real notification/tray/startup adapters, desktop embedding, or UI styling. Later tasks must not use DEV-001 placeholders as an excuse to bypass their declared dependencies.

## Architecture boundaries

The dependency direction is inward:

```text
Presentation -> Application -> Domain
Presentation -> Infrastructure -> Application -> Domain
Infrastructure -> Domain
```

The Presentation-to-Infrastructure reference is a composition-root exception only. ViewModels and Views must not call concrete repositories, SQLite, the filesystem, Registry, Win32, or notification APIs. Domain must remain free of WPF, database, dependency-injection, filesystem, and system-clock dependencies. Application owns use-case orchestration and ports; Infrastructure owns replaceable adapters.

There is one composition root in the WPF Presentation project. Avoid service locators, global mutable singletons, static database access, and parallel models that duplicate a domain contract.

## Code quality

- Nullable reference types, analyzers, deterministic builds, and warnings-as-errors are enabled centrally.
- Keep package versions in `Directory.Packages.props`; project files must not specify ad-hoc versions.
- Prefer file-scoped namespaces, explicit async cancellation, and `Task`-returning APIs. `async void` is reserved for unavoidable UI event handlers and must catch/report failures.
- Public interfaces and complex rules require XML documentation or a nearby design explanation.
- Keep production files cohesive and normally below 500 lines. Do not reformat unrelated files.
- Use parameterized SQL, short-lived SQLite connections, transactions, UTC for absolute timestamps, and `TimeProvider` for time-dependent behavior when the persistence/domain tasks begin.
- Do not log task content, attachment contents, or unnecessary private data. Do not commit secrets or machine-specific absolute paths.

## Tests and naming

Use xUnit and behavior-oriented names such as `Method_WhenCondition_ShouldExpectedResult`. Add tests with every new rule. Domain/Application tests should be deterministic and use injected time; Infrastructure tests should use real temporary SQLite files rather than replacing SQL with mocks. Windows capabilities belong behind adapters and Windows-specific smoke tests.

The architecture test project is part of the normal test run. A new project reference must be justified by the specification and reflected in the dependency-direction test.

## PowerShell 7 commands

Run commands from the repository root with PowerShell 7:

```powershell
dotnet restore .\ScheduleAssistant.sln
dotnet build .\ScheduleAssistant.sln -c Release --no-restore
dotnet test .\ScheduleAssistant.sln -c Release --no-build --no-restore
```

Use `rg`/`rg --files` for searches. Do not use Bash or `cmd.exe` command syntax in project instructions. Do not commit `bin/`, `obj/`, `.vs/`, `TestResults/`, or other generated output.

## Handoff requirements

Every task must write `docs/handoffs/DEV-xxx.md` before delivery with:

- completed work and key design choices;
- changed files and database/interface impact;
- exact verification commands and results;
- incomplete items, known issues, and follow-up dependencies; and
- the final commit SHA when Git metadata exists.

The current workspace may be an uninitialized checkout. Do not invent a commit SHA; write `Not available (Git repository metadata is absent)` when applicable.
