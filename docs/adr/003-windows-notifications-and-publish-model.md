# ADR-003：Windows 本地通知与发布模型

- Status: DEV-081 已实现；INTEGRATION-012 增加显式 Runtime 初始化；负责人报告 DEV-081 的三组 Windows 人工验收已完成
- Date: 2026-09-17
- Owners: ScheduleAssistant 工程
- Related task(s): SPIKE-001, DEV-081；DEV-080 不在本实验范围

## Context

规格要求通过 SPIKE-001 验证 WPF 未打包应用的 Windows 本地 App Notifications、通知点击激活、运行时部署和发布模型。实验不能接入真实任务数据库，也不能把通知点击激活误认为应用在退出或关机后仍能主动产生未来提醒。

实验基线为 DEV-001 修复合入后的 `main`：`2446979ad25be69af552c673c5458e9b48139320`。原型使用 .NET SDK `10.0.100`、x64、`net10.0-windows10.0.17763.0`，实验目录内固定 `Microsoft.WindowsAppSDK` `2.4.0`。官方 WPF 文档要求在 `Register()` 前订阅 `NotificationInvoked`；未打包应用的注册过程会为通知点击配置 COM 激活，且退出前应注销。

## Decision

1. 通知 API 采用 Windows App SDK 的 `AppNotificationManager`，不采用旧的 `ToastNotificationManager`、云端 Push、Azure、账号或服务器。原型使用 `AppNotificationBuilder` 发送固定模拟任务 ID `SPIKE-001-DEMO-TASK-42`。
2. 原型采用未打包 WPF 应用：`WindowsPackageType=None`，在最终运行路径注册，收到 `NotificationInvoked` 后把激活参数传给主进程。DEV-081 的正式适配器同样使用 Windows App SDK；激活只接受 `action=openTask` 和规范格式 `taskId`，由 WPF Dispatcher 路由到主窗口与任务编辑器。当前实现用当前会话 Mutex 和无载荷命名事件恢复已有实例；通知任务参数不经过该事件。
3. 对个人使用，暂定优先评估未打包 x64 self-contained 发布：它携带 .NET 运行时和 Windows App SDK Framework 内容，适合复制到含中文、空格的稳定目录后运行。该选择不是“完全零依赖”：`AppNotificationManager` 依赖 Windows App SDK Singleton/Runtime 组件，正式分发仍需在目标机验证运行时包部署；如果运行时或通知能力不可用，必须显示诊断并降级。
4. Framework-dependent 发布保留为受控机器的较小选项。它要求目标机提供兼容的 .NET Desktop Runtime 和 Windows App SDK Runtime。实验机首次检查没有当前用户 Windows App Runtime 包时，FDD 启动失败；在后续实验状态出现 Windows App Runtime `2.4.0.0` 包后，FDD 可启动。因此不能把 FDD 当作无需安装步骤的个人便携方案。
5. 正式整合关闭 Windows App SDK 对未打包 WinExe 的自动 bootstrap。Framework-dependent 路径在通知 provider 内显式调用不显示交互 UI 的 Bootstrap.TryInitialize；失败映射为 provider 不可用并保持 Reminder Pending，成功初始化后在通知注销之后调用 Bootstrap.Shutdown。RID self-contained 路径继续使用 SDK 自包含初始化，不调用 framework bootstrap。这个改动沿用 2.4.0，没有改变发布模型。
6. 本实验不建设 MSIX 商店/分发流程。若后续需要可预测的安装、更新、包身份和通知注册生命周期，应另立任务评估 MSIX 或 packaged-with-external-location，并重新验证冷启动激活。

注册与路径限制：未打包 `Register()` 使用当前可执行文件作为激活服务器；应从最终稳定安装目录注册，移动目录后需要重新注册并重新验证，不能假设旧注册仍指向新路径。应用不得要求管理员运行；管理员模式和策略/用户关闭通知都必须进入能力诊断与降级路径。

## Alternatives considered

- Framework-dependent 未打包应用 — 输出较小、运行时可集中维护；但依赖 .NET Desktop Runtime、Windows App SDK Runtime/Singleton，当前机器的首次无运行时状态已暴露启动风险。
- 未打包 self-contained 应用 — 复制部署简单，且不依赖目标机已有 .NET；代价是本实验完整 `Microsoft.WindowsAppSDK` 元包的最终 x64 产物约 295.7 MB，并且 Windows App SDK Singleton/Runtime 仍需处理，不能仅凭 self-contained 名称承诺零安装。
- MSIX — 包身份、安装和更新边界更清晰，可能更适合长期通知激活；但需要新的打包、签名、安装/更新和路径验证工作，本实验不建设。
- 云端 Push Notifications — 需要云服务、账号和服务器，不符合个人本地提醒的边界，拒绝。

