using System;

namespace CADWorkAssistant.Core.Models;

public sealed class OperationLogEntry
{
    public OperationLogEntry(DateTimeOffset timestamp, string level, string message, string detail)
    {
        Timestamp = timestamp;
        Level = level;
        Message = message;
        Detail = detail;
    }

    public DateTimeOffset Timestamp { get; }
    public string Level { get; }
    public string Message { get; }
    public string Detail { get; }
}
