using System;
using System.Collections.Generic;

namespace CADWorkAssistant.AutoCAD.Ipc;

/// <summary>
/// AutoCAD의 Managed API는 마케팅 연도("2024")를 직접 노출하지 않는다 - Application.Version과
/// acad.exe의 FileVersionInfo 모두 내부 버전("R24.3.119.0.0")만 준다 (실제 확인함, 이 PC의 AutoCAD 2024).
/// 이 표는 Autodesk가 공개한 릴리스 번호 체계를 바탕으로 한 "가장 근접한 추정"이며, 매핑에 없는
/// 버전이면 절대 연도를 지어내지 않고 원본 버전 문자열을 그대로 보여준다.
/// </summary>
internal static class AutoCadVersionMap
{
    private static readonly Dictionary<(int Major, int Minor), int> KnownReleaseYears = new()
    {
        [(24, 3)] = 2024, // 이 PC에서 FileVersionInfo로 직접 확인함
        [(24, 2)] = 2023,
        [(24, 1)] = 2022,
        [(24, 0)] = 2021,
        [(23, 1)] = 2020,
        [(23, 0)] = 2019,
        [(25, 0)] = 2025
    };

    public static string ToProductName(Version internalVersion)
    {
        return KnownReleaseYears.TryGetValue((internalVersion.Major, internalVersion.Minor), out var year)
            ? $"AutoCAD {year}"
            : $"AutoCAD (build {internalVersion})";
    }
}
