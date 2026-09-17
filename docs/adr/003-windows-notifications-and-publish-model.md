# ADR-003：Windows 本地通知与发布模型

- Status: Proposed（SPIKE-001 技术结论；不等同于 DEV-081 生产适配器批准）
- Date: 2026-09-17
- Owners: ScheduleAssistant 工程
- Related task(s): SPIKE-001, DEV-081；DEV-080 不在本实验范围

## Context

规格要求通过 SPIKE-001 验证 WPF 未打包应用的 Windows 本地 App Notifications、通知点击激活、运行时部署和发布模型。实验不能接入真实任务数据库，也不能把通知点击激活误认为应用在退出或关机后仍能主动产生未来提醒。

实验基线为 DEV-001 修复合入后的 `main`：`2446979ad25be69af552c673c5458e9b48139320`。原型使用 .NET SDK `10.0.100`、x64、`net10.0-windows10.0.17763.0`，实验目录内固定 `Microsoft.WindowsAppSDK` `2.4.0`。官方 WPF 文档要求在 `Register()` 前订阅 `NotificationInvoked`；未打包应用的注册过程会为通知点击配置 COM 激活，且退出前应注销。

## Decision

1. 通知 API 采用 Windows App SDK 的 `AppNotificationManager`，不采用旧的 `ToastNotificationManager`、云端 Push、Azure、账号或服务器。原型使用 `AppNotificationBuilder` 发送固定模拟任务 ID `SPIKE-001-DEMO-TASK-42`。
2. 原型采用未打包 WPF 应用：`WindowsPackageType=None`，在最终运行路径注册，收到 `NotificationInvoked` 后把激活参数传给主进程。实验内的当前用户 Mutex + 命名管道只用于证明单实例转发，不形成正式 IPC 公共契约。
3. 对个人使用，暂定优先评估未打包 x64 self-contained 发布：它携带 .NET 运行时和 Windows App SDK Framework 内容，适合复制到含中文、空格的稳定目录后运行。该选择不是“完全零依赖”：`AppNotificationManager` 依赖 Windows App SDK Singleton/Runtime 组件，正式分发仍需在目标机验证运行时包部署；如果运行时或通知能力不可用，必须显示诊断并降级。
4. Framework-dependent 发布保留为受控机器的较小选项。它要求目标机提供兼容的 .NET Desktop Runtime 和 Windows App SDK Runtime。实验机首次检查没有当前用户 Windows App Runtime 包时，FDD 启动失败；在后续实验状态出现 Windows App Runtime `2.4.0.0` 包后，FDD 可启动。因此不能把 FDD 当作无需安装步骤的个人便携方案。
5. 本实验不建设 MSIX 商店/分发流程。若后续需要可预测的安装、更新、包身份和通知注册生命周期，应另立任务评估 MSIX 或 packaged-with-external-location，并重新验证冷启动激活。

注册与路径限制：未打包 `Register()` 使用当前可执行文件作为激活服务器；应从最终稳定安装目录注册，移动目录后需要重新注册并重新验证，不能假设旧注册仍指向新路径。应用不得要求管理员运行；管理员模式和策略/用户关闭通知都必须进入能力诊断与降级路径。

## Alternatives considered

- Framework-dependent 未打包应用 — 输出较小、运行时可集中维护；但依赖 .NET Desktop Runtime、Windows App SDK Runtime/Singleton，当前机器的首次无运行时状态已暴露启动风险。
- 未打包 self-contained 应用 — 复制部署简单，且不依赖目标机已有 .NET；代价是本实验完整 `Microsoft.WindowsAppSDK` 元包的最终 x64 产物约 295.7 MB，并且 Windows App SDK Singleton/Runtime 仍需处理，不能仅凭 self-contained 名称承诺零安装。
- MSIX — 包身份、安装和更新边界更清晰，可能更适合长期通知激活；但需要新的打包、签名、安装/更新和路径验证工作，本实验不建设。
- 云端 Push Notifications — 需要云服务、账号和服务器，不符合个人本地提醒的边界，拒绝。

## Consequences

### Positive

- Windows 本地通知和点击激活的 API 方向与规格一致，通知载荷可以只携带任务 ID，业务数据库不进入平台适配层。
- 主窗口隐藏时仍保留进程、注册和单实例管道；已有实例的第二次启动不会创建第二个业务窗口。
- SCD 对 .NET 运行时更可控；FDD 仍可用于已有运行时的开发机或受控环境。
- 注册失败、能力不支持和发送异常都可以保留窗口并呈现明确诊断，不把平台故障升级为应用崩溃。

### Trade-offs and risks

- Windows App SDK Runtime/Singleton 是发布边界的关键风险；SCD 不等于通知 API 的完全 app-local、无安装依赖。
- 未打包 COM 注册和路径移动存在生命周期限制；安装目录必须稳定，安装/升级流程需要显式重新注册和回滚策略。
- 当前实验只在 Windows 10 验证，通知实际点击、冷启动、系统设置关闭和全新无运行时机器尚未完成真实桌面人工验收；不能据此声称 Windows 11 或生产稳定性。
- 通知点击激活只验证“已发通知的回调/激活”；它不验证也不承诺应用退出、机器关机后未来提醒仍会主动发送。
- 正式实现还要把通知适配器、激活路由、托盘/自启动和提醒调度器分开，不能复制实验的临时 IPC 或把 `showWindow` 当作业务契约。

## Validation plan and evidence

- 实验原型和可重复命令：`spikes/SPIKE-001/README.md`。
- 场景、环境、产物体积和限制：`docs/test-reports/SPIKE-001-validation.md`。
- 官方依据：[WPF app notifications](https://learn.microsoft.com/en-us/windows/apps/develop/notifications/app-notifications/app-notifications-dotnet)、[notifications overview](https://learn.microsoft.com/en-us/windows/apps/develop/notifications/)、[deployment overview](https://learn.microsoft.com/en-us/windows/apps/package-and-deploy/deploy-overview)、[self-contained deployment](https://learn.microsoft.com/en-us/windows/apps/package-and-deploy/self-contained-deploy/deploy-self-contained-apps)。
- 选定 NuGet 包：[Microsoft.WindowsAppSDK 2.4.0](https://packages.nuget.org/packages/Microsoft.WindowsAppSDK/2.4.0)。

DEV-081 开始前，必须在至少一台 Windows 10 和一台 Windows 11 的真实、已登录桌面上补验：前台/隐藏发送、通知点击定位任务、已有实例、冷启动、系统关闭通知、正常 `Unregister()` 退出、最终安装路径移动限制，以及没有预装运行时的用户提供 VM/测试机。不得通过卸载本机运行时制造“干净环境”。

## Revisit criteria

出现以下任一证据时，应由后续 ADR 取代本实验结论：

- Windows 10/11 任一目标版本无法稳定完成注册、发送或冷启动点击激活；
- 目标机无法以可接受的用户步骤部署 Singleton/Runtime；
- 稳定安装路径、自动启动或托盘生命周期要求与未打包 COM 注册冲突；
- 体积或安装复杂度不可接受，需要 MSIX 或其他发布模型；
- 正式 `INotificationService` 的能力/结果语义需要超出当前规格公共契约。
