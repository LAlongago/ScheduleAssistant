using System.Collections;
using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ScheduleAssistant.Application.Common;
using ScheduleAssistant.Application.Tasks;
using ScheduleAssistant.Domain;
using ScheduleAssistant.Presentation.Composition;

namespace ScheduleAssistant.Presentation.ViewModels;

/// <summary>Identifies whether an editor creates or updates an ordinary task.</summary>
public enum TaskEditorMode
{
    Create,
    Edit
}

/// <summary>Input context used by the reusable task-editor service.</summary>
public sealed record TaskEditorRequest(
    TaskEditorMode Mode,
    Guid? TaskId = null,
    DateOnly? PrefilledPlannedDate = null)
{
    /// <summary>Creates a request for a new ordinary task.</summary>
    public static TaskEditorRequest Create(DateOnly? prefilledPlannedDate = null)
    {
        return new TaskEditorRequest(TaskEditorMode.Create, PrefilledPlannedDate: prefilledPlannedDate);
    }

    /// <summary>Creates a request for an existing ordinary task.</summary>
    public static TaskEditorRequest Edit(Guid taskId)
    {
        if (taskId == Guid.Empty)
        {
            throw new ArgumentException("A task identity is required.", nameof(taskId));
        }

        return new TaskEditorRequest(TaskEditorMode.Edit, taskId);
    }
}

/// <summary>One selectable reminder offset displayed by the editor.</summary>
public sealed record ReminderOffsetOption(int Value, string Label);

/// <summary>One localized priority choice displayed by the editor.</summary>
public sealed record TaskPriorityOption(TaskPriority Value, string Label);

/// <summary>
/// MVVM state for creating and editing ordinary tasks. It only calls Application ports and never
/// accesses repositories, SQLite, files, or Windows APIs.
/// </summary>
public sealed partial class TaskEditorViewModel : ObservableObject, INotifyDataErrorInfo
{
    private const int DefaultReminderOffsetMinutes = -1_440;

    private readonly ITaskUseCases _taskUseCases;
    private readonly TimeProvider _timeProvider;
    private readonly ITaskEditorInteractionService _interactionService;
    private readonly TaskDeadlineResolver _deadlineResolver;
    private readonly TaskEditorRequest _request;
    private readonly List<CategoryOptionDto> _categoryOptions = [];
    private readonly Dictionary<string, IReadOnlyList<string>> _errors = new(StringComparer.Ordinal);

    private bool _isApplyingValues;
    private bool _isInitialized;
    private bool _isBusy;
    private bool _isDirty;
    private bool _hasConflict;
    private bool _closeApproved;
    private bool _deadlineWarningAcknowledged;
    private bool _reminderPlanTouched;
    private readonly bool _attachmentsEnabled;
    private readonly bool _recurrenceEnabled;
    private DateTimeOffset? _confirmedDeadlineUtc;
    private long _expectedVersion;
    private string _title = string.Empty;
    private Guid _selectedCategoryId;
    private TaskPriority _selectedPriority = TaskPriority.Normal;
    private DateTime? _plannedDateValue;
    private string _plannedStartText = string.Empty;
    private string _plannedEndText = string.Empty;
    private bool _hasDeadline;
    private DateTime? _deadlineDateValue;
    private string _deadlineTimeText = string.Empty;
    private string _timeZoneId;
    private bool _reminderEnabled = true;
    private int _reminderOffsetMinutes = DefaultReminderOffsetMinutes;
    private string _location = string.Empty;
    private string _description = string.Empty;
    private string _materials = string.Empty;
    private string _notes = string.Empty;
    private string? _errorMessage;
    private string? _warningMessage;
    private PostCommitEventStatus _lastPostCommitEventStatus = PostCommitEventStatus.NotAttempted;

