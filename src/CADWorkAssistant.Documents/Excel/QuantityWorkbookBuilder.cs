using System;
using System.Globalization;
using System.IO;
using CADWorkAssistant.Documents.Reports;
using ClosedXML.Excel;

namespace CADWorkAssistant.Documents.Excel;

/// <summary>
/// QuantityReportModel(Documents.Reports, Milestone 10에서 QuantityWorkbookModel을 renderer-neutral로
/// 일반화) -> 실제 .xlsx (Milestone 9). ClosedXML을 직접 다루는 유일한 클래스다 - Core/AutoCAD는 이
/// 타입을 참조하지 않는다(§6, §161-162). Microsoft Excel/Interop/COM을 쓰지 않는다(§4) - OpenXML
/// 파일을 직접 만들기 때문에 Excel 설치 여부와 무관하게 항상 성공한다.
/// </summary>
public sealed class QuantityWorkbookBuilder
{
    // Design System 팔레트(design-system/MASTER.md)를 그대로 쓰되 Excel 가독성을 우선한다(§47) -
    // gradient/네온/무지개 header 없이, 옅은 헤더 배경 + 얇은 border만 쓴다(§46, §59).
    private static readonly XLColor HeaderFill = XLColor.FromHtml("#EEF2F5");
    private static readonly XLColor BorderColor = XLColor.FromHtml("#CAD3DC");
    private static readonly XLColor TextColor = XLColor.FromHtml("#17212B");
    private static readonly XLColor MutedTextColor = XLColor.FromHtml("#526170");
    private static readonly XLColor AccentColor = XLColor.FromHtml("#1D6F8F");

    public ExcelExportResult BuildAndSave(QuantityReportModel model, ExcelExportOptions options, string targetPath)
    {
        using var workbook = new XLWorkbook();
        // §65: 실제 작성자(사람)를 알 수 없으니 지어내지 않는다 - Title/Subject/Company만 채운다.
        workbook.Properties.Title = $"{model.ProjectName} 수량산출서";
        workbook.Properties.Subject = "CAD Work Assistant 수량산출서";
        workbook.Properties.Company = "TRSN CLARUS";

        BuildQuantitySheet(workbook, model);
        if (options.IncludeCalculationBasis)
        {
            BuildCalculationBasisSheet(workbook, model);
        }

        if (options.IncludeVerificationDetail)
        {
            BuildVerificationSheet(workbook, model);
        }

        BuildProjectInfoSheet(workbook, model);

        SaveAtomically(workbook, targetPath);

        return new ExcelExportResult { FilePath = targetPath, RecordCount = model.TotalCount };
    }

    // ------------------------------------------------------------------
    // Sheet 1 - 수량산출서
    // ------------------------------------------------------------------
    private void BuildQuantitySheet(XLWorkbook workbook, QuantityReportModel model)
    {
        var ws = workbook.Worksheets.Add("수량산출서");
        var row = 1;

        ws.Cell(row, 1).Value = "수량산출서";
        ws.Cell(row, 1).Style.Font.Bold = true;
        ws.Cell(row, 1).Style.Font.FontSize = 16;
        ws.Cell(row, 1).Style.Font.FontColor = AccentColor;
        row += 2;

        row = WriteProjectHeaderBlock(ws, model, row);
        row++;

        ws.Cell(row, 1).Value = model.TotalCount == 0
            ? "내보낼 수량이 없습니다."
            : $"총 {model.TotalCount}건 · 검토 완료 {model.VerifiedCount} · 확인 필요 {model.NeedsReviewCount} · 검산 오류 {model.VerificationErrorCount}";
        ws.Cell(row, 1).Style.Font.FontColor = MutedTextColor;
        row += 2;

        var headerRow = row;
        string[] headers = { "No.", "구분", "내역", "산출식", "수량", "단위", "검산", "검토", "비고" };
        WriteHeaderRow(ws, headerRow, headers);
        row++;

        foreach (var line in model.Rows)
        {
            ws.Cell(row, 1).Value = line.Index;
            ws.Cell(row, 2).SetValue(line.TypeDisplayName);
            ws.Cell(row, 3).SetValue(line.Description);
            ws.Cell(row, 4).SetValue(line.CalculationExpression ?? "-");
            SetQuantityCell(ws.Cell(row, 5), line.Value, line.DecimalPlaces);
            ws.Cell(row, 6).SetValue(line.Unit);
            ws.Cell(row, 7).SetValue(VerificationCellText(line));
            ws.Cell(row, 8).SetValue(Core.Models.QuantityReviewStatusDisplay.Label(line.ReviewStatus));
            ws.Cell(row, 9).SetValue(line.IsVerificationStale ? "재검산 필요" : string.Empty);

            ws.Row(row).Style.Alignment.Vertical = XLAlignmentVerticalValues.Top;
            row++;
        }

        var lastDataRow = Math.Max(headerRow, row - 1);
        ApplyTableBorder(ws, headerRow, lastDataRow, headers.Length);
        SetColumnWidths(ws, new[] { 6d, 12d, 28d, 30d, 12d, 8d, 14d, 12d, 14d });
        ws.Column(3).Style.Alignment.WrapText = true;
        ws.Column(4).Style.Alignment.WrapText = true;

        ws.SheetView.FreezeRows(headerRow);
        if (model.Rows.Count > 0)
        {
            ws.Range(headerRow, 1, lastDataRow, headers.Length).SetAutoFilter();
        }

        ApplyPrintSettings(ws, headerRow, headers.Length);
    }

