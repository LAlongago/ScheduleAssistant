using System.Globalization;
using ScheduleAssistant.Application.Common;
using ScheduleAssistant.Application.Tasks;
using ScheduleAssistant.Presentation.Composition;

namespace ScheduleAssistant.Presentation.ViewModels;

public sealed partial class TaskEditorViewModel
{
    private async Task SaveAsync()
    {
        if (!CanSave)
        {
            return;
        }

        ValidateForm();
        if (HasErrors)
        {
            ShowValidationErrors = true;
            return;
        }

        if (!TryBuildDraft(out var draft))
        {
            ShowValidationErrors = true;
            return;
        }

        ShowValidationErrors = false;
        ErrorMessage = null;
        WarningMessage = null;
        _hasConflict = false;
        NotifyCommandState();
        SetBusy(true);
        try
        {
            var result = IsCreateMode
                ? await _taskUseCases.CreateAsync(new CreateTaskCommand(draft))
                : await _taskUseCases.UpdateAsync(
                    new UpdateTaskCommand(_request.TaskId!.Value, _expectedVersion, draft));

            if (!result.IsSuccess || result.Value is null)
            {
                ApplySaveError(result.Error);
                return;
            }

            _lastPostCommitEventStatus = result.PostCommitEventStatus;
            OnPropertyChanged(nameof(LastPostCommitEventStatus));
            OnPropertyChanged(nameof(RequiresRefresh));
            WarningMessage = result.Warnings.Count == 0 ? null : result.Warnings[0].Message;
            _isDirty = false;
            _hasConflict = false;
            OnPropertyChanged(nameof(IsDirty));
            OnPropertyChanged(nameof(HasConflict));
            NotifyCommandState();

            Saved?.Invoke(
                this,
                new TaskEditorSavedEventArgs(result.Value, result.PostCommitEventStatus));
            _closeApproved = true;
            CloseRequested?.Invoke(this, EventArgs.Empty);
        }
        catch (OperationCanceledException)
        {
            ErrorMessage = "保存已取消，请重试。";
            NotifyCommandState();
        }
        catch
        {
            ErrorMessage = "任务未能保存。请检查连接后重试。";
            NotifyCommandState();
        }
        finally
        {
            SetBusy(false);
        }
    }

    private async Task ReloadAsync()
    {
        if (!CanReload)
        {
            return;
        }

        ErrorMessage = null;
        SetBusy(true);
        try
        {
            var result = await _taskUseCases.GetAsync(new GetTaskQuery(_request.TaskId!.Value));
            if (!result.IsSuccess || result.Value is null)
            {
                SetApplicationError(result.Error);
                return;
            }

            ApplyTask(result.Value);
            _isDirty = false;
            _hasConflict = false;
            ClearErrors();
            ErrorMessage = null;
            OnPropertyChanged(nameof(IsDirty));
            OnPropertyChanged(nameof(HasConflict));
            NotifyCommandState();
        }
        catch (OperationCanceledException)
        {
            ErrorMessage = "重新加载已取消，请重试。";
            NotifyCommandState();
        }
        catch
        {
            ErrorMessage = "无法重新加载任务，请重试。";
            NotifyCommandState();
        }
        finally
        {
            SetBusy(false);
        }
    }

    private void ApplySaveError(ApplicationError? error)
    {
        ErrorMessage = error?.Message ?? "任务未能保存。请检查输入后重试。";
        _hasConflict = error?.Kind == ApplicationErrorKind.Conflict;
        OnPropertyChanged(nameof(HasConflict));
        NotifyCommandState();
    }

