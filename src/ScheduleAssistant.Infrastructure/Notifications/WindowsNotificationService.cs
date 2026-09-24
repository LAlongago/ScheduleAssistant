using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Windows.AppNotifications;
using ScheduleAssistant.Application.Reminders;

namespace ScheduleAssistant.Infrastructure.Notifications;

/// <summary>
/// Owns the Windows App SDK notification registration and maps provider-neutral reminders to app notifications.
/// </summary>
[SuppressMessage(
    "Performance",
    "CA1848",
    Justification = "These structured diagnostics run only on registration, delivery failure, capability failure, or user activation paths.")]
[SuppressMessage(
    "Performance",
    "CA1873",
    Justification = "Log values are stable IDs, enum states, exception types, or HRESULTs on low-frequency lifecycle paths.")]
[SuppressMessage(
    "Design",
    "CA1031",
    Justification = "The adapter is an application boundary: Windows API exceptions must become safe capability or delivery results without terminating WPF.")]
public sealed class WindowsNotificationService : INotificationService, INotificationActivationSource, IHostedService
{
    private const string RegistrationFailureCode = "notification.registration-failed";
    private const string RuntimeUnavailableCode = "notification.runtime-unavailable";
    private const string DeliveryFailureCode = "notification.delivery-failed";

    private readonly IWindowsAppNotificationRuntime _runtime;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<WindowsNotificationService> _logger;
    private bool _startAttempted;
    private bool _registered;
    private bool _runtimeEventSubscribed;
    private bool _runtimeInitialized;
    private string? _registrationErrorCode;
    private bool _stopped;

    /// <summary>Creates the production Windows App SDK notification adapter.</summary>
    public WindowsNotificationService(
        TimeProvider timeProvider,
        ILogger<WindowsNotificationService> logger)
        : this(new WindowsAppNotificationRuntime(), timeProvider, logger)
    {
    }

