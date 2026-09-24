# VALIDATION-081-RUNTIME：Windows App Runtime 显式初始化复验

## 基线与范围

- 验证基线：正式 main 214cc1d1f0be63034b4d8ce886202397a7692c08；开始前和人工复验后 fetch，origin/main 与该 SHA 一致。
- 验证分支：validation/dev-081-runtime，从指定 main 创建；本轮只构建、运行 smoke 和更新验证文档，没有改应用代码或数据库。
- 复验对象：INTEGRATION-012 的 framework-dependent Windows App SDK Runtime 显式初始化路径，包版本仍为 Microsoft.WindowsAppSDK 2.4.0。

## 环境与程序

- 环境：Windows 11 专业版 x64，OS build 26200。
- Release solution build 的实际目标框架目录为 net10.0-windows10.0.17763.0。任务描述中的 net10.0-windows 简写目录没有生成；本次使用实际 framework-dependent 程序：
  src/ScheduleAssistant.Presentation/bin/Release/net10.0-windows10.0.17763.0/ScheduleAssistant.exe
- 构建命令：dotnet build .\ScheduleAssistant.sln --configuration Release --nologo。
- 结果：成功；9 个项目还原/构建完成，0 warnings、0 errors，用时约 23 秒。

## WPF 启动、单实例与正常关闭 smoke

使用 ScheduleAssistant__DataRoot 指向唯一临时目录，没有打开或修改默认用户数据库：

- 主进程在 30 秒内创建非零 WPF 主窗口句柄，主窗口标题非空。
- 进程名和可读窗口标题检查没有发现新的 Runtime/安装提示进程或窗口。
- 第二实例在 15 秒内退出，退出码 0；第二次启动后 ScheduleAssistant 业务进程数为 1。
- 主实例收到正常窗口关闭后在 15 秒内退出，退出码 0；剩余 ScheduleAssistant 进程数为 0。
- 临时数据目录已删除。Smoke 脚本无错误。

当前执行环境没有可用的原生应用截图/窗口枚举接口，因此提示窗口检查依据进程及窗口标题信息，并非屏幕截图目视；用户的人工复验覆盖实际桌面通知行为。

## 人工复验

2026-09-24，负责人回复“复验通过”。按该回复，以下三组均记录为通过：

| 场景 | 结果 |
| --- | --- |
| 实际通知 | 负责人确认中文任务标题、Deadline/剩余时间及“查看任务”操作正常显示。 |
| 运行中点击 | 负责人确认通知恢复主窗口并打开对应任务，只有一个业务进程。 |
| 冷启动点击 | 负责人确认完全退出后从通知中心点击，应用冷启动并打开对应任务，只有一个业务进程。 |
| 权限关闭与恢复 | 负责人确认关闭 Windows 通知权限后应用未挂起、Reminder 保持 Pending；重新允许并重启后调度继续并显示通知。 |

以上桌面行为来自负责人的人工结果反馈。本报告没有截图、测试任务 ID、逐步时间戳或进程列表作为独立证据；没有将其表述为自动观察结果。负责人表示普通任务只能勾选为已完成，界面没有删除入口，并确认使用默认数据目录。对默认数据库只读查询标题精确匹配 222、333、666 的记录，没有找到匹配；因此没有删除或修改任何任务，临时任务是否仍存在无法确认。

## 仍未验证的环境

- 全新且没有兼容 Windows App Runtime 的机器。
- 独立 Windows 10 环境。
- 以上结论仅覆盖 Windows 11 build 26200 的 framework-dependent 构建及负责人所报告的三组人工场景，不代表其他部署环境已验收。
