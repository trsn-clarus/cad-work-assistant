using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.PlottingServices;
using CADWorkAssistant.Core.Plot;

namespace CADWorkAssistant.AutoCAD.Ipc;

/// <summary>
/// Milestone 11 §15-22, §29-33 - 실제 AutoCAD Plot Capability를 조회하는 코드. GetPlotCapabilitiesHandler와
/// PlotDrawingPdfHandler 둘 다 여기서 장치/용지/스타일을 읽는다 - 조회 로직을 두 곳에서 따로
/// 구현하지 않는다. 모든 타입/메서드는 실제 설치된 AutoCAD 2024(accoremgd.dll/acdbmgd.dll)를
/// 리플렉션으로 확인한 뒤 사용했다(§4-5) - 온라인 예제를 그대로 옮기지 않았다.
/// </summary>
internal static class PlotCapabilityReader
{
    /// <summary>
    /// §16-18: PlotConfigManager.Devices를 순회하며 각 장치가 PDF로 Plot 가능한지
    /// (PlotConfig.IsPlotToFile + DefaultFileExtension=="pdf") 실제로 로드해서 확인한다.
    /// PlotConfigManager.SetCurrentConfig(name)이 AutoCAD의 "현재 장치"를 전역으로 바꾸는 부작용이
    /// 있어(§133), 확인이 끝나면 원래 장치로 되돌린다(§61의 "capture original -> try -> set ->
    /// finally restore" 패턴을 장치 조회에도 그대로 적용). 이 되돌리기가 실제로 Plot 대화상자의
    /// 기본 장치에 영향을 주지 않는지는 Real AutoCAD 검증 대상이다(§133, §159 checklist).
    /// </summary>
    public static (IReadOnlyList<CadPlotDeviceDto> Devices, PlotConfig? PdfConfig, string? PdfDeviceName) ReadDevices()
    {
        var originalDeviceName = PlotConfigManager.CurrentConfig?.DeviceName;

        var devices = new List<CadPlotDeviceDto>();
        var pdfCandidates = new Dictionary<string, PlotConfig>(StringComparer.OrdinalIgnoreCase);

        foreach (PlotConfigInfo info in PlotConfigManager.Devices)
        {
            var isPdfCapable = false;
            try
            {
                var config = PlotConfigManager.SetCurrentConfig(info.DeviceName);
                isPdfCapable = config.IsPlotToFile &&
                    string.Equals(config.DefaultFileExtension, "pdf", StringComparison.OrdinalIgnoreCase);

                if (isPdfCapable)
                {
                    pdfCandidates[info.DeviceName] = config;
                }
            }
            catch (Autodesk.AutoCAD.Runtime.Exception)
            {
                // 일부 장치(예: 오프라인 네트워크 프린터)는 로드에 실패할 수 있다 - PDF 후보에서
                // 제외하고 계속 진행한다(§77, 장치 하나의 실패로 전체 조회를 막지 않는다).
            }

            devices.Add(new CadPlotDeviceDto(info.DeviceName, isPdfCapable));
        }

        if (originalDeviceName is not null)
        {
            try
            {
                PlotConfigManager.SetCurrentConfig(originalDeviceName);
            }
            catch (Autodesk.AutoCAD.Runtime.Exception)
            {
                // 원래 장치를 복원하지 못해도 Capability 조회 자체는 이미 끝났다 - 여기서 예외를
                // 던져 전체 요청을 실패시키지 않는다.
            }
        }

        // §17-18 "우선순위 정책"은 PlotPdfDeviceSelector 한 곳에만 있다 - 여기서 다시 구현하지 않는다.
        var selected = PlotPdfDeviceSelector.SelectBest(devices);
        var resolvedConfig = selected is not null && pdfCandidates.TryGetValue(selected.Name, out var matchedConfig)
            ? matchedConfig
            : null;

        return (devices, resolvedConfig, selected?.Name);
    }

