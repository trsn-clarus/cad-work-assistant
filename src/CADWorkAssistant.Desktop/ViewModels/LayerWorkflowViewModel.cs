using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using CADWorkAssistant.Core.Drawing;
using CADWorkAssistant.Core.Ipc;
using CADWorkAssistant.Desktop.Common;
using CADWorkAssistant.Desktop.Services;
using Serilog;

namespace CADWorkAssistant.Desktop.ViewModels;

/// <summary>
/// Layer 목록 조회/검색/개별 On-Off/"선택 Layer만 보기"를 담당한다 (§38-41, §81). Freeze/Thaw
/// 토글은 이번 Milestone에서 만들지 않는다 - Drawing 상태에 미치는 영향이 커서 조회 전용으로
/// 남겼다(§42, CadLayerDto.IsFrozen은 표시만 한다).
/// </summary>
public sealed class LayerWorkflowViewModel : ObservableObject
{
    private readonly IAutoCadConnectionManager _connectionManager;
    private readonly ObservableCollection<LayerRow> _allLayers = new();
    private readonly RelayCommand _refreshCommand;

    private string _searchText = string.Empty;
    private bool _isLoading;

    public LayerWorkflowViewModel(IAutoCadConnectionManager connectionManager)
    {
        _connectionManager = connectionManager;
        FilteredLayers = new ObservableCollection<LayerRow>();
        _refreshCommand = new RelayCommand(async () => await RefreshAsync().ConfigureAwait(true), () => !_isLoading);
    }

    public ObservableCollection<LayerRow> FilteredLayers { get; }

    public ICommand RefreshCommand => _refreshCommand;

    public bool HasLayers => _allLayers.Count > 0;

    public bool IsLoading
    {
        get => _isLoading;
        private set
        {
            if (SetProperty(ref _isLoading, value))
            {
                _refreshCommand.RaiseCanExecuteChanged();
            }
        }
    }

    /// <summary>Layer 검색 - 이름 부분 문자열, 대소문자 무시 (§89-91). ViewModel에서 직접 필터링한다 -
    /// Milestone 4.5에서 "아무것도 필터링하지 않는 가짜 검색창"을 제거한 적이 있어(design-system/pages/
    /// workspace.md), 여기서 새로 넣는 검색은 반드시 실제로 동작해야 한다.</summary>
    public string SearchText
    {
        get => _searchText;
        set
        {
            if (SetProperty(ref _searchText, value))
            {
                ApplyFilter();
            }
        }
    }

    public async Task RefreshAsync()
    {
        IsLoading = true;
        try
        {
            var response = await _connectionManager
                .SendRequestAsync(IpcMessageTypes.GetLayers, payload: null, CancellationToken.None)
                .ConfigureAwait(true);

            if (!response.Success)
            {
                return;
            }

            var layers = response.DeserializePayload<GetLayersResponse>()?.Layers ?? System.Array.Empty<CadLayerDto>();

            _allLayers.Clear();
            foreach (var dto in layers)
            {
                _allLayers.Add(new LayerRow(dto, ToggleAsync));
            }

            OnPropertyChanged(nameof(HasLayers));
            ApplyFilter();
        }
        catch (System.Exception ex)
        {
            Log.Warning(ex, "GetLayers failed");
        }
        finally
        {
            IsLoading = false;
        }
    }

    /// <summary>"선택 Layer만 보기" (§37) - DrawingWorkflowViewModel이 현재 SelectionSession의 Layer
    /// 이름들을 넘겨준다. 요청 후 실제로 적용된 상태를 다시 읽어와 반영한다 - 현재 Layer는 Handler가
    /// Off 요청을 조용히 무시하므로(§44) 로컬에서 결과를 추측하지 않는다.</summary>
    public async Task<bool> IsolateLayersAsync(System.Collections.Generic.IReadOnlyList<string> layerNamesToShow)
    {
        var toShow = new System.Collections.Generic.HashSet<string>(layerNamesToShow);
        var changes = _allLayers.Select(row => new LayerVisibilityChange(row.Name, toShow.Contains(row.Name))).ToList();

        if (changes.Count == 0)
        {
            return true;
        }

        var response = await _connectionManager
            .SendRequestAsync(IpcMessageTypes.SetLayerVisibility, new SetLayerVisibilityRequest(changes), CancellationToken.None)
            .ConfigureAwait(true);

        if (response.Success)
        {
            await RefreshAsync().ConfigureAwait(true);
        }

        return response.Success;
    }

    private async Task<bool> ToggleAsync(string layerName, bool isOn)
    {
        var response = await _connectionManager
            .SendRequestAsync(
                IpcMessageTypes.SetLayerVisibility,
                new SetLayerVisibilityRequest(new[] { new LayerVisibilityChange(layerName, isOn) }),
                CancellationToken.None)
            .ConfigureAwait(true);

        return response.Success;
    }

    private void ApplyFilter()
    {
        FilteredLayers.Clear();

        var query = _searchText.Trim();
        var matches = query.Length == 0
            ? _allLayers
            : _allLayers.Where(row => row.Name.Contains(query, System.StringComparison.OrdinalIgnoreCase));

        foreach (var row in matches)
        {
            FilteredLayers.Add(row);
        }
    }
}
