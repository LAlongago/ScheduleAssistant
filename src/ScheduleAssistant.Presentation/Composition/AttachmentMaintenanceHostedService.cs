using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ScheduleAssistant.Application.Abstractions.Events;
using ScheduleAssistant.Application.Attachments;

namespace ScheduleAssistant.Presentation.Composition;

/// <summary>
/// Runs one bounded attachment cleanup/orphan diagnosis pass at startup and subscribes to
/// post-commit TaskDeleted events for the lifetime of the host.
/// </summary>
[SuppressMessage(
    "Performance",
    "CA1848",
    Justification = "The two startup messages contain only fixed operation/status fields and remain readable at this boundary.")]
[SuppressMessage(
    "Performance",
    "CA1873",
    Justification = "The startup report contains only bounded numeric counters; keep the diagnostic message provider-neutral.")]
public sealed class AttachmentMaintenanceHostedService : IHostedService
{
    private static readonly EventId MaintenanceCompletedEvent = new(7001, "AttachmentMaintenanceCompleted");
    private static readonly EventId MaintenanceFailedEvent = new(7002, "AttachmentMaintenanceFailed");

    private readonly IDatabaseInitialization _databaseInitialization;
    private readonly InProcessEventBus _eventBus;
    private readonly AttachmentMaintenanceService _maintenanceService;
    private readonly ILogger<AttachmentMaintenanceHostedService> _logger;
    private IDisposable? _eventSubscription;

    /// <summary>Initializes the one-shot startup maintenance host service.</summary>
    public AttachmentMaintenanceHostedService(
        IDatabaseInitialization databaseInitialization,
        InProcessEventBus eventBus,
        AttachmentMaintenanceService maintenanceService,
        ILogger<AttachmentMaintenanceHostedService> logger)
    {
        _databaseInitialization = databaseInitialization ?? throw new ArgumentNullException(nameof(databaseInitialization));
        _eventBus = eventBus ?? throw new ArgumentNullException(nameof(eventBus));
        _maintenanceService = maintenanceService ?? throw new ArgumentNullException(nameof(maintenanceService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        _eventSubscription = _eventBus.Subscribe<TaskDeleted>(
            _maintenanceService.HandleTaskDeletedAsync);
        try
        {
            await _databaseInitialization
                .EnsureInitializedAsync(cancellationToken)
                .ConfigureAwait(false);
            var report = await _maintenanceService
                .RunStartupAsync(cancellationToken)
                .ConfigureAwait(false);
            _logger.LogInformation(
                MaintenanceCompletedEvent,
                "{Operation} {Status} {Count} {RetryCount}",
                "AttachmentMaintenance",
                "Completed",
                report.OrphanFileCount,
                report.QueueItemsInspected);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            // Maintenance is diagnostic/recovery work and must not make an otherwise usable app fail.
            _logger.LogError(
                MaintenanceFailedEvent,
                exception,
                "{Operation} {Status}",
                "AttachmentMaintenance",
                "Failed");
        }
    }

    /// <inheritdoc />
    public Task StopAsync(CancellationToken cancellationToken)
    {
        _eventSubscription?.Dispose();
        _eventSubscription = null;
        return Task.CompletedTask;
    }
}
