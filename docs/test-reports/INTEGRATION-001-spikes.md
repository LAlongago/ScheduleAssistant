# INTEGRATION-001 SPIKE 整合验证报告

- 任务：`INTEGRATION-001`
- 整合分支：`integration/spikes-v1`
- 共同基线：`2446979ad25be69af552c673c5458e9b48139320`
- SPIKE-001 来源：`spike/spike-001-notifications`，实际 HEAD `2b87008311b06eed442cf4d41bfdce8f9b425979`
- SPIKE-002 来源：`spike/spike-002-desktop-host`，实际 HEAD `9f67b23616cd14f6ab8d8b4d8c84dbc4aff9cc68`
- 范围：只整合和审查技术原型、报告、ADR、契约文档；不实现正式业务功能。

## 1. 审查前置检查

| 项目 | 结果 |
|---|---|
| 根工作区 | `main` 在共同基线时干净；未覆盖用户修改 |
| SPIKE-001 工作区 | 干净；实际 HEAD 与上报 SHA 一致 |
| SPIKE-002 工作区 | 干净；实际 HEAD 与上报 SHA 一致 |
| 远程 main | `2446979ad25be69af552c673c5458e9b48139320`，与本地共同基线一致 |
| 最近 main CI | run `35178286989`，`success`；历史失败 run `35082941357` 属于更早的 DEV-001 修复前提交 |
| 合并冲突 | `git merge-tree` 未发现冲突；两个 SPIKE 文件范围不重叠 |
| 空白检查 | 两个来源分支 `git diff --check` 通过 |

两个实验均只新增各自 `spikes/SPIKE-00x/`、对应 ADR、测试报告和 handoff；没有修改正式 `src/`、`tests/`、`ScheduleAssistant.sln`、根 `Directory.Packages.props` 或 CI。SPIKE-001 的 `Directory.Packages.props` 位于实验目录内，仅固定 `Microsoft.WindowsAppSDK 2.4.0`；SPIKE-002 不引入实验包。没有发现提交的 `bin/`、`obj/`、发布产物、机器密钥或绝对路径配置。

## 2. 合并记录与审查结论

### SPIKE-001

- 以非快进合并保留源分支和原始提交历史；原型只发送固定模拟任务通知 `SPIKE-001-DEMO-TASK-42`。
- `AppNotificationManager`、通知能力诊断和单实例 Mutex/命名管道均被限制在 `spikes/SPIKE-001/`；实验管道不是正式 IPC 契约。
- 代码使用有限的连接超时和取消；服务等待管道连接，不进行全量高频轮询。正式实现仍必须增加版本化消息、长度限制和正式错误语义，不能直接复制实验类。
- 构建/进程启动证据只证明原型可构建或可启动，不证明通知发送、通知点击、冷启动点击、通知关闭或运行时部署已验收。
- 结论：`AppNotificationManager` 是后续 DEV-081 的候选实现方向；通知点击、冷启动点击、通知关闭和运行时部署继续列为待验收。

### SPIKE-002

- 以非快进合并保留源分支和原始提交历史；原型只使用进程内模拟任务。
- `DesktopHostService` 集中 WorkerW/Progman 发现、Win32 父子关系和恢复；ViewModel 不引用 Win32。附着最多 3 次，退避 0/250/750 ms，失败回退普通 `WidgetFallback`。
- 未发现重启/杀死 Explorer、修改注册表/壁纸/图标或持续高频轮询；恢复入口由 shell/display 消息触发并有单次 pending 门控。
- 构建/进程启动证据只证明原型壳可启动，不证明嵌入后复选框、桌面图标共存、Win+D、Explorer 重启、DPI 或多显示器行为。
- 结论：`WidgetFallback` 是后续 DEV-084 的首选方向；WorkerW/Progman 保持显式实验能力，不能宣称完整桌面背景交互已满足。

## 3. 系统标识更正

本机原始观测同时包含 `Environment.OSVersion=10.0.26200`、注册表 `ProductName=Windows 10 Pro`、`DisplayVersion=25H2` 和 `CurrentBuild=26200`。按 Windows build 语义，build `26200`/25H2 对应 Windows 11 25H2；`10.0` 是 Windows 兼容版本字符串，注册表产品字符串是保留的审计观测，不应覆盖 build 分类。因此本报告及整合后的 SPIKE-001 记录将该环境标为 **Windows 11 25H2**，并将独立 Windows 10 环境保留为未验证。

## 4. 当前环境

| 项目 | 实测值 |
|---|---|
| OS | Windows 11 25H2（build 26200，原始产品字符串为 Windows 10 Pro） |
| PowerShell | 7.6.5 Core |
| Git | 2.55.0.windows.3 |
| .NET host | 10.0.12；PATH 默认 host 无 SDK |
| 用户级 SDK | `E:\Dev\Tools\ScheduleAssistantDotnet`，SDK 10.0.100；使用现有 SDK，不安装或修改系统配置 |
| WPF/桌面 | 本机 Windows 环境具备，但原型行为验收仍依赖真实桌面手工场景 |

## 5. 本轮命令与结果

以下命令均从 `E:\Dev\Personal\Todo_list` 使用 PowerShell 7 执行；SDK 仅通过当前进程 PATH 指向既有用户级安装。环境变量没有写入系统配置。

### 根解决方案

```powershell
dotnet --info
dotnet restore .\ScheduleAssistant.sln
dotnet build .\ScheduleAssistant.sln -c Release --no-restore
dotnet test .\ScheduleAssistant.sln -c Release --no-build --no-restore
```

