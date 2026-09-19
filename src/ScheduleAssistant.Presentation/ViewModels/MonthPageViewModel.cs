using ScheduleAssistant.Presentation.Controls;

namespace ScheduleAssistant.Presentation.ViewModels;

/// <summary>Design-preview state for the future month grid.</summary>
public sealed class MonthPageViewModel : PageViewModelBase
{
    /// <summary>Initializes the month page with compact-card examples.</summary>
    public MonthPageViewModel()
        : base(
            "月计划",
            "六行七列月历的紧凑任务呈现",
            PageContentState.Ready,
            "紧凑卡片预览",
            "月历网格、日期详情和“+N”交互将在 DEV-051 接入。")
    {
        CompactTasks =
        [
            new TaskCardViewModel(
                "提交报销材料",
                "行政",
                "9月21日 · 09:00",
                "◆ 9月30日截止",
                "重要",
                string.Empty,
                "计划",
                CategoryAccent.Administration,
                TaskCardMode.Compact),
            new TaskCardViewModel(
                "组会",
                "会议",
                "9月23日 · 15:00",
                "● 计划",
                "一般",
                string.Empty,
                "计划",
                CategoryAccent.Meeting,
                TaskCardMode.Compact)
        ];
    }

    /// <summary>Gets compact cards reserved for month-cell rendering.</summary>
    public IReadOnlyList<TaskCardViewModel> CompactTasks { get; }
}
