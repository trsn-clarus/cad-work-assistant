using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.Json;

namespace CADWorkAssistant.Persistence;

/// <summary>
/// SQLite는 decimal/DateTimeOffset/문자열 배열 타입이 없다 - 이 프로젝트 전체가 같은 규칙으로
/// 왕복시키도록 한 곳에 모았다(Milestone 6 §84-85). decimal은 REAL(부동소수점)로 저장하면 정밀도가
/// 깨질 수 있어(255.941 같은 값의 왕복 정확성이 중요, §143 기존 회귀 보호) TEXT로 저장한다.
/// 시각은 UTC로 정규화해서 저장하고(§84), 읽을 때 DateTimeOffset(오프셋 0)으로 복원한다 - 화면
/// 표시 시점에 호출부가 ToLocalTime()을 부른다.
/// </summary>
internal static class SqliteValueConverters
{
    public static string ToDbText(decimal value) => value.ToString(CultureInfo.InvariantCulture);

    public static decimal ParseDecimal(string text) => decimal.Parse(text, CultureInfo.InvariantCulture);

    public static string ToDbText(DateTimeOffset value) =>
        value.UtcDateTime.ToString("O", CultureInfo.InvariantCulture);

    public static DateTimeOffset ParseDateTimeOffset(string text)
    {
        // ToDbText는 항상 UTC DateTime을 "O" 포맷으로 쓴다 - Kind=Utc라 결과 문자열에 항상 'Z'가
        // 붙는다. RoundtripKind만으로 그 'Z'를 인식해 Kind=Utc로 복원할 수 있다 - AssumeUniversal을
        // 같이 쓰면 "RoundtripKind는 AssumeLocal/AssumeUniversal/AdjustToUniversal과 함께 쓸 수
        // 없다"는 ArgumentException이 난다(실제로 테스트에서 겪은 오류, 둘 다 명시하면 항상 예외).
        var parsed = DateTime.Parse(text, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
        return new DateTimeOffset(parsed.ToUniversalTime(), TimeSpan.Zero);
    }

    public static string? ToDbJson(IReadOnlyList<string>? values) =>
        values is null || values.Count == 0 ? null : JsonSerializer.Serialize(values);

    public static IReadOnlyList<string> ParseStringList(string? json) =>
        string.IsNullOrEmpty(json) ? Array.Empty<string>() : JsonSerializer.Deserialize<List<string>>(json) ?? new List<string>();
}
