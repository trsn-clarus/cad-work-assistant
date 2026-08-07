using System;

namespace CADWorkAssistant.Core.Models;

public sealed class DrawingFile
{
    public DrawingFile(string fileName, string path, string units, DateTimeOffset lastModified, bool isActive)
    {
        FileName = fileName;
        Path = path;
        Units = units;
        LastModified = lastModified;
        IsActive = isActive;
    }

    public string FileName { get; }
    public string Path { get; }
    public string Units { get; }
    public DateTimeOffset LastModified { get; }
    public bool IsActive { get; }
}
