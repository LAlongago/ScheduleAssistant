# INTEGRATION-010 整合交付记录

## 任务范围与门禁

- 任务 ID：`INTEGRATION-010`，整合 DEV-060 周期系列与物化、DEV-070 附件存储与 UI。
- 指定远端基线：`main` / `2d79de8054f16d97111669f57058204bd5ecbda7`。
- 远端基线核对由总控通过 GitHub REST API 完成：
  `https://api.github.com/repos/LAlongago/ScheduleAssistant/branches/main`。
- 本地整合分支：`integration/dev-060-dev-070`。
- 整合 worktree：`E:\Dev\Personal\Todo_list.worktrees\integration-dev-060-dev-070`。
- worktree 直接从精确基线提交创建；创建前确认基线对象存在，当前起点为指定 SHA。
- Git 客户端因 Schannel 凭据错误 `SEC_E_NO_CREDENTIALS (0x8009030e)` 无法刷新 `origin/main`。
  本地 `origin/main = bf333614…` 为陈旧引用，本轮没有移动、伪造或强制更新它。
- 已确认来源 HEAD：
  - DEV-060：`feat/dev-060-recurrence` / `90c3b6fa065e15952d04c514ded0181cf4502234`；
  - DEV-070：`feat/dev-070-attachments` / `bfe9cdb58c5a5179aa910035b54182ab58576c4f`。
- 两个来源 HEAD 均通过 `git merge-base --is-ancestor` 确认为指定基线后代。

## 允许修改与明确不在范围

允许范围为两个来源提交实际涉及的 Application、Infrastructure、Presentation 组合接线、对应测试、ADR/handoff 和本次整合说明；冲突解析只发生在整合分支。

明确不在范围：来源分支及来源 worktree、`origin/main` 引用、既有数据库迁移、Deadline 推导、DST 墙钟策略、DEV-061 周期编辑 UI、通知/托盘/启动适配器、覆盖率、性能测试和无关 Domain 全量回归。

## 完整来源范围与整合结果

在读取完整规格、`AGENTS.md`、`README.md`、DEV-060/DEV-070 handoff 和 ADR-006 后，分别列出并按 DEV-060、DEV-070 顺序整合完整范围：

| 来源 | 完整范围 | 提交数量 | 整合后的提交 |
|---|---|---:|---|
| DEV-060 | `2d79de8054f16d97111669f57058204bd5ecbda7..90c3b6fa065e15952d04c514ded0181cf4502234` | 1 | `5f6c427` |
| DEV-070 | `2d79de8054f16d97111669f57058204bd5ecbda7..bfe9cdb58c5a5179aa910035b54182ab58576c4f` | 1 | `a70b4c2` |

来源提交主题分别为：

- `90c3b6fa...` — `DEV-060: implement recurrence series materialization`；
- `bfe9cdb5...` — `DEV-070: add managed attachment storage and editor UI`。

没有只按最终 SHA 假定范围；`git rev-list --count` 对两个范围均为 1，并逐项 cherry-pick。
来源分支和来源 worktree 未修改、未 rebase、未 amend、未清理或删除。

## 冲突与整合修复

DEV-070 cherry-pick 在以下两个组合注册文件冲突：

- `src/ScheduleAssistant.Infrastructure/Composition/InfrastructureServiceCollectionExtensions.cs`
- `src/ScheduleAssistant.Presentation/Composition/PresentationServiceCollectionExtensions.cs`

解析结果：

- Infrastructure 同时保留 DEV-060 的周期排除/任务 provider-neutral mapping，并采用 DEV-070 的附件元数据 mapping、清理队列和 `ManagedAttachmentStore` 注册；没有用裸 SQLite 实现覆盖 Application 端口。
- Presentation 同时保留周期与附件命名空间和注册；`InProcessEventBus`、`TaskUseCases`、`RecurrenceMaterializer`、数据库初始化和 HostedService 没有重复注册。
- `IRecurrenceMaterializer` 与 `ITransactionalRecurrenceMaterializer` 都解析到同一个 singleton `RecurrenceMaterializer`。
- HostedService 顺序为 `DatabaseInitializationHostedService` 后 `AttachmentMaintenanceHostedService`；后者还显式等待同一个 `IDatabaseInitialization`，保证迁移完成并完成默认周期物化后才进行附件队列重试和孤儿诊断。

