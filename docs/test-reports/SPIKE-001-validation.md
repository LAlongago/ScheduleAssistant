# SPIKE-001 验证报告

- 任务：SPIKE-001（Windows 本地通知、点击激活与发布模型技术验证）
- 基线：`main` / `2446979ad25be69af552c673c5458e9b48139320`
- 分支：`spike/spike-001-notifications`
- 独立 worktree：`E:\Dev\Personal\Todo_list-spike-001-notifications`
- 允许修改：`spikes/SPIKE-001/`、本报告、SPIKE-001 handoff、ADR-003
- 有意不改：正式 `src/`、`tests/`、根解决方案、根中央包版本、全局 CI、正式通知公共契约
- 结论日期：2026-09-17

## 基线门禁

已完成前置检查：

- DEV-001 最新 handoff 与构建验证记录显示远程 `main` 合并提交为 `3cfbd051e026b667488ab898ada34a0a81569e2b`，合入后的 `main` 文档提交为 `2446979ad25be69af552c673c5458e9b48139320`。
- GitHub Actions 最新 `main` 工作流 run `35178286989` 为 `completed/success`；Windows `build-and-test` job `105064738962` 的 checkout、SDK、restore、build、test 和 artifact 步骤均成功。
- DEV-001 交付记录包含真实 Windows WPF 启动验证：五项检查通过，`dotnet run` 正常退出。
- 本实验从上述验证通过的提交创建了独立分支和独立 worktree；没有修改或推送 `main`。

## 实验实现

原型位于 `spikes/SPIKE-001/`，包含：

- WPF 窗口：发送测试通知、刷新能力、隐藏窗口、正常退出、能力/注册状态、运行时与发布诊断、最近一次激活和日志。
- Windows App SDK `AppNotificationManager` 注册与 `NotificationInvoked` 回调；注册前订阅事件，退出时尝试 `Unregister()`。
- 固定 payload：`action=openTask`、`taskId=SPIKE-001-DEMO-TASK-42`。
- 仅用于实验的当前用户 Mutex +命名管道单实例转发；第二次启动不创建第二个业务窗口。
- PowerShell 7 环境检查、构建和 FDD/SCD 发布脚本。

## 实测环境

| 项目 | 结果 |
|---|---|
| OS | Windows 10 Pro 25H2，build `26200.9457`，runtime `10.0.26200.0` |
| 桌面/架构 | 真实已登录 Windows 桌面；x64；标准用户，非管理员 |
| PowerShell | `7.6.5 Core` |
| .NET SDK | `10.0.100`，MSBuild `18.0.2` |
| .NET Desktop Runtime | `Microsoft.WindowsDesktop.App 10.0.0` |
| Windows App SDK 包 | `Microsoft.WindowsAppSDK 2.4.0`；实际解析的 Runtime 为 `2.4.0`，Foundation 为 `2.3.9` 等元包传递依赖 |
| Windows App Runtime 状态 | 首次环境检查在首次 SCD 启动前未返回当前用户运行时包；随后检查可见 `Microsoft.WindowsAppRuntime.2 2.4.0.0` 及系统已有的其他版本。没有卸载或清理本机运行时 |
| Windows 11 | 未验证；没有虚构跨系统结论 |

首次环境检查输出来自 `Scripts/Inspect-Environment.ps1`。由于实验期间没有干净 VM，后续运行时包状态只作为观察记录，不能证明状态变化一定由某一步单独造成。

## 构建与发布

在 PowerShell 7 中执行了以下等价命令（使用用户级 SDK 路径，不修改系统 PATH）：

```powershell
$taskDotnetRoot = 'E:\Dev\Tools\ScheduleAssistantDotnet'
$env:PATH = "$taskDotnetRoot;$env:PATH"
$env:DOTNET_ROOT = $taskDotnetRoot
$env:DOTNET_CLI_HOME = Join-Path $env:TEMP 'ScheduleAssistantSpike001CliHome'
$env:NUGET_PACKAGES = Join-Path $env:TEMP 'ScheduleAssistantSpike001NuGetPackages'
pwsh -NoLogo -NoProfile -File .\spikes\SPIKE-001\Scripts\Inspect-Environment.ps1
pwsh -NoLogo -NoProfile -File .\spikes\SPIKE-001\Scripts\Build.ps1 -Configuration Release
pwsh -NoLogo -NoProfile -File .\spikes\SPIKE-001\Scripts\Publish.ps1 -Mode framework-dependent
pwsh -NoLogo -NoProfile -File .\spikes\SPIKE-001\Scripts\Publish.ps1 -Mode self-contained
```

结果：环境检查成功；Release x64 restore/build 成功，0 warning、0 error；两种发布均成功。

最终输出目录统计：

| 模型 | 输出目录 | 总字节数 | 运行时观察 |
|---|---|---:|---|
| FDD | `spikes/SPIKE-001/artifacts/publish/framework-dependent` | `79,159,278` | 不携带 .NET Core/Windows Desktop runtime；需要目标机运行时和 Windows App SDK Runtime/Singleton |
| SCD | `spikes/SPIKE-001/artifacts/publish/self-contained` | `295,693,615` | 含 `coreclr.dll`、`hostfxr.dll`、`hostpolicy.dll` 和 Windows App SDK Framework/native 内容；通知 Singleton/Runtime 的目标机部署仍须单独确认 |

