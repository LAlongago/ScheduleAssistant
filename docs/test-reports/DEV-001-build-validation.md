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

## Current round: SDK provisioning and real validation (2026-09-16)

The historical missing-SDK results above are retained. This section records the first
successful local SDK setup and the subsequent real validation run.

### Task scope and baseline

- Task: DEV-001 environment completion and real acceptance.
- Allowed changes: DEV-001 startup/build configuration and the handoff/test-report/README
  documentation. The only source change in this round is the Presentation composition-root
  startup file.
- Intentionally out of scope: business entities/use cases, database/schema/migrations,
  notifications, tray/startup adapters, IPC, desktop embedding, UI styling, SPIKE-001,
  SPIKE-002, push, PR creation, and merging to `main`.
- Source baseline before this round: `45268f8b1e628fbec7f916ab0ef28c4abd137be0` on
  `fix/dev-001-build-validation`, with the prior repair commit `9521ce2` present.

### Environment

| Item | Result |
|---|---|
| OS | Windows 10 Pro 25H2, `10.0.26200` (build `26200`, UBR `9445`) |
| Architecture | Windows x64; process architecture `AMD64`; RID `win-x64` |
| PowerShell | 7.6.5, Core |
| .NET SDK path | `E:\Dev\Tools\ScheduleAssistantDotnet\dotnet.exe` |
| .NET SDK | `10.0.100`, selected from `global.json`; MSBuild `18.0.2` |
| Windows Desktop Runtime | `Microsoft.WindowsDesktop.App 10.0.0` at the same local SDK root |
| Disk space before install | C: 267.69 GB free; E: 3724.09 GB free |

The target directory did not exist before installation. The SDK was installed non-admin and
locally with Microsoft's official `dotnet-install.ps1` from
`https://dot.net/v1/dotnet-install.ps1` (redirected by Microsoft to
`builds.dotnet.microsoft.com`) using `-Version 10.0.100 -Architecture x64
-InstallDir E:\Dev\Tools\ScheduleAssistantDotnet -NoPath`. No system `PATH`, registry,
global Git configuration, or other SDK installation was changed. The downloaded script
SHA-256 was `E8B873E18A81E5C4CD8AB69D84DAC8FEAD291D50B3C44633CD7FDDAD709A13D6`.

The first two post-install restore attempts were blocked by this locked-down profile: the
SDK first could not create `C:\Users\ceo\.dotnet`, then NuGet could not read
`C:\Users\ceo\AppData\Roaming\NuGet\NuGet.Config`. The successful run set
`PATH`, `DOTNET_CLI_HOME`, `NUGET_PACKAGES`, and `APPDATA` only in the current PowerShell
process to writable temporary directories; the repository files and system settings were
not changed.

### SDK evidence

From the repository root, with the local SDK root prepended to the current process `PATH`:

