using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;
using CADWorkAssistant.Desktop.Services;
using CADWorkAssistant.Desktop.ViewModels;

namespace CADWorkAssistant.Desktop;

public partial class MainWindow : Window
{
    private readonly IProjectContextService _projectContext;

    public MainWindow(MainWindowViewModel viewModel, IProjectContextService projectContext)
    {
        InitializeComponent();
        _projectContext = projectContext;
        viewModel.PropertyChanged += OnViewModelPropertyChanged;
        viewModel.RequestOpenProjectDialog += (_, _) => OpenProjectDialog();
        DataContext = viewModel;
        Loaded += (_, _) =>
        {
            if (SystemParameters.ClientAreaAnimation)
            {
                AnimateElement(InspectorPanel, 0, 1, 8, 0);
            }
        };
    }

    private void OpenProjectDialog()
    {
        var dialog = new ProjectDialog(new ProjectDialogViewModel(_projectContext)) { Owner = this };
        dialog.ShowDialog();
    }

    private void OnViewModelPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (!SystemParameters.ClientAreaAnimation)
        {
            return;
        }

        if (e.PropertyName == nameof(MainWindowViewModel.IsCommandPaletteOpen)
            && sender is MainWindowViewModel { IsCommandPaletteOpen: true })
        {
            AnimateElement(CommandPaletteDialog, 0, 1, -12, 0);
        }

        if (e.PropertyName == nameof(MainWindowViewModel.IsInspectorOpen)
            && sender is MainWindowViewModel { IsInspectorOpen: true })
        {
            AnimateElement(InspectorPanel, 0, 1, 16, 0);
        }
    }

    private static void AnimateElement(UIElement element, double fromOpacity, double toOpacity, double fromX, double toX)
    {
        element.Opacity = fromOpacity;
        element.RenderTransform = new TranslateTransform(fromX, 0);

        var duration = TimeSpan.FromMilliseconds(160);
        element.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(fromOpacity, toOpacity, duration)
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        });

        ((TranslateTransform)element.RenderTransform).BeginAnimation(TranslateTransform.XProperty, new DoubleAnimation(fromX, toX, duration)
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        });
    }
}
