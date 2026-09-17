# INTEGRATION-001 交付记录

## 任务范围

- 任务编号：`INTEGRATION-001`。
- 允许修改：独立整合分支中的 SPIKE-001/SPIKE-002 原型及其 ADR、报告、handoff；本整合审查报告；`docs/specifications/DEV-010-domain-contracts.md`；必要的规格索引/事实更正。
- 明确不在范围：正式业务实体实现、仓储、数据库/迁移/表、ViewModel、正式通知/托盘/自启动、单实例 IPC、桌面宿主、UI 美化、推送、PR、远程合并和 main 修改。
- 整合策略：从共同基线创建 `integration/spikes-v1`，使用两个 `--no-ff` 本地合并；源分支和三个原有 worktree 均保留。

## 完成内容与关键设计选择

- 审查并整合 SPIKE-001 通知/发布原型和 SPIKE-002 桌面宿主原型。
- 保留实验项目不进入根解决方案；SPIKE-001 的 Windows App SDK 包版本只存在于实验目录的中央包文件中。
- 更正 build `26200`/25H2 的系统分类为 Windows 11 25H2；保留注册表 `ProductName=Windows 10 Pro` 和 `10.0.26200` 原始观测，并记录更正原因。
- 新增 DEV-010 领域契约文档，未创建正式领域代码、仓储、数据库或 ViewModel。
- 采用“原型交付完成”与“系统行为验收完成”双层结论：候选方向可以进入 DEV-081/DEV-084 设计，但未验证场景继续保留。

## 实际合并

| 来源 | 实际来源 HEAD | 整合提交 |
|---|---|---|
| SPIKE-001 | `2b87008311b06eed442cf4d41bfdce8f9b425979` | `7cd401f`（本地 `--no-ff` 合并） |
| SPIKE-002 | `9f67b23616cd14f6ab8d8b4d8c84dbc4aff9cc68` | `74c6c2d`（本地 `--no-ff` 合并） |

## 修改文件

- 两个 SPIKE 原型目录及其源分支提供的 ADR、测试报告、handoff；详见 `git diff main...HEAD --name-status`。
- `docs/adr/003-windows-notifications-and-publish-model.md`：补充 SPIKE-001 系统版本事实更正；`docs/adr/004-desktop-host-and-fallback.md` 按 SPIKE-002 原样整合，不覆盖原分支历史。
- `docs/specifications/DEV-010-domain-contracts.md`：领域语义细化和未决 DST 决策记录。
- `docs/test-reports/INTEGRATION-001-spikes.md`：本轮审查、命令、证据、待补验清单。
- `docs/handoffs/INTEGRATION-001.md`：本文件。

## 数据库与公共接口影响

- 未创建数据库、迁移、SQL、表、索引或用户数据目录。
- 未修改正式 `src/`、`tests/`、根解决方案、根中央包版本或 CI。
- 未新增或修改正式 `INotificationService`、`IDesktopHostService` 或 IPC 公共契约。
- DEV-010 文件是规格契约草案，不授权提前实现或建表。

## 验证命令与结果

完整记录见 [`docs/test-reports/INTEGRATION-001-spikes.md`](../test-reports/INTEGRATION-001-spikes.md)。本 handoff 只保留结论摘要：

- 环境：PowerShell 7.6.5、Git 2.55.0.windows.3、Windows 11 25H2 build 26200；使用既有用户级 .NET SDK 10.0.100，不安装系统工具。
- 远程 main：`2446979ad25be69af552c673c5458e9b48139320`；最近 CI run `35178286989` 成功。
- 根解决方案 `dotnet --info`、restore、Release build、全量 test：均退出码 0；SDK 10.0.100，8 项目构建 0 warning/0 error，4 个测试宿主共 7/7 通过。
- SPIKE-001 与 SPIKE-002 独立 restore/Release build：均退出码 0，均为 0 warning/0 error；没有用根 CI 替代。
- `git diff --check`：来源分支和当前工作区检查退出码 0；提交后再次复核。

## 待补验与后续依赖

- SPIKE-001：真实通知发送、通知点击/隐藏驻留、冷启动点击、通知关闭/注册失败、运行时部署/无运行时设备、独立 Windows 10 环境。
- SPIKE-002：真实复选框交互、桌面图标共存、Win+D、Explorer 重启、DPI/分辨率、多显示器、虚拟桌面和正常关闭。
- 上述平台行为通常不阻塞 DEV-010，但分别是 DEV-081 和 DEV-084 的验收条件。
- DEV-010 的 DST 无效/歧义本地时间政策影响完整领域基线；总控/产品确认后才能实现该分支。

## 最终结论

- 原型整合：完成（本地，未推送）。
- 系统行为验收：未完成；没有将构建或进程启动成功写成通知/桌面行为通过。
- DEV-010：可启动已明确的领域语义实现；DST 归一化子范围待确认。
- 远程推送、PR、合入 main：未执行，等待明确授权。

## Git 记录

- 共同基线：`2446979ad25be69af552c673c5458e9b48139320`。
- SPIKE-001 合并提交：`7cd401f`。
- SPIKE-002 合并提交：`74c6c2d`。
- 本 handoff 及整合审查文档的最终提交 SHA 在提交后由交付消息给出；不得把来源分支 SHA 当作整合分支 SHA。