    /// <summary>Initializes task-editor state for one create or edit request.</summary>
    public TaskEditorViewModel(
        ITaskUseCases taskUseCases,
        TimeProvider timeProvider,
        ITaskEditorInteractionService interactionService,
        TaskEditorRequest request,
        TaskDeadlineResolver? deadlineResolver = null,
        string? localTimeZoneId = null)
    {
        _taskUseCases = taskUseCases ?? throw new ArgumentNullException(nameof(taskUseCases));
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
        _interactionService = interactionService ?? throw new ArgumentNullException(nameof(interactionService));
        _request = request ?? throw new ArgumentNullException(nameof(request));
        _deadlineResolver = deadlineResolver ?? new TaskDeadlineResolver();
        _attachmentsEnabled = false;
        _recurrenceEnabled = false;
        _timeZoneId = string.IsNullOrWhiteSpace(localTimeZoneId)
            ? TimeZoneInfo.Local.Id
            : localTimeZoneId.Trim();

        PriorityOptions =
        [
            new TaskPriorityOption(TaskPriority.UrgentAndImportant, "紧急且重要"),
            new TaskPriorityOption(TaskPriority.Important, "重要"),
            new TaskPriorityOption(TaskPriority.Normal, "一般"),
            new TaskPriorityOption(TaskPriority.Low, "低")
        ];
        ReminderOffsetOptions =
        [
            new ReminderOffsetOption(-1_440, "提前 1 天"),
            new ReminderOffsetOption(-720, "提前 12 小时"),
            new ReminderOffsetOption(-60, "提前 1 小时"),
            new ReminderOffsetOption(0, "Deadline 到达时")
        ];
        SaveCommand = new AsyncRelayCommand(SaveAsync, () => CanSave);
        ReloadCommand = new AsyncRelayCommand(ReloadAsync, () => CanReload);
        CancelCommand = new RelayCommand(RequestCancel);
    }

    /// <summary>Raised when the editor has committed and should be closed.</summary>
    public event EventHandler<TaskEditorSavedEventArgs>? Saved;

    /// <summary>Raised when the editor requests its WPF window to close.</summary>
    public event EventHandler? CloseRequested;

    /// <summary>Gets the create/edit mode.</summary>
    public TaskEditorMode Mode => _request.Mode;

    /// <summary>Gets whether this editor creates a task.</summary>
    public bool IsCreateMode => Mode == TaskEditorMode.Create;

    /// <summary>Gets whether this editor updates a task.</summary>
    public bool IsEditMode => Mode == TaskEditorMode.Edit;

    /// <summary>Gets the title shown in the editor window.</summary>
    public string WindowTitle => IsCreateMode ? "新建任务" : "编辑任务";

    /// <summary>Gets whether initial Application data has been loaded.</summary>
    public bool IsInitialized => _isInitialized;

    /// <summary>Gets the injected local date used by future editor entry points.</summary>
    public DateOnly TodayLocalDate => DateOnly.FromDateTime(_timeProvider.GetLocalNow().DateTime);

    /// <summary>Gets whether an Application operation is in progress.</summary>
    public bool IsBusy => _isBusy;

    /// <summary>Gets whether the form has changes that are not yet committed.</summary>
    public bool IsDirty => _isDirty;

    /// <summary>Gets whether an optimistic concurrency conflict needs a reload.</summary>
    public bool HasConflict => _hasConflict;

    /// <summary>Gets whether the save command can execute.</summary>
    public bool CanSave => IsInitialized && !IsBusy && !HasErrors;

    /// <summary>Gets whether the reload command can execute.</summary>
    public bool CanReload => IsEditMode && IsInitialized && !IsBusy && HasConflict;

    /// <summary>Gets the safe Application or editor-level error message.</summary>
    public string? ErrorMessage
    {
        get => _errorMessage;
        private set => SetProperty(ref _errorMessage, value);
    }

    /// <summary>Gets a non-blocking warning returned with a successful save.</summary>
    public string? WarningMessage
    {
        get => _warningMessage;
        private set => SetProperty(ref _warningMessage, value);
    }

    /// <summary>Gets the Application post-commit event status from the last save.</summary>
    public PostCommitEventStatus LastPostCommitEventStatus => _lastPostCommitEventStatus;

    /// <summary>Gets whether a consumer should perform a lightweight local refresh.</summary>
    public bool RequiresRefresh => LastPostCommitEventStatus == PostCommitEventStatus.RefreshRequired;

    /// <summary>Gets or sets the task title.</summary>
    public string Title
    {
        get => _title;
        set => SetEditorProperty(ref _title, value ?? string.Empty);
    }

    /// <summary>Gets active category choices loaded from Application.</summary>
    public IReadOnlyList<CategoryOptionDto> CategoryOptions => _categoryOptions;

    /// <summary>Gets or sets the selected category identity.</summary>
    public Guid SelectedCategoryId
    {
        get => _selectedCategoryId;
        set => SetEditorProperty(ref _selectedCategoryId, value);
    }

    /// <summary>Gets or sets task priority.</summary>
    public TaskPriority SelectedPriority
    {
        get => _selectedPriority;
        set => SetEditorProperty(ref _selectedPriority, value);
    }

    /// <summary>Gets the localized priority choices displayed by the editor.</summary>
    public IReadOnlyList<TaskPriorityOption> PriorityOptions { get; }