结果：通过，退出码 0。`dotnet --info` 显示 SDK `10.0.100`、MSBuild `18.0.2`、Windows Desktop Runtime `10.0.0`，`global.json` 被正确发现。

| 命令 | 退出码 | 实测结果 |
|---|---:|---|
| `dotnet --info` | 0 | SDK 10.0.100；host/runtime 10.0.0；RID `win-x64` |
| `dotnet restore .\ScheduleAssistant.sln` | 0 | 8 个项目 restore 成功或已是最新 |
| `dotnet build .\ScheduleAssistant.sln -c Release --no-restore` | 0 | 8 个项目；0 warning / 0 error |
| `dotnet test .\ScheduleAssistant.sln -c Release --no-build --no-restore` | 0 | 4 个测试宿主；7 passed / 0 failed / 0 skipped |

### 两个独立原型

```powershell
dotnet restore .\spikes\SPIKE-001\ScheduleAssistant.SPIKE001.Notifications.csproj
dotnet build .\spikes\SPIKE-001\ScheduleAssistant.SPIKE001.Notifications.csproj -c Release --no-restore
dotnet restore .\spikes\SPIKE-002\ScheduleAssistant.SPIKE002.csproj
dotnet build .\spikes\SPIKE-002\ScheduleAssistant.SPIKE002.csproj -c Release --no-restore
```

结果：两项均通过，均为 0 warning / 0 error；每项 restore 和 build 退出码均为 0。根 CI 不包含独立原型，因此本地分别执行了这两项 build。

| 原型 | Restore | Release build |
|---|---:|---:|
| SPIKE-001 `ScheduleAssistant.SPIKE001.Notifications.csproj` | 0 | 0 |
| SPIKE-002 `ScheduleAssistant.SPIKE002.csproj` | 0 | 0 |

### 非构建检查

- `git diff --check`：整合前来源分支和本轮工作区均通过，退出码 0；最终提交后再次执行。
- 变更范围：仅实验目录、ADR、README/索引、SPIKE 报告/handoff、DEV-010 契约和本整合记录；无正式业务实现。

## 6. 待补验清单

| 场景 | 当前证据 | 缺少的验证 | 影响后续任务 | 是否阻塞 DEV-010 | 完成条件 |
|---|---|---|---|---|---|
| 通知前台发送 | 原型代码、历史 Release build | 在已登录桌面点击发送并观察通知中心 | DEV-081 | 否 | 通知实际出现且结果可诊断 |
| 通知点击/隐藏驻留 | 回调和单实例代码；没有点击证据 | 隐藏窗口后点击通知并确认主实例定位 | DEV-081、DEV-082 | 否 | PID/实例不变且定位结果正确 |
| 通知冷启动点击 | 有解析/转发路径；无真实闭环 | 退出进程后点击已发通知并确认冷启动参数 | DEV-081 | 否 | 冷启动只创建一个实例并收到任务 ID |
| 通知关闭/注册失败 | 有异常捕获和诊断 | 系统关闭通知或运行时不可用时手工验证 | DEV-081 | 否 | 不崩溃，能力状态和降级清晰 |
| Windows App SDK Runtime/FDD/SCD | 历史 FDD/SCD 观察；无干净 VM | 在用户提供的无预装运行时机器验证安装/升级路径 | DEV-081、DEV-100 | 否 | 记录可复现部署步骤和失败恢复 |
| Widget 初始可见/拖动 | 原型代码与历史进程元数据 | 真实桌面确认可见、拖动、非 Topmost | DEV-084 | 否 | Widget 可操作且不覆盖普通前台应用 |
| WorkerW/Progman 附着交互 | 有限重试代码；无点击证据 | 附着后真实勾选并确认状态变化 | DEV-084 | 否 | 只有真实交互后才标记 Embedded |
| 桌面图标共存 | 未验证 | Widget 覆盖/邻近图标时分别点击 | DEV-084 | 否 | 两者均可命中且无永久丢窗 |
| Win+D/窗口切换 | 未验证 | 手工执行 Win+D 和前台切换 | DEV-084 | 否 | 看板/回退窗口仍可见可操作 |
| Explorer 重启 | TaskbarCreated 恢复代码；未执行重启 | 手工重启 Explorer，观察附着失败时回退 | DEV-084 | 否 | 恢复成功或明确进入 WidgetFallback |
| DPI/分辨率变化 | 消息处理和可见区修正代码 | 手工切换缩放/分辨率 | DEV-084 | 否 | 窗口回到可见工作区 |
| 多显示器/虚拟桌面 | 当前仅 1 个活动显示器 | 连接/断开第二显示器并切换虚拟桌面 | DEV-084 | 否 | 主显示器/回退状态不丢窗，证据留档 |
| DEV-010 DST 规则 | 规格要求明确规则但未给出具体政策 | 产品/总控确认无效和歧义本地时间处理 | DEV-010 | 是（仅 DST 归一化部分） | 决策写入契约/ADR并有边界测试 |

## 7. 整合结论

“原型交付完成”与“系统行为验收完成”分开记录：两个 SPIKE 的原型、隔离边界、候选方向和历史构建证据已整合；通知和桌面系统行为仍未完成正式验收。根解决方案的本轮真实 build/test 只有在本报告命令实际执行后才能判定，独立原型必须分别 build。

DEV-010：可以基于 `docs/specifications/DEV-010-domain-contracts.md` 开始不涉及 DST 归一化的领域实现和测试；完整 DEV-010 基线在 DST 政策确认前不能宣称无未决业务语义。SPIKE-001/002 的平台补验通常不阻塞 DEV-010，但必须成为 DEV-081/DEV-084 的适配器验收条件。
