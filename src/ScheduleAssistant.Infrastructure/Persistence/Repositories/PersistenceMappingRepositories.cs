using System.IO;
using Microsoft.Data.Sqlite;
using ScheduleAssistant.Application.Abstractions.Persistence;
using ScheduleAssistant.Domain;

namespace ScheduleAssistant.Infrastructure.Persistence.Repositories;

internal static class PersistenceExceptionMapper
{
    public static bool IsProviderFailure(Exception exception)
    {
        return exception is SqliteException
            or IOException
            or UnauthorizedAccessException
            or TimeoutException;
    }

    public static PersistenceFailureException MapProviderFailure(string operation, Exception exception)
    {
        return exception switch
        {
            SqliteException sqliteException => new PersistenceFailureException(
                Classify(sqliteException),
                operation,
                sqliteException),
            IOException or UnauthorizedAccessException or TimeoutException => new PersistenceFailureException(
                PersistenceFailureKind.Unavailable,
                operation,
                exception),
            _ => new PersistenceFailureException(PersistenceFailureKind.Unknown, operation, exception)
        };
    }

    public static async Task<T> ExecuteAsync<T>(string operation, Func<Task<T>> action)
    {
        try
        {
            return await action().ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception) when (
            exception is PersistenceConflictException
            or PersistenceNotFoundException
            or PersistenceFailureException)
        {
            throw;
        }
        catch (SqliteException exception)
        {
            throw MapProviderFailure(operation, exception);
        }
        catch (Exception exception) when (
            exception is IOException
            or UnauthorizedAccessException
            or TimeoutException)
        {
            throw new PersistenceFailureException(PersistenceFailureKind.Unavailable, operation, exception);
        }
    }

    public static async Task ExecuteAsync(string operation, Func<Task> action)
    {
        try
        {
            await action().ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception) when (
            exception is PersistenceConflictException
            or PersistenceNotFoundException
            or PersistenceFailureException)
        {
            throw;
        }
        catch (SqliteException exception)
        {
            throw MapProviderFailure(operation, exception);
        }
        catch (Exception exception) when (
            exception is IOException
            or UnauthorizedAccessException
            or TimeoutException)
        {
            throw new PersistenceFailureException(PersistenceFailureKind.Unavailable, operation, exception);
        }
    }

    private static PersistenceFailureKind Classify(SqliteException exception)
    {
        return exception.SqliteErrorCode switch
        {
            5 or 6 or 8 or 10 or 13 or 14 or 15 => PersistenceFailureKind.Unavailable,
            _ => PersistenceFailureKind.Constraint
        };
    }
}

internal sealed class PersistenceMappingTaskRepository : ITaskRepository
{
    private readonly ITaskRepository _inner;

    public PersistenceMappingTaskRepository(ITaskRepository inner)
    {
        _inner = inner;
    }

    public Task<TaskItem?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        PersistenceExceptionMapper.ExecuteAsync("Task.GetById", () => _inner.GetByIdAsync(id, cancellationToken));

    public Task<TaskItem?> GetByIdAsync(Guid id, IPersistenceTransaction transaction, CancellationToken cancellationToken = default) =>
        PersistenceExceptionMapper.ExecuteAsync("Task.GetByIdInTransaction", () => _inner.GetByIdAsync(id, transaction, cancellationToken));

    public Task<IReadOnlyList<TaskItem>> GetPlannedByDateAsync(DateOnly plannedOn, CancellationToken cancellationToken = default) =>
        PersistenceExceptionMapper.ExecuteAsync("Task.GetPlannedByDate", () => _inner.GetPlannedByDateAsync(plannedOn, cancellationToken));

    public Task<IReadOnlyList<TaskItem>> GetByRangeAsync(DateOnly rangeStart, DateOnly rangeEnd, CancellationToken cancellationToken = default) =>
        PersistenceExceptionMapper.ExecuteAsync("Task.GetByRange", () => _inner.GetByRangeAsync(rangeStart, rangeEnd, cancellationToken));

    public Task<IReadOnlyList<TaskItem>> GetTodayPendingAsync(DateOnly todayLocal, DateTimeOffset nowUtc, CancellationToken cancellationToken = default) =>
        PersistenceExceptionMapper.ExecuteAsync("Task.GetTodayPending", () => _inner.GetTodayPendingAsync(todayLocal, nowUtc, cancellationToken));

    public Task<IReadOnlyList<TaskItem>> GetUpcomingDeadlinesAsync(DateTimeOffset nowUtc, DateTimeOffset? untilUtc, CancellationToken cancellationToken = default) =>
        PersistenceExceptionMapper.ExecuteAsync("Task.GetUpcomingDeadlines", () => _inner.GetUpcomingDeadlinesAsync(nowUtc, untilUtc, cancellationToken));

