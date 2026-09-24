# INTEGRATION-011 合并交付记录

## 任务范围与基线

- 任务 ID：`INTEGRATION-011`，整合 `DEV-061` 与 `DEV-080`。
- 正式 `main` 基线：`75a9fb198000242c246ee3359546b503c3fb8132`；开始前 fetch 与再次推送前 fetch 均核对到该 SHA。
- 来源：`DEV-061` `786e6d16b86187452c023ae9b90821a7223ab80d`、`DEV-080` `296bb1880d5c5ab7772b6438d6067e5c137f311a`，均以指定基线为直接父提交。
- 整合分支：`integration/dev-061-dev-080`。
- Worktree：`E:\Dev\Personal\Todo_list.worktrees\integration-dev-061-dev-080`。
- 允许范围：合并 DEV-061/DEV-080 文件；解决 Presentation 组合根、DI、服务启动次序和测试注册冲突；调整 Reminder 调度器及其通知端口以安全处理占位提供者；对应测试、README 与本交付记录。
- 明确不在范围：更改周期物化规则、附件逻辑、普通任务/日历功能、Domain Reminder 状态编码、数据库迁移/表结构、DEV-081 Windows 通知适配器，以及来源分支或 worktree。

## 完成内容与关键设计选择

- 从指定正式 `main` 创建整合分支，并完整合并两个来源提交。Git 自动合并成功；周期编辑 UI 与确认流程、持久化提醒调度器和各自测试均保留。两个来源 worktree 状态仍干净。
- 为 `INotificationService` 增加通知能力查询。当前 `UnavailableNotificationService` 明确报告 provider 未配置；能力不可用或检测异常时，调度器不读取/处理到期提醒、不建立计时器，也不改变提醒状态。
- 只有能力检查成功后才会进行补偿或设置单次计时器。计时回调与逐条到期处理都会复查能力；provider 不可用时取消计时并休眠。后续能力通过启动、Resume 或应用事件恢复时，调度器重新补偿并继续单次调度。只有能力可用且已实际调用 `ShowAsync` 后返回失败或抛出异常，才持久化 `Failed`。
- 组合根 HostedService 顺序为数据库初始化（其中先完成迁移、再物化周期实例）、附件维护、提醒调度器。
- Presentation 未增加 Domain 引用；没有修改迁移、Reminder 状态编码或持久化契约。

## 修改文件与接口/数据库影响

- DEV-061 与 DEV-080 的完整文件清单和设计说明分别见 [`DEV-061.md`](DEV-061.md) 与 [`DEV-080.md`](DEV-080.md)。
- INTEGRATION-011 的额外实现/验证变更：`README.md`；`src/ScheduleAssistant.Application/Reminders/INotificationService.cs`、`ReminderScheduler.cs`、`ReminderScheduler.Processing.cs`、`ReminderScheduler.Timer.cs`；`src/ScheduleAssistant.Presentation/Composition/PresentationServiceCollectionExtensions.cs`、`UnavailableNotificationService.cs`；`tests/ScheduleAssistant.Application.Tests/ReminderSchedulerTests.cs`、`TaskUseCaseTestDoubles.cs`；`tests/ScheduleAssistant.Presentation.Tests/CompositionRegistrationTests.cs`；以及本 handoff。
- Application 接口影响：`INotificationService` 新增 `GetCapabilityAsync` 与 provider 能力结果类型，供 DEV-081 实现可用性检测。
- 数据库影响：无；未更改迁移、表结构、Reminder 状态编码、已验收周期物化或附件行为。

## 验证命令与结果

使用仓库指定 .NET SDK `10.0.100`：

```powershell
& 'E:\Dev\Tools\ScheduleAssistantDotnet\dotnet.exe' restore .\ScheduleAssistant.sln
& 'E:\Dev\Tools\ScheduleAssistantDotnet\dotnet.exe' build .\ScheduleAssistant.sln -c Release --no-restore -m:1 -nr:false
& 'E:\Dev\Tools\ScheduleAssistantDotnet\dotnet.exe' test .\tests\ScheduleAssistant.Application.Tests\ScheduleAssistant.Application.Tests.csproj -c Release --no-build --no-restore -m:1 -nr:false
& 'E:\Dev\Tools\ScheduleAssistantDotnet\dotnet.exe' test .\tests\ScheduleAssistant.Infrastructure.Tests\ScheduleAssistant.Infrastructure.Tests.csproj -c Release --no-build --no-restore -m:1 -nr:false
& 'E:\Dev\Tools\ScheduleAssistantDotnet\dotnet.exe' test .\tests\ScheduleAssistant.Presentation.Tests\ScheduleAssistant.Presentation.Tests.csproj -c Release --no-build --no-restore -m:1 -nr:false
& 'E:\Dev\Tools\ScheduleAssistantDotnet\dotnet.exe' test .\tests\ScheduleAssistant.Architecture.Tests\ScheduleAssistant.Architecture.Tests.csproj -c Release --no-build --no-restore -m:1 -nr:false
git diff --check
```

- Restore：通过。
- Release solution build：通过，0 warnings / 0 errors。
- Application.Tests：43 passed；Infrastructure.Tests：38 passed；Presentation.Tests：51 passed；Architecture.Tests：4 passed；均为 0 failed / 0 skipped。
- 关键回归：Presentation 组合根测试使用临时真实 SQLite 数据库，插入已过期 48 小时的 Pending Reminder，启动实际 `ReminderSchedulerHostedService` 和实际不可用通知占位服务；状态仍为 Pending，计时器工厂创建次数为 0。Application 测试也覆盖启动休眠、无到期查询、恢复能力后的补偿、可用 provider 调用失败才记录 Failed。
- WPF smoke：Release 主窗口标题 `ScheduleAssistant`；通过 `CloseMainWindow()` 请求正常关闭，进程以退出码 0 退出。Smoke 使用临时隔离数据根，结束后清理；临时注入已从源码恢复，最终 Release 程序已用未注入源码重新构建。
- `git diff --check`：通过，无 whitespace error。

## 未完成、已知事项与人工验收

- PR：[#15](https://github.com/LAlongago/ScheduleAssistant/pull/15)，已合并；最终 main：`4bc82d600d4512d28508f06c8b92e0d10e5f9ee2`。
- 用户于 2026-09-23 确认 DEV-061 人工验收通过：新建并保存周期任务后重启仍能显示；每日/每周/每月/每年选项及月末策略说明符合预期；“仅当前实例”和“此后实例”的编辑/删除确认符合预期；周期模式对 Deadline、提醒和附件的禁用说明清楚，普通任务编辑未受影响。本次仅补充验收记录，没有额外代码变更。
- PR CI：`35858966830`（成功）；main CI：`35859161735`（成功，head SHA 为指定最终 main）。
- DEV-081 Windows 通知适配器已在 INTEGRATION-012 中整合；provider 不可用时 ReminderScheduler 仍休眠并保留 Pending 状态。
- 用户于 2026-09-23 确认 DEV-061 人工验收通过。PR #15 与 main CI 均成功；PR 和 main 验证细节见对应 CI 记录。来源分支与 worktree 保留。
- 最终 commit SHA 由交付消息报告。
