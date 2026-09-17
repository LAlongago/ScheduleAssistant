# DEV-010 Domain 验证报告

## 范围与环境

- 任务：`DEV-010`。
- 分支：`feat/dev-010-domain`。
- Worktree：`E:\Dev\Personal\Todo_list-dev-010-domain`。
- 正式基线：`29efb76bc20cff5b9f57e0044683c676c591df15`。
- Windows build：`10.0.26200`；PowerShell 7 环境。
- SDK：使用既有 `E:\Dev\Tools\ScheduleAssistantDotnet`，SDK `10.0.100`；没有安装或升级 SDK/包。
- 本轮未修改中央包版本或项目依赖。

每条 dotnet 命令均在当前 PowerShell 进程显式设置以下任务环境变量后执行，未修改系统环境：

```powershell
$scheduleAssistantDotnetRoot = 'E:\Dev\Tools\ScheduleAssistantDotnet'
$env:PATH = "$scheduleAssistantDotnetRoot;$env:PATH"
$env:DOTNET_ROOT = $scheduleAssistantDotnetRoot
$env:DOTNET_CLI_HOME = Join-Path $env:TEMP 'ScheduleAssistantDotnetCliHome-Dev010'
$env:NUGET_PACKAGES = Join-Path $env:TEMP 'ScheduleAssistantNuGetPackages-Dev010'
$env:APPDATA = Join-Path $env:TEMP 'ScheduleAssistantAppData-Dev010'
```

## 未修改 worktree 基线验证

首次在沙箱权限下执行时，SDK 信息成功（退出码 `0`），但 restore/build/test 因独立 worktree 的 `obj/bin` 写权限被拒绝，分别得到退出码 `1/1/1`；`git diff --check` 为 `0`。该次不是代码验证结果。

随后在同一明确任务 worktree 以受控写权限按原命令重跑，结果如下：

| 命令 | 退出码 | 实际结果 |
|---|---:|---|
| `dotnet --info` | 0 | SDK 10.0.100，x64，global.json 被发现 |
| `dotnet restore .\ScheduleAssistant.sln` | 0 | 8 个项目还原成功 |
| `dotnet build .\ScheduleAssistant.sln -c Release --no-restore` | 0 | 8 个项目，0 警告 / 0 错误 |
| `dotnet test .\ScheduleAssistant.sln -c Release --no-build --no-restore` | 0 | 4 个宿主，7/7 通过，0 failed / 0 skipped |
| `git diff --check` | 0 | 通过 |

## 实现后验证

### Domain 专项

```powershell
dotnet test .\tests\ScheduleAssistant.Domain.Tests\ScheduleAssistant.Domain.Tests.csproj -c Release --no-restore
```

退出码 `0`；Domain 测试宿主真实启动，`100 passed / 0 failed / 0 skipped`。

### 最终全量验证

```powershell
dotnet --info
dotnet restore .\ScheduleAssistant.sln
dotnet build .\ScheduleAssistant.sln -c Release --no-restore
dotnet test .\ScheduleAssistant.sln -c Release --no-build --no-restore
git diff --check
```

实际分步结果：

| 命令 | 退出码 | 实际结果 |
|---|---:|---|
| `dotnet --info` | 0 | SDK 10.0.100 |
| `dotnet restore .\ScheduleAssistant.sln` | 0 | 所有项目为最新 |
| `dotnet build .\ScheduleAssistant.sln -c Release --no-restore` | 0 | 8 个项目，0 警告 / 0 错误 |
| `dotnet test .\ScheduleAssistant.sln -c Release --no-build --no-restore` | 0 | 4 个宿主共 106 passed（Domain 100、Application 1、Infrastructure 1、Architecture 4），0 failed / 0 skipped |
| `git diff --check` | 0 | 通过 |

测试宿主均真实启动；没有用未启动宿主的 `0 failed` 作为通过依据。未运行 WPF 人工验收或 SPIKE 平台场景，因为它们明确不在 DEV-010 范围内。未收集 coverlet 覆盖率，覆盖率状态为未测量。

## 覆盖的行为

- 标题空白、trim 后空标题、1/200/201 边界；地点 300/301；Description、Materials、Notes 10,000/10,001 边界。
- 无计划日期的开始/结束时间、单端点、相等和结束早于开始；计划日期与 Deadline 独立。
- 空 Guid、非法优先级/工作流/提醒状态、周期身份不一致、完成状态与完成时间不一致、版本下界。
- 新建与重建入口、字段更新候选原子性、开始/完成/取消完成、重复操作和完成后不可直接开始。
- DisplayStatus 全优先级、Deadline 到点和前后、显式 TodayLocal、取消完成后重新逾期。
- 紧迫度等级 7/3/1/0 天及两侧边界、完成/无 Deadline 的 `None`。
- 已解析 ZonedDeadline 的本地字段、时区 ID 和 UTC 保留；未实现也未测试 DST 转换。
- 每日、每周单日/多日/跨年、星期掩码无效输入、规则有效范围交集、查询两端、排序、去重。
- 每月 29/30/31 在长短月份和闰年中的月末策略；每年 2 月 29 日和其他非法年月日。
- DateOnly 最小/最大边界的安全停止；无数据库物化或无限生成路径。
- Category、RecurrenceSeries、Reminder、Attachment 元数据不变量和受管相对路径安全。

## 静态边界复核

- Domain 源码未引用 WPF、SQLite、DI、文件系统、Registry、Win32、`TimeZoneInfo` 或系统时钟 API。
- 生产文件保持职责单一；`TaskItem` 的公共行为与候选校验拆为两个文件，未修改 Architecture.Tests。
- 未发现新增 SQL、迁移、数据库目录、通知、托盘、自启动、IPC、ViewModel 或 UI 文件。
- 构建生成的 `bin/`、`obj/` 等输出未纳入提交。

## 未完成与后续依赖

- DST 无效/歧义本地时间政策仍需产品/总控确认；DEV-010 不实现本地时间到 UTC 的转换。
- DEV-020 需要使用 Rehydrate 入口实现映射、提交时版本推进、并发检查、SQLite 约束和真实临时数据库测试。
- DEV-030 负责 TimeProvider、Category/Series 引用查询、错误映射和 Deadline 的 DST/非阻断确认编排。
- DEV-060 负责周期物化、唯一约束、排除记录、事务和系列变更；系列 Deadline 相对发生日模板政策仍未冻结。
- Reminder 状态数值是本轮内部选择，正式数据库契约需另行确认。

## Git 记录

- 基线：`29efb76bc20cff5b9f57e0044683c676c591df15`。
- 实现提交：`b4083f857937c27748ba65b5dc9fa8615fdec6fe`。
- 文档最终提交 SHA 以最终交付消息中的 `git rev-parse HEAD` 为准，不在自身内容中自引用。
