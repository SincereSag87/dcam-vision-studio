using System.Windows;
using DcamVision.App.ViewModels;
using DcamVision.Core;
using DcamVision.App.Services;
using DcamVision.Imaging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace DcamVision.App;

public partial class App : Application
{
    private ServiceProvider? _serviceProvider;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var services = new ServiceCollection();
        services.AddLogging(builder => builder.SetMinimumLevel(LogLevel.Information));
        services.AddSingleton<ICameraService, SimulatedCameraService>();
        services.AddSingleton<ImagePreviewService>();
        services.AddSingleton<ExposureSweepRunner>();
        services.AddSingleton<MainViewModel>();
        services.AddSingleton<MainWindow>();

        _serviceProvider = services.BuildServiceProvider();
        _serviceProvider.GetRequiredService<MainWindow>().Show();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _serviceProvider?.Dispose();
        base.OnExit(e);
    }
}
