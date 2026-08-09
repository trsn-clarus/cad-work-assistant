namespace CADWorkAssistant.Documents.Excel;

/// <summary>Milestone 9 §31-33 - 첫 버전은 두 범위만 지원한다. "선택 항목만"/"현재 필터만"은
/// 의도적으로 범위 밖이다(§143-144) - 나중에 실무 요구가 확인되면 추가한다.</summary>
public enum ExcelExportScope
{
    All,
    VerifiedOnly
}
