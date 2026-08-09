namespace CADWorkAssistant.Core.Text;

/// <summary>
/// Milestone 12 §9, §85-86 - AutoCAD DBText/MText를 IPC로 그대로 넘기지 않기 위한 순수 DTO. 실제
/// 필요한 필드만 담는다(§9) - 위치(Position)는 이번 Milestone의 어떤 화면에도 표시/편집하지 않아
/// 뺐다(Create는 새 점을 받지 기존 객체 위치를 옮기지 않는다).
/// </summary>
public sealed class CadTextObjectDto
{
    public CadTextObjectDto(
        string handle,
        CadTextEntityType entityType,
        string content,
        string plainText,
        string layerName,
        double height,
        double rotation,
        CadColorDto color,
        string textStyleName,
        bool isLocked,
        bool isAnnotative,
        bool hasInlineFormatting)
    {
        Handle = handle;
        EntityType = entityType;
        Content = content;
        PlainText = plainText;
        LayerName = layerName;
        Height = height;
        Rotation = rotation;
        Color = color;
        TextStyleName = textStyleName;
        IsLocked = isLocked;
        IsAnnotative = isAnnotative;
        HasInlineFormatting = hasInlineFormatting;
    }

    public string Handle { get; }

    public CadTextEntityType EntityType { get; }

    /// <summary>DBText.TextString 또는 MText.Contents(서식 코드 포함 원본) 그대로.</summary>
    public string Content { get; }

    /// <summary>DBText는 Content와 동일, MText는 MText.Text(서식 코드가 제거된 순수 텍스트) - §9
    /// PlainText.</summary>
    public string PlainText { get; }

    public string LayerName { get; }

    public double Height { get; }

    public double Rotation { get; }

    public CadColorDto Color { get; }

    public string TextStyleName { get; }

    /// <summary>객체가 속한 Layer가 Locked인지(§33, §150) - Layer 자체의 Lock이지 객체 개별 Lock이
    /// AutoCAD Text 엔티티에 따로 있는 게 아니다.</summary>
    public bool IsLocked { get; }

    public bool IsAnnotative { get; }

    /// <summary>MText 전용 - Content와 PlainText가 다르면(서식 코드가 실제로 있으면) true(§44-45).
    /// DBText는 항상 false.</summary>
    public bool HasInlineFormatting { get; }
}
