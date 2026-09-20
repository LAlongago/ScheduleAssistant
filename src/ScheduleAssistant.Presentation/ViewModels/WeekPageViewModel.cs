using ScheduleAssistant.Presentation.Controls;

namespace ScheduleAssistant.Presentation.ViewModels;

/// <summary>Placeholder state for the future seven-column week view.</summary>
public sealed class WeekPageViewModel : PageViewModelBase
{
    /// <summary>Initializes the week page placeholder.</summary>
    public WeekPageViewModel()
        : base(
            "周计划",
            "周一至周日的七列计划视图",
            PageContentState.Empty,
            "周计划功能保留为占位",
            "日期聚合和周历卡片将在 DEV-050 接入；当前不会查询或写入数据。")
    {
    }
}
