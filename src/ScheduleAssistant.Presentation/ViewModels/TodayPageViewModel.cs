using ScheduleAssistant.Presentation.Controls;

namespace ScheduleAssistant.Presentation.ViewModels;

/// <summary>Design-preview state for the Today page.</summary>
public sealed class TodayPageViewModel : PageViewModelBase
{
    /// <summary>Initializes the Today page with clearly marked temporary cards.</summary>
    public TodayPageViewModel()
        : base(
            "今天",
            "计划与 Deadline 的单日概览",
            PageContentState.Ready,
            "设计预览数据",
            "以下任务卡用于确认外壳层级和状态表达，尚未连接任务查询。")
    {
        Tasks =
        [
            new TaskCardViewModel(
                "整理文献综述提纲",
                "科研",
                "今天 · 14:00–16:00",
                "Deadline · 明天 23:59",
                "重要",
                "图书馆三层",
                "未开始 · 设计数据",
                CategoryAccent.Research),
            new TaskCardViewModel(
                "确认课程讨论时间",
                "课程",
                "今天 · 全天",
                "无 Deadline",
                "一般",
                "线上会议",
                "进行中 · 设计数据",
                CategoryAccent.Course)
        ];
    }

    /// <summary>Gets the temporary standard cards rendered by the Today page.</summary>
    public IReadOnlyList<TaskCardViewModel> Tasks { get; }
}
