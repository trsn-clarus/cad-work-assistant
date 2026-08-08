using System.IO;
using System.Linq;
using System.Text;

namespace CADWorkAssistant.Core.Drawing;

/// <summary>
/// Export 대상 파일명을 만든다 - AutoCAD/파일시스템 의존 없이 순수 문자열 로직이라 Core에 둔다
/// (§53-54, §100). 실제 저장 경로/덮어쓰기 확인은 Desktop의 native SaveFileDialog가 처리한다 (§57).
/// </summary>
public static class ExportFileNameService
{
    /// <summary>Windows에서 파일명에 쓸 수 없는 문자 (§54).</summary>
    private static readonly char[] InvalidCharacters = { '\\', '/', ':', '*', '?', '"', '<', '>', '|' };

    /// <summary>
    /// 원본 DWG 파일명 + 설명 → 제안 파일명 (§53, §100). 예: "OO학교_건축.dwg" + "실내마감표" →
    /// "OO학교_건축_실내마감표.dwg". 설명이 비어 있으면 원본 파일명 그대로(확장자 유지) 돌려준다.
    /// </summary>
    public static string SuggestFileName(string originalDrawingFileName, string description)
    {
        var baseName = Path.GetFileNameWithoutExtension(originalDrawingFileName);

        // Sanitize("")는 "Export"로 대체된 값을 돌려준다(빈 파일명 방지 목적) - 그 대체가 여기서도
        // 적용되면 설명을 안 적었을 때 "..._Export.dwg"가 되어버린다. "설명이 없다"는 Sanitize를
        // 부르기 전에 먼저 판단한다.
        if (string.IsNullOrWhiteSpace(description))
        {
            return baseName + ".dwg";
        }

        return $"{baseName}_{Sanitize(description)}.dwg";
    }

    /// <summary>
    /// 유효하지 않은 Windows 파일명 문자를 밑줄로 치환하고, 앞뒤 공백/마침표를 제거한다. 결과가
    /// 완전히 비면 "Export"로 대체한다 - 빈 파일명으로 저장 시도를 방지한다.
    /// </summary>
    public static string Sanitize(string text)
    {
        var builder = new StringBuilder(text.Length);
        foreach (var c in text)
        {
            builder.Append(InvalidCharacters.Contains(c) || char.IsControl(c) ? '_' : c);
        }

        var trimmed = builder.ToString().Trim().Trim('.');
        return trimmed.Length == 0 ? "Export" : trimmed;
    }
}