## 语义审查

### 周期、事务与事件

- 启动路径为数据库迁移完成 → 默认窗口周期物化 → 附件清理队列有限重试及一次孤儿诊断。
- 默认周期物化窗口为本地今天前 31 天至后 400 天；日、周、月查询仅在按需查询范围调用物化器，没有新增高频轮询。
- 物化器使用 singleton 进程内门闩和数据库 `series_id + occurrence_date` 唯一约束，重复/并发窗口物化幂等。
- 周期实例删除先在事务中写排除记录并删除实例，`CommitAsync` 完成后才发布 `TaskDeleted`。
- 修改系列和删除未来实例会在提交后为实际删除的普通实例发布 `TaskDeleted`，随后发布系列变更事件。
- 周期用例的 post-commit 发布失败只返回成功结果上的 `RefreshRequired`，不把已提交事务映射成数据库回滚。

### 附件、删除与补偿

- 普通任务删除、单个周期实例删除和未来周期实例删除均通过提交后的 `TaskDeleted` 触发任务附件目录清理。
- 文件删除失败进入既有 `cleanup_queue`；队列重试失败保持可诊断状态，不伪装成数据库回滚。
- 附件元数据由任务外键级联删除；附件清理订阅者不参与数据库事务，也不尝试回滚已提交任务删除。
- 启动仅处理有限队列项目并做一次孤儿诊断，不注册持续扫描计时器。
- 源文件始终保留；Presentation 只使用 Application 附件用例和 UI interaction port，不直接访问仓储、SQLite、文件系统或 Win32。

### UI、边界与迁移

- 任务编辑器附件导入/打开/定位/重命名/移除通过 `IAttachmentUseCases`，新建任务支持保存后逐个导入并保留部分失败状态。
- 周期编辑区域仍为禁用的 DEV-061 占位，不提前实现周期 UI 或规则写入。
- Presentation 源码没有直接引用 Domain；ViewModel 没有直接引用仓储、SQLite、文件系统或 Windows API。
- 既有迁移文件未修改；数据库仍只保存附件元数据，不保存附件字节。
- 未新增 Deadline 推导，未改变 DST 墙钟策略。
- ADR-006 已保留，并明确普通数据库备份不等价于附件字节备份；后续完整备份必须把数据库和受管附件目录作为一致性范围。

## 变更文件与接口/数据库影响

- 文档：`README.md`、`docs/adr/006-managed-attachments-and-backup-boundary.md`、`docs/handoffs/DEV-060.md`、`docs/handoffs/DEV-070.md`、本文件。
- Application：事件总线与 `TaskDeleted`/周期事件；周期系列、排除、物化器和用例契约；附件用例、文件存储端口、清理队列端口、维护服务；任务查询按需物化接线。
- Infrastructure：附件文件系统适配器、附件/清理队列仓储与 provider-neutral mapping；周期仓储 mapping、幂等物化插入和未来实例删除；组合注册。
- Presentation：数据库初始化接入物化器；附件维护 HostedService；任务编辑器附件 interaction port、ViewModel partial、XAML 和显示名对话框；组合注册。
- Tests：周期物化真实临时 SQLite 测试；附件导入/补偿/清理队列/中文长文件名/路径安全测试；任务编辑器附件和组合注册测试。
- 数据库：无迁移新增或修改；沿用既有 `attachments`、`cleanup_queue`、周期表和唯一约束。
- 公共接口：新增的 Application 端口不暴露 SQLite/provider 类型；未向 `ITaskUseCases` 添加附件方法。

## 验证命令与结果

使用仓库既有 SDK `E:\Dev\Tools\ScheduleAssistantDotnet\dotnet.exe`（10.0.100）。未运行覆盖率、性能测试或无关 Domain 全量测试。

```powershell
& 'E:\Dev\Tools\ScheduleAssistantDotnet\dotnet.exe' restore .\ScheduleAssistant.sln
```

- Restore：通过。

```powershell
& 'E:\Dev\Tools\ScheduleAssistantDotnet\dotnet.exe' build .\ScheduleAssistant.sln -c Release --no-restore
```

