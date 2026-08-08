namespace CADWorkAssistant.Core.VerticalArea;

/// <summary>
/// 높이 입력값이 계산 가능한 상태인지. AutoCAD 도형에서 나오는 값(Length/Area)의 음수 검증과 달리
/// 이건 사용자가 방금 타이핑한 입력이라 "있을 수 없는 일"이 아니라 흔한 정상 경로다 - 그래서 예외가
/// 아니라 값으로 돌려준다 (Milestone 4 §13).
/// </summary>
public enum VerticalAreaValidation
{
    Valid,
    HeightNotPositive
}