发布使用完整官方 `Microsoft.WindowsAppSDK` 元包，因此产物包含超出本通知 API 的投影/组件；正式工程应在 DEV-081 期间结合官方支持范围确认是否可收窄依赖。

## 场景验证表

状态含义：`通过` 表示在当前环境完成了该场景的可观察证据；`部分通过` 表示只有子项通过；`未验证` 表示不能用当前证据替代真实桌面操作。

| # | 场景 | 状态 | 证据 / 未完成项 |
|---:|---|---|---|
| 1 | 前台运行时发送通知 | 未验证 | WPF SCD 窗口可启动；尚未在可用的原生桌面交互通道中点击“发送测试通知”并观察通知中心。 |
| 2 | 窗口隐藏、进程驻留时发送 | 未验证 | 隐藏和发送代码路径已实现并构建；未完成真实按钮/通知中心操作。 |
| 3 | 点击通知打开窗口并识别模拟任务 ID | 未验证 | 回调解析和窗口显示代码已实现；没有伪造点击证据，需人工点击确认 `SPIKE-001-DEMO-TASK-42`。 |
| 4 | 已有实例点击通知不产生重复业务实例 | 部分通过 | SCD 主进程窗口标题为 `ScheduleAssistant SPIKE-001 Notifications`；第二次启动 2 秒后退出，仍只剩 1 个同名业务进程。通知点击转发路径仍需人工确认。 |
| 5 | 完全退出后点击已发通知，冷启动并接收参数 | 未验证 | 正常退出按钮和冷启动代码已实现；未完成发送、正常退出、通知中心保留通知、再点击的真实桌面闭环。 |
| 6 | 通知关闭或注册失败不崩溃并显示诊断 | 未验证 | 注册/能力/发送异常均有捕获和 UI 诊断；尚未在设置中关闭通知并完成人工操作。无运行时的 FDD 失败属于发布宿主依赖，不能冒充“通知关闭”测试。 |
| 7 | 含中文、空格的路径发布与启动 | 通过（发布/启动子项） | SCD 发布到 `C:\Users\ceo\AppData\Local\Temp\ScheduleAssistant SPIKE-001 中文 路径` 成功；启动后 3 秒仍 Responding，窗口句柄非零且标题正常。通知点击子项未验证。 |
| 8 | 发布产物的目标机运行时依赖 | 部分通过 | SCD 在 Win10 实际启动；首次无当前用户 Windows App Runtime 状态下 FDD 启动失败并出现宿主错误窗口，随后运行时包可见时 FDD 可启动。未在干净 VM、Windows 11 或无预装运行时环境完成完整验证。 |

### 进程模型证据

SCD 进程测试使用 `Start-Process` 启动主 EXE，等待 3 秒确认 `Responding=True`、窗口句柄非零和正确标题，再启动同一 EXE，等待 2 秒检查进程数。结果：第二 PID 已退出，业务进程数为 `1`。测试后按精确 PID 停止实验进程；这是清理动作，不是“正常退出”场景的通过证据。

FDD 的首次失败发生在运行时包尚未可见的实验状态；后续 SCD 启动后，`Get-AppxPackage -Name Microsoft.WindowsAppRuntime*` 可见 `Microsoft.WindowsAppRuntime.2 2.4.0.0`，FDD 再次启动得到正常原型窗口。没有通过卸载运行时来重置环境。

## 结论与降级

技术方向已明确：Windows App SDK `AppNotificationManager` 可以作为后续适配器的候选 API，未打包 WPF + 本地通知 + 单实例激活路由的最小结构可构建、可发布并在 Win10 启动。发布方向暂定优先调查未打包 SCD；但它不是已验收的零安装方案。

在 DEV-081 前必须补验真实通知中心交互、已发通知冷启动、通知关闭/注册失败、Windows 11 和用户提供的无运行时 VM。若 Singleton/Runtime 无法以可接受的步骤部署，降级为保留窗口/托盘入口和明确能力诊断；不要承诺进程退出或关机后主动提醒。

## 后续 `INotificationService` 建议（只读建议，不改公共契约）

继续使用规格中的：

```csharp
Task<NotificationCapability> GetCapabilityAsync(CancellationToken ct);
Task<NotificationDeliveryResult> ShowAsync(LocalNotification notification, CancellationToken ct);
```

DEV-081 适配器应负责初始化/注册、查询 `IsSupported` 与 `AppNotificationSetting`、把 `LocalNotification` 映射为通知文本和任务 ID、返回结构化失败原因，并把点击事件交给应用层激活路由；它不应直接访问数据库或决定提醒调度。取消令牌只应约束应用侧调用，不应伪装成 Windows 通知已经撤回。未来提醒由 DEV-080 调度器决定，不能从本实验的点击回调推导“退出/关机仍会主动发送”。

## 工具限制

本次原生桌面自动化通道未返回可操作的 Windows 应用/桌面表面。为避免伪造 UI 结果，所有依赖按钮、通知中心、设置页面或通知点击的场景均保留为 `未验证`；Shell 进程检查仅作为启动/单实例/路径的辅助证据。
