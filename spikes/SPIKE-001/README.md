# SPIKE-001：Windows 本地通知、点击激活与发布模型

这是 ScheduleAssistant 的独立技术实验，不是 DEV-081 通知适配器，也不是 DEV-080 提醒调度器。原型只发送一条固定模拟任务通知：

```text
SPIKE-001-DEMO-TASK-42
```

## 基线、任务边界与实现选择

- 任务编号：`SPIKE-001`。
- 分支：`spike/spike-001-notifications`。
- 独立 worktree：`E:\Dev\Personal\Todo_list-spike-001-notifications`。
- 基线 SHA：`2446979ad25be69af552c673c5458e9b48139320`（合入 DEV-001 后的 `main`，创建实验分支时记录）。
- 允许修改：`spikes/SPIKE-001/`、`docs/handoffs/SPIKE-001.md`、本实验测试报告、通知/发布模型 ADR。
- 明确不在范围：正式 `src/`、`tests/`、根解决方案、根 `Directory.Packages.props`、全局 CI、正式 `INotificationService`、真实任务数据库、DEV-080/DEV-081 实现、托盘/自启动正式适配器、云端 Push、Azure、账号、服务器、管理员权限。

原型使用与主工程相同的 .NET SDK 基线（`global.json` 的 .NET `10.0.100`），目标为 `net10.0-windows10.0.17763.0`/`win-x64`。实验目录内单独的 `Directory.Packages.props` 固定官方稳定元包 `Microsoft.WindowsAppSDK` `2.4.0`，不改变正式项目的中央包版本。

## 原型行为

- 发送测试本地 App Notification；
- 显示 `AppNotificationManager.IsSupported()`、`Register()` 和 `AppNotificationSetting` 状态；
- 显示 OS、.NET、Windows App SDK 包版本、进程架构、权限、可执行文件路径、命令行和固定任务 ID；
- 隐藏主窗口但保持进程、通知注册和单实例管道；
- 点击通知后恢复窗口、识别固定任务 ID，并显示激活来源、PID、激活次数；
- 使用当前用户会话范围的命名 Mutex + 命名管道，第二次启动只向首实例发送 `showWindow`，不创建第二个业务窗口；
- 注册失败、通知能力关闭或发送失败时记录诊断并继续运行；
- 使用“正常退出”调用 `Unregister()` 后结束进程。

原型的单实例管道只支持本实验的 `showWindow` / `openTask` 两种命令，不能作为正式 IPC 合同。通知点击激活与未来提醒主动触发是两件事；本实验不验证也不承诺应用彻底退出或电脑关机后能主动发送未来提醒。

## 官方 API 依据

