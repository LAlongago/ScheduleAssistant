using ScheduleAssistant.Domain;
using Xunit;

namespace ScheduleAssistant.Domain.Tests;

public sealed class MetadataEntityTests
{
    [Fact]
    public void ZonedDeadline_CreateResolvedUtc_ShouldPreserveLocalFieldsAndNormalizeOnlyUtc()
    {
        var localDate = new DateOnly(2026, 11, 1);
        var localTime = new TimeOnly(1, 30);
        var suppliedUtc = new DateTimeOffset(2026, 10, 31, 17, 30, 0, TimeSpan.FromHours(8));

        var deadline = ZonedDeadline.CreateResolvedUtc(
            localDate,
            localTime,
            "Pacific Standard Time",
            suppliedUtc);

        Assert.Equal(localDate, deadline.LocalDate);
        Assert.Equal(localTime, deadline.LocalTime);
        Assert.Equal("Pacific Standard Time", deadline.TimeZoneId);
        Assert.Equal(suppliedUtc.ToUniversalTime(), deadline.Utc);
        Assert.Equal(TimeSpan.Zero, deadline.Utc.Offset);
    }

    [Fact]
    public void ZonedDeadline_WhenTimezoneIdIsBlank_ShouldRejectIt()
    {
        Assert.Throws<DomainValidationException>(() => new ZonedDeadline(
            new DateOnly(2026, 1, 1),
            new TimeOnly(1, 0),
            " ",
            DomainTestData.CreatedAtUtc));
    }

    [Fact]
    public void Category_Create_ShouldNormalizeNameColorAndUtcTimestamp()
    {
        var createdAt = new DateTimeOffset(2026, 1, 1, 8, 0, 0, TimeSpan.FromHours(8));

        var category = Category.Create(
            DomainTestData.CategoryId,
            "  科研  ",
            "#ab12ef",
            3,
            createdAt,
            isBuiltIn: true);

        Assert.Equal("科研", category.Name);
        Assert.Equal("#AB12EF", category.ColorHex);
        Assert.True(category.IsBuiltIn);
        Assert.False(category.IsArchived);
        Assert.Equal(createdAt.ToUniversalTime(), category.CreatedAtUtc);
        Assert.Equal(1, category.Version);
    }

    [Theory]
    [InlineData("")]
    [InlineData("123456")]
    [InlineData("#12345")]
    [InlineData("#1234567")]
    [InlineData("#GGGGGG")]
    public void Category_Create_WhenColorIsNotSixDigitHex_ShouldRejectIt(string color)
    {
        Assert.Throws<DomainValidationException>(() => Category.Create(
            DomainTestData.CategoryId,
            "Category",
            color,
            0,
            DomainTestData.CreatedAtUtc));
    }

    [Fact]
    public void Category_UpdateDetails_WhenInputIsInvalid_ShouldLeaveMetadataUnchanged()
    {
        var category = Category.Create(
            DomainTestData.CategoryId,
            "Original",
            "#123456",
            1,
            DomainTestData.CreatedAtUtc);

        Assert.Throws<DomainValidationException>(() => category.UpdateDetails(
            "Changed",
            "#123456",
            2,
            true,
            DomainTestData.AtUtc(2025, 12, 31)));

        Assert.Equal("Original", category.Name);
        Assert.Equal("#123456", category.ColorHex);
        Assert.Equal(1, category.SortOrder);
        Assert.False(category.IsArchived);
    }

    [Fact]
    public void Category_Rehydrate_ShouldRestoreArchiveAndVersionMetadata()
    {
        var category = Category.Rehydrate(
            DomainTestData.CategoryId,
            "Category",
            "#123456",
            4,
            isBuiltIn: false,
            isArchived: true,
            DomainTestData.CreatedAtUtc,
            DomainTestData.AtUtc(2026, 1, 2),
            version: 8);

        Assert.True(category.IsArchived);
        Assert.Equal(4, category.SortOrder);
        Assert.Equal(8, category.Version);
    }

