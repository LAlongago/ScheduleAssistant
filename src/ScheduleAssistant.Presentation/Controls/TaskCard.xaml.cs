using System.Windows.Controls;

namespace ScheduleAssistant.Presentation.Controls;

/// <summary>
/// Reusable standard/compact task card. It only binds display fields and a future completion command.
/// </summary>
public partial class TaskCard : UserControl
{
    /// <summary>Initializes the task card.</summary>
    public TaskCard()
    {
        InitializeComponent();
    }
}
