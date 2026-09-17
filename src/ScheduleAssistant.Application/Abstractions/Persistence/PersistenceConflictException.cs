using System.Globalization;

namespace ScheduleAssistant.Application.Abstractions.Persistence;

/// <summary>Indicates that an optimistic-concurrency write did not match the stored version.</summary>
public sealed class PersistenceConflictException : InvalidOperationException
{
    /// <summary>Initializes a persistence conflict.</summary>
    public PersistenceConflictException(string entityName, Guid entityId, long expectedVersion, long? actualVersion)
        : base(CreateMessage(entityName, entityId, expectedVersion, actualVersion))
    {
        EntityName = entityName;
        EntityId = entityId;
        ExpectedVersion = expectedVersion;
        ActualVersion = actualVersion;
    }

    /// <summary>Gets the logical entity name.</summary>
    public string EntityName { get; }

    /// <summary>Gets the conflicting entity identity.</summary>
    public Guid EntityId { get; }

    /// <summary>Gets the version supplied by the caller.</summary>
    public long ExpectedVersion { get; }

    /// <summary>Gets the stored version, or <see langword="null"/> when absent.</summary>
    public long? ActualVersion { get; }

    private static string CreateMessage(string entityName, Guid entityId, long expectedVersion, long? actualVersion)
    {
        var actual = actualVersion.HasValue
            ? actualVersion.Value.ToString(CultureInfo.InvariantCulture)
            : "missing";
        return $"The {entityName} '{entityId}' changed after version {expectedVersion}; stored version is {actual}.";
    }
}