- `dotnet --info`: exit code 0; resolved `E:\Dev\Tools\ScheduleAssistantDotnet\dotnet.exe`,
  SDK `10.0.100`, base path `...\sdk\10.0.100\`, and `global.json` at the repository root.
- `dotnet --list-sdks`: exit code 0; exactly `10.0.100` at the local SDK root.
- `dotnet --list-runtimes`: exit code 0; `Microsoft.NETCore.App 10.0.0`,
  `Microsoft.AspNetCore.App 10.0.0`, and `Microsoft.WindowsDesktop.App 10.0.0`.

### Real command results

The following commands were run from `E:\Dev\Personal\Todo_list`. Each command started the
external .NET program; the environment variables described above were set only for that
PowerShell process.

| Command | Exit code | Result |
|---|---:|---|
| `dotnet restore .\ScheduleAssistant.sln` | 0 | All 8 projects restored or were already current. |
| `dotnet build .\ScheduleAssistant.sln -c Release --no-restore` | 0 | All 8 projects built; 0 warnings, 0 errors. |
| `dotnet test .\ScheduleAssistant.sln -c Release --no-build --no-restore --logger "trx" --results-directory .\TestResults` | 0 | Four test hosts started and all completed successfully; 7 executed, 7 passed, 0 failed, 0 skipped. |

Final TRX statistics from the 19:02:24 run:

| Test project | Total | Executed | Passed | Failed | Skipped | TRX outcome |
|---|---:|---:|---:|---:|---:|---|
| `ScheduleAssistant.Domain.Tests` | 1 | 1 | 1 | 0 | 0 | Passed |
| `ScheduleAssistant.Application.Tests` | 1 | 1 | 1 | 0 | 0 | Passed |
| `ScheduleAssistant.Infrastructure.Tests` | 1 | 1 | 1 | 0 | 0 | Passed |
| `ScheduleAssistant.Architecture.Tests` | 4 | 4 | 4 | 0 | 0 | Passed |
| **Total** | **7** | **7** | **7** | **0** | **0** | **Passed** |

The TRX files were generated under `TestResults` and remain ignored by Git, together with
`bin`/`obj`; none are part of the source change.

### New source fix exposed by real validation

The first real Release build failed with one error in `App.xaml.cs`: the project namespace
`ScheduleAssistant.Application` shadowed the WPF `Application` base type. The composition
root now explicitly derives from `System.Windows.Application`.

The first full test run then started all four hosts but failed one architecture assertion:
Release metadata did not retain the direct Application reference because the prior marker
anchor was only a discarded `typeof` expression. The composition root now reads
`ApplicationAssemblyMarker.DomainAssembly` in a guarded boundary check. The second complete
restore/build/test run passed with no warnings or errors. These are DEV-001 build/architecture
validation fixes only; no business behavior was added.

### WPF runtime acceptance

Command started from the repository root:

```powershell
dotnet run --project .\src\ScheduleAssistant.Presentation\ScheduleAssistant.Presentation.csproj -c Release --no-build
```

The command started the external `ScheduleAssistant.exe` process. Process inspection showed
the expected executable path, a non-zero main-window handle, and `MainWindowTitle` equal to
`ScheduleAssistant`; the attached `dotnet run` session remains active while the window is
open. However, the available native-window observation channel was not configured (`sky`
reported `Trusted RPC service is not configured`) and the available `cua` surface exposed
only browser tabs. No unsafe PowerShell UI automation or synthetic screenshot was used.

Therefore the five visual/lifecycle checks remain **待人工验证**:

| Check | Result | Verification method |
|---|---|---|
| Main window visibly displays | Pending manual verification | Process metadata only; direct desktop observation unavailable |
| No startup exception or error prompt | Pending manual verification | Must be confirmed on the visible desktop |
| Window can close normally | Pending manual verification | Must be performed by the user |
| Application process exits after close | Pending manual verification | Must be checked after the manual close |
| No residual background application process | Pending manual verification | Must be checked after the manual close |

### Reproduction in a new PowerShell 7 session

```powershell
$dotnetRoot = 'E:\Dev\Tools\ScheduleAssistantDotnet'
$env:PATH = "$dotnetRoot;$env:PATH"
$env:DOTNET_ROOT = $dotnetRoot
$env:DOTNET_CLI_HOME = Join-Path $env:TEMP 'ScheduleAssistantDotnetCliHome'
$env:NUGET_PACKAGES = Join-Path $env:TEMP 'ScheduleAssistantNuGetPackages'
$env:APPDATA = Join-Path $env:TEMP 'ScheduleAssistantAppData'
Set-Location 'E:\Dev\Personal\Todo_list'
dotnet --info
dotnet restore .\ScheduleAssistant.sln
dotnet build .\ScheduleAssistant.sln -c Release --no-restore
dotnet test .\ScheduleAssistant.sln -c Release --no-build --no-restore --logger "trx" --results-directory .\TestResults
dotnet run --project .\src\ScheduleAssistant.Presentation\ScheduleAssistant.Presentation.csproj -c Release --no-build
```

### Current conclusion

- Local environment: **通过** for SDK installation, SDK selection, and Desktop Runtime availability.
- Release build: **通过**.
- Full test run: **通过**; all four test projects started their test hosts.
- WPF run: **待人工验证** until the five desktop checks above are observed and reported.
- Remote CI: **待推送后验证**; this branch has not been pushed and no local result claims GitHub CI.
- Validated source/documentation commit SHA: `1f4766a7caec7b66e90ae65fac7d09190ba0d94c` (`DEV-001: validate local SDK and WPF build`).
- SPIKE-001/SPIKE-002 and any later task must wait for WPF manual acceptance and subsequent
  push/CI review; this round does not authorize push, PR, merge, or reset.

## Manual WPF acceptance feedback (2026-09-17)

The pending state above is retained as the earlier observation-channel limitation. The user
then performed the five checks on the real Windows desktop and reported all of them passed;
the `dotnet run` command returned exit code `0`.

| Check | Result | Evidence/method |
|---|---|---|
| Main window visibly displays | Passed | User observed the running `ScheduleAssistant` window |
| No startup exception or error prompt | Passed | User observed the desktop during startup |
| Window can close normally | Passed | User closed the window normally |
| Application process exits after close | Passed | User reported exit code `0`; the run session ended |
| No residual background application process | Passed | User checked after close; an independent `Get-Process -Name ScheduleAssistant` check returned no process |

### Resolved conclusion

- Environment preparation: **Passed**.
- Release build: **Passed**.
- Full test run: **Passed**, 4 test hosts and 7/7 tests passed.
- WPF runtime: **Passed**, all five manual checks passed and `dotnet run` exited `0`.
- Remote CI: **待推送后验证**; the branch remains unpushed and no GitHub CI result is claimed.
- The local DEV-001 validation gate is complete. Branch push and CI verification may proceed
  when separately authorized; SPIKE-001/SPIKE-002 still wait for CI green and merge to `main`.

## Remote delivery attempt (2026-09-17)

The user explicitly authorized pushing to `https://github.com/LAlongago/ScheduleAssistant.git`
and merging `main` if CI is green. The local branch was clean at
`e0ac2c560e14cbfbdd7ac6b5ee146fbc0887bc86`.

| Operation | Exit code | Result |
|---|---:|---|
| `git push --set-upstream origin fix/dev-001-build-validation` (attempt 1) | 1 | HTTPS connection reset while accessing GitHub; no remote update observed. |
| Same push (attempt 2) | 1 | Could not connect to `github.com:443` after approximately 21 seconds; no remote update observed. |
| Read-only `curl.exe --fail --silent --show-error --location --head https://github.com/LAlongago/ScheduleAssistant` | 1 | Could not connect to `github.com:443` after approximately 21 seconds. |

No authentication prompt, ref rejection, or CI run was reached. The branch remains local and
unpushed; CI and merge have not started. The blocker is outbound HTTPS connectivity to GitHub,
not a source/build/test failure. Once GitHub HTTPS access is restored, rerun the push, inspect
the resulting workflow, and continue to merge only if every required check is green.
