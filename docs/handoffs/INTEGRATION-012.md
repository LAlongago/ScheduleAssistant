# INTEGRATION-012：整合 DEV-081 Windows 原生通知

## 基线、来源与范围

- 正式 main 基线：4bc82d600d4512d28508f06c8b92e0d10e5f9ee2；开始前 fetch 后 origin/main 与指定 SHA 一致。
- DEV-081 来源：18a8ad7ba0a9ae9bc6c325f421a46754b15e2ee7，父提交为指定基线；未修改来源分支或 worktree。
- 整合分支：integration/dev-081-windows-notifications；在正式 main 上以 --no-ff 完整合并 DEV-081，整合 merge commit 为 4562ab39f53eb55b8fc223ad8165c617049de483。
- 本任务允许整合来源代码、修正直接阻断 CI 的问题，并更新 CI、README、ADR-003、INTEGRATION-011 与本 handoff。未修改 Reminder 状态编码、数据库迁移、周期规则或调度补偿策略；未重复 publish、安装或通知人工操作。

## 整合审查

| 要求 | 审查结果 |
| --- | --- |
| 注册顺序及退出 | WindowsNotificationService 先订阅 NotificationInvoked 再 Register()；仅注册成功后能力为可用。正常停止先解除事件，再 Unregister()，最后释放成功初始化的 Runtime；普通退出不调用 UnregisterAll()。 |
| 能力与 Reminder 状态 | 能力查询结合注册状态、API/Runtime 支持及 Windows 通知设置。Runtime 不可用、注册失败或通知关闭时 provider 不可用，scheduler 休眠且 Pending 保持不变；只有实际调用可用 provider 后发送失败才记录 Failed。 |
| 通知内容 | Payload 用 XML writer 转义任务标题，包含应用名、任务标题、Deadline/剩余时间和“查看任务”操作。 |
| 激活安全 | 解析器仅接受白名单 action=openTask 和规范 GUID taskId；URI、文件或命令输入不进入执行路径。 |
| 激活路由 | 运行中和冷启动均在应用及数据库初始化后经 WPF Dispatcher 打开任务。无效参数或任务已删除时仅恢复主窗口。 |
| 单实例与日志 | 第二实例通知已有实例后退出；日志仅记录任务 ID、状态、错误码、异常类型或 HRESULT，不记录任务正文。 |
| 依赖与生成物 | 继续使用 ADR-003 的 Microsoft.WindowsAppSDK 2.4.0。Git tree 不包含 artifacts/、bin/、obj/ 或 self-contained 发布文件。 |

## PR CI 诊断与修正

- PR #16 的初次 run 35948715049 因 CA1873 分析器诊断使 Release build 失败；为相关安全诊断日志添加级别检查后修复。
- Run 35949157645 和 35952566922 的 Release build 成功，但测试宿主挂起。串行 MSBuild 未解决问题。Architecture 测试原先加载 WPF 主程序集，现改为读取项目文件的引用声明；run 35954752620 中 Architecture 4/4 通过，Presentation 仍挂起。
- Run 35957374357 加入每个测试宿主 2 分钟挂起诊断后，Domain 102/102、Application 44/44、Infrastructure 56/56、Architecture 4/4 通过，Presentation 测试宿主无完成用例并超时。尝试串行 Presentation 测试；run 35957858347 中第一个用例仍超时，故撤回程序集串行配置。
- 对照已通过的 main CI 35859161735，runner image 相同。DEV-081 引入的 Windows App SDK 2.4.0 对未打包 WinExe 默认启用自动 bootstrap；包内模块初始化器在找不到匹配 Runtime 时可能显示无人可操作的 UI。Presentation 测试在加载 WPF 主程序集时挂起与此路径吻合；这是根据包源码和 CI 现象作出的原因推断，须由后续 CI 结果验证。
- 禁用 Windows App SDK 的自动 bootstrap；framework-dependent 进程由通知 provider 显式调用不显示 UI 的 Bootstrap.TryInitialize。失败返回 notification.runtime-unavailable，避免阻塞启动并保持 Reminder Pending。成功时在解除通知注册后调用 Bootstrap.Shutdown。RID self-contained 构建继续使用 SDK 的自包含路径。未升级依赖。
- CI 继续运行完整 solution 测试，串行 MSBuild，并保留详细用例日志和 2 分钟测试宿主挂起诊断。最终 PR/main CI 编号和结论由交付回复报告。

## 人工验收与限制

- 按任务输入，DEV-081 的 x64 self-contained 发布及三组 Windows 人工验收已由负责人完成：中文通知、标题与 Deadline；运行中/冷启动点击打开任务且单实例；关闭通知权限时 Pending 保留，重新允许并恢复后继续调度。
- 本次整合未重复 publish、安装、通知点击或权限切换。显式 Runtime 初始化修正后的实际通知展示、无 Runtime 的全新机器和独立 Windows 10 环境未被人工观察，不能声明这些场景通过。

## 验证命令与结果

在整合 worktree 根目录，使用项目 SDK 10.0.100：

~~~powershell
& 'E:\Dev\Tools\ScheduleAssistantDotnet\dotnet.exe' restore .\ScheduleAssistant.sln -m:1 -nr:false
& 'E:\Dev\Tools\ScheduleAssistantDotnet\dotnet.exe' build .\ScheduleAssistant.sln -c Release --no-restore -m:1 -nr:false
& 'E:\Dev\Tools\ScheduleAssistantDotnet\dotnet.exe' test .\ScheduleAssistant.sln -c Release --no-build --no-restore --nologo --logger trx --results-directory .\TestResults -m:1 --blame-hang --blame-hang-timeout 2m --blame-hang-dump-type none
git diff --check
~~~

- Restore 成功，9 个项目；Release solution build 成功，0 warnings、0 errors。
- 完整 solution test 成功：Domain 102/102、Application 44/44、Infrastructure 58/58、Presentation 55/55、Architecture 4/4；均为 0 failed、0 skipped。新增测试覆盖 Runtime 初始化失败及异常、注册失败后的释放顺序。
- WPF Release smoke 使用临时 ScheduleAssistant__DataRoot：主窗口就绪；第二实例退出码 0，业务进程数保持 1；正常关闭后主进程退出码 0，剩余进程数 0。隔离数据目录已清理。
- git diff --check 通过；Git 跟踪文件检查没有 artifacts/、bin/、obj/、TestResults/ 或发布压缩包。

## 文件、接口与数据库影响

- DEV-081 的应用契约、通知适配器、启动与单实例路由及相关测试完整整合。
- CI 阻断修正涉及 .github/workflows/ci.yml、Architecture.Tests、通知服务与 Runtime 接口/实现、Infrastructure/Presentation 项目文件及 Infrastructure.Tests；撤回未经证实的 Presentation 串行配置。
- 文档更新包括 README.md、ADR-003、INTEGRATION-011 及本 handoff。
- 通知 Runtime 接口为 Infrastructure 内部接口；无数据库、迁移或 Reminder 公共状态语义变更。
- 最终整合提交 SHA 与合并后 main SHA 由交付回复报告，避免为回填 SHA 单独建立文档 PR。