    [Fact]
    public void RecurrenceSeries_Create_ShouldKeepRuleAsTheSoleRangeAndTimezoneAuthority()
    {
        var rule = RecurrenceRule.CreateWeekly(
            new DateOnly(2026, 1, 5),
            RecurrenceWeekdayMask.Monday,
            "Custom Windows Zone");

        var series = RecurrenceSeries.Create(
            Guid.Parse("33333333-3333-3333-3333-333333333333"),
            "  Weekly review  ",
            DomainTestData.CategoryId,
            TaskPriority.Important,
            rule,
            DomainTestData.CreatedAtUtc,
            plannedStart: new TimeOnly(9, 0),
            plannedEnd: new TimeOnly(10, 0),
            location: "Office",
            description: "Details",
            materials: "Materials",
            notes: "Notes");

        Assert.Equal("Weekly review", series.Title);
        Assert.Same(rule, series.Rule);
        Assert.Equal(rule.EffectiveDate, series.EffectiveDate);
        Assert.Equal(rule.TimeZoneId, series.TimeZoneId);
        Assert.Equal(new TimeOnly(9, 0), series.PlannedStart);
        Assert.Equal(new TimeOnly(10, 0), series.PlannedEnd);
        Assert.Equal(1, series.Version);
    }

    [Fact]
    public void RecurrenceSeries_Rehydrate_WhenRuleIsNull_ShouldRejectIt()
    {
        Assert.Throws<ArgumentNullException>(() => RecurrenceSeries.Rehydrate(
            Guid.Parse("33333333-3333-3333-3333-333333333333"),
            "Series",
            DomainTestData.CategoryId,
            TaskPriority.Normal,
            null!,
            null,
            null,
            null,
            null,
            null,
            null,
            true,
            DomainTestData.CreatedAtUtc,
            DomainTestData.CreatedAtUtc,
            1));
    }

    [Fact]
    public void Reminder_Create_ShouldAllowMultipleNodesForOneTaskAndNormalizeOffsetTimestamp()
    {
        var scheduled = new DateTimeOffset(2026, 1, 2, 8, 0, 0, TimeSpan.FromHours(8));
        var first = Reminder.Create(
            Guid.Parse("44444444-4444-4444-4444-444444444444"),
            DomainTestData.TaskId,
            -1_440,
            scheduled,
            "task-1-before-deadline");
        var second = Reminder.Create(
            Guid.Parse("55555555-5555-5555-5555-555555555555"),
            DomainTestData.TaskId,
            0,
            scheduled,
            "task-1-at-deadline");

        Assert.Equal(-1_440, first.RelativeOffsetMinutes);
        Assert.Equal(scheduled.ToUniversalTime(), first.ScheduledAtUtc);
        Assert.Equal(DomainTestData.TaskId, second.TaskId);
        Assert.NotEqual(first.Id, second.Id);
        Assert.Equal(ReminderStatus.Pending, first.Status);
    }

    [Fact]
    public void Reminder_StateTransitions_ShouldKeepDeliveryMetadataConsistent()
    {
        var reminder = Reminder.Create(
            Guid.Parse("44444444-4444-4444-4444-444444444444"),
            DomainTestData.TaskId,
            -1_440,
            DomainTestData.AtUtc(2026, 1, 2),
            "dedupe");

        reminder.MarkDelivered(DomainTestData.AtUtc(2026, 1, 1));
        reminder.MarkDelivered(DomainTestData.AtUtc(2026, 1, 3));

        Assert.Equal(ReminderStatus.Delivered, reminder.Status);
        Assert.Equal(DomainTestData.AtUtc(2026, 1, 1), reminder.DeliveredAtUtc);
        Assert.Null(reminder.ErrorCode);
        Assert.Throws<InvalidOperationException>(() => reminder.Cancel());
    }

    [Fact]
    public void Reminder_WhenExpiredOrFailed_ShouldExposeExplicitState()
    {
        var expired = Reminder.Create(
            Guid.Parse("44444444-4444-4444-4444-444444444444"),
            DomainTestData.TaskId,
            -1_440,
            DomainTestData.AtUtc(2026, 1, 2),
            "expired");
        expired.MarkExpired();
        expired.MarkExpired();

        var failed = Reminder.Create(
            Guid.Parse("55555555-5555-5555-5555-555555555555"),
            DomainTestData.TaskId,
            -1_440,
            DomainTestData.AtUtc(2026, 1, 2),
            "failed");
        failed.MarkFailed("notification-unavailable");

        Assert.Equal(ReminderStatus.Expired, expired.Status);
        Assert.Equal(ReminderStatus.Failed, failed.Status);
        Assert.Equal("notification-unavailable", failed.ErrorCode);
        Assert.Null(failed.DeliveredAtUtc);
    }

