namespace CADWorkAssistant.Core.Models;

/// <summary>Milestone 13 §34 - ExportRecord.ExportType의 내부 문자열을 사용자에게 그대로
/// 노출하지 않는다. Output History 화면 하나만 이 값을 쓰지만, QuantityReviewStatusDisplay와
/// 같은 이유로 별도 클래스에 둔다 - 표시 문구가 늘어나도 호출부를 건드리지 않는다.</summary>
public static class ExportTypeDisplay
{
    public static string Label(string exportType) => exportType switch
    {
        ExportTypes.DwgSelection => "DWG",
        ExportTypes.ExcelQuantity => "Excel",
        ExportTypes.PdfQuantityReport => "PDF 보고서",
        ExportTypes.DrawingPdf => "도면 PDF",
        _ => exportType
    };
}