## Consequences

### Positive

- Windows 本地通知和点击激活的 API 方向与规格一致，通知载荷可以只携带任务 ID，业务数据库不进入平台适配层。
- 主窗口隐藏时仍保留进程和注册；第二次启动只发出单实例恢复信号，不会创建第二个业务进程或初始化第二套数据库服务。
- SCD 对 .NET 运行时更可控；FDD 仍可用于已有运行时的开发机或受控环境。
- 注册失败、能力不支持和发送异常都可以保留窗口并呈现明确诊断，不把平台故障升级为应用崩溃。

### Trade-offs and risks

- Windows App SDK Runtime/Singleton 是发布边界的关键风险；SCD 不等于通知 API 的完全 app-local、无安装依赖。
- 未打包 COM 注册和路径移动存在生命周期限制；安装目录必须稳定，安装/升级流程需要显式重新注册和回滚策略。
- SPIKE-001 只在 Windows 11 25H2（build 26200）验证。DEV-081 已实现正式适配器并通过自动化和 WPF 启停 smoke，但负责人报告三组人工验收已完成（见 INTEGRATION-012）；本次整合未重复执行；独立 Windows 10 环境和全新无运行时机器也未验证，不能据此声称跨版本或生产稳定性。原始注册表 `ProductName=Windows 10 Pro` 与 build/25H2 不一致，整合记录按 build 26200 归类为 Windows 11 25H2。
- 通知点击激活只验证“已发通知的回调/激活”；它不验证也不承诺应用退出、机器关机后未来提醒仍会主动发送。
- 托盘、自启动和应用退出期间的未来提醒仍属于后续任务；当前 DEV-081 不复制 SPIKE-001 的临时 IPC，也不把任意命令当作激活契约。

## Validation plan and evidence

- 实验原型和可重复命令：`spikes/SPIKE-001/README.md`。
- 场景、环境、产物体积和限制：`docs/test-reports/SPIKE-001-validation.md`。
- DEV-081 自动化测试、Release 构建与 WPF 启动/单实例/正常关闭 smoke 记录：`docs/handoffs/DEV-081.md`。
- 官方依据：[WPF app notifications](https://learn.microsoft.com/en-us/windows/apps/develop/notifications/app-notifications/app-notifications-dotnet)、[notifications overview](https://learn.microsoft.com/en-us/windows/apps/develop/notifications/)、[deployment overview](https://learn.microsoft.com/en-us/windows/apps/package-and-deploy/deploy-overview)、[self-contained deployment](https://learn.microsoft.com/en-us/windows/apps/package-and-deploy/self-contained-deploy/deploy-self-contained-apps)。
- 选定 NuGet 包：[Microsoft.WindowsAppSDK 2.4.0](https://packages.nuget.org/packages/Microsoft.WindowsAppSDK/2.4.0)。

DEV-081 的三组人工验收由负责人报告已完成；本次整合未重新执行。验收范围如下：

1. 创建即将触发的提醒，确认中文通知、任务标题和 Deadline 正常显示。
2. 分别在应用运行中和关闭后点击通知，确认聚焦主窗口、打开正确任务且只有一个业务进程。
3. 暂时关闭 Windows 对本应用的通知权限，确认 Reminder 保持 Pending；重新允许并重启或 Resume 后确认继续调度。

SPIKE-001 已验证 Windows 11 25H2；DEV-081 的人工验收由负责人报告完成，但本次整合未复测通知展示、激活或权限切换。独立 Windows 10 和全新无运行时环境仍没有证据，不能据此声称这些环境已通过。不得通过卸载本机运行时制造“干净环境”。

INTEGRATION-012 的本地 Release solution build、完整测试及 WPF 启停/单实例 smoke 已通过。此前 PR CI 的 Presentation 测试挂起与 SDK 自动 bootstrap 在缺失匹配 Runtime 时可能显示 UI 的路径吻合；整合分支改为显式无 UI 初始化，最终 PR CI 结论以交付输出为准。此修正之后没有重做通知点击、权限切换或全新无 Runtime 机器的人工验证，不能把原有 DEV-081 人工验收视为新路径的独立验证。

## Revisit criteria

出现以下任一证据时，应由后续 ADR 取代本实验结论：

- Windows 10/11 任一目标版本无法稳定完成注册、发送或冷启动点击激活；
- 目标机无法以可接受的用户步骤部署 Singleton/Runtime；
- 稳定安装路径、自动启动或托盘生命周期要求与未打包 COM 注册冲突；
- 体积或安装复杂度不可接受，需要 MSIX 或其他发布模型；
- 正式 `INotificationService` 的能力/结果语义需要超出当前规格公共契约。
