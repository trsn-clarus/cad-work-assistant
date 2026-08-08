using CADWorkAssistant.Desktop.Common;

namespace CADWorkAssistant.Desktop.ViewModels;

public sealed class NavItem : ObservableObject
{
    private bool _isSelected;

    public NavItem(string group, string label, string shortcut, bool showGroupHeader = false)
    {
        Group = group;
        Label = label;
        Shortcut = shortcut;
        ShowGroupHeader = showGroupHeader;
    }

    public string Group { get; }
    public string Label { get; }
    public string Shortcut { get; }
    public bool ShowGroupHeader { get; }

    public bool IsSelected
    {
        get => _isSelected;
        set => SetProperty(ref _isSelected, value);
    }
}
