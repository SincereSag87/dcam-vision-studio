using System.Windows;
using DcamVision.App.ViewModels;
using DcamVision.Core;
using DcamVision.Dcam;
using DcamVision.Dcam.Interop;
using DcamVision.Dcam.Runtime;
using DcamVision.App.Services;
using DcamVision.Imaging;
using DcamVision.Imaging.Diagnostics;
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
        var loggingOptions = new DiagnosticsLoggingOptions();
        var logStore = new InMemoryDiagnosticLogStore(loggingOptions.InMemoryCapacity, loggingOptions.RecentErrorCapacity);
        var diagnosticsLoggerProvider = new DiagnosticsLoggerProvider(logStore, loggingOptions);
        services.AddSingleton(loggingOptions);
        services.AddSingleton(logStore);
        services.AddSingleton(diagnosticsLoggerProvider);
        services.AddLogging(builder =>
        {
            builder.SetMinimumLevel(loggingOptions.MinimumLevel);
            builder.AddProvider(diagnosticsLoggerProvider);
        });
        services.AddSingleton<IApplicationDiagnosticsService, ApplicationDiagnosticsService>();
        services.AddSingleton(provider => new SupportBundleService(
            provider.GetRequiredService<IApplicationDiagnosticsService>(),
            provider.GetRequiredService<InMemoryDiagnosticLogStore>(),
            provider.GetRequiredService<DiagnosticsLoggerProvider>().LogDirectory));
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
        var logger = _serviceProvider.GetRequiredService<ILogger<App>>();
        logger.LogInformation(
            "Application startup. Version={Version} Runtime={Runtime} OS={OS} Architecture={Architecture}",
            typeof(App).Assembly.GetName().Version?.ToString() ?? "unknown",
            System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription,
            System.Runtime.InteropServices.RuntimeInformation.OSDescription,
            System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture);
        DispatcherUnhandledException += (_, args) =>
        {
            logger.LogError(args.Exception, "Unhandled dispatcher exception.");
        };
        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
        {
            if (args.ExceptionObject is Exception exception)
            {
                logger.LogCritical(exception, "Unhandled application domain exception.");
            }
        };
        TaskScheduler.UnobservedTaskException += (_, args) =>
        {
            logger.LogError(args.Exception, "Unobserved task exception.");
            args.SetObserved();
        };
        _serviceProvider.GetRequiredService<MainWindow>().Show();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _serviceProvider?.GetService<ILogger<App>>()?.LogInformation("Application shutdown requested.");
        _serviceProvider?.GetService<DiagnosticsLoggerProvider>()?.Dispose();
        _serviceProvider?.Dispose();
        base.OnExit(e);
    }
}
