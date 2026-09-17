# SPIKE-002 交付记录

## 任务与边界

- 任务编号：`SPIKE-002` Windows 可交互桌面看板宿主技术验证。
- 基线：`main` / `origin/main` `2446979ad25be69af552c673c5458e9b48139320`。
- 分支：`spike/spike-002-desktop-host`。
- 独立 worktree：`E:\Dev\Personal\Todo_list-spike-002-desktop-host`。
- 允许修改：`spikes/SPIKE-002/`、本 handoff、对应测试报告和桌面宿主 ADR。
- 明确不在范围：正式 `src/`、`tests/`、根解决方案、中央包版本、CI、数据库、任务用例、附件、提醒、托盘、自启动和正式公共契约。
- 不依赖 SPIKE-001 未合并代码。

## 完成内容与关键设计选择

- 建立独立 `net10.0-windows` WPF 最小原型，不加入 `ScheduleAssistant.sln`。
- 看板显示日期/星期、4 条进程内模拟任务、模拟 Deadline、复选框完成状态和宿主诊断。
- `DesktopHostService` 集中 WorkerW/Progman 发现、句柄父子切换、样式/Z-order、恢复和显示区域逻辑；ViewModel 无 Win32 引用。
- WorkerW/Progman 附着最多 3 次，退避 0/250/750 ms；没有持续 100 ms 扫描或强制重排。
- 附着后先显示 `Embedded · 待交互验证`；真实复选框变化后才显示 `Embedded`，否则不宣称交互成功。
- 默认/失败路径为普通无边框、可拖动、非 `Topmost` 的 `WidgetFallback`；无法建立可见窗口才是 `Unavailable`。
- Explorer 的 `TaskbarCreated`、显示设置和 DPI 消息触发有限恢复；不自动重启/杀死 Explorer。

## 修改文件

- `spikes/SPIKE-002/ScheduleAssistant.SPIKE002.csproj`
- `spikes/SPIKE-002/App.xaml`、`App.xaml.cs`
- `spikes/SPIKE-002/MainWindow.xaml`、`MainWindow.xaml.cs`
- `spikes/SPIKE-002/DashboardViewModel.cs`
- `spikes/SPIKE-002/SimulatedTask.cs`
- `spikes/SPIKE-002/DesktopHostResult.cs`
- `spikes/SPIKE-002/DesktopHostService.cs`
- `spikes/SPIKE-002/README.md`
- `docs/adr/004-desktop-host-and-fallback.md`
- `docs/test-reports/SPIKE-002-desktop-host.md`
- `docs/handoffs/SPIKE-002.md`

## 数据库/接口影响

- 未创建数据库、SQLite、数据目录、迁移、正式用户数据或设置文件。
- 未修改正式 `IDesktopHostService` 或任何其他公共契约。
- 原型自己的 `DesktopHostResult`/`DesktopHostService` 不是正式契约，后续 DEV-084 需重新设计和评审。

## 验证命令与结果

基线已独立复跑：

```powershell
dotnet restore .\ScheduleAssistant.sln
dotnet build .\ScheduleAssistant.sln -c Release --no-restore
dotnet test .\ScheduleAssistant.sln -c Release --no-build --no-restore
```

结果：通过；8 个项目构建，0 warning/0 error；4 个测试宿主，7/7 通过。

原型：

```powershell
dotnet restore .\spikes\SPIKE-002\ScheduleAssistant.SPIKE002.csproj
dotnet build .\spikes\SPIKE-002\ScheduleAssistant.SPIKE002.csproj -c Release --no-restore
dotnet run --project .\spikes\SPIKE-002\ScheduleAssistant.SPIKE002.csproj -c Release --no-build
```

结果：restore/build 通过，0 warning/0 error；最终可执行文件启动检查得到非零主窗口句柄、标题 `SPIKE-002 Desktop Host` 且进程响应正常。原生窗口观察通道在本会话不可用，无法把进程元数据当作窗口视觉或复选框交互通过。

实际环境与场景矩阵见 [`docs/test-reports/SPIKE-002-desktop-host.md`](../test-reports/SPIKE-002-desktop-host.md)。

## 未完成项、已知问题与后续依赖

- 当前实验环境只有 1 个实际活动显示器；Windows 10、第二显示器断开/重连和虚拟桌面未验证。
- WorkerW/Progman 嵌入、图标共存、Win+D、Explorer 重启后的重新附着、DPI/分辨率变化和正常关闭需要用户在真实桌面按步骤手工确认。
- 当前未获得可用的原生 Computer Use 窗口观察通道，因此不能给出“嵌入后复选框可勾选”的通过结论。
- 最终代码空闲采样已完成：300.5 秒平均/峰值 CPU 0.000%/0.000%；私有内存平均/峰值 82.4/82.6 MB；代码没有固定轮询或持续重排计时器。
- 后续 DEV-084 应以 `IDesktopHostService` 为端口重新定义可判别结果、恢复语义、取消语义、布局持久化和用户可见诊断，不直接复制本实验类。

## 最终结论与建议

本实验满足“可运行的独立原型 + 安全回退 + 集中宿主风险”的工程目标，但在交互证据完成前不能判定桌面层嵌入满足勾选要求。建议 DEV-084 默认使用普通 `WidgetFallback`；WorkerW/Progman 只作为显式实验/可选能力，必须在至少一个 Windows 10 和一个 Windows 11 环境完成交互、图标、Explorer、DPI 和多显示器矩阵后再决定是否开放。

## 对后续 IDesktopHostService 的建议

- 公共端口继续使用抽象 `WindowHandle`/宿主请求，不让 Application 或 ViewModel 引用 WPF `Window`、Win32 类型或具体 `WorkerW` 类。
- 结果至少拆分 `Mode`、`InteractionValidation`、`ParentKind`、`FailureReason`、`RetryCount` 和 `CanRecover`；不要只返回一个布尔值。
- 将“附着成功但尚未验证鼠标/键盘命中”作为显式中间状态；只有业务层收到真实完成操作事件后才将能力标为 `Embedded`。
- 提供显式 `AttachAsync`、`DetachAsync`、`UseWidgetFallbackAsync`、`RestoreVisibleBoundsAsync` 和 `RevalidateAsync`，每个操作接受取消令牌并保证在 UI Dispatcher 上完成窗口操作。
- 将 shell/display 变化作为事件输入或可替换监视器，不在宿主中使用固定高频轮询；重试策略应有上限、退避和诊断。
- 让默认配置选择 Widget；嵌入必须是能力检测后的可选项，失败不得让看板不可见，也不得自动重启 Explorer。

## 最后提交 SHA

待完成最终本地提交后补录；不推送、不合并 `main`。
