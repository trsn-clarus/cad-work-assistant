using System;
using System.Collections.Generic;
using System.Linq;
using CADWorkAssistant.Core.Models;

namespace CADWorkAssistant.Core.Verification;

/// <summary>
/// 같은 Project의 QuantityRecord 목록에서 중복/비교/형상쌍 후보를 찾기 위한 색인을 한 번만 만든다
/// (Milestone 7 §95-96). 매 레코드마다 전체 목록을 훑는 O(n²) 대신, 배치 시작 시 이 Context를 한 번
/// 만들고(O(n)) 레코드마다 그 색인에서 조회한다. 범용 Rule Engine이 아니라 이 세 가지 조회만 하는
/// 구체적인 클래스다(§12).
/// </summary>
public sealed class QuantityVerificationContext
{
    private readonly ILookup<string, QuantityRecord> _byHandleSignature;
    private readonly ILookup<string, QuantityRecord> _byDescriptionKey;
    private readonly ILookup<string, QuantityRecord> _byShapePairKey;

    private QuantityVerificationContext(
        ILookup<string, QuantityRecord> byHandleSignature,
        ILookup<string, QuantityRecord> byDescriptionKey,
        ILookup<string, QuantityRecord> byShapePairKey)
    {
        _byHandleSignature = byHandleSignature;
        _byDescriptionKey = byDescriptionKey;
        _byShapePairKey = byShapePairKey;
    }

    /// <summary>records에 검산 대상 자기 자신이 포함되어 있어도 된다 - 조회 시 자동으로 제외한다.</summary>
    public static QuantityVerificationContext Build(IReadOnlyList<QuantityRecord> projectRecords)
    {
        var byHandleSignature = projectRecords
            .Where(r => r.ObjectHandles.Count > 0)
            .ToLookup(r => DuplicateKey(r));

        var byDescriptionKey = projectRecords
            .Where(r => !string.IsNullOrWhiteSpace(r.Description))
            .ToLookup(r => DescriptionKey(r));

        var byShapePairKey = projectRecords
            .Where(r => r.ObjectHandles.Count > 0 && (r.Type == "Area" || r.Type == "Length"))
            .ToLookup(r => ShapePairKey(r));

        return new QuantityVerificationContext(byHandleSignature, byDescriptionKey, byShapePairKey);
    }

    /// <summary>같은 Type + 같은 SourceDrawing + 완전히 동일한 ObjectHandle 집합(순서 무관)을 가진
    /// 다른 레코드 - Exact Set Match만 구현한다(§33, 부분 겹침은 향후 후보).</summary>
    public QuantityRecord? FindExactDuplicate(QuantityRecord record)
    {
        if (record.ObjectHandles.Count == 0)
        {
            return null;
        }

        return _byHandleSignature[DuplicateKey(record)].FirstOrDefault(other => other.Id != record.Id);
    }

    /// <summary>같은 Type + 같은(비어있지 않은) Description을 가진 레코드 중 이 레코드보다 먼저 만들어진
    /// 것 중 가장 최근 것 - "이전 기록과 비교"용(§34).</summary>
    public QuantityRecord? FindPreviousWithSameDescription(QuantityRecord record)
    {
        if (string.IsNullOrWhiteSpace(record.Description))
        {
            return null;
        }

        return _byDescriptionKey[DescriptionKey(record)]
            .Where(other => other.Id != record.Id && other.CreatedAt < record.CreatedAt)
            .OrderByDescending(other => other.CreatedAt)
            .FirstOrDefault();
    }

    /// <summary>같은 SourceDrawing + 동일한 ObjectHandle 집합을 가진 반대 타입(Area↔Length) 레코드 -
    /// 있으면 같은 폐합 도형을 Area/Length 양쪽에서 측정한 것으로 보고 compactness를 계산할 수 있다
    /// (§43). Description만 같다는 이유로 짝짓지 않는다(§42) - 반드시 Handle 집합이 일치해야 한다.</summary>
    public QuantityRecord? FindShapeSanityPair(QuantityRecord record)
    {
        if (record.ObjectHandles.Count == 0 || (record.Type != "Area" && record.Type != "Length"))
        {
            return null;
        }

        var pairedType = record.Type == "Area" ? "Length" : "Area";
        return _byShapePairKey[ShapePairKey(record)].FirstOrDefault(other => other.Id != record.Id && other.Type == pairedType);
    }

    private static string DuplicateKey(QuantityRecord record) =>
        $"{record.Type}|{NormalizeDrawing(record.SourceDrawing)}|{HandleSignature(record.ObjectHandles)}";

    private static string ShapePairKey(QuantityRecord record) =>
        $"{NormalizeDrawing(record.SourceDrawing)}|{HandleSignature(record.ObjectHandles)}";

    private static string DescriptionKey(QuantityRecord record) => $"{record.Type}|{record.Description.Trim()}";

    /// <summary>Handle 순서가 달라도 같은 집합이면 같은 서명이 나오도록 정렬+대문자 정규화한다(§32).</summary>
    internal static string HandleSignature(IReadOnlyList<string> handles) =>
        string.Join(",", handles.Select(h => h.ToUpperInvariant()).OrderBy(h => h, StringComparer.Ordinal));

    /// <summary>Windows 경로 대소문자 무시 정책은 Milestone 6과 동일하게 재사용한다(§97).</summary>
    private static string NormalizeDrawing(string sourceDrawing) => sourceDrawing.Trim().ToUpperInvariant();
}
