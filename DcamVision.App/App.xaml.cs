using System.Windows;
using DcamVision.App.ViewModels;
using DcamVision.Core;
using DcamVision.Dcam;
using DcamVision.Dcam.Interop;
using DcamVision.Dcam.Runtime;
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
        services.AddSingleton<SimulatedCameraService>();
        services.AddSingleton<IDcamNativeApi, DcamNativeApi>();
        services.AddSingleton<IDcamRuntime, DcamRuntime>();
        services.AddSingleton<DcamAcquisitionOptions>();
        services.AddSingleton<DcamCameraService>();
        services.AddSingleton<ICameraService>(provider => new CompositeCameraService(
            provider.GetRequiredService<SimulatedCameraService>(),
            provider.GetRequiredService<DcamCameraService>()));
        services.AddSingleton<ImageDisplayProcessor>();
        services.AddSingleton<ImagePreviewService>();
        services.AddSingleton<CaptureHistoryStore>();
        services.AddSingleton<CaptureRecordFactory>();
        services.AddSingleton<ICaptureExportService, CaptureExportService>();
        services.AddSingleton(new LiveAcquisitionOptions
        {
            BufferCapacity = 4,
            OverflowStrategy = LiveBufferOverflowStrategy.DropOldest,
            MaximumPreviewFps = 30
        });
        services.AddSingleton<LiveAcquisitionService>();
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