    [Fact]
    public void Reminder_Rehydrate_WhenStatusMetadataIsInconsistent_ShouldRejectIt()
    {
        Assert.Throws<DomainValidationException>(() => Reminder.Rehydrate(
            Guid.Parse("44444444-4444-4444-4444-444444444444"),
            DomainTestData.TaskId,
            -1_440,
            DomainTestData.AtUtc(2026, 1, 2),
            null,
            ReminderStatus.Delivered,
            "dedupe",
            null));
        Assert.Throws<DomainValidationException>(() => Reminder.Rehydrate(
            Guid.Parse("44444444-4444-4444-4444-444444444444"),
            DomainTestData.TaskId,
            -1_440,
            DomainTestData.AtUtc(2026, 1, 2),
            null,
            ReminderStatus.Pending,
            "dedupe",
            "unexpected-error"));
    }

    [Fact]
    public void Attachment_Create_ShouldPreserveDisplayNameAndNormalizeManagedMetadata()
    {
        var importedAt = new DateTimeOffset(2026, 1, 1, 8, 0, 0, TimeSpan.FromHours(8));
        var attachment = Attachment.Create(
            Guid.Parse("66666666-6666-6666-6666-666666666666"),
            DomainTestData.TaskId,
            " 中文 资料 .PDF ",
            "attachments\\task\\中文 资料.pdf",
            0,
            importedAt,
            extension: ".PDF",
            mimeType: " application/pdf ",
            sha256: new string('A', 64));

        Assert.Equal(" 中文 资料 .PDF ", attachment.DisplayName);
        Assert.Equal("attachments/task/中文 资料.pdf", attachment.ManagedRelativePath);
        Assert.Equal(".pdf", attachment.Extension);
        Assert.Equal("application/pdf", attachment.MimeType);
        Assert.Equal(new string('a', 64), attachment.Sha256);
        Assert.Equal(importedAt.ToUniversalTime(), attachment.ImportedAtUtc);
    }

    [Theory]
    [InlineData("C:\\managed\\file.pdf")]
    [InlineData("D:file.pdf")]
    [InlineData("\\\\server\\share\\file.pdf")]
    [InlineData("/root/file.pdf")]
    [InlineData("../file.pdf")]
    [InlineData("folder/../file.pdf")]
    [InlineData("folder\\..\\file.pdf")]
    public void Attachment_NormalizeManagedRelativePath_WhenPathCanEscapeRoot_ShouldRejectIt(string path)
    {
        Assert.Throws<DomainValidationException>(() => Attachment.NormalizeManagedRelativePath(path));
    }

    [Fact]
    public void Attachment_NormalizeManagedRelativePath_WhenPathIsSafe_ShouldNormalizeSeparatorsAndDots()
    {
        Assert.Equal(
            "attachments/task/file.pdf",
            Attachment.NormalizeManagedRelativePath("attachments\\task\\.\\file.pdf"));
    }

    [Fact]
    public void Attachment_Create_WhenSizeOrMetadataIsInvalid_ShouldRejectIt()
    {
        Assert.Throws<DomainValidationException>(() => Attachment.Create(
            Guid.Parse("66666666-6666-6666-6666-666666666666"),
            DomainTestData.TaskId,
            "file.pdf",
            "attachments/file.pdf",
            -1,
            DomainTestData.CreatedAtUtc));
        Assert.Throws<DomainValidationException>(() => Attachment.Create(
            Guid.Parse("66666666-6666-6666-6666-666666666666"),
            DomainTestData.TaskId,
            "file.pdf",
            "attachments/file.pdf",
            1,
            DomainTestData.CreatedAtUtc,
            extension: "pdf"));
        Assert.Throws<DomainValidationException>(() => Attachment.Create(
            Guid.Parse("66666666-6666-6666-6666-666666666666"),
            DomainTestData.TaskId,
            "file.pdf",
            "attachments/file.pdf",
            1,
            DomainTestData.CreatedAtUtc,
            sha256: "not-a-hash"));
    }
}
