# SPIKE-001 交付记录

- 任务 ID：SPIKE-001
- 主题：Windows 本地通知、点击激活与发布模型技术验证
- 状态：实验原型完成；真实通知中心点击闭环和跨版本验收仍待补验
- 基线 SHA：`2446979ad25be69af552c673c5458e9b48139320`
- 分支：`spike/spike-001-notifications`
- 独立 worktree：`E:\Dev\Personal\Todo_list-spike-001-notifications`
- 允许目录/文件：`spikes/SPIKE-001/`、`docs/handoffs/SPIKE-001.md`、`docs/test-reports/SPIKE-001-validation.md`、`docs/adr/003-windows-notifications-and-publish-model.md`
- 有意不在范围：正式 `src/`、`tests/`、根解决方案、根中央包版本、全局 CI、DEV-080/DEV-081 正式实现、正式 `INotificationService` 修改、真实任务数据库、云端 Push、管理员权限、完整 MSIX 分发流程

## 前置门禁与基线

已完整阅读并按其约束执行：规格 `ScheduleAssistant_V1_Development_Specification.md`、仓库 `AGENTS.md`、根 `README.md`、ADR README/模板、DEV-001 最新 handoff 和 DEV-001 构建验证报告，以及通知/部署相关官方文档。

DEV-001 门禁已通过：

- DEV-001 合并提交：`3cfbd051e026b667488ab898ada34a0a81569e2b`。
- 合入后的远程 `main` 基线：`2446979ad25be69af552c673c5458e9b48139320`。
- GitHub Actions `main` run `35178286989` 成功；Windows `build-and-test` job `105064738962` 成功。
- DEV-001 handoff 已记录真实 Windows WPF 启动验证五项通过。

实验分支从该基线创建，没有推送或合并 `main`。

## 完成内容与关键设计

### 原型

- 新增 `spikes/SPIKE-001/` 独立 WPF 项目，目标 .NET `10.0.100`、x64、`net10.0-windows10.0.17763.0`。
- 实验目录自己的 `Directory.Packages.props` 固定 `Microsoft.WindowsAppSDK` `2.4.0`；没有改根中央包版本。
- 使用 `AppNotificationManager` 本地 App Notifications，注册前订阅 `NotificationInvoked`，正常退出尝试注销。
- UI 提供发送通知、刷新能力、隐藏窗口、正常退出；显示能力/注册状态、运行时/发布诊断、激活参数和日志。
- 通知载荷固定为模拟任务 `SPIKE-001-DEMO-TASK-42`，不连接任务数据库。
- 用当前用户 Mutex + 命名管道验证单实例和激活转发；它只属于实验，不是正式 IPC 契约。
- 注册/能力/发送异常进入 UI 诊断并保留进程，未将异常传播为应用崩溃。

### 发布结论

- FDD：较小，但依赖目标机 .NET Desktop Runtime 和 Windows App SDK Runtime/Singleton。当前 Win10 机器在首次无可见 Windows App Runtime 包时 FDD 启动失败；运行时包可见后可启动。
- SCD：最终输出 `295,693,615` bytes，Win10 实际启动并显示窗口；包含 .NET runtime 和 Windows App SDK Framework/native 内容，但不能把 Singleton/Runtime 依赖解释为完全零安装。
- 暂定个人使用推荐：未打包 x64 SCD 作为候选，配合启动能力检查、明确诊断/降级和稳定安装路径；正式采用前必须补验运行时部署及通知冷启动。
- MSIX：仅调查方向，没有建设商店、签名或完整安装更新流程。

## 改变的文件与接口/数据库影响

新增文件仅位于允许范围：

- `spikes/SPIKE-001/Directory.Build.props`
- `spikes/SPIKE-001/Directory.Packages.props`
- `spikes/SPIKE-001/ScheduleAssistant.SPIKE001.Notifications.csproj`
- `spikes/SPIKE-001/App.xaml`、`App.xaml.cs`、`MainWindow.xaml`、`MainWindow.xaml.cs`
- `spikes/SPIKE-001/Activation/ActivationCommand.cs`
- `spikes/SPIKE-001/Activation/SingleInstanceCoordinator.cs`
- `spikes/SPIKE-001/Scripts/Build.ps1`、`Publish.ps1`、`Inspect-Environment.ps1`
- `spikes/SPIKE-001/README.md`
- `docs/adr/003-windows-notifications-and-publish-model.md`
- `docs/test-reports/SPIKE-001-validation.md`

未修改正式公共接口、数据库、迁移、根解决方案、正式项目、正式测试和全局 CI。后续 `INotificationService` 建议只记录在测试报告中。

## 验证命令与结果

在 PowerShell 7 和用户级 .NET SDK `E:\Dev\Tools\ScheduleAssistantDotnet` 环境执行：

```powershell
pwsh -NoLogo -NoProfile -File .\spikes\SPIKE-001\Scripts\Inspect-Environment.ps1
pwsh -NoLogo -NoProfile -File .\spikes\SPIKE-001\Scripts\Build.ps1 -Configuration Release
pwsh -NoLogo -NoProfile -File .\spikes\SPIKE-001\Scripts\Publish.ps1 -Mode framework-dependent
pwsh -NoLogo -NoProfile -File .\spikes\SPIKE-001\Scripts\Publish.ps1 -Mode self-contained
```

结果：环境检查成功；Release restore/build 成功，0 warning、0 error；FDD/SCD 发布成功。SCD 主进程真实启动后 3 秒为 Responding、窗口句柄非零、标题正确；第二次启动 2 秒后退出，业务进程数为 1。SCD 在含中文和空格的临时目录发布并启动成功。

完整场景状态和字节数见 [`docs/test-reports/SPIKE-001-validation.md`](../test-reports/SPIKE-001-validation.md)。

## 未完成、已知问题和后续依赖

- 当前只实测 Windows 10 Pro 25H2 build `26200.9457`；Windows 11 未验证。
- 原生桌面自动化通道不可用，所以没有把按钮发送、通知中心点击、系统关闭通知和冷启动人工闭环伪报为通过。
- 需要在 Windows 10/11 真实已登录桌面人工完成全部通知场景，尤其是：隐藏驻留点击、已有实例点击、完全退出后的旧通知点击、设置关闭、正常注销。
- 需要用户提供的虚拟机或另一台测试机完成无预装 Windows App Runtime 的发布安装验证；不通过卸载本机运行时制造干净环境。
- 需要后续确认 `AppNotificationManager` 使用的 Singleton/Runtime 在选定分发模型下的安装步骤、权限、升级/移动路径和失败恢复。
- 实验的 Mutex/命名管道不能直接复制到正式工程；DEV-081 应按 ADR/规格重新设计适配器边界和激活路由。

## 是否足以支持 DEV-081

足以支持 DEV-081 继续做接口/适配器设计和受控原型工作：API 方向、异常诊断、单实例边界和 FDD/SCD 风险已经明确；不足以作为 DEV-081 完成验收或承诺 Windows 10/11 全面生产支持。正式实现前必须补齐真实通知点击/冷启动、Windows 11、无运行时 VM、通知禁用和 Singleton/Runtime 部署验证；若这些验证不稳定，应采用主界面/托盘定位等可接受降级。

## Git 记录

最终实现提交 SHA：`1be3ae260310b9c1e840d0da56c4cadd40849f5a`（SPIKE-001 原型与交付文档实现提交）。本次 handoff 的 SHA 记录更新随后提交；交付时不推送远程、不合并 `main`。
