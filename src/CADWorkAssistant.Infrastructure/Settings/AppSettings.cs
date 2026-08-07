namespace CADWorkAssistant.Infrastructure.Settings;

/// <summary>
/// 사용자 환경설정. 프로젝트/산출내역 데이터는 여기 포함하지 않는다 (docs/ARCHITECTURE.md §7).
/// </summary>
public sealed class AppSettings
{
    public int LengthDecimalPlaces { get; set; } = 3;

    public int AreaDecimalPlaces { get; set; } = 2;
}