    /// <summary>Gets or sets the planned date for the WPF DatePicker.</summary>
    public DateTime? PlannedDateValue
    {
        get => _plannedDateValue;
        set => SetEditorProperty(ref _plannedDateValue, value?.Date);
    }

    /// <summary>Gets the planned date as the domain date value.</summary>
    public DateOnly? PlannedDate => _plannedDateValue is DateTime value
        ? DateOnly.FromDateTime(value)
        : null;

    /// <summary>Gets or sets the planned start wall-clock text.</summary>
    public string PlannedStartText
    {
        get => _plannedStartText;
        set => SetEditorProperty(ref _plannedStartText, value ?? string.Empty);
    }

    /// <summary>Gets or sets the planned end wall-clock text.</summary>
    public string PlannedEndText
    {
        get => _plannedEndText;
        set => SetEditorProperty(ref _plannedEndText, value ?? string.Empty);
    }

    /// <summary>Gets or sets whether a deadline is present.</summary>
    public bool HasDeadline
    {
        get => _hasDeadline;
        set => SetEditorProperty(ref _hasDeadline, value);
    }

    /// <summary>Gets or sets the deadline date for the WPF DatePicker.</summary>
    public DateTime? DeadlineDateValue
    {
        get => _deadlineDateValue;
        set => SetEditorProperty(ref _deadlineDateValue, value?.Date);
    }

    /// <summary>Gets or sets the deadline wall-clock text.</summary>
    public string DeadlineTimeText
    {
        get => _deadlineTimeText;
        set => SetEditorProperty(ref _deadlineTimeText, value ?? string.Empty);
    }

    /// <summary>Gets the Windows time-zone identifier used for Deadline conversion.</summary>
    public string TimeZoneId
    {
        get => _timeZoneId;
        private set => SetEditorProperty(ref _timeZoneId, value);
    }

    /// <summary>Gets the display name of the selected Windows time zone.</summary>
    public string TimeZoneDisplayName
    {
        get
        {
            try
            {
                return TimeZoneInfo.FindSystemTimeZoneById(TimeZoneId).DisplayName;
            }
            catch (TimeZoneNotFoundException)
            {
                return TimeZoneId;
            }
            catch (InvalidTimeZoneException)
            {
                return TimeZoneId;
            }
            catch (ArgumentException)
            {
                return TimeZoneId;
            }
        }
    }

    /// <summary>Gets or sets whether the one supported reminder plan is enabled.</summary>
    public bool ReminderEnabled
    {
        get => _reminderEnabled;
        set => SetEditorProperty(ref _reminderEnabled, value);
    }

    /// <summary>Gets or sets the reminder offset in minutes relative to Deadline.</summary>
    public int ReminderOffsetMinutes
    {
        get => _reminderOffsetMinutes;
        set => SetEditorProperty(ref _reminderOffsetMinutes, value);
    }

    /// <summary>Gets supported reminder choices for the editor.</summary>
    public IReadOnlyList<ReminderOffsetOption> ReminderOffsetOptions { get; }

    /// <summary>Gets or sets the task location.</summary>
    public string Location
    {
        get => _location;
        set => SetEditorProperty(ref _location, value ?? string.Empty);
    }

    /// <summary>Gets or sets the concrete task details.</summary>
    public string Description
    {
        get => _description;
        set => SetEditorProperty(ref _description, value ?? string.Empty);
    }

    /// <summary>Gets or sets material-preparation text.</summary>
    public string Materials
    {
        get => _materials;
        set => SetEditorProperty(ref _materials, value ?? string.Empty);
    }

    /// <summary>Gets or sets free-form notes.</summary>
    public string Notes
    {
        get => _notes;
        set => SetEditorProperty(ref _notes, value ?? string.Empty);
    }

    /// <summary>Gets whether attachment persistence is available in this task package.</summary>
    public bool IsAttachmentsEnabled => _attachmentsEnabled;

    /// <summary>Explains the honest attachment placeholder without pretending to save files.</summary>
    public string AttachmentsPlaceholder => _request.Mode == TaskEditorMode.Create
        ? "附件功能保留为占位；DEV-070 接入后才会保存受管副本。"
        : "附件功能保留为占位；DEV-070 接入后才会保存受管副本。";

    /// <summary>Gets whether recurrence persistence is available in this task package.</summary>
    public bool IsRecurrenceEnabled => _recurrenceEnabled;

