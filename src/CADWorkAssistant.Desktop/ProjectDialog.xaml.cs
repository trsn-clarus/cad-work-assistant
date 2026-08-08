using System.Windows;
using CADWorkAssistant.Desktop.ViewModels;

namespace CADWorkAssistant.Desktop;

public partial class ProjectDialog : Window
{
    public ProjectDialog(ProjectDialogViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
        viewModel.RequestClose += (_, _) => Close();
    }
}
