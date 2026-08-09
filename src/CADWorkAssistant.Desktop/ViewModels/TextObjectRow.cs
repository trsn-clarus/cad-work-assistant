using CADWorkAssistant.Core.Text;

namespace CADWorkAssistant.Desktop.ViewModels;

/// <summary>Milestone 12 §63-65 - 선택 테이블 한 행. 긴 MText 전체가 행 높이를 늘리지 않도록 줄바꿈을
/// 공백으로 바꾼 한 줄 미리보기만 노출한다(§64) - 전체 내용은 Property Inspector가 보여준다.</summary>
public sealed class TextObjectRow
{
    public TextObjectRow(CadTextObjectDto source)
    {
        Source = source;
    }

    public CadTextObjectDto Source { get; }

    public string Handle => Source.Handle;

    public string TypeLabel => CadTextEntityTypeDisplay.Label(Source.EntityType);

    public string ContentPreview => Source.PlainText.Replace('\r', ' ').Replace('\n', ' ');

    public string LayerName => Source.LayerName;

    public double Height => Source.Height;

    public string ColorDisplay => Source.Color.DisplayName;

    public bool IsLocked => Source.IsLocked;

    public bool HasInlineFormatting => Source.HasInlineFormatting;
}
