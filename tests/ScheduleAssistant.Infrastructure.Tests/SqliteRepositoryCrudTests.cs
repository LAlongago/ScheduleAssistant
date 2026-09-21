using ScheduleAssistant.Domain;
using Xunit;

namespace ScheduleAssistant.Infrastructure.Tests;

public sealed class SqliteRepositoryCrudTests
{
    [Fact]
    public async Task CoreRepositories_ShouldPersistReloadUpdateAndDeleteMetadata()
    {
        await using var database = await PersistenceTestDatabase.CreateAsync();
        var category = PersistenceTestData.CreateCategory();
        var categoryCommit = await database.Categories.AddAsync(category);
        var series = PersistenceTestData.CreateSeries(category.Id);
        var seriesCommit = await database.Series.AddAsync(series);
        var occurrenceDate = new OccurrenceDate(new DateOnly(2026, 2, 28));
        var task = PersistenceTestData.CreateTask(
            category.Id,
            plannedDate: occurrenceDate.Date,
            seriesId: series.Id,
            occurrenceDate: occurrenceDate);
        await database.Tasks.AddAsync(task);
        var reminderBeforeDeadline = Reminder.Create(
            Guid.NewGuid(),
            task.Id,
            -1_440,
            PersistenceTestData.CreatedAtUtc.AddDays(1),
            "deadline-minus-one-day");
        var reminderAtDeadline = Reminder.Create(
            Guid.NewGuid(),
            task.Id,
            0,
            PersistenceTestData.CreatedAtUtc.AddDays(2),
            "deadline-at-time");
        await database.Reminders.AddAsync(reminderBeforeDeadline);
        await database.Reminders.AddAsync(reminderAtDeadline);
        var attachment = Attachment.Create(
            Guid.NewGuid(),
            task.Id,
            "材料 说明.pdf",
            $"{task.Id:N}/材料 说明.pdf",
            123_456,
            PersistenceTestData.CreatedAtUtc.AddHours(1),
            ".PDF",
            "application/pdf",
            new string('A', 64));
        await database.Attachments.AddAsync(attachment);
        var exclusion = new ScheduleAssistant.Application.Abstractions.Persistence.RecurrenceExclusion(
            series.Id,
            occurrenceDate.Date);
        await database.Exclusions.AddAsync(exclusion);

        var reloadedCategory = await database.Categories.GetByIdAsync(category.Id);
        var reloadedSeries = await database.Series.GetByIdAsync(series.Id);
        var reloadedTask = await database.Tasks.GetByIdAsync(task.Id);
        var reloadedReminders = await database.Reminders.GetByTaskIdAsync(task.Id);
        var reloadedAttachment = await database.Attachments.GetByIdAsync(attachment.Id);
        var reloadedExclusions = await database.Exclusions.GetBySeriesAndRangeAsync(
            series.Id,
            new DateOnly(2026, 2, 1),
            new DateOnly(2026, 3, 1));

        Assert.Equal(category.Id, reloadedCategory!.Id);
        Assert.Equal(category.Name, reloadedCategory.Name);
        Assert.Equal(category.ColorHex, reloadedCategory.ColorHex);
        Assert.Equal(categoryCommit.NewVersion, reloadedCategory.Version);
        Assert.Equal(series.Rule, reloadedSeries!.Rule);
        Assert.Equal(series.PlannedStart, reloadedSeries.PlannedStart);
        Assert.Equal(task.OccurrenceDate, reloadedTask!.OccurrenceDate);
        Assert.Equal(2, reloadedReminders.Count);
        Assert.Equal(reminderBeforeDeadline.DeduplicationKey, reloadedReminders[0].DeduplicationKey);
        Assert.Equal(attachment.DisplayName, reloadedAttachment!.DisplayName);
        Assert.Equal(attachment.ManagedRelativePath, reloadedAttachment.ManagedRelativePath);
        var renamedAttachment = Attachment.Rehydrate(
            attachment.Id,
            attachment.TaskId,
            "材料最终版.pdf",
            attachment.ManagedRelativePath,
            attachment.SizeBytes,
            attachment.Extension,
            attachment.MimeType,
            attachment.Sha256,
            attachment.ImportedAtUtc);
        await database.Attachments.UpdateDisplayNameAsync(renamedAttachment);
        Assert.Equal("材料最终版.pdf", (await database.Attachments.GetByIdAsync(attachment.Id))!.DisplayName);
        Assert.Single(await database.Attachments.GetAllAsync());
        Assert.Single(reloadedExclusions);

        category.UpdateDetails("更新分类", "#AABBCC", 20, isArchived: true, PersistenceTestData.CreatedAtUtc.AddDays(1));
        var updatedCategory = await database.Categories.UpdateAsync(category, category.Version);
        series.UpdateDetails(
            "更新周期",
            series.CategoryId,
            series.Priority,
            series.Rule,
            series.PlannedStart,
            series.PlannedEnd,
            series.Location,
            series.Description,
            series.Materials,
            series.Notes,
            isEnabled: false,
            PersistenceTestData.CreatedAtUtc.AddDays(1));
        var updatedSeries = await database.Series.UpdateAsync(series, series.Version);
        reminderBeforeDeadline.MarkDelivered(PersistenceTestData.CreatedAtUtc.AddDays(3));
        await database.Reminders.UpdateAsync(reminderBeforeDeadline);

        Assert.Equal(2, updatedCategory.NewVersion);
        Assert.True(updatedCategory.Entity.IsArchived);
        Assert.Equal(2, updatedSeries.NewVersion);
        Assert.False(updatedSeries.Entity.IsEnabled);
        Assert.Equal(ReminderStatus.Delivered, (await database.Reminders.GetByIdAsync(reminderBeforeDeadline.Id))!.Status);

        await database.Exclusions.DeleteAsync(series.Id, occurrenceDate.Date);
        await database.Tasks.DeleteAsync(task.Id);
        Assert.Empty(await database.Reminders.GetByTaskIdAsync(task.Id));
        Assert.Empty(await database.Attachments.GetByTaskIdAsync(task.Id));
        Assert.False(await database.Exclusions.ExistsAsync(series.Id, occurrenceDate.Date));
        Assert.Null(await database.Tasks.GetByIdAsync(task.Id));
    }
}
