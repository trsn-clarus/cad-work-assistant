namespace CADWorkAssistant.Core.Plot;

/// <summary>Milestone 11 §34-40 - PlotStyleResolver의 결과. StyleSheetName이 null이면서
/// IsAvailable이 true인 경우는 "기존 설정을 그대로 유지"(§35, override하지 않음)를 뜻한다 -
/// Unavailable과 혼동하지 않도록 별도 상태로 구분한다.</summary>
public sealed class PlotStyleResolution
{
    private PlotStyleResolution(bool isAvailable, string? styleSheetName, string? unavailableReason)
    {
        IsAvailable = isAvailable;
        StyleSheetName = styleSheetName;
        UnavailableReason = unavailableReason;
    }

    public bool IsAvailable { get; }

    /// <summary>null이면 override하지 않고 기존 Layout 설정을 그대로 쓴다는 뜻이다.</summary>
    public string? StyleSheetName { get; }

    public string? UnavailableReason { get; }

    public static PlotStyleResolution KeepExisting() => new(true, null, null);

    public static PlotStyleResolution Resolved(string styleSheetName) => new(true, styleSheetName, null);

    public static PlotStyleResolution Unavailable(string reason) => new(false, null, reason);
}
