using ScheduleAssistant.Presentation.Controls;

namespace ScheduleAssistant.Presentation.ViewModels;

/// <summary>Placeholder state for the future upcoming-deadline query.</summary>
public sealed class UpcomingDeadlinesPageViewModel : PageViewModelBase
{
    /// <summary>Initializes the upcoming-deadlines page placeholder.</summary>
    public UpcomingDeadlinesPageViewModel()
        : base(
            "即将截止",
            "按 Deadline 和紧迫度聚合待处理事项",
            PageContentState.Empty,
            "暂时没有可展示的 Deadline",
            "Deadline 查询将在 DEV-042 接入；这里不会伪造查询成功或写入提醒。")
    {
    }
}
