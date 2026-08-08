namespace CADWorkAssistant.Core.Parapet;

/// <summary>
/// 파라펫 측면을 한 면만 계산할지 양면(내/외측 근사)으로 계산할지. 실제 내측/외측 둘레는 벽 두께
/// 때문에 다를 수 있지만, 이번 Milestone은 "같은 기준 길이를 양쪽에 적용하는 간편 산식"만
/// 지원한다 - 독립된 내/외측 길이 입력은 향후 과제로 남긴다 (Milestone 4 §28, §30, §85).
/// </summary>
public enum ParapetFaceMode
{
    Single,
    Both
}
