using System;

namespace CADWorkAssistant.Core.Models;

public sealed class QuantityRecord
{
    public QuantityRecord(
        string id,
        string type,
        string layer,
        int objectCount,
        decimal value,
        string unit,
        string sourceDrawing,
        DateTimeOffset createdAt)
    {
        Id = id;
        Type = type;
        Layer = layer;
        ObjectCount = objectCount;
        Value = value;
        Unit = unit;
        SourceDrawing = sourceDrawing;
        CreatedAt = createdAt;
    }

    public string Id { get; }
    public string Type { get; }
    public string Layer { get; }
    public int ObjectCount { get; }
    public decimal Value { get; }
    public string Unit { get; }
    public string SourceDrawing { get; }
    public DateTimeOffset CreatedAt { get; }
}
