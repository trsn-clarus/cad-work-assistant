using System.Linq;
using CADWorkAssistant.Core.Drawing;
using CADWorkAssistant.Core.Ipc;
using CADWorkAssistant.Infrastructure.Ipc;
using CADWorkAssistant.Integration.Tests.Fixtures;

namespace CADWorkAssistant.Integration.Tests.Drawing;

/// <summary>
/// Milestone 5 §109 E2E B + §45-46(가장 중요한 원칙): GetLayers → 특정 Layer만 켜기(나머지 끄기) →
/// 상태 확인 → Restore → *정확히 원래 상태*로 돌아오는지 확인한다. "Restore = 전부 On"이 되면 이
/// 테스트가 실패해야 한다 - DrawingNavigationNormal 시나리오는 A-DOOR가 원래 Off로 시작한다.
/// </summary>
public class LayerIsolationEndToEndTests
{
    [Fact]
    public async Task IsolateByLayer_ThenRestore_ReturnsExactOriginalState_NotAllOn()
    {
        await using var fake = await FakeAutoCadProcess.StartAsync("DrawingNavigationNormal", CancellationToken.None);

        using var client = new AutoCadPipeClient();
        await client.ConnectAsync(fake.ProcessId, IpcProtocol.ConnectTimeoutMs, CancellationToken.None);

        var beforeResponse = await client.SendRequestAsync(
            IpcMessageTypes.GetLayers, null, IpcProtocol.RequestTimeoutMs, CancellationToken.None);
        var before = beforeResponse.DeserializePayload<GetLayersResponse>()!.Layers;

        // 시나리오 전제 확인: A-DOOR는 원래 Off, 나머지는 On으로 시작한다.
        Assert.False(before.Single(l => l.Name == "A-DOOR").IsOn);
        Assert.True(before.Single(l => l.Name == "A-WALL").IsOn);

        // "A-WALL만 보기" - 나머지 전부 끄고 A-WALL만 켠다 (선택 Layer만 보기, §37).
        var changes = before
            .Select(l => new LayerVisibilityChange(l.Name, l.Name == "A-WALL"))
            .ToList();

        var setResponse = await client.SendRequestAsync(
            IpcMessageTypes.SetLayerVisibility,
            new SetLayerVisibilityRequest(changes),
            IpcProtocol.RequestTimeoutMs,
            CancellationToken.None);
        Assert.True(setResponse.Success);

        var duringResponse = await client.SendRequestAsync(
            IpcMessageTypes.GetLayers, null, IpcProtocol.RequestTimeoutMs, CancellationToken.None);
        var during = duringResponse.DeserializePayload<GetLayersResponse>()!.Layers;

        Assert.True(during.Single(l => l.Name == "A-WALL").IsOn);
        Assert.False(during.Single(l => l.Name == "A-FLOOR").IsOn);
        Assert.False(during.Single(l => l.Name == "A-DOOR").IsOn);

        var restoreResponse = await client.SendRequestAsync(
            IpcMessageTypes.RestoreVisibility, null, IpcProtocol.RequestTimeoutMs, CancellationToken.None);
        Assert.True(restoreResponse.Success);

        var afterResponse = await client.SendRequestAsync(
            IpcMessageTypes.GetLayers, null, IpcProtocol.RequestTimeoutMs, CancellationToken.None);
        var after = afterResponse.DeserializePayload<GetLayersResponse>()!.Layers;

        // 핵심 단언: 전부 On이 아니라 딱 원래 상태(A-DOOR는 여전히 Off)로 돌아왔는지.
        foreach (var originalLayer in before)
        {
            var restoredLayer = after.Single(l => l.Name == originalLayer.Name);
            Assert.Equal(originalLayer.IsOn, restoredLayer.IsOn);
        }
    }

    [Fact]
    public async Task SetLayerVisibility_CurrentLayer_CannotBeTurnedOff()
    {
        // §44: 현재 활성 Layer(A-WALL, IsCurrent=true)는 꺼서 작업을 혼란스럽게 만들지 않는다.
        await using var fake = await FakeAutoCadProcess.StartAsync("DrawingNavigationNormal", CancellationToken.None);

        using var client = new AutoCadPipeClient();
        await client.ConnectAsync(fake.ProcessId, IpcProtocol.ConnectTimeoutMs, CancellationToken.None);

        await client.SendRequestAsync(
            IpcMessageTypes.SetLayerVisibility,
            new SetLayerVisibilityRequest(new[] { new LayerVisibilityChange("A-WALL", false) }),
            IpcProtocol.RequestTimeoutMs,
            CancellationToken.None);

        var response = await client.SendRequestAsync(
            IpcMessageTypes.GetLayers, null, IpcProtocol.RequestTimeoutMs, CancellationToken.None);
        var layers = response.DeserializePayload<GetLayersResponse>()!.Layers;

        Assert.True(layers.Single(l => l.Name == "A-WALL").IsOn);
    }

    [Fact]
    public async Task GetLayers_ReflectsFullLayerCatalog()
    {
        await using var fake = await FakeAutoCadProcess.StartAsync("LayerListNormal", CancellationToken.None);

        using var client = new AutoCadPipeClient();
        await client.ConnectAsync(fake.ProcessId, IpcProtocol.ConnectTimeoutMs, CancellationToken.None);

        var response = await client.SendRequestAsync(
            IpcMessageTypes.GetLayers, null, IpcProtocol.RequestTimeoutMs, CancellationToken.None);
        var layers = response.DeserializePayload<GetLayersResponse>()!.Layers;

        Assert.Equal(60, layers.Count);
    }
}
