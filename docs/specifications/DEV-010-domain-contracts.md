# DEV-010 领域契约细化

- 任务：`INTEGRATION-001` 为 `DEV-010` 准备契约基线
- 状态：Draft for DEV-010 implementation review
- 依据：`ScheduleAssistant_V1_Development_Specification.md` 第 3、4、8、9、12、14 节
- 范围：仅把现有规格中的领域语义、边界和职责写成可测试的契约；本文件不创建实体、仓储、数据库、迁移或 ViewModel。

## 1. 不变的范围与未决事项

DEV-010 只实现 Domain 与 Domain.Tests。它不负责持久化、Windows、WPF、文件系统、DI、通知、托盘或调度器适配器。

规格第 8.2 节要求对 DST 无效或歧义的本地时间“提示并采用明确规则”，但没有规定具体规则。这是会影响用户可见 Deadline 语义的未决项，不能由实现会话静默选择。建议由产品/总控确认以下候选政策后再冻结：

- 无效本地时间：拒绝归一化并要求用户修改；
- 歧义本地时间：要求用户选择偏移，或明确采用较早/较晚偏移；
- 确认后的规则应进入后续 ADR，并覆盖 Domain/Application 测试。

除该 DST 决策外，本文件对规格已有含义作工程化细化，不增加 V1 功能。

## 2. 领域对象与值对象

### 2.1 实体

| 名称 | 身份与语义 | DEV-010 关注的核心不变量 |
|---|---|---|
| `TaskItem` | 普通任务和已物化的周期实例；`Id` 创建后不变 | 标题、计划时间、Deadline、状态、完成时间和乐观版本一致 |
| `Category` | 任务分类及其颜色/排序元数据 | 名称、颜色和归档状态满足分类规则；任务只能引用有效分类 |
| `RecurrenceSeries` | 周期任务的公共字段快照和规则模板 | 规则、发生窗口、时区和启用状态自洽 |
| `Reminder` | 一个任务的相对提醒节点及交付状态 | 调度 UTC、交付状态和去重键一致；同一任务未来可有多个提醒 |
| `Attachment` | 受管附件的元数据，不包含二进制内容 | 相对路径、任务归属和显示名满足附件边界 |

周期实例仍是 `TaskItem`，完成实例不得改变系列或其他实例。删除/物化排除记录属于 DEV-020/DEV-060 的持久化协作，不能在 DEV-010 中提前建表。

### 2.2 值对象

| 名称 | 内容 | 规则 |
|---|---|---|
| `ZonedDeadline` | 本地日期、本地墙钟时间、Windows 时区 ID、归一化 UTC 时间点 | 本地输入与时区必须共同存在；UTC 是计算结果，不替代用户输入；DST 规则待确认 |
| `PlannedTimeRange` | 可选计划开始/结束 `TimeOnly` | 只有存在 `PlannedDate` 时才允许；结束时间不得早于开始时间 |
| `RecurrenceRule` | 频率、星期掩码、月日或月/日、规则有效范围及时区 | 只接受 V1 已列出的每日/每周/每月/每年规则，间隔固定为 1 |
| `OccurrenceDate` | 周期实例的本地 `DateOnly` 发生日 | 使用纯日期，不携带时区或时间点 |

`DateOnly`、`TimeOnly`、`DateTimeOffset` 等 .NET 类型表达上述语义，但类型本身不是业务规则的替代品。

## 3. 枚举与稳定取值

以下整数值是为后续持久化和跨层交换预先固定的候选编码；UI 文案可以本地化，不能改变代码含义。当前没有数据库，因此 DEV-020 建表前如发现兼容性问题必须通过新的决策记录调整，不能悄悄重排。

### 3.1 持久化工作流与优先级

| 枚举 | 稳定值 | 含义 |
|---|---:|---|
| `WorkflowStatus.Pending` | 0 | 尚未开始；由用户行为设置 |
| `WorkflowStatus.InProgress` | 1 | 用户已开始处理；由用户行为设置 |
| `WorkflowStatus.Completed` | 2 | 用户已完成；由用户行为设置，并应有 `CompletedAtUtc` |
| `TaskPriority.Low` | 0 | 低优先级 |
| `TaskPriority.Normal` | 1 | 一般 |
| `TaskPriority.Important` | 2 | 重要 |
| `TaskPriority.UrgentAndImportant` | 3 | 紧急且重要 |

优先级编码按规格的“降序排序”约定使数值越大越优先；它不改变类型颜色或 Deadline 紧迫度。

### 3.2 周期频率

| 枚举 | 稳定值 | 含义 |
|---|---:|---|
| `RecurrenceFrequency.Daily` | 0 | 每日 |
| `RecurrenceFrequency.Weekly` | 1 | 每周一个或多个星期 |
| `RecurrenceFrequency.Monthly` | 2 | 每月指定日期 |
| `RecurrenceFrequency.Yearly` | 3 | 每年指定月/日 |

### 3.3 派生展示状态

`DisplayStatus` 由当前时间和任务字段计算，不写入任务数据库，也不能由用户直接设置。其语义名称为：`Completed`、`Overdue`、`InProgress`、`PlannedPast`、`NotStarted`。`Pending` 只属于工作流状态，展示层的“未开始”使用 `NotStarted` 避免混淆。

计算顺序必须固定：

1. `WorkflowStatus == Completed` → `Completed`；
2. `DeadlineUtc < NowUtc` → `Overdue`；
3. `WorkflowStatus == InProgress` → `InProgress`；
4. `PlannedDate < TodayLocal` → `PlannedPast`；
5. 其他 → `NotStarted`。

