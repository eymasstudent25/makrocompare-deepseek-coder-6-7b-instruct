using Avalonia.Controls;
using MakroCompare1408.ViewModels;

namespace MakroCompare1408.Views;

public partial class MainWindow : Window
{
	public MainWindow()
	{
		InitializeComponent();
	}

	public MainWindow(MainWindowViewModel viewModel)
	{
		InitializeComponent();
		DataContext = viewModel;
	}
}