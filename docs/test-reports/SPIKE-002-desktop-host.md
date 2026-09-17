# SPIKE-002 Windows 桌面宿主验证报告

- Date: 2026-09-17
- Task: `SPIKE-002`
- Branch: `spike/spike-002-desktop-host`
- Worktree: `E:\Dev\Personal\Todo_list-spike-002-desktop-host`
- Baseline SHA: `2446979ad25be69af552c673c5458e9b48139320`
- Scope: isolated WPF prototype under `spikes/SPIKE-002/`

## Baseline gate

DEV-001 已在当前 `main` 合入。`docs/handoffs/DEV-001.md` 记录了合入提交 `3cfbd051e026b667488ab898ada34a0a81569e2b`、PR CI `35176861980` 和合入后 main CI `35177073922` 均为 success，并记录了真实 Windows WPF 启动、窗口显示、关闭、进程退出和无残留进程五项人工验收通过。

本轮另行复跑基线：

| Command | Result |
|---|---|
| `dotnet restore .\ScheduleAssistant.sln` | Passed |
| `dotnet build .\ScheduleAssistant.sln -c Release --no-restore` | Passed，8 个项目，0 warning / 0 error |
| `dotnet test .\ScheduleAssistant.sln -c Release --no-build --no-restore` | Passed，4 个宿主，7/7 passed |

SPIKE-002 没有修改正式 `src/`、`tests/`、根解决方案、包版本或 CI。

## Environment

| Item | Observed value |
|---|---|
| .NET SDK | 10.0.100，MSBuild 18.0.2 |
| Windows runtime | `Microsoft.WindowsDesktop.App 10.0.0` |
| OS version | `10.0.26200`，x64；`Win32_OperatingSystem` caption 报告 Windows 11 Professional |
| Registry OS label | `ProductName=Windows 10 Pro`, `DisplayVersion=25H2`, `CurrentBuild=26200`, `UBR=9457`；系统命名存在差异，报告以 build 和运行时能力为准 |
| PowerShell | 7.6.5 Core（DEV-001 环境记录） |
| System DPI | 96 DPI（100%） |
| Active display topology | `EnumDisplayMonitors` 实际枚举 1 个活动显示器：`DISPLAY1`, 3440×1440；工作区 3440×1392 |
| WMI monitor inventory | 2 条记录，其中 1 条为默认/无尺寸记录；不作为活动显示器数量依据 |

Windows 10 和第二个实际显示器本轮未获得独立设备条件，标记为未验证。

## Prototype implementation

- `ScheduleAssistant.SPIKE002.csproj`：独立 `net10.0-windows` WPF 项目，不加入根解决方案。
- `MainWindow`/`DashboardViewModel`：日期、星期、4 条模拟任务、Deadline 和勾选完成状态。
- `DesktopHostService`：集中 WorkerW/Progman 发现、Win32 父窗口切换、样式恢复、有限退避、shell/display 消息恢复和 Widget 可见区修正。
- `EmbeddedPendingInteraction`：附着成功但等待真实复选框变化的中间诊断状态；避免虚报 `Embedded`。
- Widget：无边框、可拖动、非 `Topmost`，失败时保持可见。

## Reproducible commands

```powershell
$dotnetRoot = 'E:\Dev\Tools\ScheduleAssistantDotnet'
$env:PATH = "$dotnetRoot;$env:PATH"
$env:DOTNET_ROOT = $dotnetRoot
$env:DOTNET_CLI_HOME = Join-Path $env:TEMP 'ScheduleAssistantDotnetCliHome'
$env:NUGET_PACKAGES = Join-Path $env:TEMP 'ScheduleAssistantNuGetPackages'
$env:APPDATA = Join-Path $env:TEMP 'ScheduleAssistantAppData'
Set-Location 'E:\Dev\Personal\Todo_list-spike-002-desktop-host'
dotnet restore .\spikes\SPIKE-002\ScheduleAssistant.SPIKE002.csproj
dotnet build .\spikes\SPIKE-002\ScheduleAssistant.SPIKE002.csproj -c Release --no-restore
dotnet run --project .\spikes\SPIKE-002\ScheduleAssistant.SPIKE002.csproj -c Release --no-build
```

Observed build result: **Passed**, 0 warning / 0 error. The final executable launch smoke check returned a running, responsive process with a non-zero main-window handle and title `SPIKE-002 Desktop Host`. The native computer-use observation channel was unavailable in this session (`cua` returned `nodeRepl.fetch request failed` after retry and reset), so process metadata is not treated as visual or interaction evidence.

Final Release idle sampling with no interaction completed for 300.5 seconds: average/peak CPU **0.000%** at the reported 0.001% precision, average/peak private memory **82.4/82.6 MB**, and average/peak working set **132.4/132.6 MB**. Private memory is below the specification's 150 MB target; working set is reported separately and is not substituted for private memory.

## Scenario matrix

| Scenario | Status | Evidence / remaining action |
|---|---|---|
| WorkerW/Progman discovery and bounded attach | Not verified | Code path builds; requires visible Windows run and host diagnostic observation |
| Embedded checkbox changes task state | Not verified | Must click a checkbox after `Embedded · 待交互验证`; only then can `Embedded` be recorded |
| Desktop icons remain clickable | Not verified | Place widget over/near an icon and click both targets manually |
| Ordinary foreground app is not covered | Design pass; runtime unverified | `Topmost=False`; confirm visually with another foreground app |
| Win+D and window switching | Not verified | User must press `Win+D` and switch windows manually |
| Detach, normal close, no residual prototype window | Not verified | Native UI close unavailable to this agent; manual close and process check required |
| Explorer restart reattach or WidgetFallback | Not verified | User must save work and manually restart Explorer; no automatic restart is performed |
| Resolution/DPI changes recover visible Widget | Not verified | Current code handles display/DPI messages; perform manual settings change |
| Multi-monitor disconnect/reconnect | Not verified | No second active monitor in current environment |
| Virtual desktop switching | Not verified | Requires user desktop configuration and manual test |
| Idle CPU/resource cost | Passed for this machine/sample | 300.5 s: CPU avg/peak 0.000%/0.000%; private memory avg/peak 82.4/82.6 MB; no fixed polling loop |

## Safety and recovery

The prototype does not terminate/restart Explorer, alter wallpaper, desktop icon settings, registry, startup, scheduled tasks or theme, close other applications, or request elevation. All disruptive tests are manual. If the embedded child becomes inaccessible, use the widget buttons/Alt+Tab first; as a last resort terminate only the SPIKE-002 prototype process and relaunch it.

## Preliminary conclusion

The experiment establishes a buildable, isolated host adapter and a safe interactive Widget fallback. It does not yet have enough visual evidence to claim that WorkerW embedding satisfies the required “desktop task can be checked” criterion. Until the manual matrix proves otherwise, the recommended default for DEV-084 is `WidgetFallback`; embedding should remain opt-in, bounded, and diagnosable.
