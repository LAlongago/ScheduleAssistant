using ScheduleAssistant.Presentation.Controls;

namespace ScheduleAssistant.Presentation.ViewModels;

/// <summary>Design-preview state for the future settings surface.</summary>
public sealed class SettingsPageViewModel : PageViewModelBase
{
    /// <summary>Initializes the settings page placeholder.</summary>
    public SettingsPageViewModel()
        : base(
            "设置",
            "主题、关闭行为和提醒偏好的集中入口",
            PageContentState.Ready,
            "设置占位",
            "当前只展示设计结构；自启动、托盘和提醒设置由后续 Windows 任务接入。")
    {
    }
}
