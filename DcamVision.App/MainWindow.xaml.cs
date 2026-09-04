using System.Windows;
using DcamVision.App.ViewModels;

namespace DcamVision.App;

public partial class MainWindow : Window
{
    public MainWindow(MainViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }
}
