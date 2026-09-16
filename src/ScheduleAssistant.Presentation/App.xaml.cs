using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ScheduleAssistant.Application;
using ScheduleAssistant.Infrastructure.Composition;

namespace ScheduleAssistant.Presentation;

/// <summary>
/// WPF composition root for the ScheduleAssistant desktop process.
/// </summary>
public partial class App : Application
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
            mainWindow.Show();
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
        _ = typeof(ApplicationAssemblyMarker);

        var builder = Host.CreateApplicationBuilder(args);
        builder.Services.AddInfrastructure();
        builder.Services.AddSingleton<MainWindow>();
        return builder.Build();
    }
}