    /// <summary>§19-22: 주어진 PDF 장치가 실제로 지원하는 용지 목록. 장치별 Canonical Media 이름을
    /// 얻으려면 그 장치가 이미 지정된 PlotSettings가 필요하다(PlotSettingsValidator.
    /// GetCanonicalMediaNameList) - 여기서만 쓰는 임시 PlotSettings이고 어떤 Database/Layout에도
    /// 연결하지 않는다(Add하지 않음, §6 원본 비변경 원칙).</summary>
    public static IReadOnlyList<CadPlotMediaDto> ReadMedia(PlotConfig deviceConfig, string deviceName)
    {
        var validator = PlotSettingsValidator.Current;
        using var scratch = new PlotSettings(modelType: true);
        validator.SetPlotConfigurationName(scratch, deviceName, null);

        var media = new List<CadPlotMediaDto>();
        StringCollection canonicalNames;
        try
        {
            canonicalNames = validator.GetCanonicalMediaNameList(scratch);
        }
        catch (Autodesk.AutoCAD.Runtime.Exception)
        {
            return media;
        }

        foreach (string canonicalName in canonicalNames)
        {
            MediaBounds bounds;
            try
            {
                bounds = deviceConfig.GetMediaBounds(canonicalName);
            }
            catch (Autodesk.AutoCAD.Runtime.Exception)
            {
                continue;
            }

            // §124: PageSize의 실제 단위(mm 가정)는 Real AutoCAD에서 물리 출력 크기로 재확인해야
            // 한다 - 이 앱의 CadPlotMediaDto.WidthMm/HeightMm은 mm를 전제로 한 이름이다.
            var localeName = validator.GetLocaleMediaName(scratch, canonicalName);
            media.Add(new CadPlotMediaDto(canonicalName, localeName, bounds.PageSize.X, bounds.PageSize.Y));
        }

        return media;
    }

    /// <summary>§30-33: 현재 세션에서 사용 가능한 CTB/STB Style Sheet 이름 목록 - 도면과 무관하게
    /// AutoCAD 설치본 단위로 조회된다.</summary>
    public static (IReadOnlyList<string> ColorDependent, IReadOnlyList<string> Named) ReadStyleSheets()
    {
        var ctb = PlotConfigManager.ColorDependentPlotStyles.Cast<string>().ToList();
        var stb = PlotConfigManager.NamedPlotStyles.Cast<string>().ToList();
        return (ctb, stb);
    }

    /// <summary>§31 - Database.PlotStyleMode(bool)를 도메인 열거값으로 바꾼다. true=Named(STB),
    /// false=ColorDependent(CTB)는 AutoCAD API의 안정적인 공개 규약이다 - 그래도 실제 도면 두 종류
    /// (CTB 한 장, STB 한 장)로 Real Validation에서 재확인한다(§131, §159).</summary>
    public static CadPlotStyleMode ReadCurrentStyleMode(Database database) =>
        database.PlotStyleMode ? CadPlotStyleMode.Named : CadPlotStyleMode.ColorDependent;

    /// <summary>§44-46: Model + 모든 Paper Space Layout 목록. Layout.ModelType(PlotSettings 상속
    /// 프로퍼티)으로 Model 여부를 판단한다 - 이름이 "Model"인지 문자열로 추측하지 않는다.</summary>
    public static IReadOnlyList<CadPlotLayoutDto> ReadLayouts(Database database, Transaction transaction)
    {
        var currentLayoutName = LayoutManager.Current.CurrentLayout;
        var layoutDictionary = (DBDictionary)transaction.GetObject(database.LayoutDictionaryId, OpenMode.ForRead);

        var layouts = new List<CadPlotLayoutDto>();
        foreach (DBDictionaryEntry entry in layoutDictionary)
        {
            if (transaction.GetObject(entry.Value, OpenMode.ForRead) is not Layout layout)
            {
                continue;
            }

            layouts.Add(new CadPlotLayoutDto(
                layout.LayoutName,
                layout.ModelType,
                string.Equals(layout.LayoutName, currentLayoutName, StringComparison.Ordinal)));
        }

        return layouts;
    }

    /// <summary>
    /// §10-11: PlotDrawingPdfHandler가 실제로 Plot할 Layout을 찾는다. Window scope는 항상 Model
    /// Space(ModelType==true인 Layout 하나)를 대상으로 한다(§11) - 이름을 "Model"로 문자열
    /// 추측하지 않는다. CurrentLayout scope는 요청에 담긴 LayoutName으로 찾고, 없으면 AutoCAD의
    /// 현재 Layout을 쓴다.
    /// </summary>
    public static bool TryResolveLayout(
        Database database,
        Transaction transaction,
        CadPlotScope scope,
        string? layoutName,
        out ObjectId layoutId,
        out Layout layout)
    {
        var layoutDictionary = (DBDictionary)transaction.GetObject(database.LayoutDictionaryId, OpenMode.ForRead);
        var targetName = scope == CadPlotScope.CurrentLayout && !string.IsNullOrWhiteSpace(layoutName)
            ? layoutName
            : LayoutManager.Current.CurrentLayout;

        foreach (DBDictionaryEntry entry in layoutDictionary)
        {
            if (transaction.GetObject(entry.Value, OpenMode.ForRead) is not Layout candidate)
            {
                continue;
            }

            var isMatch = scope == CadPlotScope.Window
                ? candidate.ModelType
                : string.Equals(candidate.LayoutName, targetName, StringComparison.OrdinalIgnoreCase);

            if (!isMatch)
            {
                continue;
            }

            layoutId = entry.Value;
            layout = candidate;
            return true;
        }

        layoutId = ObjectId.Null;
        layout = null!;
        return false;
    }
}
