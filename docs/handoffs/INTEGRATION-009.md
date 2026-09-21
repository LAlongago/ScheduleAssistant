# INTEGRATION-009：整合 DEV-050 七列周历与 DEV-051 月历

## 任务范围

- 任务 ID：`INTEGRATION-009`。
- 整合分支：`integration/dev-050-dev-051-calendars`。
- 整合 worktree：`E:\Dev\Personal\Todo_list.worktrees\integration-dev-050-dev-051-calendars`。
- 基线：`main` / `f3bdacbf2f714507419e5746d48c63d1f77393ed`。
- 本次允许修改：整合分支中 DEV-050、DEV-051 的完整提交范围，以及本文件；若发现必要的整合回归，只在整合分支追加最小修复提交。
- 明确不在范围：来源分支和来源 worktree、`main`、Today、Upcoming Deadlines、任务编辑器的功能改造；共享顶部日期导航、搜索、附件、周期任务、通知和其他后续功能。

## 基线与来源核对

- 通过 GitHub ref API `https://api.github.com/repos/LAlongago/ScheduleAssistant/git/ref/heads/main` 核对远端 `main`，返回 SHA 仍为 `f3bdacbf2f714507419e5746d48c63d1f77393ed`。
- 本地 `origin/main` 跟踪引用未作为远端结论使用；直接 `git ls-remote` 在本机因 Schannel 凭据错误失败，随后使用上述远端 ref API 完成核对。
- `f3bdacbf2f714507419e5746d48c63d1f77393ed` 是两个来源 HEAD 的祖先：
  - `feat/dev-050-week-calendar` / `5cbeb7a7719f1281676352cd312ac7e6162bc6e4`：是基线后代；
  - `feat/dev-051-month-calendar` / `e89c92b38aa1f3fa7cb08b69e05dceb447d36762`：是基线后代。
- 两个来源 worktree 在整合前均干净，来源分支和 worktree 未被修改。

## 完整提交范围与整合顺序

从基线到来源 HEAD 的完整范围均已列出并按原顺序重放：

1. DEV-050：`5cbeb7a7719f1281676352cd312ac7e6162bc6e4` — `DEV-050: implement seven-column week calendar`。
2. DEV-051：`e89c92b38aa1f3fa7cb08b69e05dceb447d36762` — `DEV-051: add month calendar`。

按 DEV-050、DEV-051 顺序 cherry-pick 后，整合分支新增提交为：

- `734e3b9` — DEV-050 整合提交；
- `d0e898a` — DEV-051 整合提交。

## 冲突处理与代码审查

- 两个提交 cherry-pick 均无冲突；未 amend、rebase、清理或删除来源提交、分支或 worktree。
- Week 与 Month 的来源变更文件集合互不相交。Week 仅新增/修改 Week 页面、周历专用卡片、周日 ViewModel 和 Week 测试；Month 仅新增/修改 Month 页面、月格 ViewModel、日期详情查询 partial 和 Month 测试。
- 两页均通过 `ITaskQueries` / `ITaskUseCases` 等 Application 契约读取和完成任务；没有仓储、SQLite、Domain 或具体 Infrastructure 数据访问引用。数据库初始化和进程内事件总线继续使用 DEV-048 已存在的 Presentation 组合服务。
- `ScheduleAssistant.Architecture.Tests` 通过，DEV-048 的 Presentation → Application 边界未被破坏。
- 组合根继续以 singleton 注册 `WeekPageViewModel`、`MonthPageViewModel`、`TaskUseCases`、`ITaskQueries`、`InProcessEventBus` 和 `ITaskCardMapper`，未新增重复服务实例或重复事件订阅路径；主窗口构造图可解析两个真实页面。
- 周历保持周一至周日七列、单一外层垂直滚动、计划 `●`、Deadline `◆` 和同日双标记合并；周导航由页面自身提供。
- 月历保持固定 42 格、相邻月份日期弱化、每格最多三项和 `+N`；日期头和 `+N` 均打开完整日期详情；月导航由页面自身提供。
- 两页均保留 Loading/Empty/Error/Ready 状态、完成/取消完成命令、Application 事件驱动局部刷新；Today、Upcoming Deadlines 和任务编辑器没有被本次范围修改。
- 未实现共享顶部日期导航、搜索、附件、周期任务、通知或其他后续功能。

## 验证命令与结果

使用仓库约定 SDK：`E:\Dev\Tools\ScheduleAssistantDotnet\dotnet.exe`。

```powershell
& 'E:\Dev\Tools\ScheduleAssistantDotnet\dotnet.exe' build .\ScheduleAssistant.sln -c Release
```

- 通过：9 个项目，`0 warnings / 0 errors`。

```powershell
& 'E:\Dev\Tools\ScheduleAssistantDotnet\dotnet.exe' test .\tests\ScheduleAssistant.Presentation.Tests\ScheduleAssistant.Presentation.Tests.csproj -c Release --no-build --no-restore --list-tests --verbosity normal
```

- 通过测试发现；Week 测试类被发现 5 项，Month 测试类被发现 7 项。

```powershell
& 'E:\Dev\Tools\ScheduleAssistantDotnet\dotnet.exe' test .\tests\ScheduleAssistant.Presentation.Tests\ScheduleAssistant.Presentation.Tests.csproj -c Release --no-build --no-restore --verbosity minimal
```

- 通过：`32 passed / 0 failed / 0 skipped`。

```powershell
& 'E:\Dev\Tools\ScheduleAssistantDotnet\dotnet.exe' test .\tests\ScheduleAssistant.Architecture.Tests\ScheduleAssistant.Architecture.Tests.csproj -c Release --no-build --no-restore --verbosity minimal
```

- 通过：`4 passed / 0 failed / 0 skipped`。

```powershell
git diff --check f3bdacbf2f714507419e5746d48c63d1f77393ed..HEAD
```

- 通过，无 whitespace error。

WPF Release smoke：启动整合 worktree 的 `src\ScheduleAssistant.Presentation\bin\Release\net10.0-windows\ScheduleAssistant.exe` 后，进程拥有非零主窗口句柄，窗口标题为 `ScheduleAssistant`；调用 `CloseMainWindow()`，随后进程退出且未残留。Computer-use 窗口枚举在本机返回空并伴随浏览器连接错误，因此未伪造视觉验收结果；本次记录的是进程级启动与正常关闭证据。

## 待人工验收项与已知事项

- 需人工观察周历七列宽度、单一垂直滚动、计划/Deadline 双标记、周导航与完成交互。
- 需人工观察月历 6×7 布局、相邻月份弱化、三项截断/`+N`、日期详情和完成交互。
- 需人工确认 Week、Month 与 Today、Upcoming Deadlines、任务编辑器之间切换后状态和事件刷新体验。
- 顶部共享日期导航和搜索仍按 DEV-048/后续任务保持禁用；这不是本整合的回归。
- 已创建面向 `main` 的 [PR #13](https://github.com/LAlongago/ScheduleAssistant/pull/13)；当前远端 PR HEAD 为 `96435c7f75eb4cb3c1946e8b286df091bc314671`，目标基线为 `f3bdacbf2f714507419e5746d48c63d1f77393ed`。
- PR `build-and-test` CI 已通过（运行 `35579751026`，约 1 分 39 秒）；人工验收仍待完成。

## 最后提交 SHA

整合代码提交 SHA：`734e3b9`、`d0e898a`。本 handoff 将作为后续整合记录提交；为避免 handoff 自引用，最终包含本记录的提交 SHA 在交付时另行报告。

