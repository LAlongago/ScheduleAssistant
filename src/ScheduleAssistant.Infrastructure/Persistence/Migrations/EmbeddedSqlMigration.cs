namespace ScheduleAssistant.Infrastructure.Persistence.Migrations;

internal sealed record EmbeddedSqlMigration(int Version, string Name, string Sql, string Checksum);