    public Task<IReadOnlyList<TaskItem>> GetDeadlinesAsync(DateTimeOffset nowUtc, DateTimeOffset? untilUtc, bool includeOverdue, CancellationToken cancellationToken = default) =>
        PersistenceExceptionMapper.ExecuteAsync("Task.GetDeadlines", () => _inner.GetDeadlinesAsync(nowUtc, untilUtc, includeOverdue, cancellationToken));

    public Task<IReadOnlyList<TaskItem>> SearchAsync(TaskSearchFilter filter, long offset, int limit, CancellationToken cancellationToken = default) =>
        PersistenceExceptionMapper.ExecuteAsync("Task.Search", () => _inner.SearchAsync(filter, offset, limit, cancellationToken));

    public Task<long> CountSearchAsync(TaskSearchFilter filter, CancellationToken cancellationToken = default) =>
        PersistenceExceptionMapper.ExecuteAsync("Task.CountSearch", () => _inner.CountSearchAsync(filter, cancellationToken));

    public Task<PersistenceCommitResult<TaskItem>> AddAsync(TaskItem item, CancellationToken cancellationToken = default) =>
        PersistenceExceptionMapper.ExecuteAsync("Task.Add", () => _inner.AddAsync(item, cancellationToken));

    public Task<PersistenceCommitResult<TaskItem>> AddAsync(TaskItem item, IPersistenceTransaction transaction, CancellationToken cancellationToken = default) =>
        PersistenceExceptionMapper.ExecuteAsync("Task.AddInTransaction", () => _inner.AddAsync(item, transaction, cancellationToken));

    public Task<PersistenceCommitResult<TaskItem>> UpdateAsync(TaskItem item, long expectedVersion, CancellationToken cancellationToken = default) =>
        PersistenceExceptionMapper.ExecuteAsync("Task.Update", () => _inner.UpdateAsync(item, expectedVersion, cancellationToken));

    public Task<PersistenceCommitResult<TaskItem>> UpdateAsync(TaskItem item, long expectedVersion, IPersistenceTransaction transaction, CancellationToken cancellationToken = default) =>
        PersistenceExceptionMapper.ExecuteAsync("Task.UpdateInTransaction", () => _inner.UpdateAsync(item, expectedVersion, transaction, cancellationToken));

    public Task DeleteAsync(Guid id, CancellationToken cancellationToken = default) =>
        PersistenceExceptionMapper.ExecuteAsync("Task.Delete", () => _inner.DeleteAsync(id, cancellationToken));

    public Task DeleteAsync(Guid id, IPersistenceTransaction transaction, CancellationToken cancellationToken = default) =>
        PersistenceExceptionMapper.ExecuteAsync("Task.DeleteInTransaction", () => _inner.DeleteAsync(id, transaction, cancellationToken));

    public Task<bool> DeleteAsync(Guid id, long expectedVersion, CancellationToken cancellationToken = default) =>
        PersistenceExceptionMapper.ExecuteAsync("Task.DeleteConditional", () => _inner.DeleteAsync(id, expectedVersion, cancellationToken));

    public Task<bool> DeleteAsync(Guid id, long expectedVersion, IPersistenceTransaction transaction, CancellationToken cancellationToken = default) =>
        PersistenceExceptionMapper.ExecuteAsync("Task.DeleteConditionalInTransaction", () => _inner.DeleteAsync(id, expectedVersion, transaction, cancellationToken));
}

internal sealed class PersistenceMappingCategoryRepository : ICategoryRepository
{
    private readonly ICategoryRepository _inner;

    public PersistenceMappingCategoryRepository(ICategoryRepository inner)
    {
        _inner = inner;
    }

    public Task<Category?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        PersistenceExceptionMapper.ExecuteAsync("Category.GetById", () => _inner.GetByIdAsync(id, cancellationToken));

    public Task<Category?> GetByIdAsync(Guid id, IPersistenceTransaction transaction, CancellationToken cancellationToken = default) =>
        PersistenceExceptionMapper.ExecuteAsync("Category.GetByIdInTransaction", () => _inner.GetByIdAsync(id, transaction, cancellationToken));

    public Task<IReadOnlyList<Category>> GetAllAsync(CancellationToken cancellationToken = default) =>
        PersistenceExceptionMapper.ExecuteAsync("Category.GetAll", () => _inner.GetAllAsync(cancellationToken));

    public Task<PersistenceCommitResult<Category>> AddAsync(Category category, CancellationToken cancellationToken = default) =>
        PersistenceExceptionMapper.ExecuteAsync("Category.Add", () => _inner.AddAsync(category, cancellationToken));

