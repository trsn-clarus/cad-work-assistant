namespace CADWorkAssistant.Core.Plot;

/// <summary>Milestone 11 §44-45.</summary>
public sealed class CadPlotLayoutDto
{
    public CadPlotLayoutDto(string name, bool isModel, bool isCurrent)
    {
        Name = name;
        IsModel = isModel;
        IsCurrent = isCurrent;
    }

    public string Name { get; }

    public bool IsModel { get; }

    public bool IsCurrent { get; }
}
