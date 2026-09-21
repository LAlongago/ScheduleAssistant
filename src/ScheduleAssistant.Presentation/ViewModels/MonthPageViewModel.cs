using ScheduleAssistant.Presentation.Controls;

namespace ScheduleAssistant.Presentation.ViewModels;

/// <summary>Honest placeholder state for the future month grid.</summary>
public sealed class MonthPageViewModel : PageViewModelBase
{
    /// <summary>Initializes the month page placeholder without design-time task data.</summary>
    public MonthPageViewModel()
        : base(
            "月计划",
            "六行七列月历的紧凑任务呈现",
            PageContentState.Empty,
            "月计划功能保留为占位",
            "月历网格、日期详情和“+N”交互将在 DEV-051 接入；当前不会查询或写入数据。")
    {
    }
}
