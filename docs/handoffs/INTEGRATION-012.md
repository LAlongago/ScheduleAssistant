# INTEGRATION-012：整合 DEV-081 Windows 原生通知

## 基线、来源与范围

- 正式 `main` 基线：`4bc82d600d4512d28508f06c8b92e0d10e5f9ee2`；开始前 fetch 后 `origin/main` 与该 SHA 一致。
- DEV-081 来源提交：`18a8ad7ba0a9ae9bc6c325f421a46754b15e2ee7`，父提交为指定基线。
- 整合分支：`integration/dev-081-windows-notifications`；worktree：`E:\Dev\Personal\Todo_list.worktrees\integration-dev-081-windows-notifications`。
- 基于正式 main 创建整合分支，以 `--no-ff` 完整合并 DEV-081；整合 merge commit：`4562ab39f53eb55b8fc223ad8165c617049de483`。
- 未修改 DEV-081 来源分支或来源 worktree。
- 本次补充更新 README、ADR-003 和 INTEGRATION-011 交付记录，并新增本 handoff。未改 Reminder 状态编码、数据库迁移、周期规则或既有调度补偿策略。

## 整合审查

| 要求 | 审查结果 |
| --- | --- |
| 注册顺序及退出 | `WindowsNotificationService` 先订阅 `NotificationInvoked` 再 `Register()`；仅注册成功后能力为可用。正常停止解除事件并调用 `Unregister()`；没有普通退出调用 `UnregisterAll()` 的路径。 |
| 能力与 Reminder 状态 | 能力查询结合注册状态、API/Runtime 支持与 Windows `AppNotificationSetting`。能力关闭或检测失败时 scheduler 休眠、不处理 Pending；只有实际调用可用 provider 后发送失败才记录 Failed。 |
| 通知内容 | Payload 使用 XML writer 转义文本，包含应用名、任务标题、Deadline/剩余时间和“查看任务”操作。 |
| 激活安全 | 解析器只接受白名单 `action=openTask` 与规范 GUID `taskId`；未知字段及 URI、文件、命令输入不进入执行路径。 |
| 运行中和冷启动路由 | 数据库初始化先于激活路由和通知服务；激活在主窗口可用后经 WPF Dispatcher 路由。无效参数或任务已删除时仅激活主窗口。 |
| 单实例与日志 | 第二实例只通知既有实例后退出。通知日志使用任务 ID、状态、错误码或异常类型，不记录标题、备注等任务正文。 |
| 依赖与边界 | 继续使用 ADR-003 确定的 `Microsoft.WindowsAppSDK` 2.4.0。提交未包含生成的发布内容；未修改 Reminder 状态码、迁移、周期逻辑或调度补偿策略。 |

## 人工验收记录

- 根据本次任务输入，DEV-081 的 x64 self-contained 发布和三组 Windows 人工验收已由负责人完成。本次整合未重复 publish，也未重做通知点击或权限切换测试。
- 三组已报告通过的范围：临近提醒通知显示中文标题及 Deadline；运行中与冷启动通知激活打开正确任务且保持单实例；关闭通知权限时 Reminder 保持 Pending，重新允许并恢复后继续调度。
- 本次自动验证不能观测通知中心展示和用户权限 UI。独立 Windows 10 与全新无 Windows App Runtime 的机器仍没有验证证据，详见 ADR-003。

## PR CI 首次结果与修正

- PR #16 首次 CI run `35948715049` 的 Release build 因 `NotificationActivationRouter` 的 CA1873 分析器诊断失败，GitHub Actions 未进入测试阶段。
- 在无任务可打开时的安全诊断日志外加 `_logger.IsEnabled(LogLevel.Information)` 检查，避免日志级别关闭时仍构造日志参数；不改变激活路由或日志字段。
- PR run `35949157645` 的两次尝试均通过 Release build，但 `dotnet test` 超过 20 分钟未结束。日志显示 Domain、Application、Infrastructure 通过；Architecture 和 Presentation 测试宿主均已启动但没有结果。
- 为保留所有 solution tests，将 CI 测试命令改为 `-m:1`。串行 PR run `35952566922` 通过 Release build，Domain 102/102、Application 44/44、Infrastructure 56/56 通过；Architecture 测试宿主启动后超过 20 分钟无结果，故取消。串行设置未解决该问题。
- Architecture 测试原先通过 `typeof(App).Assembly` 加载 WPF 主程序集。现改为直接读取各生产项目 `.csproj` 的 `ProjectReference` 声明；Architecture.Tests 改回 `net10.0` 并移除生产项目引用，以免测试宿主加载 WPF/Windows App SDK。修正后本地 Architecture.Tests 4/4 通过，Release solution build 0 warnings、0 errors。
- PR run `35954752620` 中 Architecture.Tests 4/4 已通过；Presentation.Tests 开始后超 20 分钟无结果，已取消。
- 为避免继续长时间等待，PR run `35957374357` 对单个测试设置 2 分钟挂起诊断并输出用例。Release build、Domain 102/102、Application 44/44、Infrastructure 56/56、Architecture 4/4 通过。Presentation 测试宿主中四个用例并发启动后均无结果；诊断超时并终止宿主。该证据指向 Presentation 测试并行运行时的宿主阻塞，尚不能据此判定具体测试用例本身失败。
- Presentation.Tests 增加程序集级串行配置，保留全部 55 项测试，避免 WPF Application/Dispatcher 全进程状态与其他测试类并行运行。本地重新执行 Release solution build（0 warnings、0 errors）和 Presentation.Tests（55/55）均通过。最终 PR/main CI 状态在交付回复中报告。