    /// <summary>Explains the honest recurrence placeholder without pretending to save rules.</summary>
    public string RecurrencePlaceholder => _request.Mode == TaskEditorMode.Create
        ? "周期功能保留为占位；DEV-061 接入后才会保存周期规则。"
        : "周期功能保留为占位；DEV-061 接入后才会保存周期规则。";

    /// <summary>Gets the UI validation summary.</summary>
    public IReadOnlyList<string> ValidationMessages => _errors.Values.SelectMany(messages => messages).ToArray();

    /// <summary>Gets whether the UI validation summary contains errors.</summary>
    public bool HasErrors => _errors.Count != 0;

    /// <summary>Gets save, cancel, and conflict-reload commands.</summary>
    public IAsyncRelayCommand SaveCommand { get; }

    /// <summary>Gets the explicit reload command shown after an optimistic conflict.</summary>
    public IAsyncRelayCommand ReloadCommand { get; }

    /// <summary>Gets the cancel command that respects dirty-form confirmation.</summary>
    public IRelayCommand CancelCommand { get; }

    /// <inheritdoc />
    public event EventHandler<DataErrorsChangedEventArgs>? ErrorsChanged;

    /// <summary>Loads categories and, for edit mode, the requested task.</summary>
    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        if (_isInitialized)
        {
            return;
        }

