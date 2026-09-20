# INTEGRATION-008 整合交接

## 范围与基线

- 任务：`INTEGRATION-008`，整合 DEV-041 与 DEV-042 的核心 UI。
- 基线：`origin/main` / `bf333614d4af03cfeb97fd2a2d15bb7674610bcd`。
- 来源：`feat/dev-041-task-editor` / `c8c6feaeb63995a784e9cdb00a34f829c7ba501d`；
  `feat/dev-042-today-deadlines` / `080f397e281cdd6193e9b78c1765baee1c551521`。
- 整合分支：`integration/dev-041-dev-042-core-ui`。
- 独立 worktree：`E:\Dev\Personal\Todo_list.worktrees\integration-dev-041-dev-042`。
- 允许范围：Presentation 组合根、任务编辑器与今天/Deadline 页面整合修复；相关 Presentation/Infrastructure 测试；README 与 handoff 文档。
- 明确不在范围：修改 DEV-041/DEV-042 来源分支或 worktree；修改 Domain/Application 公共契约；修改既有 `001` migration；实现 Week、Month、AllTasks、搜索、附件、周期或后续 Windows 集成功能。

## 完成内容与关键设计选择

- 先核对两条来源分支：均从指定基线直接增加 3 个提交，来源 worktree 均干净；共享组合根提交分别为 `2e8ff698095493c6d839ae0980dadd988913badb` 与 `ba1c47d03d839a795966c8fb0a6e3019a227d134`。
- 组合根保留一份 `TaskUseCases`，同时映射为 `ITaskUseCases` 与 `ITaskQueries`；保留一份 `InProcessEventBus`，同时映射为 `IApplicationEventPublisher`。
- `TimeProvider`、`TaskDeadlineResolver`、页面 ViewModel、Dispatcher、倒计时 timer 和编辑器服务均由同一 DI 图解析；`DatabaseInitialization` 是一次性 gate，hosted service 与查询型页面共用该 gate。
- 保留 DEV-041 的任务新建/编辑、校验、脏表单确认、乐观并发、Deadline/DST 选择与默认类型 seed；保留 DEV-042 的今天分组、同日计划/Deadline 单卡双标记、倒计时、范围筛选、完成/取消完成和事件局部刷新。
- 移除月页失效的设计任务卡数据，并清理外壳中的 DEV-040 设计预览/临时数据文案；Week、Month、AllTasks、搜索、附件和周期仍显示诚实占位，不伪造功能。
- 编辑器初始化、保存、重载和外壳新建命令捕获取消/异常，失败时保留编辑内容并显示安全提示；不记录任务正文。
- 人工启动发现任务卡首次渲染后闪退；Windows 事件记录确认为 `XamlParseException`，原因是 `TaskCard.xaml` 在内容之后声明 `CategoryColorConverter`，`StaticResource` 无法解析。已将资源声明移到内容之前，修复提交为 `76f1c21`。

## 修改文件与数据库/接口影响

- 修改 Presentation 组合根、数据库初始化 hosted service、MainWindow/占位页、任务编辑器异常路径、月页空状态及组合注册测试。
- 保留且整合 DEV-041/DEV-042 新增页面、控件、事件刷新和测试文件。
- 保留新增不可变 `002_seed_default_categories.sql`；未修改 `001_initial_schema.sql`，仅补充 migration 002 名称断言。
- 未修改 Domain/Application 公共契约、SQLite 表结构或仓储 SQL。

## 验证命令与结果

以下结果在整合 worktree 记录；SDK 解析使用工作区已配置的
`E:\Dev\Tools\ScheduleAssistantDotnet\dotnet.exe`（系统 `dotnet` 未安装项目要求的 10.0.100 SDK）。

- `& 'E:\Dev\Tools\ScheduleAssistantDotnet\dotnet.exe' restore .\ScheduleAssistant.sln`：通过。
- `& 'E:\Dev\Tools\ScheduleAssistantDotnet\dotnet.exe' build .\ScheduleAssistant.sln -c Release --no-restore`：通过，0 警告、0 错误。
- `& 'E:\Dev\Tools\ScheduleAssistantDotnet\dotnet.exe' test .\tests\ScheduleAssistant.Presentation.Tests\ScheduleAssistant.Presentation.Tests.csproj -c Release --no-build --no-restore`：通过，19 passed / 0 failed / 0 skipped。
- `& 'E:\Dev\Tools\ScheduleAssistantDotnet\dotnet.exe' test .\tests\ScheduleAssistant.Infrastructure.Tests\ScheduleAssistant.Infrastructure.Tests.csproj -c Release --no-build --no-restore`：通过，25 passed / 0 failed / 0 skipped。
- `git diff --check`：通过。
- WPF 进程级冒烟：通过；`ScheduleAssistant.exe` 启动后获得主窗口句柄 `2885202`，`Responding=True`，`CloseMainWindow()` 正常关闭，退出码 0，未残留 `ScheduleAssistant` 进程。当前没有可用的窗口观察通道，未宣称视觉通过。
- 针对人工发现的闪退修复后回归：使用现有 `schedule.db` 启动并等待 12 秒，主窗口保持响应并正常退出（退出码 0）；最近 5 分钟无新的 `.NET Runtime`/Application Error；Presentation.Tests 19 passed、Infrastructure.Tests 25 passed。

## PR、CI 与人工验收门禁

- 推送与 PR：已推送整合分支并创建 [PR #12](https://github.com/LAlongago/ScheduleAssistant/pull/12)，只推送了 `integration/dev-041-dev-042-core-ui`。
- PR CI：首次运行失败，失败步骤为 `ScheduleAssistant.Architecture.Tests.DependencyDirectionTests.Presentation_ShouldDependOnApplicationAndInfrastructure`；[Actions run 35496602126](https://github.com/LAlongago/ScheduleAssistant/actions/runs/35496602126)。Application、Domain、Infrastructure、Presentation 和新增 Presentation 测试均已通过，失败只发生在架构依赖断言。
- 人工验收：尚未完成。必须在真实 WPF 窗口中验证新建/编辑/保存失败保留内容、脏表单关闭、完成/取消完成、倒计时刷新和重启持久化；不可观察时不得声称视觉通过。

## 未完成项与已知问题

- Week、Month、AllTasks、搜索、附件、周期和 Windows 集成功能仍是明确占位。
- Git 直接实时查询最初因 Git Credential Manager 无凭据失败；随后确认 GitHub CLI 已登录并配置 Git 凭据，推送前后均核对 `origin/main` 为指定基线。
- CI 阻断：现有 Application DTO 公共字段使用 Domain 枚举，Presentation 因消费这些字段产生 Domain 程序集引用；Presentation 项目文件仍只直接引用 Application/Infrastructure。移除该程序集边缘需要修改 Application 公共契约或增加不诚实的重复/反射适配；本任务未进行，也未放宽架构测试约束。需由用户确认后续架构方向。
- 需要人工复测：新建合法任务保存后卡片应正常渲染；重启加载已有任务不应空白或闪退。
- 人工验收和最终合并后的 main CI 尚未执行。

## 最后提交 SHA

- 核心整合合并提交：`3be052408d02eac50f95074807348f9a579f168d`。
- 任务卡闪退修复提交：`76f1c21`。
- 后续 PR、人工验收和合并后的文档更新提交将在同一 handoff 继续补录。
- Git 元数据存在；不使用伪造 SHA。