    private bool TryBuildDraft(out TaskDraft draft)
    {
        draft = null!;
        ValidateForm();
        if (HasErrors)
        {
            return false;
        }

        var plannedDate = PlannedDate;
        var plannedStart = ParseTimeOrNull(PlannedStartText);
        var plannedEnd = ParseTimeOrNull(PlannedEndText);
        DeadlineInput? deadline = null;
        if (HasDeadline)
        {
            var deadlineDate = DateOnly.FromDateTime(DeadlineDateValue!.Value);
            var deadlineTime = ParseTimeOrNull(DeadlineTimeText)!.Value;
            if (plannedDate is DateOnly planDate
                && deadlineDate < planDate
                && !_deadlineWarningAcknowledged)
            {
                if (!_interactionService.ConfirmDeadlineBeforePlannedDate(planDate, deadlineDate))
                {
                    ErrorMessage = "请确认 Deadline 早于计划日期，或修改日期后再保存。";
                    return false;
                }

                _deadlineWarningAcknowledged = true;
            }

            deadline = new DeadlineInput(
                deadlineDate,
                deadlineTime,
                TimeZoneId,
                _confirmedDeadlineUtc);
            if (!ResolveDeadlineForSubmission(ref deadline))
            {
                return false;
            }
        }

        var reminderPlan = IsCreateMode || _reminderPlanTouched
            ? new ReminderPlanInput(ReminderEnabled, ReminderOffsetMinutes)
            : null;
        draft = new TaskDraft(
            Title,
            SelectedCategoryId,
            SelectedPriority,
            plannedDate,
            plannedStart,
            plannedEnd,
            deadline,
            NormalizeOptional(Location),
            NormalizeOptional(Description),
            NormalizeOptional(Materials),
            NormalizeOptional(Notes),
            reminderPlan);
        return true;
    }

    private bool ResolveDeadlineForSubmission(ref DeadlineInput input)
    {
        var resolution = _deadlineResolver.Resolve(input);
        if (resolution.Status == DeadlineResolutionStatus.Resolved)
        {
            return true;
        }

        if (resolution.Status == DeadlineResolutionStatus.RequiresModification)
        {
            var message = resolution.Error?.Message
                ?? "该 Deadline 时间无效，请修改后再保存。";
            SetDeadlineResolutionError(message, nameof(DeadlineTimeText));
            return false;
        }

        var candidates = GetAmbiguousCandidates(input);
        if (candidates.Length != 2)
        {
            SetDeadlineResolutionError(
                "无法确定该 Deadline 的两个有效时刻，请修改时间或时区后再试。",
                nameof(DeadlineTimeText));
            return false;
        }

        var confirmedUtc = _interactionService.ChooseAmbiguousDeadline(
            input.LocalDate,
            input.LocalTime,
            input.TimeZoneId,
            candidates);
        if (!confirmedUtc.HasValue)
        {
            ErrorMessage = "请选择一个有效的 Deadline 时刻后再保存。";
            return false;
        }

        input = input with { ConfirmedUtc = confirmedUtc.Value };
        var confirmedResolution = _deadlineResolver.Resolve(input);
        if (confirmedResolution.Status != DeadlineResolutionStatus.Resolved)
        {
            SetDeadlineResolutionError(
                confirmedResolution.Error?.Message
                    ?? "所选 Deadline 时刻无效，请重新选择。",
                nameof(DeadlineTimeText));
            return false;
        }

        _confirmedDeadlineUtc = confirmedUtc.Value;
        return true;
    }

    private static DateTimeOffset[] GetAmbiguousCandidates(DeadlineInput input)
    {
        try
        {
            var timeZone = TimeZoneInfo.FindSystemTimeZoneById(input.TimeZoneId);
            var local = input.LocalDate.ToDateTime(input.LocalTime, DateTimeKind.Unspecified);
            return timeZone
                .GetAmbiguousTimeOffsets(local)
                .Select(offset => new DateTimeOffset(local, offset).ToUniversalTime())
                .Distinct()
                .OrderBy(value => value)
                .ToArray();
        }
        catch (TimeZoneNotFoundException)
        {
            return Array.Empty<DateTimeOffset>();
        }
        catch (InvalidTimeZoneException)
        {
            return Array.Empty<DateTimeOffset>();
        }
    }

    private void SetDeadlineResolutionError(string message, string propertyName)
    {
        ErrorMessage = message;
        var next = _errors.ToDictionary(
            pair => pair.Key,
            pair => pair.Value,
            StringComparer.Ordinal);
        next[propertyName] = new[] { message };
        ReplaceErrors(next);
    }

