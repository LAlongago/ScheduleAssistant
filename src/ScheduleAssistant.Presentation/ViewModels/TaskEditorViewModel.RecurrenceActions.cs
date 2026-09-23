using ScheduleAssistant.Application.Common;
using ScheduleAssistant.Application.Recurrence;
using ScheduleAssistant.Application.Tasks;

namespace ScheduleAssistant.Presentation.ViewModels;

public sealed partial class TaskEditorViewModel
{
    private async Task LoadRecurrenceSeriesForTaskAsync(CancellationToken cancellationToken)
    {
        _loadedRecurrenceSeries = null;
        RecurrenceLoadMessage = null;
        if (!IsRecurrenceOccurrence || _recurrenceUseCases is null)
        {
            RecurrenceLoadMessage = IsRecurrenceOccurrence
                ? "周期操作暂不可用。"
                : null;
            OnPropertyChanged(nameof(RecurrenceSeriesSummary));
            NotifyRecurrenceCommandState();
            return;
        }

        var result = await _recurrenceUseCases.GetAsync(
            new GetRecurrenceSeriesQuery(_loadedTask!.SeriesId!.Value),
            cancellationToken).ConfigureAwait(true);
        if (!result.IsSuccess || result.Value is null)
        {
            RecurrenceLoadMessage = result.Error?.Message ?? "无法读取周期系列信息，请重新加载后重试。";
            OnPropertyChanged(nameof(RecurrenceSeriesSummary));
            NotifyRecurrenceCommandState();
            return;
        }

        _loadedRecurrenceSeries = result.Value;
        RecurrenceLoadMessage = null;
        if (_deleteFromDateValue is null && _loadedTask?.OccurrenceDate is DateOnly occurrenceDate)
        {
            _deleteFromDateValue = occurrenceDate.ToDateTime(TimeOnly.MinValue);
            OnPropertyChanged(nameof(DeleteFromDateValue));
        }
        OnPropertyChanged(nameof(RecurrenceSeriesSummary));
        NotifyRecurrenceCommandState();
    }

    private bool CanEditRecurrenceSeries() =>
        IsRecurrenceOccurrence
        && _loadedRecurrenceSeries is not null
        && !IsEditingRecurrenceSeries
        && !_recurrenceOperationCommitted
        && !IsBusy;

    private async Task EditRecurrenceSeriesAsync()
    {
        if (!CanEditRecurrenceSeries() || _loadedTask is null)
        {
            return;
        }

        if (IsDirty && !_interactionService.ConfirmDiscardChanges())
        {
            return;
        }

        await LoadRecurrenceSeriesForTaskAsync(CancellationToken.None);
        if (_loadedRecurrenceSeries is null)
        {
            ErrorMessage = RecurrenceLoadMessage ?? "无法读取周期系列信息，请重试。";
            return;
        }

        ApplyRecurrenceSeries(_loadedRecurrenceSeries);
    }

    private void ApplyRecurrenceSeries(RecurrenceSeriesDto series)
    {
        _isApplyingValues = true;
        try
        {
            Title = series.Title;
            SelectedCategoryId = series.CategoryId;
            SelectedPriority = series.Priority;
            PlannedDateValue = null;
            PlannedStartText = FormatTime(series.PlannedStart);
            PlannedEndText = FormatTime(series.PlannedEnd);
            HasDeadline = false;
            DeadlineDateValue = null;
            DeadlineTimeText = string.Empty;
            ReminderEnabled = false;
            ReminderOffsetMinutes = DefaultReminderOffsetMinutes;
            Location = series.Location ?? string.Empty;
            Description = series.Description ?? string.Empty;
            Materials = series.Materials ?? string.Empty;
            Notes = series.Notes ?? string.Empty;

            SelectedRecurrenceFrequency = series.Rule.Frequency;
            RecurrenceEffectiveDateValue = series.Rule.EffectiveDate.ToDateTime(TimeOnly.MinValue);
            RecurrenceEndDateValue = series.Rule.EndDate?.ToDateTime(TimeOnly.MinValue);
            RecurrenceTimeZoneId = series.Rule.TimeZoneId;
            MonthlyDay = series.Rule.MonthDay ?? 1;
            YearMonth = series.Rule.YearMonth ?? 1;
            YearDay = series.Rule.YearDay ?? 1;
            foreach (var option in RecurrenceWeekdayOptions)
            {
                option.IsSelected = series.Rule.Weekdays.HasFlag(option.Value);
            }

            var occurrenceDate = _loadedTask?.OccurrenceDate ?? series.Rule.EffectiveDate;
            ApplyFromDateValue = occurrenceDate.ToDateTime(TimeOnly.MinValue);
            DeleteFromDateValue = occurrenceDate.ToDateTime(TimeOnly.MinValue);
            OnPropertyChanged(nameof(TimeZoneDisplayName));
        }
        finally
        {
            _isApplyingValues = false;
        }

        _loadedRecurrenceSeries = series;
        _isEditingRecurrenceSeries = true;
        _reminderPlanTouched = false;
        _isDirty = false;
        _hasConflict = false;
        RecurrenceModeError = null;
        ErrorMessage = null;
        ClearErrors();
        OnPropertyChanged(nameof(IsDirty));
        OnPropertyChanged(nameof(HasConflict));
        OnPropertyChanged(nameof(RecurrenceSeriesSummary));
        NotifyRecurrenceStateChanged();
        ValidateForm();
    }

