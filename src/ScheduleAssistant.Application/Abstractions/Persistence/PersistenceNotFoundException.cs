namespace ScheduleAssistant.Application.Abstractions.Persistence;

/// <summary>Indicates that a conditional persistence operation found no entity.</summary>
public sealed class PersistenceNotFoundException : KeyNotFoundException
{
    /// <summary>Initializes a provider-neutral not-found failure.</summary>
    public PersistenceNotFoundException(string entityName, Guid entityId)
        : base("The requested persistence entity was not found.")
    {
        EntityName = string.IsNullOrWhiteSpace(entityName) ? "Entity" : entityName;
        EntityId = entityId;
    }

    /// <summary>Gets the logical entity name.</summary>
    public string EntityName { get; }

    /// <summary>Gets the entity identity.</summary>
    public Guid EntityId { get; }
}
