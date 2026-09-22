using ScheduleAssistant.Application.Abstractions.Attachments;
using Microsoft.Data.Sqlite;
using ScheduleAssistant.Application.Abstractions.Persistence;
using ScheduleAssistant.Domain;
using ScheduleAssistant.Infrastructure.Persistence;
using ScheduleAssistant.Infrastructure.Persistence.Repositories;

namespace ScheduleAssistant.Infrastructure.Tests;

internal sealed class PersistenceTestDatabase : IAsyncDisposable
{
    private PersistenceTestDatabase(string rootDirectory)
    {
        RootDirectory = rootDirectory;
        Paths = new AppPaths(rootDirectory);
        ConnectionFactory = new SqliteConnectionFactory(Paths);
        TransactionFactory = new SqlitePersistenceTransactionFactory(ConnectionFactory);
        Initializer = new SqliteDatabaseInitializer(ConnectionFactory);
        Tasks = new SqliteTaskRepository(ConnectionFactory, TransactionFactory);
        Categories = new SqliteCategoryRepository(ConnectionFactory, TransactionFactory);
        Series = new SqliteRecurrenceSeriesRepository(ConnectionFactory, TransactionFactory);
        Reminders = new SqliteReminderRepository(ConnectionFactory, TransactionFactory);
        Attachments = new SqliteAttachmentRepository(ConnectionFactory, TransactionFactory);
        CleanupQueue = new SqliteAttachmentCleanupQueue(ConnectionFactory, TransactionFactory);
        Exclusions = new SqliteRecurrenceExclusionRepository(ConnectionFactory, TransactionFactory);
    }

    public string RootDirectory { get; }

    public AppPaths Paths { get; }

    public SqliteConnectionFactory ConnectionFactory { get; }

    public SqlitePersistenceTransactionFactory TransactionFactory { get; }

    public SqliteDatabaseInitializer Initializer { get; }

    public ITaskRepository Tasks { get; }

    public ICategoryRepository Categories { get; }

    public IRecurrenceSeriesRepository Series { get; }

    public IReminderRepository Reminders { get; }

    public IAttachmentRepository Attachments { get; }

    public IAttachmentCleanupQueue CleanupQueue { get; }

    public IRecurrenceExclusionRepository Exclusions { get; }

    public static async Task<PersistenceTestDatabase> CreateAsync()
    {
        var root = Path.Combine(Path.GetTempPath(), "ScheduleAssistant-DEV020-" + Guid.NewGuid().ToString("N"));
        var database = new PersistenceTestDatabase(root);
        await database.Initializer.InitializeAsync().ConfigureAwait(false);
        return database;
    }

    public static PersistenceTestDatabase OpenExisting(string rootDirectory)
    {
        return new PersistenceTestDatabase(rootDirectory);
    }

    public async ValueTask DisposeAsync()
    {
        await Task.CompletedTask.ConfigureAwait(false);
        SqliteConnection.ClearAllPools();
        if (Directory.Exists(RootDirectory))
        {
            Directory.Delete(RootDirectory, recursive: true);
        }
    }
}

internal static class PersistenceTestData
{
    public static readonly DateTimeOffset CreatedAtUtc = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    public static Category CreateCategory(Guid? id = null)
    {
        return Category.Create(
            id ?? Guid.NewGuid(),
            "科研",
            "#1A2B3C",
            10,
            CreatedAtUtc,
            isBuiltIn: false);
    }

    public static TaskItem CreateTask(
        Guid categoryId,
        Guid? id = null,
        DateOnly? plannedDate = null,
        ZonedDeadline? deadline = null,
        Guid? seriesId = null,
        OccurrenceDate? occurrenceDate = null)
    {
        return TaskItem.Create(
            id ?? Guid.NewGuid(),
            "持久化任务",
            categoryId,
            TaskPriority.Important,
            CreatedAtUtc,
            plannedDate,
            plannedDate.HasValue ? new TimeOnly(9, 10, 11).Add(TimeSpan.FromTicks(1_234_567)) : null,
            plannedDate.HasValue ? new TimeOnly(16, 20, 30).Add(TimeSpan.FromTicks(7_654_321)) : null,
            deadline,
            "上海\n办公室",
            "说明\n含换行",
            null,
            "备注🙂",
            seriesId,
            occurrenceDate);
    }

    public static ZonedDeadline CreateDeadline(DateOnly date)
    {
        return ZonedDeadline.CreateResolvedUtc(
            date,
            new TimeOnly(23, 59, 58).Add(TimeSpan.FromTicks(1_234_567)),
            "China Standard Time",
            new DateTimeOffset(date.ToDateTime(new TimeOnly(15, 59, 58).Add(TimeSpan.FromTicks(1_234_567))), TimeSpan.Zero));
    }

    public static RecurrenceSeries CreateSeries(Guid categoryId, Guid? id = null)
    {
        var rule = RecurrenceRule.CreateMonthly(
            new DateOnly(2026, 1, 1),
            31,
            "China Standard Time");
        return RecurrenceSeries.Create(
            id ?? Guid.NewGuid(),
            "周期报销",
            categoryId,
            TaskPriority.Normal,
            rule,
            CreatedAtUtc,
            new TimeOnly(8, 0).Add(TimeSpan.FromTicks(123)),
            new TimeOnly(9, 0).Add(TimeSpan.FromTicks(456)),
            "财务",
            "系列说明",
            "发票",
            "系列备注");
    }
}
