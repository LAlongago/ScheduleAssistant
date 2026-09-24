using Microsoft.Extensions.Hosting;
using ScheduleAssistant.Application.Reminders;

namespace ScheduleAssistant.Presentation.Composition;

/// <summary>Connects the platform-neutral activation source to the WPF routing boundary.</summary>
public sealed class NotificationActivationHostedService : IHostedService
{
    private readonly INotificationActivationSource _activationSource;
    private readonly NotificationActivationRouter _router;
    private bool _subscribed;

    /// <summary>Initializes activation routing before the notification provider registers.</summary>
    public NotificationActivationHostedService(
        INotificationActivationSource activationSource,
        NotificationActivationRouter router)
    {
        _activationSource = activationSource ?? throw new ArgumentNullException(nameof(activationSource));
        _router = router ?? throw new ArgumentNullException(nameof(router));
    }

    /// <inheritdoc />
    public Task StartAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!_subscribed)
        {
            _activationSource.NotificationActivated += OnNotificationActivated;
            _subscribed = true;
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task StopAsync(CancellationToken cancellationToken)
    {
        _ = cancellationToken;
        if (_subscribed)
        {
            _activationSource.NotificationActivated -= OnNotificationActivated;
            _subscribed = false;
        }

        return Task.CompletedTask;
    }

    private void OnNotificationActivated(object? sender, NotificationActivationEventArgs eventArgs)
    {
        _ = sender;
        _ = _router.HandleActivationAsync(eventArgs);
    }
}