- [Use app notifications with a .NET app](https://learn.microsoft.com/en-us/windows/apps/develop/notifications/app-notifications/app-notifications-dotnet)：WPF 中先订阅 `NotificationInvoked`，再 `Register()`；未打包应用由 `Register()` 自动配置通知激活 COM 注册。
- [Windows notifications overview](https://learn.microsoft.com/en-us/windows/apps/develop/notifications/)：WPF/未打包 Win32 使用 `AppNotificationManager`，本实验不使用旧的 `ToastNotificationManager`，也不使用 Push Notifications。
- [Windows App SDK deployment overview](https://learn.microsoft.com/en-us/windows/apps/package-and-deploy/deploy-overview)：Windows App SDK 的 framework-dependent 与 self-contained 是独立于 MSIX/未打包模型的选择。
- [Windows App SDK deployment guide for self-contained apps](https://learn.microsoft.com/en-us/windows/apps/package-and-deploy/self-contained-deploy/deploy-self-contained-apps)：`WindowsAppSDKSelfContained=true` 会将 Windows App SDK Framework 内容复制到输出；App Notification API 仍依赖 Singleton 包，不能把 self-contained 误解成完全无系统运行时依赖。

## 构建、发布与运行

以下命令均为 PowerShell 7。若当前会话没有 `dotnet`，先按主工程 README 配置用户级 SDK 路径；不要修改系统 PATH，也不要卸载本机运行时：

```powershell
$dotnetRoot = 'E:\Dev\Tools\ScheduleAssistantDotnet'
$env:PATH = "$dotnetRoot;$env:PATH"
$env:DOTNET_ROOT = $dotnetRoot
$env:DOTNET_CLI_HOME = Join-Path $env:TEMP 'ScheduleAssistantSpike001CliHome'
$env:NUGET_PACKAGES = Join-Path $env:TEMP 'ScheduleAssistantSpike001NuGetPackages'
Set-Location 'E:\Dev\Personal\Todo_list-spike-001-notifications\spikes\SPIKE-001'
pwsh -NoLogo -NoProfile -File .\Scripts\Inspect-Environment.ps1
pwsh -NoLogo -NoProfile -File .\Scripts\Build.ps1
dotnet run --project .\ScheduleAssistant.SPIKE001.Notifications.csproj -c Release --no-build
```

Framework-dependent 发布（需要目标机有兼容的 .NET Desktop Runtime 和 Windows App SDK Runtime）：

```powershell
pwsh -NoLogo -NoProfile -File .\Scripts\Publish.ps1 -Mode framework-dependent
& '.\artifacts\publish\framework-dependent\ScheduleAssistant.SPIKE001.Notifications.exe'
```

Self-contained 发布（同时携带 .NET 运行时和 Windows App SDK Framework 内容；App Notification 的 Singleton 依赖仍需按诊断结果处理）：

```powershell
pwsh -NoLogo -NoProfile -File .\Scripts\Publish.ps1 -Mode self-contained
& '.\artifacts\publish\self-contained\ScheduleAssistant.SPIKE001.Notifications.exe'
```

含中文和空格的路径验证示例：

```powershell
$pathWithChinese = Join-Path $env:TEMP 'ScheduleAssistant SPIKE-001 中文 路径'
New-Item -ItemType Directory -Force $pathWithChinese | Out-Null
pwsh -NoLogo -NoProfile -File .\Scripts\Publish.ps1 -Mode self-contained -OutputDirectory $pathWithChinese
& (Join-Path $pathWithChinese 'ScheduleAssistant.SPIKE001.Notifications.exe')
```

## 手工场景

在真实、已登录且未锁屏的 Windows 桌面执行；不要用 Linux 编译或云端静态检查替代：

1. 前台运行，点击“发送测试通知”。
2. 点击“隐藏窗口”，确认进程仍在，再发送通知（可从通知或重新显示窗口操作）。
3. 点击通知，确认窗口恢复且“最近一次激活”显示 `SPIKE-001-DEMO-TASK-42`。
4. 已有实例时再次点击通知或启动第二个 EXE，确认 PID 不变、业务实例数仍显示 `1`。
5. 发送通知，点击“正常退出”，确认进程消失；再点击仍在通知中心的旧通知，确认冷启动并收到激活参数。
6. 在 Windows 设置中关闭本应用通知，回到原型点击“刷新通知能力”，确认状态明确且发送失败不崩溃。恢复设置由用户手动完成。
7. 从含中文、空格的发布路径启动并重复发送/点击激活。
8. 分别运行 FDD 和 self-contained 发布目录，记录 .NET、Windows App SDK Runtime/Singleton 依赖与结果。

发布模型调查只记录实际验证到的运行时依赖；不通过卸载本机 .NET 或 Windows App SDK 制造“干净环境”。缺少 Windows 10 或 Windows 11 环境时必须在报告中写明“未验证”。

## 预期降级

如果通知能力返回 `false`、系统设置为 `DisabledForApplication`/`DisabledForUser`/`DisabledByGroupPolicy`，或 `Register()`/`Show()` 抛出异常，原型仍保留窗口、单实例和诊断。正式 V1 可按 ADR 回退到托盘/主界面“即将截止”定位；本实验不把该回退实现并入正式工程。
