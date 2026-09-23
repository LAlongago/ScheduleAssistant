using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ScheduleAssistant.Application.Reminders;
using ScheduleAssistant.Application;
using ScheduleAssistant.Infrastructure.Composition;
using ScheduleAssistant.Presentation.Composition;

namespace ScheduleAssistant.Presentation;

/// <summary>
/// WPF composition root for the ScheduleAssistant desktop process.
/// </summary>
public partial class App : System.Windows.Application, IDisposable
{
    private readonly object _activationGate = new();
    private IHost? _host;
    private SingleInstanceCoordinator? _singleInstance;
    private bool _mainWindowReady;
    private bool _secondaryLaunchPending;
    private bool _disposed;

    /// <inheritdoc />
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        try
        {
            _singleInstance = new SingleInstanceCoordinator();
            if (!_singleInstance.IsPrimary)
            {
                _singleInstance.SignalPrimary();
                Shutdown(0);
                return;
            }

            _host = BuildHost(e.Args);
            _singleInstance.StartListening(HandleSecondaryLaunch);
            _host.Start();

            var mainWindow = _host.Services.GetRequiredService<MainWindow>();
            MainWindow = mainWindow;
            _host.Services.GetRequiredService<IWindowService>().ShowMainWindow(mainWindow);

            var activationRouter = _host.Services.GetRequiredService<NotificationActivationRouter>();
            _ = activationRouter.AttachMainWindowAsync();
            bool secondaryLaunchPending;
            lock (_activationGate)
            {
                _mainWindowReady = true;
                secondaryLaunchPending = _secondaryLaunchPending;
                _secondaryLaunchPending = false;
            }

            if (secondaryLaunchPending)
            {
                _ = activationRouter.HandleActivationAsync(new NotificationActivationEventArgs(null));
            }
        }
        catch (Exception exception)
        {
            MessageBox.Show(
                $"ScheduleAssistant could not start ({exception.GetType().Name}).",
                "ScheduleAssistant",
                MessageBoxButton.OK,
                MessageBoxImage.Error);

            Shutdown(1);
        }
    }

    /// <inheritdoc />
    protected override void OnExit(ExitEventArgs e)
    {
        try
        {
            _host?.StopAsync().GetAwaiter().GetResult();
        }
        catch
        {
            // Shutdown must continue even if a future hosted service reports an error.
        }
        finally
        {
            _host?.Dispose();
            _host = null;
            Dispose();
            base.OnExit(e);
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _singleInstance?.Dispose();
        _singleInstance = null;
        GC.SuppressFinalize(this);
    }

    private void HandleSecondaryLaunch()
    {
        lock (_activationGate)
        {
            if (!_mainWindowReady)
            {
                _secondaryLaunchPending = true;
                return;
            }
        }

        var router = _host?.Services.GetService<NotificationActivationRouter>();
        if (router is not null)
        {
            _ = router.HandleActivationAsync(new NotificationActivationEventArgs(null));
        }
    }

    private static IHost BuildHost(string[] args)
    {
        var builder = Host.CreateApplicationBuilder(args);
        if (ApplicationAssemblyMarker.DomainAssembly is null)
        {
            throw new InvalidOperationException("The Application assembly boundary is unavailable.");
        }

        builder.Services.AddInfrastructure(builder.Configuration["ScheduleAssistant:DataRoot"]);
        builder.Services.AddPresentation();
        return builder.Build();
    }
}