    private void ValidateForm()
    {
        var errors = new Dictionary<string, List<string>>(StringComparer.Ordinal);

        if (string.IsNullOrWhiteSpace(Title))
        {
            AddError(errors, nameof(Title), "请输入标题。");
        }
        else if (CountRunes(Title.Trim()) > 200)
        {
            AddError(errors, nameof(Title), "标题最多 200 个字符。");
        }

        if (SelectedCategoryId == Guid.Empty)
        {
            AddError(errors, nameof(SelectedCategoryId), "请选择任务类型。");
        }
        else if (!_categoryOptions.Any(category => category.Id == SelectedCategoryId))
        {
            AddError(errors, nameof(SelectedCategoryId), "请选择列表中的任务类型。");
        }

        if (!Enum.IsDefined(SelectedPriority))
        {
            AddError(errors, nameof(SelectedPriority), "请选择有效的优先级。");
        }

        if (!ReminderOffsetOptions.Any(option => option.Value == ReminderOffsetMinutes))
        {
            AddError(errors, nameof(ReminderOffsetMinutes), "请选择有效的提醒时间。");
        }

        var plannedStart = ParseTime(PlannedStartText, nameof(PlannedStartText), errors);
        var plannedEnd = ParseTime(PlannedEndText, nameof(PlannedEndText), errors);
        if ((plannedStart.HasValue || plannedEnd.HasValue) && PlannedDate is null)
        {
            AddError(errors, nameof(PlannedDateValue), "填写计划时间前必须先选择计划日期。");
        }

        if (plannedStart.HasValue && plannedEnd.HasValue && plannedEnd < plannedStart)
        {
            AddError(errors, nameof(PlannedEndText), "计划结束时间不能早于开始时间。");
        }

        if (HasDeadline)
        {
            if (!DeadlineDateValue.HasValue)
            {
                AddError(errors, nameof(DeadlineDateValue), "请选择 Deadline 日期。");
            }

            if (string.IsNullOrWhiteSpace(DeadlineTimeText))
            {
                AddError(errors, nameof(DeadlineTimeText), "请选择 Deadline 时间。");
            }
            else
            {
                _ = ParseTime(DeadlineTimeText, nameof(DeadlineTimeText), errors);
            }
            if (string.IsNullOrWhiteSpace(TimeZoneId))
            {
                AddError(errors, nameof(TimeZoneId), "未找到可用的 Windows 时区。");
            }
        }

        AddMaximumLengthError(errors, nameof(Location), Location, 300, "地点最多 300 个字符。");
        AddMaximumLengthError(errors, nameof(Description), Description, 10_000, "具体事务最多 10,000 个字符。");
        AddMaximumLengthError(errors, nameof(Materials), Materials, 10_000, "材料准备最多 10,000 个字符。");
        AddMaximumLengthError(errors, nameof(Notes), Notes, 10_000, "备注最多 10,000 个字符。");

        var readOnlyErrors = errors.ToDictionary(
            pair => pair.Key,
            pair => (IReadOnlyList<string>)pair.Value.AsReadOnly(),
            StringComparer.Ordinal);
        ReplaceErrors(readOnlyErrors);
    }

    private static TimeOnly? ParseTime(
        string value,
        string propertyName,
        IDictionary<string, List<string>> errors)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        if (TryParseTime(value, out var parsed))
        {
            return parsed;
        }

        AddError(errors, propertyName, "请输入有效时间，例如 14:30。");
        return null;
    }

    private static TimeOnly? ParseTimeOrNull(string value)
    {
        return string.IsNullOrWhiteSpace(value) || !TryParseTime(value, out var parsed)
            ? null
            : parsed;
    }

    private static bool TryParseTime(string value, out TimeOnly parsed)
    {
        return TimeOnly.TryParse(
                   value,
                   CultureInfo.CurrentCulture,
                   DateTimeStyles.AllowWhiteSpaces,
                   out parsed)
            || TimeOnly.TryParse(
                value,
                CultureInfo.InvariantCulture,
                DateTimeStyles.AllowWhiteSpaces,
                out parsed);
    }

    private static void AddMaximumLengthError(
        IDictionary<string, List<string>> errors,
        string propertyName,
        string value,
        int maximum,
        string message)
    {
        if (CountRunes(value) > maximum)
        {
            AddError(errors, propertyName, message);
        }
    }

    private static void AddError(
        IDictionary<string, List<string>> errors,
        string propertyName,
        string message)
    {
        if (!errors.TryGetValue(propertyName, out var messages))
        {
            messages = [];
            errors.Add(propertyName, messages);
        }

        messages.Add(message);
    }
}
