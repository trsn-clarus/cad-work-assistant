using System;

namespace CADWorkAssistant.Core.Text;

/// <summary>
/// Milestone 12 §26, §85 - AutoCAD Autodesk.AutoCAD.Colors.Color를 IPC로 그대로 넘기지 않기 위한
/// 순수 DTO. Mode에 따라 의미 있는 필드가 달라진다 - AciIndex는 Mode==Aci일 때만, Red/Green/Blue는
/// Mode==TrueColor일 때만 의미가 있다(ByLayer/ByBlock은 둘 다 무시). DisplayName은 AutoCAD가 주는
/// 이름(ColorNameForDisplay) 또는 이 앱이 붙인 이름(§28 compact palette)을 그대로 담아 UI가 다시
/// 계산하지 않게 한다.
/// </summary>
public sealed class CadColorDto : IEquatable<CadColorDto>
{
    public CadColorDto(CadColorMode mode, short aciIndex, byte red, byte green, byte blue, string displayName)
    {
        Mode = mode;
        AciIndex = aciIndex;
        Red = red;
        Green = green;
        Blue = blue;
        DisplayName = displayName;
    }

    public CadColorMode Mode { get; }

    public short AciIndex { get; }

    public byte Red { get; }

    public byte Green { get; }

    public byte Blue { get; }

    public string DisplayName { get; }

    public bool Equals(CadColorDto? other) =>
        other is not null &&
        Mode == other.Mode &&
        AciIndex == other.AciIndex &&
        Red == other.Red &&
        Green == other.Green &&
        Blue == other.Blue;

    public override bool Equals(object? obj) => Equals(obj as CadColorDto);

    // netstandard2.0에는 System.HashCode가 없다 - 직접 조합한다.
    public override int GetHashCode()
    {
        unchecked
        {
            var hash = 17;
            hash = (hash * 31) + Mode.GetHashCode();
            hash = (hash * 31) + AciIndex.GetHashCode();
            hash = (hash * 31) + Red.GetHashCode();
            hash = (hash * 31) + Green.GetHashCode();
            hash = (hash * 31) + Blue.GetHashCode();
            return hash;
        }
    }
}
