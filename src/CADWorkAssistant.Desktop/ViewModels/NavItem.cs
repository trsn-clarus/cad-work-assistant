using CADWorkAssistant.Desktop.Common;

namespace CADWorkAssistant.Desktop.ViewModels;

public sealed class NavItem : ObservableObject
{
    private bool _isSelected;

    public NavItem(string group, string label, string shortcut, bool showGroupHeader = false, bool isImplemented = true)
    {
        Group = group;
        Label = label;
        Shortcut = shortcut;
        ShowGroupHeader = showGroupHeader;
        IsImplemented = isImplemented;
    }

    public string Group { get; }
    public string Label { get; }
    public string Shortcut { get; }
    public bool ShowGroupHeader { get; }

    /// <summary>실제로 동작하는 화면인지 - 아직 없는 기능을 클릭 가능한 것처럼 보여주지 않는다 (Milestone 4.5 §23).</summary>
    public bool IsImplemented { get; }

    public string TooltipText => IsImplemented ? Label : $"{Label} (곧 제공됩니다)";

    public bool IsSelected
    {
        get => _isSelected;
        set => SetProperty(ref _isSelected, value);
    }
}