    // ------------------------------------------------------------------
    // Sheet 2 - 산출근거
    // ------------------------------------------------------------------
    private void BuildCalculationBasisSheet(XLWorkbook workbook, QuantityReportModel model)
    {
        var ws = workbook.Worksheets.Add("산출근거");
        var row = 1;
        ws.Cell(row, 1).Value = "산출근거";
        ws.Cell(row, 1).Style.Font.Bold = true;
        ws.Cell(row, 1).Style.Font.FontSize = 14;
        ws.Cell(row, 1).Style.Font.FontColor = AccentColor;
        row += 2;

        var headerRow = row;
        string[] headers =
        {
            "No.", "내역", "구분", "최종 수량", "원본값", "원본단위", "변환단위", "산출식",
            "측정방법", "원본 DWG", "객체 수", "작성일"
        };
        WriteHeaderRow(ws, headerRow, headers);
        row++;

        foreach (var line in model.Rows)
        {
            ws.Cell(row, 1).Value = line.Index;
            ws.Cell(row, 2).SetValue(line.Description);
            ws.Cell(row, 3).SetValue(line.TypeDisplayName);
            SetQuantityCell(ws.Cell(row, 4), line.Value, line.DecimalPlaces);
            ws.Cell(row, 5).SetValue(line.Unit);
            if (line.RawValue.HasValue)
            {
                ws.Cell(row, 5).Value = line.RawValue.Value;
                ws.Cell(row, 5).Style.NumberFormat.Format = "#,##0.000";
            }
            else
            {
                ws.Cell(row, 5).SetValue("-");
            }

            ws.Cell(row, 6).SetValue(line.SourceUnit ?? "-");
            ws.Cell(row, 7).SetValue(line.Unit);
            ws.Cell(row, 8).SetValue(line.CalculationExpression ?? "-");
            ws.Cell(row, 9).SetValue(line.MeasurementSourceDisplay);
            ws.Cell(row, 10).SetValue(line.SourceDrawingFileName ?? "-");
            ws.Cell(row, 11).Value = line.ObjectCount;
            ws.Cell(row, 12).Value = line.CreatedAt.LocalDateTime;
            ws.Cell(row, 12).Style.DateFormat.Format = "yyyy-MM-dd";

            ws.Row(row).Style.Alignment.Vertical = XLAlignmentVerticalValues.Top;
            row++;
        }

        var lastDataRow = Math.Max(headerRow, row - 1);
        ApplyTableBorder(ws, headerRow, lastDataRow, headers.Length);
        SetColumnWidths(ws, new[] { 6d, 26d, 12d, 12d, 12d, 10d, 10d, 30d, 16d, 22d, 10d, 12d });
        ws.Column(2).Style.Alignment.WrapText = true;
        ws.Column(8).Style.Alignment.WrapText = true;

        ws.SheetView.FreezeRows(headerRow);
        if (model.Rows.Count > 0)
        {
            ws.Range(headerRow, 1, lastDataRow, headers.Length).SetAutoFilter();
        }

        ApplyPrintSettings(ws, headerRow, headers.Length);
    }