    public Task<PersistenceCommitResult<Category>> AddAsync(Category category, IPersistenceTransaction transaction, CancellationToken cancellationToken = default) =>
        PersistenceExceptionMapper.ExecuteAsync("Category.AddInTransaction", () => _inner.AddAsync(category, transaction, cancellationToken));

    public Task<PersistenceCommitResult<Category>> UpdateAsync(Category category, long expectedVersion, CancellationToken cancellationToken = default) =>
        PersistenceExceptionMapper.ExecuteAsync("Category.Update", () => _inner.UpdateAsync(category, expectedVersion, cancellationToken));

    public Task<PersistenceCommitResult<Category>> UpdateAsync(Category category, long expectedVersion, IPersistenceTransaction transaction, CancellationToken cancellationToken = default) =>
        PersistenceExceptionMapper.ExecuteAsync("Category.UpdateInTransaction", () => _inner.UpdateAsync(category, expectedVersion, transaction, cancellationToken));

    public Task DeleteAsync(Guid id, CancellationToken cancellationToken = default) =>
        PersistenceExceptionMapper.ExecuteAsync("Category.Delete", () => _inner.DeleteAsync(id, cancellationToken));

    public Task DeleteAsync(Guid id, IPersistenceTransaction transaction, CancellationToken cancellationToken = default) =>
        PersistenceExceptionMapper.ExecuteAsync("Category.DeleteInTransaction", () => _inner.DeleteAsync(id, transaction, cancellationToken));
}

internal sealed class PersistenceMappingReminderRepository : IReminderRepository
{
    private readonly IReminderRepository _inner;

    public PersistenceMappingReminderRepository(IReminderRepository inner)
    {
        _inner = inner;
    }

    public Task<Reminder?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        PersistenceExceptionMapper.ExecuteAsync("Reminder.GetById", () => _inner.GetByIdAsync(id, cancellationToken));

    public Task<IReadOnlyList<Reminder>> GetByTaskIdAsync(Guid taskId, CancellationToken cancellationToken = default) =>
        PersistenceExceptionMapper.ExecuteAsync("Reminder.GetByTaskId", () => _inner.GetByTaskIdAsync(taskId, cancellationToken));

    public Task<IReadOnlyList<Reminder>> GetByTaskIdAsync(Guid taskId, IPersistenceTransaction transaction, CancellationToken cancellationToken = default) =>
        PersistenceExceptionMapper.ExecuteAsync("Reminder.GetByTaskIdInTransaction", () => _inner.GetByTaskIdAsync(taskId, transaction, cancellationToken));

    public Task<Reminder?> GetNextPendingAsync(CancellationToken cancellationToken = default) =>
        PersistenceExceptionMapper.ExecuteAsync("Reminder.GetNextPending", () => _inner.GetNextPendingAsync(cancellationToken));

    public Task AddAsync(Reminder reminder, CancellationToken cancellationToken = default) =>
        PersistenceExceptionMapper.ExecuteAsync("Reminder.Add", () => _inner.AddAsync(reminder, cancellationToken));

    public Task AddAsync(Reminder reminder, IPersistenceTransaction transaction, CancellationToken cancellationToken = default) =>
        PersistenceExceptionMapper.ExecuteAsync("Reminder.AddInTransaction", () => _inner.AddAsync(reminder, transaction, cancellationToken));

    public Task UpdateAsync(Reminder reminder, CancellationToken cancellationToken = default) =>
        PersistenceExceptionMapper.ExecuteAsync("Reminder.Update", () => _inner.UpdateAsync(reminder, cancellationToken));

    public Task UpdateAsync(Reminder reminder, IPersistenceTransaction transaction, CancellationToken cancellationToken = default) =>
        PersistenceExceptionMapper.ExecuteAsync("Reminder.UpdateInTransaction", () => _inner.UpdateAsync(reminder, transaction, cancellationToken));

    public Task DeleteAsync(Guid id, CancellationToken cancellationToken = default) =>
        PersistenceExceptionMapper.ExecuteAsync("Reminder.Delete", () => _inner.DeleteAsync(id, cancellationToken));

    public Task DeleteAsync(Guid id, IPersistenceTransaction transaction, CancellationToken cancellationToken = default) =>
        PersistenceExceptionMapper.ExecuteAsync("Reminder.DeleteInTransaction", () => _inner.DeleteAsync(id, transaction, cancellationToken));

    public Task<int> CancelPendingByTaskIdAsync(Guid taskId, IPersistenceTransaction transaction, CancellationToken cancellationToken = default) =>
        PersistenceExceptionMapper.ExecuteAsync("Reminder.CancelPendingByTaskIdInTransaction", () => _inner.CancelPendingByTaskIdAsync(taskId, transaction, cancellationToken));
}
