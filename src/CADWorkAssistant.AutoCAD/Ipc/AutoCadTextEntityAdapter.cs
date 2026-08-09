using System;
using Autodesk.AutoCAD.Colors;
using Autodesk.AutoCAD.DatabaseServices;
using CADWorkAssistant.Core.Text;

namespace CADWorkAssistant.AutoCAD.Ipc;

/// <summary>
/// Milestone 12 §86-89 - DBText/MText 공통 속성을 읽고 <see cref="TextUpdatePatch"/>를 적용하는 유일한
/// 곳. 두 타입의 실제 프로퍼티 이름이 다르다(DBText.TextString/Height, MText.Contents/TextHeight)
/// - 이 어댑터가 그 차이를 흡수한다. 거대한 추상화(Handler 인터페이스 계층 등)를 만들지 않고 정적
/// 헬퍼로 충분하다(§88).
/// </summary>
internal static class AutoCadTextEntityAdapter
{
    public static bool IsSupported(Entity entity) => entity is DBText or MText;

    public static CadTextObjectDto BuildDto(Entity entity, Transaction transaction) => entity switch
    {
        DBText dbText => BuildFromDBText(dbText, transaction),
        MText mText => BuildFromMText(mText, transaction),
        _ => throw new ArgumentException($"Unsupported text entity type: {entity.GetType().Name}", nameof(entity))
    };

    private static CadTextObjectDto BuildFromDBText(DBText dbText, Transaction transaction) => new(
        dbText.Handle.ToString(),
        CadTextEntityType.SingleLine,
        content: dbText.TextString,
        plainText: dbText.TextString,
        layerName: dbText.Layer,
        height: dbText.Height,
        rotation: dbText.Rotation,
        color: ToColorDto(dbText.Color),
        textStyleName: dbText.TextStyleName,
        isLocked: IsLayerLocked(dbText.LayerId, transaction),
        isAnnotative: dbText.Annotative == AnnotativeStates.True,
        hasInlineFormatting: false);

    private static CadTextObjectDto BuildFromMText(MText mText, Transaction transaction)
    {
        // §44-45: Contents(서식 코드 포함 원본)와 Text(서식 코드가 제거된 순수 텍스트)가 다르면 실제로
        // inline formatting이 있다는 뜻이다 - 별도 파서를 만들지 않고 AutoCAD가 이미 계산해 주는 두
        // 값을 비교하는 것만으로 판정한다(§46, 파서를 재구현하지 않는다).
        var raw = mText.Contents;
        var plain = mText.Text;

        return new CadTextObjectDto(
            mText.Handle.ToString(),
            CadTextEntityType.MultiLine,
            content: raw,
            plainText: plain,
            layerName: mText.Layer,
            height: mText.TextHeight,
            rotation: mText.Rotation,
            color: ToColorDto(mText.Color),
            textStyleName: mText.TextStyleName,
            isLocked: IsLayerLocked(mText.LayerId, transaction),
            isAnnotative: mText.Annotative == AnnotativeStates.True,
            hasInlineFormatting: !string.Equals(raw, plain, StringComparison.Ordinal));
    }

    private static bool IsLayerLocked(ObjectId layerId, Transaction transaction) =>
        transaction.GetObject(layerId, OpenMode.ForRead) is LayerTableRecord { IsLocked: true };

    /// <summary>§154 - Height/Content/Layer/Color만 골라 바꾼다. Width Factor/Oblique/Justify 등
    /// 패치하지 않은 속성은 손대지 않는다 - 호출부가 UpgradeOpen까지 마친 엔티티를 넘겨야 한다.</summary>
    public static void ApplyPatch(Entity entity, TextUpdatePatch patch)
    {
        switch (entity)
        {
            case DBText dbText:
                if (patch.Content.HasValue)
                {
                    dbText.TextString = patch.Content.Value!;
                }

                if (patch.Height.HasValue)
                {
                    dbText.Height = patch.Height.Value;
                }

                break;

            case MText mText:
                if (patch.Content.HasValue)
                {
                    // §45: 서식이 있는 MText의 Content를 plain text로 덮어쓰면 기존 서식이 사라질 수
                    // 있다 - 이 경고/제한은 Desktop UI 책임이다(§45, 여기서는 요청받은 대로 적용한다).
                    mText.Contents = patch.Content.Value!;
                }

                if (patch.Height.HasValue)
                {
                    mText.TextHeight = patch.Height.Value;
                }

                break;
        }

        if (patch.LayerName.HasValue)
        {
            entity.Layer = patch.LayerName.Value!;
        }

        if (patch.Color.HasValue)
        {
            entity.Color = ToAutoCadColor(patch.Color.Value!);
        }
    }

    public static CadColorDto ToColorDto(Color color)
    {
        if (color.IsByLayer)
        {
            return CadColorPalette.ByLayer;
        }

        if (color.IsByBlock)
        {
            return CadColorPalette.ByBlock;
        }

        if (color.IsByColor)
        {
            return new CadColorDto(CadColorMode.TrueColor, 0, color.Red, color.Green, color.Blue, DisplayName(color));
        }

        if (color.IsByAci)
        {
            return new CadColorDto(CadColorMode.Aci, color.ColorIndex, 0, 0, 0, DisplayName(color));
        }

        // ByPen/Foreground/LayerOff/LayerFrozen 등은 이 앱이 편집 대상으로 삼지 않는 특수 상태다
        // (§26 주석) - ByLayer로 안전하게 대체 표시한다.
        return CadColorPalette.ByLayer;
    }

    private static string DisplayName(Color color) =>
        string.IsNullOrEmpty(color.ColorNameForDisplay) ? $"색상 {color.ColorIndex}" : color.ColorNameForDisplay;

    public static Color ToAutoCadColor(CadColorDto dto) => dto.Mode switch
    {
        CadColorMode.ByLayer => Color.FromColorIndex(ColorMethod.ByLayer, 0),
        CadColorMode.ByBlock => Color.FromColorIndex(ColorMethod.ByBlock, 0),
        CadColorMode.Aci => Color.FromColorIndex(ColorMethod.ByAci, dto.AciIndex),
        CadColorMode.TrueColor => Color.FromRgb(dto.Red, dto.Green, dto.Blue),
        _ => Color.FromColorIndex(ColorMethod.ByLayer, 0)
    };
}
