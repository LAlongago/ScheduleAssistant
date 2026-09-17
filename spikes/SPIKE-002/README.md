# SPIKE-002：Windows 可交互桌面看板宿主

这是独立的 Windows WPF 技术实验，不是 DEV-084 的正式桌面模式实现，也不依赖 SPIKE-001 的未合并代码。

## 基线与范围

- 任务：`SPIKE-002`。
- 验证分支：`spike/spike-002-desktop-host`。
- 独立 worktree：`E:\Dev\Personal\Todo_list-spike-002-desktop-host`。
- 基线：`main` / `origin/main`，`2446979ad25be69af552c673c5458e9b48139320`。
- 允许范围：本目录中的独立 WPF 原型、测试说明和文档；`docs/handoffs/SPIKE-002.md`、测试报告和 ADR。
- 明确不在范围：正式 `src/`、`tests/`、根解决方案、中央包版本、数据库、任务用例、附件、提醒、托盘、自启动和正式 `IDesktopHostService` 公共契约。

原型的任务是进程内临时模拟数据，关闭进程即丢弃；不会创建 SQLite、正式数据目录或用户设置。

## 原型行为

看板包含当前日期/星期、4 条模拟任务、模拟 Deadline、可勾选完成状态和宿主诊断。窗口是普通无边框 WPF 窗口，`Topmost=False`，可以拖动位置。

`DesktopHostService` 是唯一的桌面宿主边界，集中处理：

- `Progman`/`WorkerW` 发现和有限退避重试；
- `SetParent`、窗口样式切换、Z-order 和恢复；
- `TaskbarCreated`、显示设置和 DPI 消息；
- 普通 Widget 可见区域恢复。

进入嵌入后，界面先显示 `Embedded · 待交互验证`。只有真实点击复选框并观察到完成状态变化，才会显示 `Embedded`；因此“窗口显示出来”不会被误写成“桌面交互可用”。嵌入失败自动回退为 `WidgetFallback`，无法建立可见窗口时才报告 `Unavailable`。

## PowerShell 7 构建与运行

从独立 worktree 根目录执行。若 SDK 不在 PATH，使用仓库 DEV-001 记录的当前会话配置：

```powershell
$dotnetRoot = 'E:\Dev\Tools\ScheduleAssistantDotnet'
$env:PATH = "$dotnetRoot;$env:PATH"
$env:DOTNET_ROOT = $dotnetRoot
$env:DOTNET_CLI_HOME = Join-Path $env:TEMP 'ScheduleAssistantDotnetCliHome'
$env:NUGET_PACKAGES = Join-Path $env:TEMP 'ScheduleAssistantNuGetPackages'
$env:APPDATA = Join-Path $env:TEMP 'ScheduleAssistantAppData'
Set-Location 'E:\Dev\Personal\Todo_list-spike-002-desktop-host'
```

构建：

```powershell
dotnet restore .\spikes\SPIKE-002\ScheduleAssistant.SPIKE002.csproj
dotnet build .\spikes\SPIKE-002\ScheduleAssistant.SPIKE002.csproj -c Release --no-restore
```

运行：

```powershell
dotnet run --project .\spikes\SPIKE-002\ScheduleAssistant.SPIKE002.csproj -c Release --no-build
```

该项目故意不加入 `ScheduleAssistant.sln`，因此不会改变正式解决方案的项目图或 CI。

## 手工验证步骤

以下步骤应在真实 Windows 桌面完成。Explorer 重启、显示器断开、DPI/分辨率切换和虚拟桌面切换均由用户手工执行；原型不会杀死/重启 Explorer，不修改注册表、壁纸、主题、图标设置，也不请求管理员权限。

1. 启动后确认初始状态为 `WidgetFallback`，拖动蓝色标题区，确认位置可调整且没有始终置顶行为。
2. 点击“进入嵌入模式”。若显示 `Embedded · 待交互验证`，点击任一复选框；只有状态变为 `Embedded` 且任务完成数改变，才记录嵌入交互通过。
3. 将看板放在桌面图标附近，分别点击任务复选框和桌面图标，确认两者都能收到点击。
4. 打开普通前台应用并切换焦点；确认普通 Widget 不覆盖该应用。由于没有 `Topmost=True`，Widget 可以被前台应用遮挡。
5. 用户手工按 `Win+D` 显示桌面，再切换窗口，验证看板是否仍可见且可交互。自动化代理不模拟 Windows 键。
6. 点击“退出嵌入”和“切换普通 Widget”，确认状态和诊断原因变化；点击关闭，确认没有残留原型进程。
7. 保存当前桌面工作后，在任务管理器中手工重启 Windows Explorer。观察 `TaskbarCreated` 后宿主是否重新附着；如果没有稳定附着，必须仍然显示可操作的 `WidgetFallback`。
8. 手工改变显示器缩放/分辨率，确认 Widget 被恢复到可见工作区。若有第二显示器，连接、断开并重复；虚拟桌面切换也应单独记录。

恢复办法：优先使用“切换普通 Widget”或“恢复可见位置”。若窗口不可见，使用 Alt+Tab 找到原型并切换回 Widget；最后仅结束原型自己的进程并重新运行。不得为了恢复测试而结束或重启 Explorer。

详细证据和场景矩阵见 [`docs/test-reports/SPIKE-002-desktop-host.md`](../../docs/test-reports/SPIKE-002-desktop-host.md)。
