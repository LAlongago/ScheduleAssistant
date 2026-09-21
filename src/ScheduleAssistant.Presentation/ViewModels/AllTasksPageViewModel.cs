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
            "全部任务搜索保留为占位",
            "关键词、类型、优先级和状态筛选将在后续查询页面开放；当前搜索框保持关闭。")
    {
    }
}
