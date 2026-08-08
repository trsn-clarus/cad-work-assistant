using System.Collections.Generic;
using System.Linq;
using CADWorkAssistant.Core.Drawing;
using CADWorkAssistant.FakeAutoCad.Scenarios;

namespace CADWorkAssistant.FakeAutoCad.Handlers;

/// <summary>
/// 실제 AutoCAD.Ipc.DrawingIsolationState와 같은 역할이지만, FakeAutoCad는 진짜 도면이 없어서
/// "지금 각 Layer가 켜져 있는지"까지 직접 들고 있어야 한다(실제 Plugin은 LayerTable 자체가 그
/// 상태를 갖고 있다). Isolate → Restore가 "전부 On"이 아니라 "원래 상태"로 정확히 돌아오는지
/// (§45-46) 실제 프로세스 간 Named Pipe로 검증하는 것이 이 클래스의 존재 이유다.
/// </summary>
internal sealed class FakeDrawingState
{
    private readonly Dictionary<string, bool> _layerOnState;
    private readonly Dictionary<string, CadLayerDto> _layerTemplates;

    public FakeDrawingState(SimulationScenario scenario)
    {
        _layerTemplates = scenario.Layers.ToDictionary(l => l.Name);
        _layerOnState = scenario.Layers.ToDictionary(l => l.Name, l => l.IsOn);
    }

    public HashSet<string>? HiddenObjectHandles { get; set; }

    public Dictionary<string, bool>? OriginalLayerOnState { get; set; }

    public bool HasActiveIsolation => HiddenObjectHandles is not null || OriginalLayerOnState is not null;

    public IReadOnlyList<CadLayerDto> CurrentLayers => _layerTemplates.Values
        .Select(template => new CadLayerDto(
            template.Name,
            isOn: _layerOnState[template.Name],
            isFrozen: template.IsFrozen,
            isLocked: template.IsLocked,
            isPlottable: template.IsPlottable,
            colorIndex: template.ColorIndex,
            isCurrent: template.IsCurrent))
        .ToList();

    public bool LayerExists(string name) => _layerTemplates.ContainsKey(name);

    public bool IsCurrentLayer(string name) => _layerTemplates.TryGetValue(name, out var t) && t.IsCurrent;

    public void SetLayerOn(string name, bool isOn) => _layerOnState[name] = isOn;

    public bool GetLayerOn(string name) => _layerOnState[name];

    public void SnapshotLayerStateIfNeeded()
    {
        OriginalLayerOnState ??= new Dictionary<string, bool>(_layerOnState);
    }

    public void Clear()
    {
        HiddenObjectHandles = null;
        OriginalLayerOnState = null;
    }
}
