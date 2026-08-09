using System;
using System.IO;
using PdfSharp.Fonts;

namespace CADWorkAssistant.Documents.Pdf;

/// <summary>
/// Milestone 10 §48-52 - PDFsharp 6.x는 .NET 8(비-GDI 빌드)에서 폰트를 전혀 모른다: 실제로 검증해보니
/// <see cref="GlobalFontSettings.FontResolver"/>를 등록하지 않으면 첫 텍스트 렌더링에서
/// "No appropriate font found" InvalidOperationException을 즉시 던진다(§169). 이 앱은 애초에
/// Windows 전용 설치형 프로그램(WPF, CLAUDE.md)이므로, Windows가 Vista부터 항상 기본 제공해온 한글
/// UI 폰트 "맑은 고딕"(malgun.ttf/malgunbd.ttf, %WINDIR%\Fonts)을 실행 시점에 직접 읽어 PDF에
/// 임베드한다. 폰트 파일 자체를 설치 프로그램에 복사/재배포하지 않는다(§49) - 사용자의 Windows
/// 설치본에 이미 있는 폰트를 그때그때 읽어서 PDF 안에 필요한 글리프만 내장할 뿐이다.
/// </summary>
internal sealed class WindowsKoreanFontResolver : IFontResolver
{
    private const string RegularFace = "MalgunGothic#Regular";
    private const string BoldFace = "MalgunGothic#Bold";

    private static readonly string FontDirectory =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "Fonts");

    public byte[] GetFont(string faceName)
    {
        var fileName = faceName == BoldFace ? "malgunbd.ttf" : "malgun.ttf";
        var path = Path.Combine(FontDirectory, fileName);
        if (!File.Exists(path))
        {
            // §48: 다른 Windows 설치본에는 맑은 고딕이 없을 수 있다는 극단적인 경우를 대비해, 최소한
            // 원인을 알 수 있는 메시지로 실패한다 - PDFsharp의 기본 "No appropriate font" 메시지보다
            // 실제 원인(폰트 파일 부재)을 더 명확히 가리킨다.
            throw new InvalidOperationException(
                $"Windows Korean font not found at '{path}'. CAD Work Assistant's PDF export requires " +
                "the Malgun Gothic font that ships with Windows by default.");
        }

        return File.ReadAllBytes(path);
    }

    public FontResolverInfo ResolveTypeface(string familyName, bool isBold, bool isItalic) =>
        new(isBold ? BoldFace : RegularFace);

    /// <summary>여러 번 호출돼도 안전 - 마지막에 등록한 인스턴스가 그대로 유지된다(idempotent).</summary>
    public static void EnsureRegistered()
    {
        GlobalFontSettings.FontResolver ??= new WindowsKoreanFontResolver();
    }
}