    // ------------------------------------------------------------------
    // Sheet 3 - 검산내역
    // ------------------------------------------------------------------
    private void BuildVerificationSheet(XLWorkbook workbook, QuantityReportModel model)
    {
        var ws = workbook.Worksheets.Add("검산내역");
        var row = 1;
        ws.Cell(row, 1).Value = "검산내역";
        ws.Cell(row, 1).Style.Font.Bold = true;
        ws.Cell(row, 1).Style.Font.FontSize = 14;
        ws.Cell(row, 1).Style.Font.FontColor = AccentColor;
        row += 2;

        var headerRow = row;
        string[] headers = { "No.", "내역", "검산결과", "검토상태", "검산 세부", "검토메모" };
        WriteHeaderRow(ws, headerRow, headers);
        row++;

        foreach (var line in model.Rows)
        {
            ws.Cell(row, 1).Value = line.Index;
            ws.Cell(row, 2).SetValue(line.Description);
            ws.Cell(row, 3).SetValue(VerificationCellText(line));
            ws.Cell(row, 4).SetValue(Core.Models.QuantityReviewStatusDisplay.Label(line.ReviewStatus));
            ws.Cell(row, 5).SetValue(line.VerificationCheckLines.Count > 0
                ? string.Join("\n", line.VerificationCheckLines)
                : "-");
            ws.Cell(row, 5).Style.Alignment.WrapText = true;
            ws.Cell(row, 6).SetValue(string.IsNullOrWhiteSpace(line.ReviewNote) ? "-" : line.ReviewNote);
            ws.Cell(row, 6).Style.Alignment.WrapText = true;

            ws.Row(row).Style.Alignment.Vertical = XLAlignmentVerticalValues.Top;
            row++;
        }

        var lastDataRow = Math.Max(headerRow, row - 1);
        ApplyTableBorder(ws, headerRow, lastDataRow, headers.Length);
        SetColumnWidths(ws, new[] { 6d, 26d, 14d, 12d, 40d, 30d });
        ws.Column(2).Style.Alignment.WrapText = true;

        ws.SheetView.FreezeRows(headerRow);
        if (model.Rows.Count > 0)
        {
            ws.Range(headerRow, 1, lastDataRow, headers.Length).SetAutoFilter();
        }

        ApplyPrintSettings(ws, headerRow, headers.Length);
    }

    // ------------------------------------------------------------------
    // Sheet 4 - 프로젝트정보
    // ------------------------------------------------------------------
    private void BuildProjectInfoSheet(XLWorkbook workbook, QuantityReportModel model)
    {
        var ws = workbook.Worksheets.Add("프로젝트정보");
        var row = 1;
        ws.Cell(row, 1).Value = "프로젝트 정보";
        ws.Cell(row, 1).Style.Font.Bold = true;
        ws.Cell(row, 1).Style.Font.FontSize = 14;
        ws.Cell(row, 1).Style.Font.FontColor = AccentColor;
        row += 2;

        void WriteField(string label, string? value)
        {
            ws.Cell(row, 1).Value = label;
            ws.Cell(row, 1).Style.Font.Bold = true;
            ws.Cell(row, 1).Style.Font.FontColor = MutedTextColor;
            ws.Cell(row, 2).SetValue(string.IsNullOrWhiteSpace(value) ? "-" : value);
            row++;
        }

        WriteField("프로젝트명", model.ProjectName);
        WriteField("발주처", model.Client);
        WriteField("현장", model.Site);
        WriteField("설명", model.Description);
        WriteField("작성일", model.GeneratedAt.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture));
        WriteField("CAD Work Assistant 버전", model.AppVersion);
        row++;

        WriteField("총 산출내역", model.TotalCount.ToString(CultureInfo.InvariantCulture));
        WriteField("검토 완료", model.VerifiedCount.ToString(CultureInfo.InvariantCulture));
        WriteField("확인 필요", model.NeedsReviewCount.ToString(CultureInfo.InvariantCulture));
        WriteField("검산 오류", model.VerificationErrorCount.ToString(CultureInfo.InvariantCulture));

        ws.Column(1).Width = 24;
        ws.Column(2).Width = 40;
        ws.Column(2).Style.Alignment.WrapText = true;

        row += 2;
        ws.Cell(row, 1).Value = "Generated by CAD Work Assistant · TRSN CLARUS";
        ws.Cell(row, 1).Style.Font.FontColor = MutedTextColor;
        ws.Cell(row, 1).Style.Font.FontSize = 9;

