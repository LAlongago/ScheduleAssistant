using System.Reflection;
using System.Security.Cryptography;
using System.Text;

namespace ScheduleAssistant.Infrastructure.Persistence.Migrations;

internal static class MigrationCatalog
{
    private const string ResourceMarker = ".Migrations.";

    public static IReadOnlyList<EmbeddedSqlMigration> Load(Assembly assembly)
    {
        var migrations = assembly
            .GetManifestResourceNames()
            .Where(name => name.Contains(ResourceMarker, StringComparison.Ordinal)
                && name.EndsWith(".sql", StringComparison.OrdinalIgnoreCase))
            .Select(name => ReadMigration(assembly, name))
            .OrderBy(migration => migration.Version)
            .ToArray();

        if (migrations.Length == 0)
        {
            throw new InvalidOperationException("No embedded SQLite migrations were found.");
        }

        if (migrations.Select(migration => migration.Version).Distinct().Count() != migrations.Length)
        {
            throw new InvalidOperationException("SQLite migration versions must be unique.");
        }

        for (var index = 0; index < migrations.Length; index++)
        {
            if (migrations[index].Version != index + 1)
            {
                throw new InvalidOperationException("SQLite migration versions must be sequential starting at 001.");
            }
        }

        return migrations;
    }

    private static EmbeddedSqlMigration ReadMigration(Assembly assembly, string resourceName)
    {
        var markerIndex = resourceName.LastIndexOf(ResourceMarker, StringComparison.Ordinal);
        var name = resourceName[(markerIndex + ResourceMarker.Length)..];
        if (name.Length < 5 || name[3] != '_'
            || !int.TryParse(name[..3], out var version)
            || version < 1)
        {
            throw new InvalidOperationException($"Migration resource '{resourceName}' has an invalid name.");
        }

        using var stream = assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException($"Migration resource '{resourceName}' could not be opened.");
        using var buffer = new MemoryStream();
        stream.CopyTo(buffer);
        var bytes = buffer.ToArray();
        var sql = Encoding.UTF8.GetString(bytes);
        var checksum = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
        return new EmbeddedSqlMigration(version, name, sql, checksum);
    }
}
