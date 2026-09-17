# ADR-004: 桌面宿主与普通 Widget 回退

- Status: Proposed
- Date: 2026-09-17
- Owners: ScheduleAssistant integration owner
- Related task(s): SPIKE-002；后续 DEV-084

## Context

V1.0 需要可交互桌面看板，但 Windows 的 `WorkerW`/`Progman` 结构不是正式稳定的桌面宿主公共契约。窗口嵌入可能随 Explorer、Windows 版本、DPI、显示器和桌面 shell 状态变化；仅能显示而不能点击的方案不满足“桌面任务可勾选”。规格 FR-DESK-003/004 要求隔离风险、返回明确能力状态并在失败时保持窗口可见。

## Decision

SPIKE-002 采用独立宿主适配器探索 `WorkerW`，找不到或附着失败时再尝试 `Progman` 兼容路径。所有句柄发现、Win32 调用、父窗口切换、样式切换、恢复和显示区域修正都集中在 `DesktopHostService`；模拟任务 ViewModel 不接触句柄或 Win32。

默认方案建议为普通无边框 `WidgetFallback`：

- `Topmost=False`，不覆盖普通前台应用；
- 可拖动并可恢复到可见工作区；
- 明确提示这是普通小组件，不承诺固定在桌面底层；
- 不依赖 Explorer 的非正式层级结构。

嵌入必须是显式、可诊断的实验动作。宿主使用最多 3 次、间隔 0/250/750 ms 的有限重试，不进行 100 ms 轮询或持续强制重排。附着后先报告 `Embedded · 待交互验证`；只有复选框状态真实变化后才报告 `Embedded`。失败回退到 `WidgetFallback`；无法建立可见窗口才报告 `Unavailable`。

Explorer 重启通过 `TaskbarCreated` 消息触发一次恢复尝试；显示设置、DPI 和工作区变化通过窗口消息/系统参数事件处理。恢复失败不能隐藏窗口，必须回退 Widget。原型不自动杀死或重启 Explorer，不修改注册表、自启动、壁纸、主题或桌面图标设置。

## Alternatives considered

- 始终置顶窗口：交互简单，但会覆盖普通前台应用，不能代表桌面背景，违反本实验约束；拒绝。
- 只把窗口设为 `Progman` 子窗口：实现短，但未隔离 `WorkerW` 发现差异，且不能证明跨版本稳定；仅保留为兼容尝试。
- 动态壁纸/静态图片：可以显示但不能可靠承载 WPF 复选框交互，也超出本实验范围；拒绝。
- 仅普通 Widget：稳定、安全，是默认回退方案，但不验证桌面底层交互；保留为默认方案和强制回退。

## Consequences

### Positive

- 业务层与非正式 Windows shell 结构隔离；
- 失败时仍有可交互、可见、非置顶的窗口；
- `Embedded` 的语义包含真实复选框交互证据，不把“能显示”当成通过；
- 不会为了修复窗口层级而破坏 Explorer 或其他应用。

### Trade-offs and risks

- `WorkerW`/`Progman` 依赖非正式结构，不能承诺跨 Windows 版本稳定；
- 子窗口模式可能位于桌面图标视图下方，导致显示成功但鼠标命中失败；
- Explorer、DPI、显示器和虚拟桌面变化仍需要 Windows 手工矩阵验证；
- 后续 DEV-084 不能直接把本原型代码当正式公共服务，需要根据证据决定是否默认禁用嵌入。

## Validation plan and evidence

命令、目标系统、显示器/DPI 和场景矩阵记录在 [`SPIKE-002-desktop-host.md`](../test-reports/SPIKE-002-desktop-host.md)。原型构建使用仓库 `global.json` 的 .NET SDK 10.0.100。当前实验环境的实际 Windows 版本/build、显示器配置和无法执行的 Explorer/显示器破坏性测试以测试报告为准。

## Revisit criteria

若至少一个 Windows 10 和一个 Windows 11 环境均能证明嵌入后复选框可点击、图标不被阻挡、Explorer 重启可恢复且无永久丢窗，才可在 DEV-084 中重新评估嵌入作为可选模式。任一关键场景失败，正式实现应默认 Widget，并仅把嵌入保留为明确实验开关或移除。
