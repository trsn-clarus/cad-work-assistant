namespace CADWorkAssistant.Documents.Reports;

/// <summary>Milestone 9 §31-33 - 첫 버전은 두 범위만 지원한다. "선택 항목만"/"현재 필터만"은
/// 의도적으로 범위 밖이다(§143-144) - 나중에 실무 요구가 확인되면 추가한다. Milestone 10 §68에서
/// Excel 전용 이름(ExcelExportScope)을 벗고 Excel/PDF가 공유하는 renderer-neutral 타입으로
/// 일반화했다 - 두 포맷의 "전체/검토 완료만" 의미가 완전히 같기 때문이다.</summary>
public enum QuantityExportScope
{
    All,
    VerifiedOnly
}
