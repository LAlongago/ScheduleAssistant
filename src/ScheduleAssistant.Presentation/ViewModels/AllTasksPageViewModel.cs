using ScheduleAssistant.Presentation.Controls;

namespace ScheduleAssistant.Presentation.ViewModels;

/// <summary>Placeholder state for the future all-tasks search view.</summary>
public sealed class AllTasksPageViewModel : PageViewModelBase
{
    /// <summary>Initializes the all-tasks page placeholder.</summary>
    public AllTasksPageViewModel()
        : base(
            "全部任务",
            "按关键词、类型、优先级和状态筛选",
            PageContentState.Empty,
            "任务搜索尚未接入",
            "搜索框保持禁用；DEV-031 的查询端口将在后续页面任务中接入。")
    {
    }
}
