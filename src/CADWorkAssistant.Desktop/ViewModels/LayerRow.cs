using System;
using System.Threading.Tasks;
using CADWorkAssistant.Core.Drawing;
using CADWorkAssistant.Desktop.Common;

namespace CADWorkAssistant.Desktop.ViewModels;

/// <summary>
/// Layer Manager의 한 행. 체크박스는 이 행 자신의 <see cref="IsOn"/>에만 TwoWay 바인딩되므로 클릭 한
/// 번은 정확히 이 Layer 하나만 건드린다(§41 "실수로 다수 Layer를 변경하지 않도록"). IPC 호출이
/// 실패하면 체크 상태를 원래대로 되돌린다 - 낙관적 UI로 실제와 다른 상태를 보여주지 않는다.
/// </summary>
public sealed class LayerRow : ObservableObject
{
    private readonly Func<string, bool, Task<bool>> _applyToggle;
    private bool _isOn;
    private bool _isApplying;

    internal LayerRow(CadLayerDto dto, Func<string, bool, Task<bool>> applyToggle)
    {
        Name = dto.Name;
        _isOn = dto.IsOn;
        IsFrozen = dto.IsFrozen;
        IsLocked = dto.IsLocked;
        IsPlottable = dto.IsPlottable;
        IsCurrent = dto.IsCurrent;
        _applyToggle = applyToggle;
    }

    public string Name { get; }

    public bool IsFrozen { get; }

    public bool IsLocked { get; }

    public bool IsPlottable { get; }

    /// <summary>현재 활성 Layer - Off로 바꾸는 요청은 AutoCAD Handler가 조용히 무시한다(§44) - 여기서도
    /// 같은 규칙을 미리 반영해 체크박스를 비활성화한다(끄기 시도 자체를 UI에서 막는다).</summary>
    public bool IsCurrent { get; }

    public bool IsOn
    {
        get => _isOn;
        set
        {
            if (_isApplying || _isOn == value)
            {
                return;
            }

            // 서버(AutoCAD Handler/FakeAutoCad)는 현재 Layer를 끄는 요청을 "조용히 무시"하고도
            // 전체 SetLayerVisibility 호출 자체는 성공으로 응답한다(§44) - 그래서 ApplyAsync의
            // 실패 시 되돌리기 로직만으로는 이 경우를 잡을 수 없다. 서버를 부르기 전에 여기서
            // 먼저 막아 체크박스가 실제로 안 바뀐 상태를 거짓으로 보여주지 않게 한다.
            if (IsCurrent && !value)
            {
                OnPropertyChanged(nameof(IsOn));
                return;
            }

            var previous = _isOn;
            SetProperty(ref _isOn, value);
            _ = ApplyAsync(value, previous);
        }
    }

    internal void SetFromServer(bool isOn)
    {
        _isApplying = true;
        SetProperty(ref _isOn, isOn);
        _isApplying = false;
    }

    private async Task ApplyAsync(bool value, bool previous)
    {
        _isApplying = true;
        try
        {
            var succeeded = await _applyToggle(Name, value).ConfigureAwait(true);
            if (!succeeded)
            {
                SetProperty(ref _isOn, previous);
            }
        }
        finally
        {
            _isApplying = false;
        }
    }
}