    private bool CanReturnToOccurrence() => IsEditingRecurrenceSeries && !_recurrenceOperationCommitted && !IsBusy;

    private async Task ReturnToOccurrenceAsync()
    {
        if (!CanReturnToOccurrence() || _loadedTask is null)
        {
            return;
        }

        if (IsDirty && !_interactionService.ConfirmDiscardChanges())
        {
            return;
        }

        _isEditingRecurrenceSeries = false;
        ApplyTask(_loadedTask);
        await LoadAttachmentsAsync(CancellationToken.None).ConfigureAwait(true);
        _isDirty = false;
        _hasConflict = false;
        ClearErrors();
        ErrorMessage = null;
        OnPropertyChanged(nameof(IsDirty));
        OnPropertyChanged(nameof(HasConflict));
        NotifyRecurrenceStateChanged();
        ValidateForm();
    }

    private bool CanDeleteOccurrence() =>
        IsRecurrenceOccurrence
        && _recurrenceUseCases is not null
        && !IsEditingRecurrenceSeries
        && !_recurrenceOperationCommitted
        && !IsBusy;

    private async Task DeleteOccurrenceAsync()
    {
        if (!CanDeleteOccurrence() || _loadedTask is null || _recurrenceUseCases is null)
        {
            return;
        }

        var occurrenceDate = _loadedTask.OccurrenceDate!.Value;
        if (!_interactionService.ConfirmRecurrenceOperation(
                "删除当前周期实例",
                $"仅删除 {occurrenceDate:yyyy-MM-dd} 的当前实例。该日期会被记录为排除项，后续物化不会重新创建它；系列其他日期不受影响。此任务的 Deadline、提醒和附件管理副本也会随任务删除。",
                "删除当前实例"))
        {
            return;
        }

        SetBusy(true);
        ErrorMessage = null;
        try
        {
            var result = await _recurrenceUseCases.DeleteInstanceAsync(
                new DeleteRecurrenceInstanceCommand(_loadedTask.Id, _loadedTask.Version));
            if (!result.IsSuccess || result.Value is null)
            {
                ApplyRecurrenceOperationError(result.Error);
                return;
            }

            CompleteRecurrenceOperation(result.PostCommitEventStatus, "当前周期实例已删除。");
        }
        catch (OperationCanceledException)
        {
            ErrorMessage = "删除已取消，请重试。";
        }
        catch
        {
            ErrorMessage = "无法删除当前周期实例，请重试。";
        }
        finally
        {
            SetBusy(false);
        }
    }

    private bool CanDeleteFutureOccurrences() =>
        IsRecurrenceOccurrence
        && _loadedRecurrenceSeries is not null
        && _recurrenceUseCases is not null
        && !IsEditingRecurrenceSeries
        && !_recurrenceOperationCommitted
        && !IsBusy;

    private async Task DeleteFutureOccurrencesAsync()
    {
        if (!CanDeleteFutureOccurrences() || _loadedTask is null || _loadedRecurrenceSeries is null
            || _recurrenceUseCases is null)
        {
            return;
        }

        var fromDate = ToDateOnly(DeleteFromDateValue);
        var occurrenceDate = _loadedTask.OccurrenceDate!.Value;
        if (!fromDate.HasValue || fromDate.Value < occurrenceDate)
        {
            RecurrenceModeError = "删除范围日期不能早于当前实例日期。";
            ShowValidationErrors = true;
            return;
        }

        if (!_interactionService.ConfirmRecurrenceOperation(
                "删除此后未完成实例",
                $"从 {fromDate.Value:yyyy-MM-dd} 起，删除尚未完成且未单独覆盖的实例，并停止系列在该日期及之后生成新实例。已完成和已覆盖实例保留；被删除实例的 Deadline、提醒和附件管理副本也会随任务删除。此操作不可撤销。",
                "删除此后实例"))
        {
            return;
        }

        SetBusy(true);
        ErrorMessage = null;
        try
        {
            var result = await _recurrenceUseCases.DeleteFutureAsync(
                new DeleteFutureRecurrenceCommand(
                    _loadedRecurrenceSeries.Id,
                    _loadedRecurrenceSeries.Version,
                    fromDate.Value));
            if (!result.IsSuccess || result.Value is null)
            {
                ApplyRecurrenceOperationError(result.Error);
                return;
            }

            CompleteRecurrenceOperation(result.PostCommitEventStatus, "此后未完成周期实例已删除。");
        }
        catch (OperationCanceledException)
        {
            ErrorMessage = "删除已取消，请重试。";
        }
        catch
        {
            ErrorMessage = "无法删除此后周期实例，请重试。";
        }
        finally
        {
            SetBusy(false);
        }
    }

