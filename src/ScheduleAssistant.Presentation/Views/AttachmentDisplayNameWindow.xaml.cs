using System.Windows;
namespace ScheduleAssistant.Presentation.Views;

/// <summary>Small themed dialog for changing an attachment display name.</summary>
public partial class AttachmentDisplayNameWindow : Window
{
    /// <summary>Initializes the rename dialog.</summary>
    public AttachmentDisplayNameWindow(string currentDisplayName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(currentDisplayName);
        InitializeComponent();
        DisplayNameTextBox.Text = currentDisplayName;
        DisplayNameTextBox.SelectAll();
        DisplayNameTextBox.Focus();
    }

    /// <summary>Gets the confirmed display name.</summary>
    public string SelectedDisplayName { get; private set; } = string.Empty;

    private void OnCancelClick(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }

    private void OnConfirmClick(object sender, RoutedEventArgs e)
    {
        var value = DisplayNameTextBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(value)
            || value.Any(character => char.IsControl(character) || character is '/' or '\\'))
        {
            MessageBox.Show(
                "显示名不能为空，且不能包含路径分隔符或控制字符。",
                "无法修改附件显示名",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            return;
        }

        SelectedDisplayName = value;
        DialogResult = true;
    }
}