## 验证命令与结果

以下命令在整合 worktree 根目录执行，使用项目 SDK `10.0.100`：

```powershell
& 'E:\Dev\Tools\ScheduleAssistantDotnet\dotnet.exe' restore .\ScheduleAssistant.sln -m:1 -nr:false
& 'E:\Dev\Tools\ScheduleAssistantDotnet\dotnet.exe' build .\ScheduleAssistant.sln -c Release --no-restore -m:1 -nr:false
& 'E:\Dev\Tools\ScheduleAssistantDotnet\dotnet.exe' test .\tests\ScheduleAssistant.Application.Tests\ScheduleAssistant.Application.Tests.csproj -c Release --no-build --no-restore -m:1 -nr:false
& 'E:\Dev\Tools\ScheduleAssistantDotnet\dotnet.exe' test .\tests\ScheduleAssistant.Infrastructure.Tests\ScheduleAssistant.Infrastructure.Tests.csproj -c Release --no-build --no-restore -m:1 -nr:false
& 'E:\Dev\Tools\ScheduleAssistantDotnet\dotnet.exe' test .\tests\ScheduleAssistant.Presentation.Tests\ScheduleAssistant.Presentation.Tests.csproj -c Release --no-build --no-restore -m:1 -nr:false
& 'E:\Dev\Tools\ScheduleAssistantDotnet\dotnet.exe' test .\tests\ScheduleAssistant.Architecture.Tests\ScheduleAssistant.Architecture.Tests.csproj -c Release --no-build --no-restore -m:1 -nr:false
git diff --check
```

- Restore：成功，9 个项目。
- Release solution build：成功，0 warnings、0 errors。
- Application.Tests：44/44；Infrastructure.Tests：56/56；Presentation.Tests：55/55；Architecture.Tests：4/4；均为 0 failed、0 skipped。
- 关键覆盖来自合并后的测试：能力状态映射、通知 XML 转义、激活白名单、注册失败、通知关闭时 Pending 保留、运行中/冷启动路由及单实例组合注册。
在 Architecture.Tests 改为读取项目文件之前，为诊断远端挂起还运行了以下完整 solution test 命令，临时结果目录在完成后清理：

```powershell
$resultPath = Join-Path $env:TEMP ('ScheduleAssistant-INTEGRATION-012-tests-' + [Guid]::NewGuid().ToString('N'))
& 'E:\Dev\Tools\ScheduleAssistantDotnet\dotnet.exe' test .\ScheduleAssistant.sln --configuration Release --no-build --no-restore --nologo --logger 'console;verbosity=normal' --results-directory $resultPath
```

结果：退出码 0；Domain、Application、Infrastructure、Presentation、Architecture 项目测试均完成。
- `git diff --check`：通过。
- WPF Release smoke 使用 `ScheduleAssistant__DataRoot` 指向临时隔离目录。主窗口句柄就绪；第二实例 15 秒内以退出码 0 退出，进程数保持为 1；主窗口正常关闭后主进程以退出码 0 退出，进程数为 0。隔离目录已清理。
- Git tree 检查：DEV-081 来源提交和整合提交均不包含 `artifacts/`、`bin/`、`obj/` 或 self-contained 发布文件。

PR CI 在创建 PR 后运行；最终 PR/main CI 编号、合并 SHA 在交付回复中报告。

## 文件、接口与数据库影响

- DEV-081 的应用激活契约、基础设施 Windows App SDK 通知适配器、Presentation 启动和单实例路由，以及相应 Application、Infrastructure、Presentation、Architecture 测试均随来源提交整合。
- Architecture.Tests 直接检查项目文件中的引用方向，不加载生产程序集。
- CI 测试执行配置：`.github/workflows/ci.yml` 串行运行 solution 测试项目，添加 2 分钟挂起诊断和用例日志；Presentation.Tests 使用程序集级串行配置。
- 文档更新：`README.md`、`docs/adr/003-windows-notifications-and-publish-model.md`、`docs/handoffs/INTEGRATION-011.md`；本文件为新 handoff。
- `INotificationService` 的正式 provider 能力语义来自 DEV-081；无数据库、迁移、Reminder 状态编码或周期行为变更。
- 最终提交 SHA 无法写入此 handoff 自身并保持不变；由交付回复报告。