    private async Task SaveRecurrenceAsync()
    {
        if (!TryBuildRecurrenceDraft(out var draft))
        {
            ShowValidationErrors = true;
            return;
        }

        if (IsEditingRecurrenceSeries)
        {
            var applyFromDate = ToDateOnly(ApplyFromDateValue)!.Value;
            if (_loadedRecurrenceSeries is null)
            {
                ErrorMessage = "周期系列信息已失效，请重新加载。";
                return;
            }

            if (!_interactionService.ConfirmRecurrenceOperation(
                    "确认修改周期系列",
                    $"从 {applyFromDate:yyyy-MM-dd}（ApplyFromDate）起更新系列并重建未完成且未单独覆盖的实例。已完成和已覆盖实例保留；被替换实例上的 Deadline、提醒和附件管理副本会随原任务删除，这些内容不会复制到新系列实例。",
                    "更新此后实例"))
            {
                return;
            }
        }

        SetBusy(true);
        ErrorMessage = null;
        WarningMessage = null;
        try
        {
            if (IsEditingRecurrenceSeries)
            {
                var series = _loadedRecurrenceSeries!;
                var applyFromDate = ToDateOnly(ApplyFromDateValue)!.Value;
                var result = await _recurrenceUseCases!.UpdateAsync(
                    new UpdateRecurrenceSeriesCommand(
                        series.Id,
                        series.Version,
                        draft,
                        ApplyFromDate: applyFromDate));
                if (!result.IsSuccess || result.Value is null)
                {
                    ApplyRecurrenceOperationError(result.Error);
                    return;
                }

                _loadedRecurrenceSeries = result.Value;
                OnPropertyChanged(nameof(RecurrenceSeriesSummary));
                CompleteRecurrenceOperation(result.PostCommitEventStatus, "周期系列已更新。");
                return;
            }

            var createResult = await _recurrenceUseCases!.CreateAsync(new CreateRecurrenceSeriesCommand(draft));
            if (!createResult.IsSuccess || createResult.Value is null)
            {
                ApplyRecurrenceOperationError(createResult.Error);
                return;
            }

            _loadedRecurrenceSeries = createResult.Value;
            CompleteRecurrenceOperation(createResult.PostCommitEventStatus, "周期任务已创建。");
        }
        catch (OperationCanceledException)
        {
            ErrorMessage = "保存已取消，请重试。";
        }
        catch
        {
            ErrorMessage = IsEditingRecurrenceSeries
                ? "周期系列未能更新，请重试。"
                : "周期任务未能创建，请重试。";
        }
        finally
        {
            SetBusy(false);
        }
    }

    private void CompleteRecurrenceOperation(PostCommitEventStatus eventStatus, string successMessage)
    {
        _lastPostCommitEventStatus = eventStatus;
        OnPropertyChanged(nameof(LastPostCommitEventStatus));
        OnPropertyChanged(nameof(RequiresRefresh));
        WarningMessage = eventStatus == PostCommitEventStatus.RefreshRequired
            ? "操作已保存，但部分视图可能尚未刷新。请切换页面或重新打开后确认。"
            : null;
        _isDirty = false;
        _hasConflict = false;
        OnPropertyChanged(nameof(IsDirty));
        OnPropertyChanged(nameof(HasConflict));
        if (eventStatus == PostCommitEventStatus.RefreshRequired)
        {
            _recurrenceOperationCommitted = true;
            ErrorMessage = successMessage;
            NotifyRecurrenceStateChanged();
            NotifyCommandState();
            return;
        }

        _closeApproved = true;
        CloseRequested?.Invoke(this, EventArgs.Empty);
    }

    private void ApplyRecurrenceOperationError(ApplicationError? error)
    {
        ErrorMessage = error?.Message ?? "周期操作未能完成，请检查输入后重试。";
        _hasConflict = error?.Kind == ApplicationErrorKind.Conflict;
        OnPropertyChanged(nameof(HasConflict));
        NotifyCommandState();
    }
}
