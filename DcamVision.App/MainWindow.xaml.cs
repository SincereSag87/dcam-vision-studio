using System.Windows;
using DcamVision.App.ViewModels;
using DcamVision.Imaging;

namespace DcamVision.App;

public partial class MainWindow : Window
{
    private readonly LiveAcquisitionService _liveAcquisitionService;
    private bool _isClosingAfterPipelineStop;

    public MainWindow(MainViewModel viewModel, LiveAcquisitionService liveAcquisitionService)
    {
        _liveAcquisitionService = liveAcquisitionService;
        InitializeComponent();
        DataContext = viewModel;
    }

    protected override async void OnClosing(System.ComponentModel.CancelEventArgs e)
    {
        if (!_isClosingAfterPipelineStop && _liveAcquisitionService.State is not LiveAcquisitionState.Stopped)
        {
            e.Cancel = true;
            await _liveAcquisitionService.StopAsync();
            _isClosingAfterPipelineStop = true;
            Close();
            return;
        }

        base.OnClosing(e);
    }
}
