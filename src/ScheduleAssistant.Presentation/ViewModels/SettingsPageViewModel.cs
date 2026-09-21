using ScheduleAssistant.Presentation.Controls;

namespace ScheduleAssistant.Presentation.ViewModels;

/// <summary>Honest placeholder state for the future settings surface.</summary>
public sealed class SettingsPageViewModel : PageViewModelBase
{
    /// <summary>Initializes the settings page placeholder.</summary>
    public SettingsPageViewModel()
        : base(
            "设置",
            "主题、关闭行为和提醒偏好的集中入口",
            PageContentState.Ready,
            "设置功能保留为占位",
            "自启动、托盘和提醒设置将在后续 Windows 任务接入；当前不修改系统设置。")
    {
    }
}
