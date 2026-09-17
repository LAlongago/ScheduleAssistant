# ADR-002：SQLite 数据访问与迁移

- Status: Accepted
- Date: 2026-09-17
- Owners: ScheduleAssistant 工程
- Related task(s): DEV-020；后续 DEV-030、DEV-060、DEV-080、DEV-090

## Context

ScheduleAssistant 需要一个本地优先、可长期演进的 SQLite 数据层。规格要求使用 `Microsoft.Data.Sqlite + Dapper`，短生命周期连接、WAL、外键、事务、参数化 SQL、版本化迁移和真实临时文件集成测试。DEV-010 已冻结领域 `Rehydrate` 入口、UTC/本地日期时间语义以及仓储负责版本推进的职责边界；Reminder 的数据库编码仍需在本任务中正式锁定。

## Decision

1. Infrastructure 使用 `Microsoft.Data.Sqlite` 建立 SQLite 连接，使用 Dapper 执行显式列名和参数化 SQL。每次操作打开独立短生命周期连接，不共享全局可变连接。每个连接启用 `foreign_keys=ON`、`journal_mode=WAL`、`synchronous=NORMAL` 和 5 秒 `busy_timeout`。
2. `AppPaths` 接收可注入的应用根目录；数据库固定在 `<root>/data/schedule.db`，附件、备份、日志和设置目录由同一服务解析。`SqliteDatabaseInitializer.InitializeAsync` 是显式初始化入口，不在连接工厂或 DI 注册时隐式创建数据库。
3. `Persistence/Migrations/*.sql` 是嵌入资源。迁移文件使用三位顺序编号和不可变文件名，例如 `001_initial_schema.sql`。`schema_migrations` 记录 `version`、`name`、SHA-256 `checksum` 和 UTC `applied_at_utc`。所有待执行迁移在一个 SQLite 事务中应用；失败回滚；已应用迁移的名称或校验值变化、或数据库含有当前程序未知的高版本迁移时拒绝启动。禁止删库重建或修改已应用迁移。
4. 持久化编码固定如下：

   - `Guid` 使用小写 `D` 格式文本；`DateOnly` 使用 `YYYY-MM-DD`；
   - `TimeOnly` 使用 `HH:mm:ss.fffffff` 文本，不使用分钟整数；
   - UTC 时间点使用 `yyyy-MM-dd'T'HH:mm:ss.fffffff'Z'`，入口和重建均规范化为零偏移；
   - Deadline 同时保存本地日期时间文本、Windows 时区 ID 和已解析 UTC；Infrastructure 不解析时区、不实施 DST 政策；
   - `WorkflowStatus`、`TaskPriority`、`RecurrenceFrequency` 和星期掩码沿用 DEV-010：分别为 `0/1/2`、`0/1/2/3`、`0/1/2/3`，星期位为周一 `1` 至周日 `64`、`All=127`；
   - ReminderStatus 正式锁定为 `Pending=0`、`Delivered=1`、`Expired=2`、`Cancelled=3`、`Failed=4`；
   - `DisplayStatus` 与 Deadline 紧迫度是派生值，不写入数据库；系列 Deadline 模板和 DST 转换后置。

5. 初始 schema 建立任务、分类、周期系列、提醒、附件、周期排除、`settings`、`cleanup_queue` 和迁移表，并用显式索引/约束保证：分类和系列被任务引用时禁止物理删除；任务删除只级联其提醒和附件元数据；系列删除不级联任务，保护已完成历史；同一系列同一发生日的实例唯一；同一系列同一发生日的排除记录唯一；Reminder 去重键全局唯一，同时不对 `task_id` 建唯一约束，因此同一任务可有多个不同提醒节点。
6. Application 只暴露领域实体、仓储、`IPersistenceTransaction`/工厂和 `PersistenceCommitResult`，不暴露 SQLite 类型。仓储写入支持独立事务或调用方提供的跨仓储事务。更新使用 `expectedVersion` 条件更新；成功后设置 `version + 1` 并从数据库通过 `Rehydrate` 返回新实体，冲突不覆盖现有行。

## Alternatives considered

- Entity Framework Core 或重量级追踪 ORM — 与规格的 Dapper/显式 SQL 选择不符，放弃。
- 一个由所有仓储共享的长生命周期连接 — 会放大线程、锁和关闭失败风险，放弃；跨仓储一致性通过不泄漏 provider 类型的事务端口完成。
- 以 JSON、分钟数或本地机器默认时区保存时间 — 会丢失 `TimeOnly` tick、本地输入或时区语义，放弃。
- 分类/系列 `ON DELETE CASCADE` — 可能误删仍被引用的任务和已完成周期历史，放弃；使用外键拒绝并交给后续 Application 用例编排迁移/停用。
- FTS5 和同步搜索表 — 规格要求先用参数化 `LIKE`，待性能证据后另立 ADR，暂不引入。

## Consequences

### Positive

- 数据库从空目录可重建，迁移有可审计校验值，失败不会留下半套 schema。
- 领域时间和元数据可无损往返；所有实体重建仍经过 Domain 不变量。
- SQLite 约束承担关键防线，事务入口可支持周期物化、系列操作和提醒计划的后续原子编排。
- 临时目录真实文件测试不会接触用户数据库。

### Trade-offs and risks

- 迁移 SQL 是公共数据契约，发布后只能追加新编号；修改旧文件会触发校验失败。
- Deadline 的本地时间到 UTC 转换和 DST 无效/歧义政策仍由后续 Application 决定。
- Reminder 全局去重依赖调用方生成稳定 key；后续若需要不同作用域必须新增迁移和决策。
- 当前未实现设置/清理队列管理、备份、周期物化、附件文件复制或提醒调度。

## Validation plan and evidence

- 使用 .NET SDK `10.0.100`、Windows x64 和真实临时 SQLite 文件运行 DEV-020 Infrastructure 集成测试。
- 覆盖空库迁移/重复初始化、重开无损读取、乐观并发、Deadline-only 范围查询、外键/周期/排除/提醒约束及跨仓储事务回滚。
- 迁移校验、WAL 和连接外键状态由测试直接读取 SQLite 元数据验证。

## Revisit criteria

出现 FTS5 性能证据、需要改变时间/DST 归一化政策、Reminder 去重作用域变化、需要远程/加密数据库，或需要兼容已发布 schema 的破坏性调整时，必须以新的 ADR 和追加迁移取代或扩展本决定。