        ApplyPrintSettings(ws, headerRow: 0, columnCount: 2);
    }

    // ------------------------------------------------------------------
    // Shared helpers
    // ------------------------------------------------------------------
    private static int WriteProjectHeaderBlock(IXLWorksheet ws, QuantityReportModel model, int row)
    {
        ws.Cell(row, 1).Value = $"프로젝트명: {model.ProjectName}";
        ws.Cell(row, 1).Style.Font.Bold = true;
        row++;

        if (!string.IsNullOrWhiteSpace(model.Site))
        {
            ws.Cell(row, 1).Value = $"현장: {model.Site}";
            row++;
        }

        if (!string.IsNullOrWhiteSpace(model.Client))
        {
            ws.Cell(row, 1).Value = $"발주처: {model.Client}";
            row++;
        }

        ws.Cell(row, 1).Value = $"작성일: {model.GeneratedAt:yyyy-MM-dd}";
        row++;
        return row;
    }

    private static void WriteHeaderRow(IXLWorksheet ws, int headerRow, string[] headers)
    {
        for (var i = 0; i < headers.Length; i++)
        {
            var cell = ws.Cell(headerRow, i + 1);
            cell.Value = headers[i];
            cell.Style.Font.Bold = true;
            cell.Style.Font.FontColor = TextColor;
            cell.Style.Fill.BackgroundColor = HeaderFill;
            cell.Style.Border.BottomBorder = XLBorderStyleValues.Thin;
            cell.Style.Border.BottomBorderColor = BorderColor;
            cell.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
        }

        ws.Row(headerRow).Height = 20;
    }

    /// <summary>모든 cell에 굵은 검정 테두리를 두르지 않는다(§59) - 표 바깥 얇은 border 하나만.</summary>
    private static void ApplyTableBorder(IXLWorksheet ws, int headerRow, int lastDataRow, int columnCount)
    {
        if (lastDataRow < headerRow)
        {
            return;
        }

        var range = ws.Range(headerRow, 1, lastDataRow, columnCount);
        range.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
        range.Style.Border.OutsideBorderColor = BorderColor;
        range.Style.Border.InsideBorder = XLBorderStyleValues.Hair;
        range.Style.Border.InsideBorderColor = BorderColor;
    }

    private static void SetColumnWidths(IXLWorksheet ws, double[] widths)
    {
        for (var i = 0; i < widths.Length; i++)
        {
            ws.Column(i + 1).Width = widths[i];
        }
    }

    /// <summary>숫자는 항상 진짜 numeric cell로 저장한다(§17) - 문자열로 저장하지 않는다. 단위는
    /// 별도 컬럼.</summary>
    private static void SetQuantityCell(IXLCell cell, decimal value, int decimalPlaces)
    {
        cell.Value = value;
        cell.Style.NumberFormat.Format = "#,##0." + new string('0', Math.Max(decimalPlaces, 0));
        cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;
    }

    private static string VerificationCellText(QuantityReportRow line) =>
        $"{Core.Verification.VerificationSeverityDisplay.Glyph(line.VerificationSeverity)} " +
        $"{Core.Verification.VerificationSeverityDisplay.Label(line.VerificationSeverity)}";

    /// <summary>§60-64: A4 Landscape, 폭 1페이지에 맞춤, Header Row 인쇄 반복, 페이지 번호 Footer.</summary>
    private static void ApplyPrintSettings(IXLWorksheet ws, int headerRow, int columnCount)
    {
        ws.PageSetup.PageOrientation = XLPageOrientation.Landscape;
        ws.PageSetup.PaperSize = XLPaperSize.A4Paper;
        ws.PageSetup.FitToPages(1, 0);
        ws.PageSetup.Margins.Top = 0.5;
        ws.PageSetup.Margins.Bottom = 0.5;
        ws.PageSetup.Margins.Left = 0.5;
        ws.PageSetup.Margins.Right = 0.5;

        if (headerRow > 0)
        {
            ws.PageSetup.SetRowsToRepeatAtTop(headerRow, headerRow);
        }

        ws.PageSetup.Header.Center.AddText(ws.Name);
        ws.PageSetup.Footer.Right.AddText("Page &P / &N", XLHFOccurrence.AllPages);
    }

    /// <summary>§84-87: 대상 경로에 직접 쓰지 않는다 - 같은 폴더에 임시 파일로 저장 → 다시 열어
    /// 최소한의 구조(Sheet 존재)를 검증 → 성공했을 때만 원래 경로로 원자적 교체. 중간에 실패하면
    /// 임시 파일을 지우고 원래 경로는 건드리지 않는다(§85, 깨진 .xlsx가 남지 않는다).</summary>
    private static void SaveAtomically(XLWorkbook workbook, string targetPath)
    {
        var directory = Path.GetDirectoryName(Path.GetFullPath(targetPath)) ?? ".";
        Directory.CreateDirectory(directory);
        var tempPath = Path.Combine(directory, $"~cwa_{Guid.NewGuid():N}.xlsx");

        try
        {
            workbook.SaveAs(tempPath);

            using (var verify = new XLWorkbook(tempPath))
            {
                if (verify.Worksheets.Count == 0)
                {
                    throw new InvalidOperationException("Generated workbook has no worksheets.");
                }
            }

            File.Move(tempPath, targetPath, overwrite: true);
        }
        catch
        {
            TryDelete(tempPath);
            throw;
        }
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch
        {
            // §85: 최선의 노력으로 정리한다 - 정리 실패 자체가 원래 오류를 가리면 안 된다.
        }
    }
}
