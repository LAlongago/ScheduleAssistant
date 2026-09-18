using Microsoft.Extensions.DependencyInjection;
using Microsoft.Data.Sqlite;
using ScheduleAssistant.Application.Abstractions.Persistence;
using ScheduleAssistant.Domain;
using ScheduleAssistant.Infrastructure.Composition;
using ScheduleAssistant.Infrastructure.Persistence;
using Xunit;

namespace ScheduleAssistant.Infrastructure.Tests;

public sealed class PersistenceExceptionMappingTests
{
    [Fact]
    public async Task TransactionFactory_WhenDatabasePathCannotBeOpened_ShouldExposeProviderNeutralFailure()
    {
        var filePath = Path.Combine(Path.GetTempPath(), "ScheduleAssistant-DEV030-map-file-" + Guid.NewGuid().ToString("N"));
        await File.WriteAllTextAsync(filePath, "not a directory");
        try
        {
            var paths = new AppPaths(filePath);
            var factory = new SqlitePersistenceTransactionFactory(new SqliteConnectionFactory(paths));

            var exception = await Assert.ThrowsAsync<PersistenceFailureException>(() => factory.BeginAsync());

            Assert.Equal(PersistenceFailureKind.Unavailable, exception.Kind);
            Assert.Equal("Transaction.Begin", exception.Operation);
            Assert.DoesNotContain("SQLite", exception.Message, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            File.Delete(filePath);
        }
    }

    [Fact]
    public async Task ComposedRepositories_WhenSqliteRejectsConstraint_ShouldExposeProviderNeutralFailure()
    {
        var root = Path.Combine(Path.GetTempPath(), "ScheduleAssistant-DEV030-map-" + Guid.NewGuid().ToString("N"));
        try
        {
            using (var services = new ServiceCollection()
                .AddInfrastructure(root)
                .BuildServiceProvider())
            {
                await services.GetRequiredService<SqliteDatabaseInitializer>().InitializeAsync();

                var task = TaskItem.Create(
                    Guid.NewGuid(),
                    "missing category",
                    Guid.NewGuid(),
                    new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero));

                var exception = await Assert.ThrowsAsync<PersistenceFailureException>(() =>
                    services.GetRequiredService<ITaskRepository>().AddAsync(task));

                Assert.Equal(PersistenceFailureKind.Constraint, exception.Kind);
                Assert.Equal("Task.Add", exception.Operation);
                Assert.DoesNotContain("SQLite", exception.Message, StringComparison.OrdinalIgnoreCase);
            }

            SqliteConnection.ClearAllPools();
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }
}
