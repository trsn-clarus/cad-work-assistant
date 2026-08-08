namespace CADWorkAssistant.Desktop.ViewModels;

/// <summary>Property Inspector에 표시하는 한 줄(라벨/값). 활성 도구에 따라 MainWindowViewModel이 다시 채운다.</summary>
public sealed record InspectorRow(string Label, string Value);