    internal WindowsNotificationService(
        IWindowsAppNotificationRuntime runtime,
        TimeProvider timeProvider,
        ILogger<WindowsNotificationService> logger)
    {
        _runtime = runtime ?? throw new ArgumentNullException(nameof(runtime));
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public event EventHandler<NotificationActivationEventArgs>? NotificationActivated;

    /// <inheritdoc />
    public Task StartAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (_startAttempted || _stopped)
        {
            return Task.CompletedTask;
        }

        _startAttempted = true;
        try
        {
            if (!_runtime.TryInitialize(out var hresult))
            {
                _registrationErrorCode = RuntimeUnavailableCode;
                _logger.LogWarning(
                    "Windows App SDK runtime is unavailable; error code {ErrorCode}, HRESULT {HResult}.",
                    _registrationErrorCode,
                    hresult);
                return Task.CompletedTask;
            }

            _runtimeInitialized = true;
        }
        catch (Exception exception)
        {
            _registrationErrorCode = RuntimeUnavailableCode;
            _logger.LogWarning(
                "Windows App SDK runtime initialization failed; error code {ErrorCode}, exception type {ExceptionType}, HRESULT {HResult}.",
                _registrationErrorCode,
                exception.GetType().Name,
                exception.HResult);
            return Task.CompletedTask;
        }

        bool isSupported;
        try
        {
            isSupported = _runtime.IsSupported();
        }
        catch (Exception exception)
        {
            _registrationErrorCode = RuntimeUnavailableCode;
            _logger.LogError(
                "Windows notification API availability check failed; error code {ErrorCode}, exception type {ExceptionType}, HRESULT {HResult}.",
                _registrationErrorCode,
                exception.GetType().Name,
                exception.HResult);
            return Task.CompletedTask;
        }

        if (!isSupported)
        {
            _registrationErrorCode = "notification.api-unsupported";
            _logger.LogWarning("Windows notification registration is unavailable; error code {ErrorCode}.", _registrationErrorCode);
            return Task.CompletedTask;
        }

        _runtime.NotificationInvoked += OnRuntimeNotificationInvoked;
        _runtimeEventSubscribed = true;
        try
        {
            _runtime.Register();
            _registered = true;
        }
        catch (Exception exception)
        {
            UnsubscribeRuntimeEvent();
            _registrationErrorCode = RegistrationFailureCode;
            _logger.LogError(
                "Windows notification registration failed; error code {ErrorCode}, exception type {ExceptionType}, HRESULT {HResult}.",
                _registrationErrorCode,
                exception.GetType().Name,
                exception.HResult);
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task StopAsync(CancellationToken cancellationToken)
    {
        _ = cancellationToken;
        if (_stopped)
        {
            return Task.CompletedTask;
        }

        _stopped = true;
        UnsubscribeRuntimeEvent();
        if (_registered)
        {
            try
            {
                _runtime.Unregister();
            }
            catch (Exception exception)
            {
                _logger.LogWarning(
                    "Windows notification unregistration failed; exception type {ExceptionType}, HRESULT {HResult}.",
                    exception.GetType().Name,
                    exception.HResult);
            }
            finally
            {
                _registered = false;
            }
        }

        if (_runtimeInitialized)
        {
            try
            {
                _runtime.Shutdown();
            }
            catch (Exception exception)
            {
                _logger.LogWarning(
                    "Windows App SDK runtime shutdown failed; exception type {ExceptionType}, HRESULT {HResult}.",
                    exception.GetType().Name,
                    exception.HResult);
            }
            finally
            {
                _runtimeInitialized = false;
            }
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task<NotificationProviderCapability> GetCapabilityAsync(
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!_registered)
        {
            return Task.FromResult(
                NotificationProviderCapability.Unavailable(
                    _registrationErrorCode ?? "notification.not-registered"));
        }

        try
        {
            var isSupported = _runtime.IsSupported();
            if (!isSupported)
            {
                return Task.FromResult(WindowsNotificationCapabilityMapper.Map(
                    isSupported: false,
                    isRegistered: _registered,
                    setting: AppNotificationSetting.Unsupported));
            }

            return Task.FromResult(WindowsNotificationCapabilityMapper.Map(
                isSupported: true,
                isRegistered: _registered,
                setting: _runtime.Setting));
        }
        catch (Exception exception)
        {
            _logger.LogWarning(
                "Windows notification capability query failed; error code {ErrorCode}, exception type {ExceptionType}, HRESULT {HResult}.",
                RuntimeUnavailableCode,
                exception.GetType().Name,
                exception.HResult);
            return Task.FromResult(NotificationProviderCapability.Unavailable(RuntimeUnavailableCode));
        }
    }

    /// <inheritdoc />
    public async Task<NotificationDeliveryResult> ShowAsync(
        ReminderNotification notification,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(notification);
        cancellationToken.ThrowIfCancellationRequested();

        var capability = await GetCapabilityAsync(cancellationToken).ConfigureAwait(false);
        if (!capability.IsAvailable)
        {
            return NotificationDeliveryResult.Failure(capability.ErrorCode ?? "notification.provider-unavailable");
        }

        try
        {
            _runtime.Show(WindowsNotificationPayloadBuilder.Build(notification, _timeProvider));
            return NotificationDeliveryResult.Success();
        }
        catch (Exception exception)
        {
            _logger.LogError(
                "Windows notification delivery failed for task {TaskId}; error code {ErrorCode}, exception type {ExceptionType}, HRESULT {HResult}.",
                notification.TaskId,
                DeliveryFailureCode,
                exception.GetType().Name,
                exception.HResult);
            return NotificationDeliveryResult.Failure(DeliveryFailureCode);
        }
    }

    private void OnRuntimeNotificationInvoked(string? argument)
    {
        var activation = NotificationActivationParser.Parse(argument);
        if (!activation.TaskId.HasValue)
        {
            _logger.LogInformation(
                "Windows notification activation contained invalid or unsupported arguments; error code {ErrorCode}.",
                "notification.activation-invalid");
        }

        var handlers = NotificationActivated;
        if (handlers is null)
        {
            return;
        }

        foreach (EventHandler<NotificationActivationEventArgs> handler in handlers.GetInvocationList())
        {
            try
            {
                handler(this, activation);
            }
            catch (Exception exception)
            {
                _logger.LogWarning(
                    "Notification activation subscriber failed; exception type {ExceptionType}, HRESULT {HResult}.",
                    exception.GetType().Name,
                    exception.HResult);
            }
        }
    }

    private void UnsubscribeRuntimeEvent()
    {
        if (!_runtimeEventSubscribed)
        {
            return;
        }

        _runtime.NotificationInvoked -= OnRuntimeNotificationInvoked;
        _runtimeEventSubscribed = false;
    }
}
