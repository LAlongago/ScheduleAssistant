using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ScheduleAssistant.Application;
using ScheduleAssistant.Infrastructure.Composition;
using ScheduleAssistant.Presentation.Composition;

namespace ScheduleAssistant.Presentation;

/// <summary>
/// WPF composition root for the ScheduleAssistant desktop process.
/// </summary>
public partial class App : System.Windows.Application
{
    private IHost? _host;

    /// <inheritdoc />
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        try
        {
            _host = BuildHost(e.Args);
            _host.Start();

            var mainWindow = _host.Services.GetRequiredService<MainWindow>();
            MainWindow = mainWindow;
            _host.Services.GetRequiredService<IWindowService>().ShowMainWindow(mainWindow);
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
            base.OnExit(e);
        }
    }

    private static IHost BuildHost(string[] args)
    {
        var builder = Host.CreateApplicationBuilder(args);
        if (ApplicationAssemblyMarker.DomainAssembly is null)
        {
            throw new InvalidOperationException("The Application assembly boundary is unavailable.");
        }

        builder.Services.AddInfrastructure();
        builder.Services.AddPresentation();
        return builder.Build();
    }
}
