using System;
using System.IO;
using System.Reflection;
using CADWorkAssistant.Documents.Pdf.UserManual;
using CADWorkAssistant.ManualBuilder;

if (args.Length < 2)
{
    Console.Error.WriteLine("Usage: CADWorkAssistant.ManualBuilder <markdown-path> <output-pdf-path>");
    return 1;
}

var markdownPath = Path.GetFullPath(args[0]);
var outputPath = Path.GetFullPath(args[1]);

if (!File.Exists(markdownPath))
{
    Console.Error.WriteLine($"Markdown source not found: {markdownPath}");
    return 1;
}

var markdownText = File.ReadAllText(markdownPath);
var baseDirectory = Path.GetDirectoryName(markdownPath)!;
var document = ManualMarkdownParser.Parse(markdownText, baseDirectory);

var result = new UserManualPdfBuilder().BuildAndSave(document, AppVersion(), DateTimeOffset.Now, outputPath);

Console.WriteLine($"User manual PDF generated: {result.FilePath}");
Console.WriteLine($"  Pages: {result.PageCount}, Chapters: {result.ChapterCount}");
return 0;

// Directory.Build.props의 CwaVersion(단일 소스, docs/DEPLOYMENT.md)이 이 도구의 어셈블리 버전에도
// 그대로 반영된다 - Desktop의 QuantityPdfExportCoordinator.AppVersion과 같은 방식(버전을 하드코딩하지 않는다).
static string AppVersion()
{
    var version = Assembly.GetExecutingAssembly().GetName().Version;
    return version is null ? "-" : $"{version.Major}.{version.Minor}.{version.Build}";
}