        SetBusy(true);
        try
        {
            var categoriesTask = _taskUseCases.GetCategoryOptionsAsync(
                new GetCategoryOptionsQuery(),
                cancellationToken);
            var taskTask = IsEditMode
                ? _taskUseCases.GetAsync(new GetTaskQuery(_request.TaskId!.Value), cancellationToken)
                : null;

            var categoriesResult = await categoriesTask;
            ApplicationResult<TaskDto>? taskResult = null;
            if (taskTask is not null)
            {
                taskResult = await taskTask;
            }

            if (!categoriesResult.IsSuccess || categoriesResult.Value is null)
            {
                SetApplicationError(categoriesResult.Error);
                return;
            }

            ReplaceCategoryOptions(categoriesResult.Value);
            if (IsEditMode)
            {
                if (taskResult is null || !taskResult.IsSuccess || taskResult.Value is null)
                {
                    SetApplicationError(taskResult?.Error);
                    return;
                }

                ApplyTask(taskResult.Value);
            }
            else
            {
                ApplyCreateDefaults(categoriesResult.Value);
            }

            _isInitialized = true;
            OnPropertyChanged(nameof(IsInitialized));
            ClearErrors();
            ValidateForm();
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            ErrorMessage = "加载已取消，请重试。";
            NotifyCommandState();
        }
        catch
        {
            SetApplicationError(error: null);
        }
        finally
        {
            SetBusy(false);
        }
    }

    /// <inheritdoc />
    public IEnumerable GetErrors(string? propertyName)
    {
        if (propertyName is null)
        {
            return _errors.Values.SelectMany(messages => messages).ToArray();
        }

        return _errors.TryGetValue(propertyName, out var errors)
            ? errors
            : Array.Empty<string>();
    }

    /// <summary>Validates whether the WPF closing event may proceed.</summary>
    public bool TryCloseFromWindow()
    {
        if (_closeApproved)
        {
            _closeApproved = false;
            return true;
        }

        return ConfirmDiscardIfNeeded();
    }

    private void RequestCancel()
    {
        if (!ConfirmDiscardIfNeeded())
        {
            return;
        }

        _closeApproved = true;
        CloseRequested?.Invoke(this, EventArgs.Empty);
    }

    private bool ConfirmDiscardIfNeeded()
    {
        if (!IsDirty || _interactionService.ConfirmDiscardChanges())
        {
            return true;
        }

        return false;
    }

    private bool SetEditorProperty<T>(
        ref T storage,
        T value,
        [CallerMemberName] string? propertyName = null)
    {
        if (!SetProperty(ref storage, value, propertyName))
        {
            return false;
        }

        if (!_isApplyingValues)
        {
            MarkChanged(propertyName);
        }

        return true;
    }

    private void MarkChanged(string? propertyName)
    {
        _isDirty = true;
        _hasConflict = false;
        ErrorMessage = null;
        WarningMessage = null;
        if (propertyName is nameof(DeadlineDateValue) or nameof(DeadlineTimeText) or nameof(TimeZoneId))
        {
            _confirmedDeadlineUtc = null;
            _deadlineWarningAcknowledged = false;
        }

        if (propertyName is nameof(PlannedDateValue) or nameof(HasDeadline))
        {
            _deadlineWarningAcknowledged = false;
        }

        if (propertyName is nameof(ReminderEnabled) or nameof(ReminderOffsetMinutes))
        {
            _reminderPlanTouched = true;
        }

        ValidateForm();
        NotifyCommandState();
        OnPropertyChanged(nameof(IsDirty));
        OnPropertyChanged(nameof(HasConflict));
    }

    private void SetBusy(bool value)
    {
        if (SetProperty(ref _isBusy, value, nameof(IsBusy)))
        {
            NotifyCommandState();
        }
    }

    private void NotifyCommandState()
    {
        OnPropertyChanged(nameof(CanSave));
        OnPropertyChanged(nameof(CanReload));
        SaveCommand.NotifyCanExecuteChanged();
        ReloadCommand.NotifyCanExecuteChanged();
    }

    private void ReplaceCategoryOptions(IReadOnlyList<CategoryOptionDto> options)
    {
        _categoryOptions.Clear();
        _categoryOptions.AddRange(options);
        OnPropertyChanged(nameof(CategoryOptions));
    }

    private void ApplyCreateDefaults(IReadOnlyList<CategoryOptionDto> options)
    {
        _isApplyingValues = true;
        try
        {
            if (_request.PrefilledPlannedDate is DateOnly plannedDate)
            {
                PlannedDateValue = plannedDate.ToDateTime(TimeOnly.MinValue);
            }

            if (options.Count != 0)
            {
                SelectedCategoryId = options[0].Id;
            }
        }
        finally
        {
            _isApplyingValues = false;
            _isDirty = false;
        }
    }

    private void ApplyTask(TaskDto task)
    {
        _isApplyingValues = true;
        try
        {
            _expectedVersion = task.Version;
            Title = task.Title;
            SelectedCategoryId = task.CategoryId;
            SelectedPriority = task.Priority;
            PlannedDateValue = task.PlannedDate?.ToDateTime(TimeOnly.MinValue);
            PlannedStartText = FormatTime(task.PlannedStart);
            PlannedEndText = FormatTime(task.PlannedEnd);
            HasDeadline = task.Deadline is not null;
            DeadlineDateValue = task.Deadline?.LocalDate.ToDateTime(TimeOnly.MinValue);
            DeadlineTimeText = FormatTime(task.Deadline?.LocalTime);
            TimeZoneId = task.Deadline?.TimeZoneId ?? TimeZoneInfo.Local.Id;
            _confirmedDeadlineUtc = task.Deadline?.Utc;
            ReminderEnabled = true;
            ReminderOffsetMinutes = DefaultReminderOffsetMinutes;
            Location = task.Location ?? string.Empty;
            Description = task.Description ?? string.Empty;
            Materials = task.Materials ?? string.Empty;
            Notes = task.Notes ?? string.Empty;
            OnPropertyChanged(nameof(TimeZoneDisplayName));
        }
        finally
        {
            _isApplyingValues = false;
            _isDirty = false;
            _reminderPlanTouched = false;
        }
    }

    private static string FormatTime(TimeOnly? value)
    {
        return value?.ToString("HH:mm", CultureInfo.CurrentCulture) ?? string.Empty;
    }

    private void SetApplicationError(ApplicationError? error)
    {
        ErrorMessage = error?.Message ?? "无法加载任务编辑器数据。请重试。";
        _hasConflict = error?.Kind == ApplicationErrorKind.Conflict;
        OnPropertyChanged(nameof(HasConflict));
        NotifyCommandState();
    }

    private void ClearErrors()
    {
        var names = _errors.Keys.ToArray();
        _errors.Clear();
        foreach (var name in names)
        {
            ErrorsChanged?.Invoke(this, new DataErrorsChangedEventArgs(name));
        }

        OnPropertyChanged(nameof(HasErrors));
        OnPropertyChanged(nameof(ValidationMessages));
    }

    private void ReplaceErrors(IReadOnlyDictionary<string, IReadOnlyList<string>> next)
    {
        var names = _errors.Keys.Union(next.Keys, StringComparer.Ordinal).ToArray();
        _errors.Clear();
        foreach (var pair in next)
        {
            _errors[pair.Key] = pair.Value;
        }

        foreach (var name in names)
        {
            ErrorsChanged?.Invoke(this, new DataErrorsChangedEventArgs(name));
        }

        OnPropertyChanged(nameof(HasErrors));
        OnPropertyChanged(nameof(ValidationMessages));
        NotifyCommandState();
    }

    private static string? NormalizeOptional(string value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }

    private static int CountRunes(string value)
    {
        return value.EnumerateRunes().Count();
    }
}
