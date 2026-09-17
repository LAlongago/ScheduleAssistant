using System.Globalization;
using Microsoft.Data.Sqlite;
using Xunit;

namespace ScheduleAssistant.Infrastructure.Tests;

public sealed class SqliteMigrationTests
{
    [Fact]
    public async Task InitializeAsync_WhenDatabaseIsEmpty_ShouldApplySchemaAndRepeatWithoutNewRows()
    {
        await using var database = await PersistenceTestDatabase.CreateAsync();

        await using var firstConnection = await database.ConnectionFactory.CreateOpenConnectionAsync();
        var firstCount = await ScalarAsync(firstConnection, "SELECT COUNT(*) FROM schema_migrations;");
        var firstAppliedAt = await ScalarAsync(
            firstConnection,
            "SELECT applied_at_utc FROM schema_migrations WHERE version = 1;");
        var migrationName = await ScalarAsync(
            firstConnection,
            "SELECT name FROM schema_migrations WHERE version = 1;");
        var tables = await ScalarAsync(
            firstConnection,
            "SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name IN (" +
            "'schema_migrations', 'categories', 'recurrence_series', 'tasks', 'reminders', " +
            "'attachments', 'recurrence_exclusions', 'settings', 'cleanup_queue');");
        var foreignKeys = await ScalarAsync(firstConnection, "PRAGMA foreign_keys;");
        var journalMode = await ScalarAsync(firstConnection, "PRAGMA journal_mode;");
        var checksum = await ScalarAsync(
            firstConnection,
            "SELECT checksum FROM schema_migrations WHERE version = 1;");

        await database.Initializer.InitializeAsync();

        await using var secondConnection = await database.ConnectionFactory.CreateOpenConnectionAsync();
        var secondCount = await ScalarAsync(secondConnection, "SELECT COUNT(*) FROM schema_migrations;");
        var secondAppliedAt = await ScalarAsync(
            secondConnection,
            "SELECT applied_at_utc FROM schema_migrations WHERE version = 1;");

        Assert.Equal(1L, Convert.ToInt64(firstCount, CultureInfo.InvariantCulture));
        Assert.Equal(firstCount, secondCount);
        Assert.Equal(firstAppliedAt, secondAppliedAt);
        Assert.Equal("001_initial_schema.sql", Convert.ToString(migrationName, CultureInfo.InvariantCulture));
        Assert.Equal(9L, Convert.ToInt64(tables, CultureInfo.InvariantCulture));
        Assert.Equal(1L, Convert.ToInt64(foreignKeys, CultureInfo.InvariantCulture));
        Assert.Equal("wal", Convert.ToString(journalMode, CultureInfo.InvariantCulture));
        Assert.NotNull(checksum);
        Assert.NotEqual(string.Empty, checksum);
    }

    [Fact]
    public async Task InitializeAsync_WhenMigrationFails_ShouldRollbackBootstrapAndMigrationChanges()
    {
        var root = Path.Combine(Path.GetTempPath(), "ScheduleAssistant-DEV020-" + Guid.NewGuid().ToString("N"));
        await using var database = PersistenceTestDatabase.OpenExisting(root);

        await using (var setupConnection = await database.ConnectionFactory.CreateOpenConnectionAsync())
        {
            await using var setupCommand = setupConnection.CreateCommand();
            setupCommand.CommandText = "CREATE TABLE tasks (id TEXT PRIMARY KEY);";
            await setupCommand.ExecuteNonQueryAsync();
        }

        await Assert.ThrowsAsync<SqliteException>(() => database.Initializer.InitializeAsync());

        await using var verificationConnection = await database.ConnectionFactory.CreateOpenConnectionAsync();
        var tasksTableCount = await ScalarAsync(
            verificationConnection,
            "SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name = 'tasks';");
        var migrationsTableCount = await ScalarAsync(
            verificationConnection,
            "SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name = 'schema_migrations';");
        var remindersTableCount = await ScalarAsync(
            verificationConnection,
            "SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name = 'reminders';");

        Assert.Equal(1L, Convert.ToInt64(tasksTableCount, CultureInfo.InvariantCulture));
        Assert.Equal(0L, Convert.ToInt64(migrationsTableCount, CultureInfo.InvariantCulture));
        Assert.Equal(0L, Convert.ToInt64(remindersTableCount, CultureInfo.InvariantCulture));
    }

    private static async Task<object?> ScalarAsync(SqliteConnection connection, string sql)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        return await command.ExecuteScalarAsync();
    }
}
