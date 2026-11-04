using System.Windows;
using TiemChe.Wpf.ViewModels;

namespace TiemChe.Wpf.Views;

public partial class MainWindow : Window
{
    public MainWindow(ShellViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }
}