- Release solution build：通过，9 个项目，`0 warnings / 0 errors`。

```powershell
& 'E:\Dev\Tools\ScheduleAssistantDotnet\dotnet.exe' test .\tests\ScheduleAssistant.Application.Tests\ScheduleAssistant.Application.Tests.csproj -c Release --no-build --no-restore
& 'E:\Dev\Tools\ScheduleAssistantDotnet\dotnet.exe' test .\tests\ScheduleAssistant.Infrastructure.Tests\ScheduleAssistant.Infrastructure.Tests.csproj -c Release --no-build --no-restore
& 'E:\Dev\Tools\ScheduleAssistantDotnet\dotnet.exe' test .\tests\ScheduleAssistant.Presentation.Tests\ScheduleAssistant.Presentation.Tests.csproj -c Release --no-build --no-restore
& 'E:\Dev\Tools\ScheduleAssistantDotnet\dotnet.exe' test .\tests\ScheduleAssistant.Architecture.Tests\ScheduleAssistant.Architecture.Tests.csproj -c Release --no-build --no-restore
```

- Application.Tests：`33 passed / 0 failed / 0 skipped`。
- Infrastructure.Tests：`37 passed / 0 failed / 0 skipped`。
- Presentation.Tests：`34 passed / 0 failed / 0 skipped`。
- Architecture.Tests：`4 passed / 0 failed / 0 skipped`。

测试发现核对：

- Application.Tests 发现附件导入成功/失败补偿、清理队列、`TaskDeleted` 维护测试；
- Infrastructure.Tests 发现 `RecurrenceMaterializationTests` 7 项，覆盖月末、并发幂等、默认窗口、排除、更新/未来删除和事务回滚；同时发现受管附件、路径安全和清理队列测试；
- Presentation.Tests 发现新建任务逐个附件导入失败、编辑加载及打开/定位/重命名/移除测试。

```powershell
git diff --check
```

- 通过；仅有 Git 对冲突解析文件换行格式的工作树提示，无 whitespace error。

WPF Release 启动/正常关闭 smoke：

- 启动：`src/ScheduleAssistant.Presentation/bin/Release/net10.0-windows/ScheduleAssistant.exe`；
- `MainWindowHandle` 非零：`3671452`；窗口标题：`ScheduleAssistant`；`Responding=True`；
- `CloseMainWindow=True`；15 秒内正常退出；退出码 `0`；未使用强制终止；
- 启动日志确认 `AttachmentMaintenance Completed 0 0`，并出现正常 Host shutdown。

PR CI 首次运行：

- Run：`35623810364` / [PR #14 CI](https://github.com/LAlongago/ScheduleAssistant/actions/runs/35623810364)。
- `Release build` 首次失败，GitHub runner 的更新分析器报告 `CA1873`，位置为
  `AttachmentMaintenanceHostedService.cs:54` 的启动诊断日志调用；测试步骤因此未执行。
- 整合分支仅增加该 HostedService 的局部 `CA1873` 分析器说明，未改变启动、清理、事件或数据库语义。
- 修复后的最小本地验证：Presentation build `0 warnings / 0 errors`；Presentation.Tests `34 passed / 0 failed / 0 skipped`；`git diff --check` 通过。

## 未完成、已知问题与后续依赖

- PR 已创建为 [#14](https://github.com/LAlongago/ScheduleAssistant/pull/14)，首次 CI 失败原因和局部分析器修复已记录；修复后的新 HEAD 仍需推送并等待最新 CI。
- 尚未进行用户人工验收；在人工验收通过前不得合并。
- Computer Use 视觉通道此前不可用，因此 smoke 只证明 WPF 进程启动、窗口句柄和正常关闭，不把它表述为完整视觉验收。
- DEV-061 周期编辑 UI、备份/恢复、Windows 通知/托盘/启动等仍为后续任务。

## 最后提交 SHA

本文件及 README 状态说明将在本整合分支的后续整合提交中提交；该提交 SHA 在交付消息中报告。之后每次 PR/人工验收/CI 文档更新均应记录更新前的整合 HEAD，并在最终交付消息报告最终 main SHA。
