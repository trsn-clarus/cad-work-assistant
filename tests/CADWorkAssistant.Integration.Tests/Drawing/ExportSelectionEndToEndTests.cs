using System.Linq;
using CADWorkAssistant.Core.Drawing;
using CADWorkAssistant.Core.Ipc;
using CADWorkAssistant.Infrastructure.Ipc;
using CADWorkAssistant.Integration.Tests.Fixtures;

namespace CADWorkAssistant.Integration.Tests.Drawing;

/// <summary>
/// Milestone 5 §109 E2E C + §110 Error E2E: Select → Export → 성공/실패. FakeAutoCad는 실제
/// WBLOCK을 하지 않지만 대상 경로에 실제로 파일을 쓴다 - "Desktop → IPC → 파일시스템" 배관은
/// 진짜로 검증하고, DWG Binary 정확성은 주장하지 않는다(§69, §72).
/// </summary>
public class ExportSelectionEndToEndTests
{
    [Fact]
    public async Task ExportSelection_ValidHandles_WritesFileAndReportsSuccess()
    {
        await using var fake = await FakeAutoCadProcess.StartAsync("ExportSelectionSuccess", CancellationToken.None);

        using var client = new AutoCadPipeClient();
        await client.ConnectAsync(fake.ProcessId, IpcProtocol.ConnectTimeoutMs, CancellationToken.None);

        var selectionResponse = await client.SendRequestAsync(
            IpcMessageTypes.SelectDrawingObjects,
            new SelectDrawingObjectsRequest(SelectionMode.Window),
            IpcProtocol.RequestTimeoutMs,
            CancellationToken.None);
        var selection = selectionResponse.DeserializePayload<DrawingSelectionResponse>()!;

        var targetPath = Path.Combine(Path.GetTempPath(), $"cwa-export-test-{Guid.NewGuid():n}.dwg");
        try
        {
            var suggestedName = ExportFileNameService.SuggestFileName("School_Roof.dwg", "실내마감표");
            Assert.Equal("School_Roof_실내마감표.dwg", suggestedName);

            var exportResponse = await client.SendRequestAsync(
                IpcMessageTypes.ExportSelection,
                new ExportSelectionRequest(selection.Objects.Select(o => o.Handle).ToList(), targetPath),
                IpcProtocol.RequestTimeoutMs,
                CancellationToken.None);

            Assert.True(exportResponse.Success);
            var result = exportResponse.DeserializePayload<ExportSelectionResponse>()!;
            Assert.Equal(selection.Objects.Count, result.ObjectCount);
            Assert.Equal(targetPath, result.FilePath);
            Assert.True(File.Exists(targetPath));

            // §63: Export 후에도 원본 도면은 그대로 남아 있어야 한다 - GetDrawingOverview로 원본
            // 객체 수가 그대로인지 확인한다(원본 Database를 건드리지 않았다는 방증).
            var overviewResponse = await client.SendRequestAsync(
                IpcMessageTypes.GetDrawingOverview, null, IpcProtocol.RequestTimeoutMs, CancellationToken.None);
            var overview = overviewResponse.DeserializePayload<DrawingOverviewResponse>()!;
            Assert.Equal(6, overview.ObjectCount);
        }
        finally
        {
            if (File.Exists(targetPath))
            {
                File.Delete(targetPath);
            }
        }
    }

    [Fact]
    public async Task ExportSelection_NoHandles_FailsWithInvalidRequest()
    {
        await using var fake = await FakeAutoCadProcess.StartAsync("ExportSelectionSuccess", CancellationToken.None);

        using var client = new AutoCadPipeClient();
        await client.ConnectAsync(fake.ProcessId, IpcProtocol.ConnectTimeoutMs, CancellationToken.None);

        var response = await client.SendRequestAsync(
            IpcMessageTypes.ExportSelection,
            new ExportSelectionRequest(Array.Empty<string>(), Path.Combine(Path.GetTempPath(), "should-not-be-created.dwg")),
            IpcProtocol.RequestTimeoutMs,
            CancellationToken.None);

        Assert.False(response.Success);
        Assert.Equal(IpcErrorCode.InvalidRequest, response.Error!.Code);
    }

    [Fact]
    public async Task ExportSelection_SimulatedWblockFailure_ReturnsStructuredError()
    {
        await using var fake = await FakeAutoCadProcess.StartAsync("ExportSelectionError", CancellationToken.None);

        using var client = new AutoCadPipeClient();
        await client.ConnectAsync(fake.ProcessId, IpcProtocol.ConnectTimeoutMs, CancellationToken.None);

        var response = await client.SendRequestAsync(
            IpcMessageTypes.ExportSelection,
            new ExportSelectionRequest(new[] { "2A7F" }, Path.Combine(Path.GetTempPath(), "should-fail.dwg")),
            IpcProtocol.RequestTimeoutMs,
            CancellationToken.None);

        Assert.False(response.Success);
        Assert.Equal(IpcErrorCode.ApiExecutionFailed, response.Error!.Code);
    }
}