严格使用“小于”判断逾期：Deadline 恰好等于 `NowUtc` 时尚未进入 `Overdue`，但会落在“到点”紧迫度边界。

## 4. 日期、墙钟时间与 UTC

| 语义 | Domain 表达 | 处理规则 |
|---|---|---|
| 纯本地日期 | `DateOnly` | 计划日期和周期发生日期；序列化为 ISO `YYYY-MM-DD`，不转换 UTC |
| 本地墙钟时间 | `TimeOnly` | 计划开始/结束；依附计划日期，不独立转换 UTC，不支持跨午夜的隐式解释 |
| 绝对时间点 | `DateTimeOffset` | 创建、更新、完成、提醒触发/交付统一使用 UTC |
| Deadline | `ZonedDeadline` | 保存用户本地日期时间和 Windows 时区 ID，同时保存归一化 UTC；比较和调度使用 UTC，展示使用本地输入/当前区域 |

Domain 规则接收显式的 `NowUtc`/`TodayLocal` 或等价时间上下文，不直接读取系统时钟。Application 负责用注入的 `TimeProvider` 取得当前时间，并把时区转换结果传给 Domain。

## 5. 字段校验边界

### 5.1 Domain 必须拒绝

- `Title` 去除首尾空白后为空，或长度不在 1～200 字符范围；保存的标题应使用去除首尾空白后的值。
- `Id`、`CategoryId`、`SeriesId`（出现时）不符合其 Guid/引用边界；Category 是否存在由 Application 查询确认。
- 存在计划开始或结束时间但没有 `PlannedDate`。
- 同时有开始和结束时间且结束早于开始。
- 字段超过规格上限：`Location` 300；`Description`、`Materials`、`Notes` 各 10,000 字符。
- `WorkflowStatus == Completed` 但完成时间状态无法保持一致，或非完成状态带有不应保留的完成时间。

### 5.2 Domain 允许、Application 必须提示或编排

- Deadline 早于计划日期：规格允许，但 UI/Application 必须做非阻断式确认；Domain 不把它当作格式错误。
- Category 是否为有效、未归档分类：需要读取分类目录，属于 Application/持久化边界。
- Deadline 本地时间到 UTC 的时区/DST 归一化：Application 负责提示、确认和转换；DST 规则未决，禁止隐式转换。
- 创建/更新/完成时间的实际生成：Application 通过 `TimeProvider` 提供，Domain 不调用系统时钟。

## 6. 周期规则与日期策略

V1 仅支持间隔为 1 的每日、每周指定星期、每月指定日期、每年指定月/日。规则的生效/结束日期均为本地 `DateOnly`；规则命中和实例发生日不携带时间。

- 每月 29/30/31 日在当月不存在该日时落在当月最后一天；因此每月 31 日在 2 月落到 2 月最后一天。
- 每年 2 月 29 日在非闰年落到 2 月最后一天。
- 月末策略是规则语义，不是 UI 猜测；必须用固定时钟/显式日期测试。
- 周规则使用星期掩码；跨年只改变日期年份，不改变星期含义。
- 周期实例可以单独完成；实例覆盖不修改系列。具体物化窗口、唯一约束、删除排除和“此后未完成实例”事务属于 DEV-020/DEV-060。

## 7. Deadline 紧迫度边界

紧迫度只对存在 Deadline 的未完成任务计算。令 `remaining = DeadlineUtc - NowUtc`：

| 条件 | 等级 | 语义 |
|---|---:|---|
| `remaining > 7 days` | 0 | 中性 |
| `3 days < remaining <= 7 days` | 1 | 黄色提示 |
| `1 day <= remaining <= 3 days` | 2 | 橙色提示 |
| `0 < remaining < 1 day` | 3 | 红色提示 |
| `remaining == 0` | 3 | 到点但尚未满足严格 `<` 逾期条件 |
| `remaining < 0` | 4 | 已逾期，深红色并显示“已逾期” |

这样明确了 7、3、1、0 和负数的边界：恰好 7 天为等级 1，恰好 3 天为等级 2，恰好 1 天为等级 2，恰好 0 为等级 3，负数为等级 4。已完成任务不进入紧迫度列表；无 Deadline 返回“无紧迫度”，不是人为制造一个等级。

## 8. Domain 与 Application 职责边界

### Domain

- 保存实体和值对象的不变量；执行状态展示、字段校验、优先级/紧迫度和周期日期等纯规则。
- 只接收显式输入和时间上下文；不引用 WPF、SQLite、DI、文件系统、Registry、Win32 或系统时钟。
- 不检查数据库中的 Category 是否存在，不负责事务、事件发布、通知或文件复制。

### Application

- 编排用例、输入 DTO、错误分类、Category/Series 引用解析、事务边界和提交后事件。
- 通过注入的 `TimeProvider` 提供当前 UTC/本地日期；负责 Deadline 时区转换、DST 提示/确认和“Deadline 早于计划日期”的非阻断确认。
- 调用仓储和端口，但不把仓储类型泄露给 View；不实现 SQLite、WPF、Win32 或具体通知/桌面适配器。
- 把 Domain 错误映射为 `Validation`、`NotFound`、`Conflict` 等规格要求的可判别结果。

## 9. DEV-010 实现门禁

DEV-010 可以从上述已明确规则开始实现实体、值对象、枚举、纯规则和边界测试；但在实现 DST 本地时间归一化前，必须由总控/产品确认第 1 节的未决政策。此文件不授权提前创建数据库表、迁移、仓储接口或 ViewModel。
